using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    // A real Session and original update loop: equipment creates the guardian,
    // town AI consumes its own RNG, and actual attacks retain all damage paths.
    internal static class NativeCombatGuardianQueryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object owner;
        private static int frame;
        private static string phase;
        private static long npcUpdates,projectileUpdates;
        private static void NpcUpdated(){npcUpdates++;}
        private static void ProjectileUpdated(){projectileUpdates++;}
        private static readonly List<string> replies=new List<string>();
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            owner=native;npcUpdates=projectileUpdates=0;replies.Clear();replies.Add("frame,phase,capture,arrive,outcome,frames,fullNpcs,fullProjectiles,terrainChunks,advanceMs");
            var rows=new List<string>{"frame,phase,selected,published,tankPet,attacking,dependencies,projectiles,reason"};
            var counts=new Dictionary<string,int[]>();
            var harmony=new Harmony("JueMingR.Tests.GuardianQuery");
            bool hard=Main.hardMode;
            try
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
                Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
                var player=Main.LocalPlayer;
                player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
                player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;
                player.fallStart=player.fallStart2=(int)(player.position.Y/16);
                player.statLife=player.statLifeMax=player.statLifeMax2=400;player.immune=true;player.immuneTime=100000;
                for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                for(int i=0;i<3;i++)player.armor[i].TurnToAir();
                typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,219,Start:16,Target:Main.myPlayer);
                Main.npc[target].life=Main.npc[target].lifeMax=100000;
                var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
                Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                harmony.Patch(native.GetType().GetMethod("Receive",Flags),new HarmonyMethod(typeof(NativeCombatGuardianQueryChecks).GetMethod(nameof(Receiving),Flags)),new HarmonyMethod(typeof(NativeCombatGuardianQueryChecks).GetMethod(nameof(Received),Flags)));
                harmony.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),postfix:new HarmonyMethod(typeof(NativeCombatGuardianQueryChecks).GetMethod(nameof(NpcUpdated),Flags)));
                harmony.Patch(typeof(Projectile).GetMethod("Update",Flags,null,new Type[]{typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatGuardianQueryChecks).GetMethod(nameof(ProjectileUpdated),Flags)));
                long requests=(long)Get(native,"Requests"),refused=(long)Get(native,"Refused"),rejected=(long)Get(native,"Rejected");
                int maximumDependencies=0,attackUpdates=0,guardianSelected=0,guardianPublished=0;
                var backgrounds=new List<int>();
                for(frame=0;frame<1440;frame++)
                {
                    phase=frame<180?"simple":frame<900?"guardian-town":frame<1080?"guardian-wet-town":frame<1200?"guardian-exit":"simple-return";
                    if(frame==180)
                    {
                        player.armor[0].SetDefaults(3381);player.armor[1].SetDefaults(3382);player.armor[2].SetDefaults(3383);
                        // Initial observed towns use native defaults. NewNPC's
                        // cosmetic profile loading needs a graphics Content
                        // service; this CPU fixture does not fake that service.
                        for(int i=0;i<8;i++)
                        {var town=new NPC();town.SetDefaults(678);town.whoAmI=i;town.active=true;town.position=new Vector2(3000+i*30,2400-town.height);Main.npc[i]=town;backgrounds.Add(i);}
                    }
                    if(frame==900)
                    {
                        // A real water observation; background AI remains live.
                        for(int x=185;x<207;x++)for(int y=147;y<150;y++)Main.tile[x,y].liquid=255;
                    }
                    if(frame==1080)for(int i=0;i<3;i++)player.armor[i].TurnToAir();
                    if(frame==1200)foreach(int slot in backgrounds)Main.npc[slot].active=false;
                    SampleMouse(context,Main.npc[target].Center);
                    if(frame==480 && Environment.GetEnvironmentVariable("JUEMINGR_NPC_DIAGNOSE_DIFFERENCE")=="1")
                    {
                        var capture=native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureSceneRegion",Flags);
                        File.WriteAllBytes(Path.Combine(output,"difference-one-step.bin"),(byte[])capture.Invoke(null,new object[]{new[]{target},Main.projectile.Where(p=>p.active).Select(p=>p.whoAmI).ToArray(),target,(long)Main.GameUpdateCount,1,1L,20,70,140,210,false}));
                        step();File.WriteAllLines(Path.Combine(output,"difference-native.txt"),NativeCombatPrivateImageChecks.Dump(Main.npc[target],player));
                        Console.WriteLine("DIAGNOSTIC guardian one-step values retained; continuous assertions not executed.");return;
                    }
                    step();
                    bool selected=(bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==target;
                    var path=cache.Read(0);bool published=path!=null && path.Identity.Slot==target;
                    if(published && (path.SampleTick!=Main.GameUpdateCount || path.Count<31 || path.Count>121))throw new InvalidOperationException("Guardian display lost its current 30..120 future contract.");
                    bool attacking=Main.projectile.Any(p=>p.active && p.type==623 && p.ai[0]==2);
                    int dependencies=(int)Get(Get(native,"npcs"),"Count");
                    if(frame>=360 && frame<1080){maximumDependencies=Math.Max(maximumDependencies,dependencies);if(attacking)attackUpdates++;if(selected)guardianSelected++;if(published)guardianPublished++;}
                    int[] count;if(!counts.TryGetValue(phase,out count))counts.Add(phase,count=new int[3]);count[0]++;if(selected)count[1]++;if(published)count[2]++;
                    rows.Add(Csv(frame,phase,selected,published,player.tankPet,attacking,dependencies,Get(Get(native,"projectiles"),"Count"),Get(native,"Reason")));
                }
                foreach(var pair in counts)Console.WriteLine("GUARDIAN phase="+pair.Key+" updates="+pair.Value[0]+" selected="+pair.Value[1]+" published="+pair.Value[2]);
                Console.WriteLine("GUARDIAN attack-updates="+attackUpdates+" steady-selected="+guardianSelected+" steady-published="+guardianPublished+" max-full-npcs="+maximumDependencies+" replies="+(replies.Count-1));
                Console.WriteLine("GUARDIAN requests="+((long)Get(native,"Requests")-requests)+" refused="+((long)Get(native,"Refused")-refused)+" rejected="+((long)Get(native,"Rejected")-rejected)+" original-npc-updates="+npcUpdates+" original-projectile-updates="+projectileUpdates+" worker-ai-call-count=unmeasured");
                if(attackUpdates==0 || guardianSelected<600)throw new InvalidOperationException("Guardian fixture did not exercise real attack/selection.");
                if(maximumDependencies!=1 || guardianPublished<600)throw new InvalidOperationException("Guardian pure eligibility query expanded town AI or failed continuous useful publication.");
                NativeCombatQueryRetirementChecks.Run(native,cache,()=>{SampleMouse(context,Main.npc[target].Center);step();});
                // The prefix receipt/age proof deliberately remains strict-long;
                // this scene's display consumer was measured separately above.
                var prefixHost=Get(context,"CombatObservation");var prefixOptions=(ObservationOptions)Get(prefixHost,"Options");
                cache.Demand(1,120);
                try
                {
                    NativeCombatObservationChecks.Save(prefixHost,prefixOptions.Path?prefixOptions.Toggle(1):prefixOptions);
                    if(cache.Read(0)!=null || (int)Get(cache,"MinimumRequired")!=120 || cache.Required!=120)throw new InvalidOperationException("Prefix proof owns only the strict120 consumer.");
                    NativeCombatPrefixSessionChecks.Run(native,cache,step,point=>SampleMouse(context,point));
                }
                finally{cache.Release(1);NativeCombatObservationChecks.Save(prefixHost,prefixOptions);}
            }
            finally
            {
                harmony.UnpatchAll(harmony.Id);Main.hardMode=hard;owner=null;
                File.WriteAllLines(Path.Combine(output,"guardian-updates.csv"),rows);File.WriteAllLines(Path.Combine(output,"guardian-replies.csv"),replies);
            }
        }
        private static void Receiving(object __instance,object response,ref object[] __state)
        {if(ReferenceEquals(__instance,owner))__state=new[]{Get(__instance,"pending"),Get(response,"Result")};}
        private static void Received(object __instance,long tick,object[] __state)
        {
            if(__state==null || __state[0]==null)return;
            var measurement=((IEnumerable)Get(__instance,"Measurements")).Cast<object>().LastOrDefault();
            // Early retirement does not populate Measurement's worker times.
            // Read the actual decoded reply; absent timing remains an empty cell.
            object advance=__state[1]!=null && (double)Get(__state[1],"TotalMs")>0?Get(__state[1],"AdvanceMs"):null;
            replies.Add(Csv(frame,phase,Get(__state[0],"Tick"),tick,measurement==null?"unmeasured":Get(measurement,"Outcome"),((Array)Get(__state[1],"Frames"))?.Length,
                ((int[])Get(__state[0],"Npcs")).Length,((int[])Get(__state[0],"Projectiles")).Length,((Array)Get(Get(__state[0],"Terrain"),"Chunks")).Length,advance));
        }
        private static string Csv(params object[] values)=>string.Join(",",values.Select(v=>"\""+Convert.ToString(v,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
        private static object Get(object value,string name){var f=value.GetType().GetField(name,Flags);return f!=null?f.GetValue(value):value.GetType().GetProperty(name,Flags)?.GetValue(value);}
        private static void Call(object value,string name,params object[] args){value.GetType().GetMethod(name,Flags).Invoke(value,args);}
        private static void SampleMouse(object context,Vector2 point)
        {
            Main.screenPosition=point-new Vector2(Main.screenWidth/2,Main.screenHeight/2);var input=Get(context,"Input");Call(input,"BeginUpdate");
            Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(Main.screenWidth/2,Main.screenHeight/2,0,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
            Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"CombatObservation"),"SampleMouse");
        }
    }
}
