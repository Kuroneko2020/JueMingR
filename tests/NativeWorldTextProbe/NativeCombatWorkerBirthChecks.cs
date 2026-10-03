using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace NativeWorldTextProbe
{
    // This oracle records unmodified original entities. It does not call the
    // product timeline writer, page guard, or update selector.
    internal static class NativeCombatWorkerBirthChecks
    {
        internal static void Record(BinaryWriter writer,int step)
        {
            writer.Write(step);writer.Write(Main.npc.Count(n=>n.active));
            for(int slot=0;slot<Main.npc.Length;slot++)
            {
                var n=Main.npc[slot];if(!n.active)continue;
                writer.Write(slot);writer.Write(n.generation);writer.Write(n.type);writer.Write(n.netID);
                writer.Write(n.position.X);writer.Write(n.position.Y);writer.Write(n.velocity.X);writer.Write(n.velocity.Y);
                writer.Write(n.life);for(int i=0;i<4;i++)writer.Write(n.ai[i]);
            }
            writer.Write(Main.projectile.Count(p=>p.active));
            for(int slot=0;slot<Main.projectile.Length;slot++)
            {
                var p=Main.projectile[slot];if(!p.active)continue;
                writer.Write(slot);writer.Write((uint)p.key);writer.Write(p.type);
                writer.Write(p.position.X);writer.Write(p.position.Y);writer.Write(p.velocity.X);writer.Write(p.velocity.Y);
                writer.Write(p.timeLeft);for(int i=0;i<3;i++)writer.Write(p.ai[i]);
            }
        }
        internal static void Compare(BinaryReader reader,byte[] actual,string output,string name,Action<byte[],byte[]> comparison=null)
        {
            int length=reader.ReadInt32();Require(length>=0 && length<4194304,"Bounded dependency timeline.");
            byte[] predicted=reader.ReadBytes(length);
            File.WriteAllBytes(Path.Combine(output,name+"-dependencies-native.bin"),actual);
            File.WriteAllBytes(Path.Combine(output,name+"-dependencies-predicted.bin"),predicted);
            if(comparison!=null){comparison(actual,predicted);return;}
            int difference=0;while(difference<Math.Min(actual.Length,predicted.Length) && actual[difference]==predicted[difference])difference++;
            Require(difference==actual.Length && difference==predicted.Length,name+" native dependency timeline mismatch at byte="+difference+" native-bytes="+actual.Length+" predicted-bytes="+predicted.Length);
        }
        internal static void Run(Assembly host,string layout,string output)
        {
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                var randomField=typeof(Main).GetField("_rngs",BindingFlags.Static|BindingFlags.NonPublic);object oldRandom=randomField.GetValue(null);
                Main previousMain=Main.instance;bool previousIgnore=Main.ignoreErrors;Main.ignoreErrors=false;
                // Original UpdateWorld_Projectiles requires only these two
                // CPU helpers on its receiver. Do not run Game/Main constructors
                // or acquire a graphics device just to execute that phase.
                var original=(Main)FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(original);
                original.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
                original.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
                var originalPhase=typeof(Main).GetMethod("UpdateWorld_Projectiles",BindingFlags.Instance|BindingFlags.NonPublic);
                Main.instance=original;Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
                try
                {
                    NpcBirth(host,child,output);
                    foreach(int parentType in new[]{89,639})foreach(int parentSlot in new[]{0,5})
                    {
                        NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();
                        var n=Main.npc[0];n.SetDefaults(2);n.active=true;n.whoAmI=0;n.position=new Vector2(800,250);n.target=0;n.timeLeft=750;
                        var source=new EntitySource_DebugCommand();
                        for(int i=0;i<parentSlot;i++)Require(Projectile.NewProjectile(source,new Vector2(400,400),Vector2.Zero,1,0,0,Main.myPlayer)==i,"Original sequential slots.");
                        int parent=Projectile.NewProjectile(source,parentType==89?new Vector2(450,350):new Vector2(800,500),parentType==89?new Vector2(8,-2):new Vector2(4,0),parentType,0,0,Main.myPlayer);
                        Require(parent==parentSlot,"Original parent slot.");
                        for(int i=0;i<parentSlot;i++)Main.projectile[i].Kill();
                        randomField.SetValue(null,new Dictionary<string,Terraria.Utilities.UnifiedRandom>{{"UpdateNPCs",new Terraria.Utilities.UnifiedRandom(701)},{"UpdateProjectiles",new Terraria.Utilities.UnifiedRandom(702)}});
                        if(parentType==89)Main.projectile[parent].timeLeft=1;
                        else
                        {
                            // Let the original AI establish its recall position
                            // and velocity, then approach natural expiry. The
                            // 44 warm-up ticks are not forecast ticks.
                            for(int tick=957;tick<=1000;tick++)
                            {typeof(Main).GetField("_gameUpdateCount",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(uint)tick);using(Main.SwapRandom("UpdateProjectiles"))originalPhase.Invoke(original,null);}
                            Require(Main.projectile[parent].active && Main.projectile[parent].timeLeft==2,"Native MoonlordArrow warm-up reaches pre-expiry state: active="+Main.projectile[parent].active+" timeLeft="+Main.projectile[parent].timeLeft+" position="+Main.projectile[parent].position);
                        }
                        var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new[]{parent},0);
                        string name=(parentType==89?"crystal":"moonlord-arrow")+"-birth-parent-"+parentSlot;
                        NativeCombatWorkerChecks.Compare(scene.Future,0,output,name,nativeStreams:true,projectileUpdate:()=>originalPhase.Invoke(original,null));
                        if(parentType==89)AssertBirth(Path.Combine(output,name+"-dependencies-native.bin"),parentSlot);
                        else AssertRecall(Path.Combine(output,name+"-dependencies-native.bin"),parentSlot);
                    }
                }
                finally
                {
                    randomField.SetValue(null,oldRandom);
                    Main.instance=previousMain;Main.ignoreErrors=previousIgnore;
                    NativeCombatWorkerChecks.Exit(child,"birth helper exits");
                    Require(errors.Wait(5000),"Birth helper stderr closes.");
                    File.WriteAllText(Path.Combine(output,"birth-worker.log"),errors.Result);Console.WriteLine(errors.Result);
                }
            }
            Console.WriteLine("PASS original projectile phase: crystal high/low birth and natural MoonlordArrow trail, identity/AI/motion/timeLeft at every future step.");
        }
        private static void NpcBirth(Assembly host,System.Diagnostics.Process child,string output)
        {
            const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            foreach(int parentSlot in new[]{0,5})
            {
                NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();
                var source=new EntitySource_DebugCommand();
                int parent=NPC.NewNPC(source,700,400,594,Start:parentSlot,Target:0);Require(parent==parentSlot,"Original balloon parent order.");
                var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",flags);
                byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{parent},new int[0],parent,1000L,120});
                byte[] future=NativeCombatWorkerChecks.Exchange(child,snapshot);
                using(var reader=new BinaryReader(new MemoryStream(future,false)))
                {
                    int protocol=reader.ReadInt32();
                    if(protocol<0){reader.ReadString();throw new InvalidOperationException("NPC birth must not demand every inactive projectile page: "+reader.ReadString());}
                    Require(protocol==NativeCombatWorkerChecks.ExpectedProtocol,"NPC birth returns timeline.");
                }
                string name="windy-balloon-birth-parent-"+parentSlot;
                NativeCombatWorkerChecks.Compare(future,parent,output,name);
                using(var reader=new BinaryReader(File.OpenRead(Path.Combine(output,name+"-dependencies-native.bin"))))
                for(int frame=0;frame<=2;frame++)
                {
                    Require(reader.ReadInt32()==frame,"Ordered balloon frame.");int npcs=reader.ReadInt32();Require(npcs==(frame==0?1:2),"Balloon actually creates one native slime.");
                    for(int i=0;i<npcs;i++)
                    {
                        int slot=reader.ReadInt32();byte generation=reader.ReadByte();int type=reader.ReadInt32();reader.ReadInt32();reader.BaseStream.Position+=20;
                        float a0=reader.ReadSingle(),a1=reader.ReadSingle();reader.ReadSingle();float a3=reader.ReadSingle();
                        if(frame==0)continue;
                        if(type==594)Require((int)a3==(parentSlot==0?1:0),"Parent retains native child slot.");
                        else Require(type==1 && slot==(parentSlot==0?1:0) && generation!=0 && a0==-999 && (frame==1 && parentSlot==5?a1==0:a1!=0),"New higher NPC advances now; lower NPC waits one tick.");
                    }
                    Require(reader.ReadInt32()==0,"Balloon fixture has no projectiles.");
                }
            }
            Console.WriteLine("PASS original NPC birth high/low slots without whole-projectile pages; parent/child identity and first actual update.");
        }
        private static void AssertRecall(string path,int parentSlot)
        {
            int[] x=parentSlot==0?new[]{799,811,823,835,847}:new[]{795,803,815,827,839};
            int[] phase=parentSlot==0?new[]{1,4,7,10,13}:new[]{0,2,5,8,11};
            int[] life=parentSlot==0?new[]{89,86,83,80,77}:new[]{90,88,85,82,79};
            using(var reader=new BinaryReader(File.OpenRead(path)))
            for(int frame=0;frame<=5;frame++)
            {
                Require(reader.ReadInt32()==frame,"Ordered recall frame.");int npcs=reader.ReadInt32();reader.BaseStream.Position+=49*npcs;
                int count=reader.ReadInt32();Require(count==1,"Exactly one original recall actor.");
                int slot=reader.ReadInt32();uint key=reader.ReadUInt32();int type=reader.ReadInt32();
                float px=reader.ReadSingle(),py=reader.ReadSingle(),vx=reader.ReadSingle(),vy=reader.ReadSingle();int time=reader.ReadInt32();
                float a0=reader.ReadSingle(),a1=reader.ReadSingle(),a2=reader.ReadSingle();
                if(frame==0)continue;
                Require(type==640 && slot==(parentSlot==0?1:0) && key==(parentSlot==0?0x00040100U:0x00080000U),"Recall has the native child key and slot.");
                Require(px==x[frame-1] && py==495 && vx==4 && vy==0 && time==life[frame-1] && a0==phase[frame-1] && a1==(parentSlot!=0 && frame==1?1:0) && a2==0,"Recall substeps and phase-cursor compensation at tick="+frame);
            }
        }
        private static void AssertBirth(string path,int parentSlot)
        {
            using(var reader=new BinaryReader(File.OpenRead(path)))
            for(int frame=0;frame<=1;frame++)
            {
                Require(reader.ReadInt32()==frame,"Ordered native dependency frame.");int npcs=reader.ReadInt32();reader.BaseStream.Position+=49*npcs;
                int count=reader.ReadInt32();if(frame==1)Require(count==2,"Native crystal parent actually creates two children.");
                for(int i=0;i<count;i++)
                {
                    int slot=reader.ReadInt32();reader.ReadUInt32();int type=reader.ReadInt32();
                    reader.BaseStream.Position+=16;int timeLeft=reader.ReadInt32();reader.BaseStream.Position+=12;
                    if(frame==1)
                    {
                        Require(type==90 && (parentSlot==0?slot>parentSlot:slot<parentSlot),"Native child lies on expected side of the phase cursor.");
                        var defaults=new Projectile();defaults.SetDefaults(90);
                        Require(parentSlot==0?timeLeft<defaults.timeLeft:timeLeft==defaults.timeLeft,"High child advances this tick; lower child waits.");
                    }
                }
            }
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
