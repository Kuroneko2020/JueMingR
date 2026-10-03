using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    // Real envelopes and original frozen combat, not a graph-only unit test.
    internal static class NativeCombatPurposeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(Assembly host,Func<byte[],Tuple<byte[],byte[]>> exchange,string output)
        {
            NativeCombatWorkerImmunityChecks.CombatFont(output);
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
            var result=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true);
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            var observe=alignment.GetMethod("Observe",Flags);var difference=alignment.GetMethod("Difference",Flags);
            foreach(string scenario in new[]{"clear","blocked","fall-enter","exit","blocked-shift"})
            {
                NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();
                for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
                Main.player[0].immune=true;Main.player[0].immuneTime=100000;
                bool shifted=scenario=="blocked-shift";
                var target=Main.npc[0];target.SetDefaults(3);target.whoAmI=0;target.active=true;target.life=target.lifeMax=1000;target.position=new Vector2(shifted?850:800,1000);target.target=0;
                var background=Main.npc[1];background.SetDefaults(3);background.whoAmI=1;background.active=true;background.life=background.lifeMax=1000;background.position=new Vector2(scenario=="clear" || scenario=="exit"?1400:shifted?740:700,scenario=="fall-enter"?940:1000);background.target=0;
                if(scenario=="fall-enter")Require(background.Bottom.Y<1010,"Entering background really begins above the shot's hitbox.");
                int shot=Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(shifted?610:600,1015),new Vector2(scenario=="fall-enter"?4:shifted?11:12,0),1,20,3,0);
                var arrow=Main.projectile[shot];arrow.aiStyle=0;arrow.tileCollide=false;arrow.timeLeft=100;
                Require(arrow.friendly && arrow.penetrate==1,"Original arrow is a finite friendly shot.");
                byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0,1},new[]{shot},0,1000L,120});
                var reply=exchange(snapshot);File.WriteAllBytes(Path.Combine(output,"purpose-"+scenario+"-core.bin"),reply.Item1);File.WriteAllBytes(Path.Combine(output,"purpose-"+scenario+"-proof.bin"),reply.Item2);
                var identity=new NpcIdentity(1,target,0,target.generation,target.type,target.netID);
                object decoded=result.GetMethod("Read",Flags).Invoke(null,new object[]{reply.Item1,reply.Item2,identity,1000L,1L,false});
                var frames=(Array)result.GetField("Frames",Flags).GetValue(decoded);object frame=frames.GetValue(0);
                bool[] required=(bool[])frame.GetType().GetField("NpcRequired",Flags).GetValue(frame);
                bool expectsBlock=scenario=="blocked" || scenario=="fall-enter" || shifted;
                background.life--;
                object actual=observe.Invoke(null,new object[]{1000L,new[]{0,1},new[]{shot},0});
                Require(difference.Invoke(null,new[]{frame,actual})!=null,"Real captured frame zero stays strict even for unused background.");background.life++;
                frame.GetType().GetField("IsSample",Flags).SetValue(frame,false);background.life--;
                actual=observe.Invoke(null,new object[]{1000L,new[]{0,1},new[]{shot},0});
                Require(difference.Invoke(null,new[]{frame,actual})!=null,"Even unused future background vitality is an independent condition.");background.life++;
                Vector2 oldPosition=background.position,oldVelocity=background.velocity;
                background.position+=new Vector2(1,0);background.velocity+=new Vector2(.1f,0);
                actual=observe.Invoke(null,new object[]{1000L,new[]{0,1},new[]{shot},0});
                Require((difference.Invoke(null,new[]{frame,actual})!=null)==expectsBlock,"Unharmed background motion still follows genuine collision roles.");background.position=oldPosition;background.velocity=oldVelocity;
                byte oldGeneration=background.generation;var generation=typeof(NPC).GetField("<generation>k__BackingField",Flags);generation.SetValue(background,(byte)(oldGeneration+1));
                actual=observe.Invoke(null,new object[]{1000L,new[]{0,1},new[]{shot},0});
                Require(difference.Invoke(null,new[]{frame,actual})!=null,"Even unused background cannot masquerade as another instance.");generation.SetValue(background,oldGeneration);
                frame.GetType().GetField("IsSample",Flags).SetValue(frame,true);
                // Classification must retire when the formerly protected player
                // can become hittable inside this window; it is not permanent.
                Main.player[0].immuneTime=180;
                actual=observe.Invoke(null,new object[]{1000L,new[]{0,1},new[]{shot},0});
                Require(difference.Invoke(null,new[]{frame,actual})!=null,"Long immunity window condition is authenticated.");Main.player[0].immuneTime=100000;
                int firstHit=-1;float maxPositive=0;
                NativeCombatWorkerChecks.Compare(reply.Item1,0,output,"purpose-"+scenario,nativeStep:(tick,n)=>
                {if(n.life<1000 && firstHit<0)firstHit=tick;if(n.life<1000)maxPositive=Math.Max(maxPositive,n.velocity.X);});
                Console.WriteLine("PURPOSE original "+scenario+" background-required="+required[1]+" target-life="+target.life+" background-life="+background.life+" background-position="+background.position+" first-target-hit="+firstHit+" max-positive-velocity="+maxPositive);
                Require(required[0] && required[1]==expectsBlock,"Only genuine native collision/impact makes background a full route premise: "+scenario);
                Require(expectsBlock?target.life==1000 && background.life<1000:target.life<1000,"Original blocked versus clear target damage is preserved: "+scenario);
                Require(expectsBlock?firstHit<0:firstHit>0 && maxPositive>0,"Prediction's exact oracle includes the actual target knockback: "+scenario);
                Console.WriteLine("PURPOSE "+scenario+" background-required="+required[1]+" target-life="+target.life+" background-life="+background.life+" first-target-hit="+firstHit+" max-positive-velocity="+maxPositive);
            }
            Console.WriteLine("PASS bounded purpose / finite background absorption / future fall enters / active exit / exact native damage knockback / sampled state and immunity condition");
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
