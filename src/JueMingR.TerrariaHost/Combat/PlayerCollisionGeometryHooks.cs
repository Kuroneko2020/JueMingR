using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    // Player attacks which never enter Projectile.Damage or ItemCheck melee.
    // Read at their natural call boundary, before recoil/clock changes. The
    // observer never searches for victims, rolls crits or repeats native AI.
    internal static class PlayerCollisionGeometryHooks
    {
        internal static void Install(Harmony harmony,List<MethodBase> methods)
        {
            Patch(harmony,methods,"CollideWithNPCs",nameof(Contact));
            Patch(harmony,methods,"JumpMovement",nameof(Jump));
            Patch(harmony,methods,"DashMovement",nameof(Dash));
            Patch(harmony,methods,"Update_NPCCollision",nameof(Cart));
            Patch(harmony,methods,"DoDeadCellsGroundPoundEffect",nameof(GroundPound));
            Patch(harmony,methods,"UpdateBuffs",nameof(Inferno));
            Patch(harmony,methods,"ApplyDamageToNPC",nameof(Retaliation));
            Patch(harmony,methods,"TryHittingNPC",nameof(SelectedTarget));
        }
        private static void Patch(Harmony harmony,List<MethodBase> methods,string name,string observer)
        {var method=name=="ApplyDamageToNPC"?AccessTools.DeclaredMethod(typeof(Player),name,new[]{typeof(NPC),typeof(int),typeof(float),typeof(int),typeof(bool),typeof(string),typeof(int),typeof(int)}):AccessTools.DeclaredMethod(typeof(Player),name);if(method==null)throw new MissingMethodException("Player",name);methods.Add(method);harmony.Patch(method,prefix:new HarmonyMethod(typeof(PlayerCollisionGeometryHooks),observer));}
        private static void Contact(Player __instance,Rectangle __0,float __1)
        {var host=CombatGeometryHooks.CaptureHost;if(host==null)return;try{if(__1>0)host.Geometry.Body(__instance,__0);}catch{host.CollisionFailed();}}
        private static void Jump(Player __instance)
        {
            var host=CombatGeometryHooks.CaptureHost;if(host==null)return;
            try
            {
                var p=__instance;if(p.velocity.Y<=0 || !(p.isPerformingJump_DownDash || p.mount.Active && (p.mount.Type==17 || p.mount.IsConsideredASlimeMount && p.wetSlime==0)))return;
                // All three native windows have the identical foot strip. A
                // slime hit can cancel later windows by reversing Y velocity;
                // their union is still this one region frozen before recoil.
                var box=p.getRect();box.Offset(0,p.height-1);box.Height=2;box.Inflate(12,6);host.Geometry.Body(p,box);
            }
            catch{host.CollisionFailed();}
        }
        private static void Dash(Player __instance)
        {
            var host=CombatGeometryHooks.CaptureHost;if(host==null)return;
            try
            {
                var p=__instance;int type=p.mount.Active && p.mount.Type>=62 && p.mount.Type<=65?6:p.dashType;
                int dash=p.dashDelay==0?type:p.dash;
                if(!(dash==2 && p.eocDash>0 && p.eocHit<0 || (dash==3 || dash==6) && p.dashDelay<0 && p.whoAmI==Main.myPlayer))return;
                var box=new Rectangle((int)((double)p.position.X+p.velocity.X*.5-4),(int)((double)p.position.Y+p.velocity.Y*.5-4),p.width+8,p.height+8);
                if(dash==6){box.Width+=60;if(p.direction==-1)box.X-=60;}host.Geometry.Body(p,box);
            }
            catch{host.CollisionFailed();}
        }
        private static void Cart(Player __instance)
        {
            var host=CombatGeometryHooks.CaptureHost;if(host==null)return;
            try
            {
                // Player.Update calls this immediately after the cart attack
                // loop. That loop changes victims/crit clocks, not the player's
                // position or velocity. It is inside the local-player branch.
                var p=__instance;if(p.whoAmI!=Main.myPlayer || !p.mount.Active || !p.mount.Cart || p.velocity.Length()<=4)return;
                var box=p.getRect();if(p.velocity.X< -1)box.X-=15;if(p.velocity.X>1)box.Width+=15;if(p.velocity.X< -10)box.X-=10;if(p.velocity.X>10)box.Width+=10;if(p.velocity.Y< -1)box.Y-=10;if(p.velocity.Y>1)box.Height+=10;
                host.Geometry.Body(p,box);
            }
            catch{host.CollisionFailed();}
        }
        private static void GroundPound(Player __instance)
        {var host=CombatGeometryHooks.CaptureHost;if(host==null)return;try{if(__instance.whoAmI==Main.myPlayer)host.Geometry.BodyCircle(__instance,32*__instance.downDashTime>300?176:128,true,11);}catch{host.CollisionFailed();}}
        private static void Inferno(Player __instance)
        {
            var host=CombatGeometryHooks.CaptureHost;if(host==null)return;
            try
            {
                if(__instance.whoAmI!=Main.myPlayer)return;
                // A time=1 buff still executes its native branch this update.
                // The ring applies DOT between direct-damage pulses as well.
                for(int i=0;i<Player.maxBuffs;i++)if(__instance.buffType[i]==116 && __instance.buffTime[i]>0){host.Geometry.BodyCircle(__instance,200,false,12);break;}
            }
            catch{host.CollisionFailed();}
        }
        private static void Retaliation(Player __instance,NPC __0,int __1,string __5)
        {
            var host=CombatGeometryHooks.CaptureHost;if(host==null)return;
            try{if(__1>0 && (__5==PlayerNPCHitSources.PlayerThorns || __5==PlayerNPCHitSources.CactusThorns))host.Geometry.TargetEvent(__instance,__0);}
            catch{host.CollisionFailed();}
        }
        private static void SelectedTarget(Player __instance,NPC __0,int __1,int __4)
        {
            var host=CombatGeometryHooks.CaptureHost;if(host==null)return;
            try{if(__4==5478 && __1>0 && __instance.whoAmI==Main.myPlayer && __0.active && !__0.dontTakeDamage && !__0.friendly && __0.immune[__instance.whoAmI]==0 && __instance.CanNPCBeHitByPlayerOrPlayerProjectile(__0))host.Geometry.TargetEvent(__instance,__0);}
            catch{host.CollisionFailed();}
        }
    }
}
