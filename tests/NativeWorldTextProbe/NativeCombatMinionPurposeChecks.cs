using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMinionPurposeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(Assembly host,Func<byte[],Tuple<byte[],byte[]>> exchange)
        {
            ClosedOwner(host,exchange);
            NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();var player=Main.player[0];player.position=new Vector2(300,1000);player.maxMinions=10;
            Main.npc[0].position=new Vector2(1600,1000);Main.npc[0].ai[0]=-100;
            var minion=Main.projectile[0];minion.SetDefaults(393);minion.whoAmI=0;minion.owner=0;minion.active=true;minion.position=new Vector2(300,1000);minion.timeLeft=600;
            if(!minion.minion || minion.friendly)throw new InvalidOperationException("Original spider starts as a non-attacking minion.");
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
            byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0},new[]{0},0,1000L,1});var reply=exchange(snapshot);
            var n=Main.npc[0];var identity=new NpcIdentity(1,n,0,n.generation,n.type,n.netID);
            object result=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true).GetMethod("Read",Flags).Invoke(null,new object[]{reply.Item1,reply.Item2,identity,1000L,1L,false});
            var frames=(Array)result.GetType().GetField("Frames",Flags).GetValue(result);if(frames==null)throw new InvalidOperationException("Non-attacking minion scene needs a successful native step: "+result.GetType().GetField("Error",Flags).GetValue(result));
            object first=frames.GetValue(0);var required=(bool[])first.GetType().GetField("ProjectileRequired",Flags).GetValue(first);
            if(required.Length!=1 || !required[0])throw new InvalidOperationException("A non-attacking minion still contributes rank and slot-budget premises.");
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            first.GetType().GetField("IsSample",Flags).SetValue(first,false);minion.minionSlots+=1;
            object changed=alignment.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{0},new[]{0},0});
            if(alignment.GetMethod("Difference",Flags).Invoke(null,new[]{first,changed})==null)throw new InvalidOperationException("Changed minion budget cannot disappear from the proof.");
            Console.WriteLine("PASS original non-attacking spider minion contributes required aggregate premises");
        }
        private static void ClosedOwner(Assembly host,Func<byte[],Tuple<byte[],byte[]>> exchange)
        {
            NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();Main.player[0].position=new Vector2(300,1000);Main.player[0].maxMinions=10;
            Main.npc[0].position=new Vector2(1600,1000);Main.npc[0].ai[0]=-100;
            foreach(int slot in new[]{0,3,4,7}){var p=Main.projectile[slot];p.SetDefaults(393);p.whoAmI=slot;p.active=slot!=7;p.owner=slot==3?1:0;p.position=new Vector2(300+slot*10,1000);p.timeLeft=600;}
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
            var read=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true).GetMethod("Read",Flags);
            var n=Main.npc[0];var identity=new NpcIdentity(1,n,0,n.generation,n.type,n.netID);
            var missing=exchange((byte[])capture.Invoke(null,new object[]{new[]{0},new[]{4},0,1000L,1}));
            var refusal=read.Invoke(null,new object[]{missing.Item1,missing.Item2,identity,1000L,1L,false});var type=refusal.GetType();
            if((int)type.GetField("Kind",Flags).GetValue(refusal)!=2 || (int)type.GetField("Slot",Flags).GetValue(refusal)!=0)throw new InvalidOperationException("A missing earlier same-owner minion must request its exact full page.");
            var full=exchange((byte[])capture.Invoke(null,new object[]{new[]{0},new[]{0,4},0,1000L,1}));
            object recovered=read.Invoke(null,new object[]{full.Item1,full.Item2,identity,1000L,2L,false});
            if(type.GetField("Trajectory",Flags).GetValue(recovered)==null)throw new InvalidOperationException("The same worker must recover after the owner closure is sampled: "+type.GetField("Error",Flags).GetValue(recovered));
            Console.WriteLine("PASS same-owner minion missing full page / fresh closure / other owner and inactive excluded / same worker recovery");
        }
    }
}
