using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class CombatGeometryHooks : IDisposable
    {
        private static CombatGeometryHooks current;
        private readonly HostCombatObservation host;
        private readonly Harmony harmony=new Harmony("JueMingR.CombatObservation");
        private readonly List<MethodBase> methods=new List<MethodBase>();
        [ThreadStatic] private static Projectile damageOwner;
        [ThreadStatic] private static int killDepth;
        [ThreadStatic] private static Player itemOwner;
        internal bool Ready {get;private set;}
        internal Exception Error {get;private set;}
        internal CombatGeometryHooks(HostCombatObservation host)
        {
            this.host=host;
            try
            {
                if(current!=null)throw new InvalidOperationException("Collision observer already installed");current=this;
                Patch(typeof(Projectile),"Damage",nameof(BeforeDamage),null,nameof(EndDamage));
                Patch(typeof(Projectile),"Damage_GetHitbox",null,nameof(Hitbox),null);
                Patch(typeof(Projectile),"Kill",nameof(BeforeKill),null,nameof(EndKill));
                Patch(typeof(Player),"ItemCheck_MeleeHitNPCs",nameof(Melee),null,null);
                Patch(typeof(Player),"AnimatePlayerAndGetItemFrame",null,nameof(RemoteMelee),null);
                Patch(typeof(Player),"ItemCheck",nameof(BeforeItem),null,nameof(EndItem));
                Ready=true;
            }
            catch(Exception e){Error=e;Dispose();}
        }
        private void Patch(Type type,string name,string prefix,string postfix,string finalizer)
        {
            var method=AccessTools.DeclaredMethod(type,name);if(method==null)throw new MissingMethodException(type.Name,name);
            methods.Add(method);harmony.Patch(method,prefix==null?null:new HarmonyMethod(GetType(),prefix),postfix==null?null:new HarmonyMethod(GetType(),postfix),null,finalizer==null?null:new HarmonyMethod(GetType(),finalizer));
        }
        private static void BeforeDamage(Projectile __instance,out Projectile __state)
        {
            __state=damageOwner;var self=current;damageOwner=self!=null && self.Ready && self.host.Capture?__instance:null;
            if(damageOwner!=null && killDepth==0)self.host.Geometry.BeginDamage(__instance);
        }
        private static void EndDamage(Projectile __state){damageOwner=__state;}
        private static void BeforeKill(Projectile __instance,out int __state)
        {__state=killDepth;killDepth++;var self=current;if(self!=null && self.Ready && self.host.Capture)self.host.Geometry.BeginDamage(__instance);}
        private static void EndKill(int __state){killDepth=__state;}
        private static void BeforeItem(Player __instance,out Player __state)
        {__state=itemOwner;var self=current;itemOwner=self!=null && self.Ready && self.host.Capture?__instance:null;}
        private static void EndItem(Player __state){itemOwner=__state;}
        private static void Hitbox(Projectile __instance,Rectangle __result)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture || !ReferenceEquals(damageOwner,__instance))return;
            try{self.host.Geometry.Projectile(__instance,__result,killDepth>0);}catch{self.host.CollisionFailed();}
        }
        private static void Melee(Player __instance,Item __0,Rectangle __1,int __2)
        {var self=current;if(self==null || !self.Ready || !self.host.Capture)return;try{self.host.Geometry.Melee(__instance,__0,__1,__2);}catch{self.host.CollisionFailed();}}
        private static void RemoteMelee(Player __instance,Item __1,Rectangle __result)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture || __instance.whoAmI==Main.myPlayer || !ReferenceEquals(itemOwner,__instance) || __instance.whoAmI<0 || __instance.whoAmI>=Main.maxPlayers || !ReferenceEquals(Main.player[__instance.whoAmI],__instance))return;
            try
            {
                // The owner-only damage entry never runs for a remote player.
                // Observe its naturally updated pose, then use the audited pure
                // getter with the actual returned item frame. No animation or
                // damage method is advanced a second time.
                if(__instance.JustDroppedAnItem || !(__instance.IsAllowedToHoldItems || __instance.mount.DismountOnItemUse) || __instance.itemAnimation<=0 || __1.type<=0 || __1.noMelee || __1.damage<=0)return;
                bool blocked;Rectangle box;__instance.ItemCheck_GetMeleeHitbox(__1,__result,out blocked,out box);
                if(!blocked)self.host.Geometry.Melee(__instance,__1,box,__1.damage);
            }
            catch{self.host.CollisionFailed();}
        }
        public void Dispose()
        {Ready=false;foreach(var method in methods)try{harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);}catch(Exception e){if(Error==null)Error=e;}methods.Clear();if(ReferenceEquals(current,this))current=null;damageOwner=null;itemOwner=null;killDepth=0;}
    }
}
