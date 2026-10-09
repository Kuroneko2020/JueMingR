using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Utilities;
using JueMingR.Features.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Narrow original-game counterexamples, not a prediction backend. The
    // hooks only count actual method calls/results and are always removed.
    internal static class NativeCombatHostileQueryOracle
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static int inner,collisions,intersections,strikes,damaging;
        internal static void Run(object context,string output)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var rows=new List<string>{"scenario,step,tick,inner,collisionCalls,intersections,strikeCalls,positiveStrikes,life0,life1,penetrate,active,x,y,hostileImmune,detail"};
            var patches=new Harmony("JueMingR.Tests.HostileQueryOracle");
            bool finished=false;
            try
            {
                patches.Patch(typeof(Projectile).GetMethod("Damage_PVE_Inner",Flags),prefix:Hook(nameof(Inner)));
                patches.Patch(typeof(Projectile).GetMethod("Colliding",Flags),postfix:Hook(nameof(Collision)));
                patches.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags),postfix:Hook(nameof(Strike)));
                Projectile p=Scene();var town=Main.npc[0];Main.npc[1].active=false;
                p.Center=town.Center-new Vector2(300,0);p.Damage();Record(rows,"positive-nonintersecting",0,p);
                Require(inner==1 && collisions==1 && intersections==0 && strikes==0 && town.life==town.lifeMax && p.penetrate==1,"Default hostile/town eligibility reaches actual geometry without a hit.");

                p=Scene();town=Main.npc[0];Main.npc[1].active=false;town.dontTakeDamageFromHostiles=true;p.Center=town.Center;p.Damage();Record(rows,"hostile-immunity-true",0,p);
                Require(inner==1 && collisions==0 && strikes==0 && p.penetrate==1,"Explicit hostile immunity returns before geometry, unlike default town.");
                ResetCounts();town.dontTakeDamageFromHostiles=false;NativeQuickItemChecks.BeginWorldStep();p.Damage();Record(rows,"hostile-immunity-removed",1,p);
                Require(intersections==1 && damaging==1 && town.life<town.lifeMax && p.penetrate==0,"A later qualification change permits a real hit and consumes penetration.");

                p=Scene();p.Center=Main.npc[0].Center;Main.npc[1].position=Main.npc[0].position;p.Damage();Record(rows,"two-towns-one-penetration",0,p);
                Require(inner==1 && damaging==1 && Main.npc[0].life<Main.npc[0].lifeMax && Main.npc[1].life==Main.npc[1].lifeMax && p.penetrate==0,"The first native PVE victim consumes the single penetration before the second candidate.");
                // Damage_PVE only consumes penetration; Update owns Kill.
                // Use a fresh arrow for the complete native Update order:
                // manually calling Damage and then Update would damage twice.
                p=Scene();p.Center=Main.npc[0].Center;Main.npc[1].position=Main.npc[0].position;
                Main.ProjectileUpdateLoopIndex=p.whoAmI;try{p.Update(p.whoAmI);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                Record(rows,"native-update-two-towns-one-penetration",1,p);
                Require(damaging==1 && Main.npc[0].life<Main.npc[0].lifeMax && Main.npc[1].life==Main.npc[1].lifeMax && !p.active,"One complete native update damages only the first town and terminates the exhausted arrow.");

                p=Scene();town=Main.npc[0];Main.npc[1].position=new Vector2(1550,2400-Main.npc[1].height);p.Center=town.Center-new Vector2(180,0);p.velocity=new Vector2(11,0);
                Require(!p.Hitbox.Intersects(town.Hitbox),"The future-entry scene starts outside the true hit rectangles.");Record(rows,"future-entry",0,p);
                int hitStep=-1;
                for(int step=1;step<=40 && p.active;step++)
                {
                    NativeQuickItemChecks.BeginWorldStep();NPC.UpdateProtectedSpawnSlots();NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();
                    using(Main.SwapRandom("UpdateNPCs"))for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);
                    using(Main.SwapRandom("UpdateProjectiles")){Main.ProjectileUpdateLoopIndex=p.whoAmI;try{p.Update(p.whoAmI);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                    if(hitStep<0 && town.life<town.lifeMax)hitStep=step;Record(rows,"future-entry",step,p);
                }
                Require(hitStep>0 && damaging==1 && !p.active && Main.npc[1].life==Main.npc[1].lifeMax,"Initially separate original actors later collide; the arrow cannot continue to the more distant town.");

                p=Scene();town=Main.npc[0];
                var host=Get(context,"CombatObservation").GetType().Assembly;var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcEligibility+Premise",true);
                object premise=Activator.CreateInstance(type,Flags,null,new object[]{town},null);
                Require((bool)type.GetProperty("Current",Flags).GetValue(premise),"Default town is an existing friendly-only query premise.");
                town.dontTakeDamageFromHostiles=true;
                Require((bool)type.GetProperty("Current",Flags).GetValue(premise),"Current friendly-only premise does not promise hostile immunity; a future negative-hostile contract needs more facts.");
                Record(rows,"existing-premise-hostile-flag",0,p,"Current remains true; production NoHit still excludes hostile");
                var replacement=new NPC();replacement.SetDefaults(678);replacement.whoAmI=town.whoAmI;replacement.active=true;replacement.position=town.position;Main.npc[0]=replacement;
                Require(replacement.type==town.type && replacement.netID==town.netID && replacement.generation==town.generation && !(bool)type.GetProperty("Current",Flags).GetValue(premise),"Equal visible identity fields cannot transfer a premise to another object.");
                Record(rows,"same-slot-generation-new-object",0,p,"Old premise Current=false");
                finished=true;Console.WriteLine("HOSTILE-ORACLE original qualification/geometry/penetration/future-entry/identity assertions passed; hit step="+hitStep);
            }
            finally
            {
                patches.UnpatchAll(patches.Id);File.WriteAllLines(Path.Combine(output,"hostile-oracle.csv"),rows);
                File.WriteAllText(Path.Combine(output,"hostile-oracle-status.txt"),finished?"narrow-original-counterexamples-passed":"interrupted-or-invalid");
            }
        }
        private static Projectile Scene()
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.rand=new UnifiedRandom(171);
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
            Main.LocalPlayer.position=new Vector2(500,2400-Main.LocalPlayer.height);Main.LocalPlayer.velocity=Vector2.Zero;Main.LocalPlayer.immune=true;Main.LocalPlayer.immuneTime=100000;
            for(int i=0;i<2;i++){var n=new NPC();n.SetDefaults(678);n.whoAmI=i;n.active=true;n.position=new Vector2(1300+i*120,2400-n.height);Main.npc[i]=n;Require(n.friendly && !n.dontTakeDamageFromHostiles && !n.dontTakeDamage,"Locked default 678 remains damageable by hostile projectiles.");}
            int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(1100,2360),new Vector2(11,0),82,35,0,Main.myPlayer);var p=Main.projectile[slot];
            Require(p.hostile && !p.friendly && p.penetrate==1 && p.maxPenetrate==1,"Locked native P82 defaults are the tested damage mechanism.");ResetCounts();return p;
        }
        private static void Record(List<string> rows,string scenario,int step,Projectile p,string detail="")
        {rows.Add(Csv(scenario,step,Main.GameUpdateCount,inner,collisions,intersections,strikes,damaging,Main.npc[0].life,Main.npc[1].life,p.penetrate,p.active,p.position.X,p.position.Y,Main.npc[0].dontTakeDamageFromHostiles,detail));}
        private static void ResetCounts(){inner=collisions=intersections=strikes=damaging=0;}
        private static void Inner(){inner++;}
        private static void Collision(bool __result){collisions++;if(__result)intersections++;}
        private static void Strike(int __result){strikes++;if(__result>0)damaging++;}
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeCombatHostileQueryOracle).GetMethod(name,Flags));
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
