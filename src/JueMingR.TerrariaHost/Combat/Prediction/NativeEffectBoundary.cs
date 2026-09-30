using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Net;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // These patches are installed only in the owned helper. Behavior-bearing
    // AI, buffs, HitEffect, FindFrame, spawning and local terrain edits remain
    // original. A forbidden operation is sticky even if native code catches it.
    internal sealed class NativeEffectBoundary : IDisposable
    {
        private readonly Harmony patches=new Harmony("JueMingR.PredictionWorker.Effects");
        private static Terraria.Graphics.CameraModifiers.CameraModifierStack camera;
        internal static string Failure { get; private set; }
        internal NativeEffectBoundary(bool fixtureOriginal=false)
        {
            var cost=PredictionPipeProtocol.Measure?System.Diagnostics.Stopwatch.StartNew():null;
            NativeImmunitySnapshot.Install(patches);
            NativeLightingSnapshot.Install(patches);
            NativeNpcMotionTrace.Install(patches);
            Part(cost,"immunity-install");
            Patch(typeof(NetMessage),"SendData","Skip");
            foreach(string name in new[]{"Broadcast","SendToServer","SendToClient"})Patch(typeof(NetManager),name,"RecyclePacket");
            Patch(typeof(WorldGen),"TransformWorldOnBackgroundThread","Block");
            Patch(typeof(WorldGen),"saveToonWhilePlaying","Block");
            Patch(typeof(Player),"SavePlayer","Block");
            Patch(typeof(Player),"InternalSavePlayerFile","Block");
            Patch(typeof(Player),"KillMeForGood","Block");
            // Do not let a background save escape the request before the file
            // fence runs. Native death catches this exception, so it is sticky.
            foreach(var ctor in typeof(Terraria.Testing.Cloning.DeepCloneContext).GetConstructors())patches.Patch(ctor,prefix:Prefix("Block"));
            Patch(typeof(Terraria.Testing.StateSnapshot),"Restore","Block");
            // MemberwiseClone would detach an unknown directory actor from its
            // identity guard without obtaining a full page. No audited native
            // prediction path currently needs it; fail instead of losing scope.
            Patch(typeof(NPC),"Clone","Block");
            Patch(typeof(Terraria.IO.WorldFile),"SaveWorld","Block");Patch(typeof(Terraria.IO.WorldFile),"SaveNewWorld","Block");
            Patch(typeof(Terraria.Utilities.FileUtilities),"Write","Block");
            foreach(string name in new[]{"Delete","Copy","Move","MoveToCloud","MoveToLocal","CopyToLocal","CopyFolder"})Patch(typeof(Terraria.Utilities.FileUtilities),name,"Block");
            Patch(typeof(Terraria.Social.SocialAPI),"Initialize","Block");
            foreach(string name in new[]{"LoadSteam","LoadWeGame","LoadDiscord"})Patch(typeof(Terraria.Social.SocialAPI),name,"Block");
            Patch(typeof(Terraria.Audio.SoundEngine),"Initialize","Block");Patch(typeof(Terraria.Audio.SoundEngine),"Load","Block");
            Part(cost,"external-outlet-install");
            VerifyFacilities();
            Patch(typeof(Terraria.Achievements.AchievementManager),"Save","Block");
            Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper),"HandleSpecialEvent","Skip");
            // Taskbar flashing only: even a subsequently rejected death must
            // not leave external callbacks in the private main-thread queue.
            Patch(typeof(Main),"NotifyOfEvent","Skip");
            Patch(typeof(Terraria.GameContent.Drawing.ParticleOrchestrator),"SpawnParticlesDirect","Skip");
            Patch(typeof(Terraria.GameContent.Events.MoonlordDeathDrama),"ThrowPieces","Skip");
            Patch(typeof(Terraria.GameContent.Events.MoonlordDeathDrama),"RequestLight","Skip");
            Patch(typeof(Terraria.GameContent.Events.MoonlordDeathDrama),"AddExplosion","Explosion");
            Patch(typeof(Terraria.GameContent.UI.Chat.RemadeChatMonitor),"AddNewMessage","Skip");
            // Preserve text slot allocation and every RNG call. Font metrics
            // affect only text placement; no font or graphics asset is loaded.
            foreach(var method in typeof(CombatText).GetMethods().Where(m=>m.Name=="NewText" && m.GetParameters()[2].ParameterType==typeof(string)))
                patches.Patch(method,transpiler:Prefix("TextMetrics"));
            foreach(var method in typeof(Lighting).GetMethods().Where(m=>m.Name=="AddLight"))
                patches.Patch(method,prefix:Prefix("Skip"));
            var prepared=typeof(Main).Assembly.GetType(NativePresentationImage.GuardName,false);
            if(prepared!=null)
            {
                NativeAssetSnapshot.Bind(prepared);prepared.GetField("Camera").SetValue(null,(Func<object>)(()=>camera));
                Part(cost,"presentation-outlet-install");Console.Error.WriteLine("STARTUP presentation uses authenticated prepared image");return;
            }
            if(!fixtureOriginal)throw new InvalidDataException("Production private presentation image missing.");
            var instance=typeof(Main).GetField("instance");
            Part(cost,"presentation-outlet-install");
            var inventory=new NativeInstructionInventory();double discovery=0,installation=0;
            foreach(var method in new[]{typeof(NPC),typeof(Gore),typeof(Terraria.GameContent.Events.DD2Event)}.SelectMany(t=>t.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)))
            {
                if(method.GetMethodBody()==null)continue;
                var measured=System.Diagnostics.Stopwatch.StartNew();var instructions=inventory.Read(method);discovery+=measured.Elapsed.TotalMilliseconds;measured.Restart();
                if(NativeAssetSnapshot.UsesMetadata(instructions))
                    patches.Patch(method,transpiler:new HarmonyMethod(typeof(NativeAssetSnapshot).GetMethod("Rewrite",BindingFlags.Static|BindingFlags.NonPublic)));
                if(instructions.Any(i=>Equals(i.operand,Deactivate)) || instructions.Any(i=>i.opcode==OpCodes.Ldsfld && Equals(i.operand,instance)) && instructions.Any(i=>i.opcode==OpCodes.Ldfld && Equals(i.operand,typeof(Main).GetField("CameraModifiers"))))
                    patches.Patch(method,transpiler:Prefix("CameraAccess"));
                installation+=measured.Elapsed.TotalMilliseconds;
            }
            Console.Error.WriteLine("STARTUP operand-discovery-ms="+discovery.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" selected-transpiler-install-ms="+installation.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
        private static void Part(System.Diagnostics.Stopwatch watch,string name)
        {if(watch!=null){Console.Error.WriteLine("STARTUP "+name+"-ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));watch.Restart();}}
        internal static void Begin(){Failure=null;VerifyFacilities();camera=new Terraria.Graphics.CameraModifiers.CameraModifierStack();}
        private static void VerifyFacilities()
        {
            if(Terraria.Audio.SoundEngine.IsAudioSupported || Terraria.Audio.SoundEngine.SoundPlayer!=null || Terraria.Audio.SoundEngine.LegacySoundPlayer!=null)
                throw new InvalidOperationException("Prediction acquired an audio facility.");
            foreach(var field in typeof(Terraria.Social.SocialAPI).GetFields(BindingFlags.Public|BindingFlags.Static))
                if(!field.FieldType.IsValueType && field.GetValue(null)!=null)throw new InvalidOperationException("Prediction acquired a social facility: "+field.Name);
        }
        private void Patch(Type type,string name,string prefix)
        {
            var methods=type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name==name).ToArray();
            if(methods.Length==0)throw new InvalidOperationException("Missing native effect boundary: "+type.FullName+"."+name);
            foreach(var method in methods)
                try{patches.Patch(method,prefix:Prefix(prefix));}
                catch(Exception error){throw new InvalidOperationException("Native boundary install failed: "+method.DeclaringType.FullName+"."+method.Name,error);}
        }
        private static HarmonyMethod Prefix(string name){return new HarmonyMethod(typeof(NativeEffectBoundary).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));}
        private static bool Skip(){return false;}
        // Allocation is an AI RNG input: NewDust/NewGore/NewText consume
        // different random calls when the observed pools are crowded/full.
        // Carry only bounded occupancy and Dust's spawn-range scalar. We do
        // not run particle aging/drawing or recreate unrelated visual state;
        // future random branches remain conditional on this sampled premise.
        internal static void WriteAllocation(BinaryWriter writer)
        {
            writer.Write(Dust.dCount);
            for(int start=0;start<Main.dust.Length;start+=8){byte bits=0;for(int b=0;b<8 && start+b<Main.dust.Length;b++)if(Main.dust[start+b]?.active==true)bits|=(byte)(1<<b);writer.Write(bits);}
            for(int start=0;start<Main.gore.Length;start+=8){byte bits=0;for(int b=0;b<8 && start+b<Main.gore.Length;b++)if(Main.gore[start+b]?.active==true)bits|=(byte)(1<<b);writer.Write(bits);}
            for(int start=0;start<Main.combatText.Length;start+=8){byte bits=0;for(int b=0;b<8 && start+b<Main.combatText.Length;b++)if(Main.combatText[start+b]?.active==true)bits|=(byte)(1<<b);writer.Write(bits);}
        }
        internal static void ReadAllocation(BinaryReader reader)
        {
            Dust.dCount=reader.ReadSingle();if(float.IsNaN(Dust.dCount) || float.IsInfinity(Dust.dCount) || Dust.dCount<0 || Dust.dCount>1)throw new InvalidDataException("Dust allocation premise.");
            for(int start=0;start<Main.dust.Length;start+=8){byte bits=AllocationBits(reader,Main.dust.Length-start);for(int b=0;b<8 && start+b<Main.dust.Length;b++)Main.dust[start+b].active=(bits&(1<<b))!=0;}
            for(int start=0;start<Main.gore.Length;start+=8){byte bits=AllocationBits(reader,Main.gore.Length-start);for(int b=0;b<8 && start+b<Main.gore.Length;b++)Main.gore[start+b].active=(bits&(1<<b))!=0;}
            for(int start=0;start<Main.combatText.Length;start+=8){byte bits=AllocationBits(reader,Main.combatText.Length-start);for(int b=0;b<8 && start+b<Main.combatText.Length;b++)Main.combatText[start+b].active=(bits&(1<<b))!=0;}
        }
        private static byte AllocationBits(BinaryReader reader,int remaining)
        {byte bits=reader.ReadByte();if(remaining<8 && (bits>>remaining)!=0)throw new InvalidDataException("Effect allocation padding.");return bits;}
        private static bool RecyclePacket(NetPacket __0){__0.Recycle();return false;}
        private static bool Block(MethodBase __originalMethod)
        {
            Failure=__originalMethod.DeclaringType.FullName+"."+__originalMethod.Name;
            throw new InvalidDataException("Prediction cannot execute external or whole-world operation: "+Failure);
        }
        private static bool Explosion(){Main.rand.Next(2,4);return false;}
        private static ReLogic.Graphics.DynamicSpriteFont NoFont(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont> asset){return null;}
        private static Vector2 NoMetrics(ReLogic.Graphics.DynamicSpriteFont font,string text){return Vector2.Zero;}
        private static IEnumerable<CodeInstruction> TextMetrics(IEnumerable<CodeInstruction> source)
        {
            foreach(var instruction in source)
            {
                var method=instruction.operand as MethodInfo;string replacement=null;
                if(method!=null && method.DeclaringType==typeof(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>) && method.Name=="get_Value")replacement="NoFont";
                if(method!=null && method.DeclaringType==typeof(ReLogic.Graphics.DynamicSpriteFont) && method.Name=="MeasureString" && method.GetParameters().Length==1 && method.GetParameters()[0].ParameterType==typeof(string))replacement="NoMetrics";
                if(replacement!=null){instruction.opcode=OpCodes.Call;instruction.operand=typeof(NativeEffectBoundary).GetMethod(replacement,BindingFlags.Static|BindingFlags.NonPublic);}
                yield return instruction;
            }
        }
        private static Terraria.Graphics.CameraModifiers.CameraModifierStack Camera(){return camera;}
        private static readonly MethodInfo Deactivate=typeof(Terraria.Graphics.Effects.EffectManager<Terraria.Graphics.Effects.Filter>).GetMethod("Deactivate");
        private static void NoFilter(Terraria.Graphics.Effects.EffectManager<Terraria.Graphics.Effects.Filter> manager,string name,object[] arguments){}
        private static IEnumerable<CodeInstruction> CameraAccess(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();var instance=typeof(Main).GetField("instance");var field=typeof(Main).GetField("CameraModifiers");
            for(int i=0;i<code.Count;i++)
            {
                if(i+1<code.Count && code[i].opcode==OpCodes.Ldsfld && Equals(code[i].operand,instance) && code[i+1].opcode==OpCodes.Ldfld && Equals(code[i+1].operand,field))
                {code[i].opcode=OpCodes.Nop;code[i].operand=null;code[i+1].opcode=OpCodes.Call;code[i+1].operand=typeof(NativeEffectBoundary).GetMethod("Camera",BindingFlags.Static|BindingFlags.NonPublic);}
                if(Equals(code[i].operand,Deactivate)){code[i].opcode=OpCodes.Call;code[i].operand=typeof(NativeEffectBoundary).GetMethod("NoFilter",BindingFlags.Static|BindingFlags.NonPublic);}
                yield return code[i];
            }
        }
        public void Dispose(){patches.UnpatchAll(patches.Id);NativeNpcMotionTrace.Clear();NativeLightingSnapshot.Release();camera=null;}
    }
}
