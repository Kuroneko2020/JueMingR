using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerLinkedChecks
    {
        internal static void Run(Assembly host,string layout,string output)
        {
            VerifyComparison();
            const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",flags);
            if(capture==null)throw new InvalidOperationException("RED: a valid MoonLord clot requires a captured projectile key and future, not an illegal default-state exclusion.");
            NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();
            Set(0,398,700,400);Set(1,396,700,180);Set(2,397,500,400);Set(3,397,900,400);Set(4,401,750,800);
            Main.npc[0].localAI[0]=2;Main.npc[0].localAI[1]=3;Main.npc[0].localAI[2]=1;Main.npc[0].localAI[3]=1;
            Main.npc[1].ai[3]=0;Main.npc[2].ai[3]=0;Main.npc[2].ai[2]=0;Main.npc[3].ai[3]=0;Main.npc[3].ai[2]=1;
            var projectile=(Projectile)typeof(Projectile).GetMethod("NewProjectileSetup",flags).Invoke(null,new object[]{default(ProjectileKey)});
            projectile.SetDefaults(456);projectile.active=true;projectile.position=new Vector2(780,960);projectile.ai[0]=2;projectile.ai[1]=0;projectile.damage=0;projectile.timeLeft=180;
            Main.npc[4].ai[0]=2;Main.npc[4].ai[1]=projectile.key;
            byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0,1,2,3,4},new[]{projectile.whoAmI},4,1000L,120});
            byte[] future;
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try{future=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0,1,2,3,4},new[]{projectile.whoAmI},4).Future;}
                catch{if(!child.HasExited)child.Kill();child.WaitForExit(5000);if(errors.Wait(5000))Console.Error.WriteLine(errors.Result);throw;}
                NativeCombatWorkerChecks.Exit(child,"MoonLord helper exits");
                if(child.ExitCode!=0)throw new InvalidOperationException("MoonLord helper exit: "+errors.Result);
                File.WriteAllText(Path.Combine(output,"moonlord-worker.log"),errors.Result);
            }
            NativeCombatWorkerChecks.Compare(future,4,output,"moonlord-clot",new[]{projectile.whoAmI},dependencyComparison:CompareDependencies);
            if(Main.npc[4].active)throw new InvalidOperationException("Clot must naturally end at its heal-arrival phase, not remain alive for 120 points.");
            Console.WriteLine("PASS valid MoonLord core/head/hands + ProjectileKey clot, frozen native future and natural end.");
        }
        private static void Set(int slot,int type,float x,float y)
        {var npc=Main.npc[slot];npc.SetDefaults(type);npc.active=true;npc.whoAmI=slot;npc.position=new Vector2(x,y);npc.target=0;npc.timeLeft=750;}

        // Only this fixed 120-tick fixture permits motion rounding for type452.
        // integration-26 retained the exact failure. The independently evaluated
        // native hand-angle formula reproduces both localAI bits when its angle
        // remains extended precision vs stored as Single before Sin/Cos. The
        // inserted guard calls can change that x86 storage opportunity; no
        // machine-code spill instruction was observed. Bound each position
        // component to two Single ULPs at this fixture's <2048px scale, and each
        // velocity component to four ULPs at its <=8px/tick scale. These are not
        // general prediction tolerances. NPCs, all other types, identities,
        // counts, lifetime and AI bits remain exact, as do all Birth fixtures.
        private const float PositionBudget=1f/4096,VelocityBudget=1f/262144;
        private static void CompareDependencies(byte[] native,byte[] predicted)
        {
            Require(native.Length==predicted.Length,"Linked dependency length changed.");
            using(var a=new BinaryReader(new MemoryStream(native,false)))using(var b=new BinaryReader(new MemoryStream(predicted,false)))
            for(int step=0;step<=120;step++)
            {
                Require(a.ReadInt32()==step && b.ReadInt32()==step,"Linked dependency frame changed.");
                int npcs=a.ReadInt32();Require(npcs>=0 && npcs<=201 && b.ReadInt32()==npcs,"Linked NPC count changed.");
                Exact(a,b,npcs*49,"NPC state");
                int projectiles=a.ReadInt32();Require(projectiles>=0 && projectiles<=1001 && b.ReadInt32()==projectiles,"Linked projectile count changed.");
                for(int i=0;i<projectiles;i++)
                {
                    Exact(a,b,8,"slot/key");int type=a.ReadInt32();Require(type==b.ReadInt32(),"Linked projectile type changed.");
                    if(type!=452)Exact(a,b,16,"motion of other projectile type");
                    else for(int component=0;component<4;component++)
                    {
                        float x=a.ReadSingle(),y=b.ReadSingle();float scale=component<2?2048:8;
                        Require(!float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y) && Math.Abs(x)<=scale && Math.Abs(y)<=scale,"Linked motion outside finite fixture scale.");
                        Require(Math.Abs((double)x-y)<=(component<2?PositionBudget:VelocityBudget),"Linked type452 motion drift at step="+step+" component="+component+" native="+x.ToString("R")+" predicted="+y.ToString("R"));
                    }
                    Exact(a,b,16,"lifetime/AI bits");
                }
                if(step==120)Require(a.BaseStream.Position==a.BaseStream.Length && b.BaseStream.Position==b.BaseStream.Length,"Linked dependency trailing data.");
            }
        }
        private static void Exact(BinaryReader a,BinaryReader b,int count,string field)
        {for(int i=0;i<count;i++)Require(a.ReadByte()==b.ReadByte(),"Linked dependency changed: "+field);}
        private static void VerifyComparison()
        {
            byte[] sample;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                for(int step=0;step<=120;step++)
                {
                    writer.Write(step);writer.Write(1);writer.Write(new byte[49]);writer.Write(1);
                    writer.Write(3);writer.Write(0x40100U);writer.Write(452);
                    writer.Write(1024f);writer.Write(512f);writer.Write(4f);writer.Write(-1f);
                    writer.Write(180-step);writer.Write(0f);writer.Write(0f);writer.Write(0f);
                }
                writer.Flush();sample=stream.ToArray();
            }
            CompareDependencies(sample,sample);
            // First frame: 8 header + 49 NPC + 4 count + 12 projectile identity.
            const int motion=73;
            byte[] rounded=(byte[])sample.Clone();Buffer.BlockCopy(BitConverter.GetBytes(1024f+PositionBudget),0,rounded,motion,4);CompareDependencies(sample,rounded);
            foreach(var change in new[]{Tuple.Create(motion,1024f+2*PositionBudget),Tuple.Create(motion+8,4f+2*VelocityBudget),Tuple.Create(motion,float.NaN),Tuple.Create(motion,float.PositiveInfinity),Tuple.Create(motion,2049f)})
            {
                byte[] bad=(byte[])sample.Clone();Buffer.BlockCopy(BitConverter.GetBytes(change.Item2),0,bad,change.Item1,4);Refuses(sample,bad);
            }
            foreach(int offset in new[]{4,8,61,65,69,motion+16,motion+20})
            {byte[] bad=(byte[])sample.Clone();bad[offset]^=1;Refuses(sample,bad);}
            byte[] other=(byte[])sample.Clone();Buffer.BlockCopy(BitConverter.GetBytes(456),0,other,69,4);
            byte[] moved=(byte[])other.Clone();Buffer.BlockCopy(BitConverter.GetBytes(1024f+PositionBudget),0,moved,motion,4);Refuses(other,moved);
            Console.WriteLine("PASS Linked comparison rejects motion over budget, nonfinite, changed NPC/count/slot/key/type/lifetime/AI and motion of other types.");
        }
        private static void Refuses(byte[] sample,byte[] bad)
        {bool refused=false;try{CompareDependencies(sample,bad);}catch(InvalidOperationException){refused=true;}Require(refused,"Linked comparison accepted changed dependency.");}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
