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
        private AccessTools.FieldRef<Player,bool> volcanoPending;
        [ThreadStatic] private static Projectile damageOwner;
        [ThreadStatic] private static int killDepth;
        [ThreadStatic] private static int transientDepth;
        [ThreadStatic] private static bool damageTransient;
        private struct DamageScope {internal Projectile Owner;internal bool Transient;}
        private struct KillScope {internal int Depth,Category;internal bool Capture;internal Vector2 Position,Size,OldVelocity;internal float Scale;}
        private struct MovementScope {internal int Depth;internal Projectile Owner;internal float InitialBounce;internal bool Sampled;}
        [ThreadStatic] private static Projectile movementOwner;
        [ThreadStatic] private static float initialBounce;
        [ThreadStatic] private static bool movementSampled;
        [ThreadStatic] private static Player itemOwner;
        internal bool Ready {get;private set;}
        internal Exception Error {get;private set;}
        internal static HostCombatObservation CaptureHost {get{var self=current;return self!=null && self.Ready && self.host.Capture?self.host:null;}}
        internal CombatGeometryHooks(HostCombatObservation host)
        {
            this.host=host;
            try
            {
                if(current!=null)throw new InvalidOperationException("Collision observer already installed");current=this;
                volcanoPending=AccessTools.FieldRefAccess<Player,bool>("_spawnVolcanoExplosion");
                Patch(typeof(Projectile),"Damage",nameof(BeforeDamage),null,nameof(EndDamage));
                Patch(typeof(Projectile),"Damage_GetHitbox",null,nameof(Hitbox),null);
                Patch(typeof(Projectile),"Kill",nameof(BeforeKill),null,nameof(EndKill));
                Patch(typeof(Projectile),"AI",nameof(BeforeAi),null,nameof(EndTransient));
                Patch(typeof(Projectile),"HandleMovement",nameof(BeforeMovement),null,nameof(EndMovement));
                Patch(typeof(Projectile),"UpdatePosition",nameof(BeforePosition),null,null);
                Patch(typeof(Projectile),"DoRainbowCrystalStaffExplosion",nameof(RemoteCrystal),null,null);
                Patch(typeof(Projectile),"SelfHurtPlayers",nameof(SelfHurt),null,null);
                Patch(typeof(Player),"ItemCheck_MeleeHitNPCs",nameof(Melee),nameof(AfterMelee),null);
                Patch(typeof(Player),"AnimatePlayerAndGetItemFrame",null,nameof(RemoteMelee),null);
                Patch(typeof(Player),"ItemCheck",nameof(BeforeItem),null,nameof(EndItem));
                // Share the established native-observer lifetime, not the
                // collision-display gate. Prediction needs actual relocation
                // facts even when only its path consumer is enabled.
                Patch(typeof(Player),"Teleport",nameof(PlayerTeleport),null,null);
                Patch(typeof(Player),"Spawn",nameof(PlayerSpawn),null,null);
                Patch(typeof(Player),"Hurt",null,nameof(PlayerHurt),null);
                Prediction.NativeNpcImpact.Install(harmony,methods);
                Patch(typeof(NPC),"SetDefaults",nameof(NpcReset),null,null);
                Patch(typeof(MessageBuffer),"GetData",nameof(PlayerNetwork),nameof(NpcNetwork),null);
                PlayerCollisionGeometryHooks.Install(harmony,methods);
                Ready=true;
            }
            catch(Exception e){Error=e;Dispose();}
        }
        private void Patch(Type type,string name,string prefix,string postfix,string finalizer)
        {
            var method=AccessTools.DeclaredMethod(type,name);if(method==null)throw new MissingMethodException(type.Name,name);
            methods.Add(method);harmony.Patch(method,prefix==null?null:new HarmonyMethod(GetType(),prefix),postfix==null?null:new HarmonyMethod(GetType(),postfix),null,finalizer==null?null:new HarmonyMethod(GetType(),finalizer));
        }
        private static HostCombatObservation PredictionHost
        {get{var self=current;return self!=null && self.Ready && self.host.Session>0 && self.host.Prediction.Cache.Required>0?self.host:null;}}
        internal static Prediction.NativePredictionSession PredictionSession=>PredictionHost?.Prediction.Native;
        private static bool Live(Player player)
        {return player!=null && player.active && player.whoAmI>=0 && player.whoAmI<Main.maxPlayers && ReferenceEquals(Main.player[player.whoAmI],player);}
        private static void PlayerTeleport(Player __instance,Vector2 __0)
        {var owner=PredictionHost;if(owner!=null && Live(__instance) && __instance.position!=__0)owner.Prediction.Native?.ObservePlayerRelocation();}
        private static void PlayerSpawn(Player __instance)
        {var owner=PredictionHost;if(owner!=null && Live(__instance))owner.Prediction.Native?.ObservePlayerRelocation();}
        private static void PlayerHurt(Player __instance,double __result)
        {
            // A successful native hit is new external input, including recoil
            // on otherwise conditional mounts. Rejected/immune hits do not
            // revoke results; ordinary immunity clocks are not exact premises.
            var owner=PredictionHost;if(owner!=null && __result>0 && Live(__instance))owner.Prediction.Native?.ObservePlayerRelocation();
        }
        private static void NpcReset(NPC __instance)
        {
            // SetDefaults/Transform can restore the same type within one tick,
            // retaining both object and generation. Latch the reconstruction.
            var owner=PredictionHost;if(owner!=null)owner.Prediction.Native?.ObserveNpcReset(__instance);
        }
        private static void NpcNetwork(MessageBuffer __instance,int __0,int __1)
        {
            var owner=PredictionHost;if(owner==null || Main.netMode!=1)return;
            var data=__instance.readBuffer;
            if(data==null || __0<0 || __1<3 || __0>data.Length-3 || data[__0]!=23)return;
            int slot=data[__0+1]|data[__0+2]<<8;if(slot<Main.maxNPCs)owner.Prediction.Native?.ObserveNpcQueryUpdate(slot);
        }
        private static void PlayerNetwork(MessageBuffer __instance,int __0,int __1)
        {
            var owner=PredictionHost;if(owner==null || Main.netMode!=1)return;
            var data=__instance.readBuffer;
            // Locked .8 packet 13: type, player, four bitsets, held slot,
            // position. Observe only a complete header without consuming its
            // reader or changing native handling. This is the original large
            // correction threshold: netOffset is reset to zero AFTER it, so
            // polling netOffset later would miss the actual discontinuity.
            if(data==null || __0<0 || __1<15 || __0>data.Length-15 || data[__0]!=13)return;
            int slot=data[__0+1];if(slot>=Main.maxPlayers || slot==Main.myPlayer && !Main.ServerSideCharacter)return;
            var player=Main.player[slot];if(!Live(player) || player.unacknowledgedTeleports>0 || player.position==Vector2.Zero)return;
            var incoming=new Vector2(BitConverter.ToSingle(data,__0+7),BitConverter.ToSingle(data,__0+11));
            if((player.netOffset+player.position-incoming).Length()>Main.multiplayerNPCSmoothingRange)owner.Prediction.Native?.ObservePlayerRelocation();
        }
        private static void BeforeDamage(Projectile __instance,out DamageScope __state)
        {
            __state=new DamageScope{Owner=damageOwner,Transient=damageTransient};var self=current;damageOwner=self!=null && self.Ready && self.host.Capture?__instance:null;
            if(damageOwner==null)return;
            // Read this before the getter consumes localAI[0]. A later harmless
            // substep retires a sustained region but cannot erase this event.
            bool expansion=(__instance.type==301 || __instance.type==383 || __instance.type==262) && __instance.localAI[0]>0;
            damageTransient=killDepth>0 || transientDepth>0 || expansion;
            if(!damageTransient && __instance.type!=949)self.host.Geometry.BeginDamage(__instance);
        }
        private static void EndDamage(DamageScope __state){damageOwner=__state.Owner;damageTransient=__state.Transient;}
        private static void BeforeKill(Projectile __instance,out KillScope __state)
        {
            __state=new KillScope{Depth=killDepth};killDepth++;var self=current;
            if(self==null || !self.Ready || !self.host.Capture)return;
            self.host.Geometry.BeginDamage(__instance);
            if(__instance.active && __instance.owner!=Main.myPlayer)
            {__state.Capture=true;__state.Category=CombatGeometry.Category(__instance);__state.Position=__instance.position;__state.Size=__instance.Size;__state.OldVelocity=__instance.oldVelocity;__state.Scale=__instance.scale;}
        }
        private static Exception EndKill(Projectile __instance,KillScope __state,Exception __exception)
        {
            killDepth=__state.Depth;var self=current;
            if(__exception==null && __state.Capture && self!=null && self.Ready && self.host.Capture)
                try{self.host.Geometry.RemoteTermination(__instance,__state.Position,__state.Size,__state.OldVelocity,__state.Scale,__state.Category);}catch{self.host.CollisionFailed();}
            return __exception;
        }
        private static void BeforeTransient(out int __state)
        {__state=transientDepth;var self=current;if(self!=null && self.Ready && self.host.Capture)transientDepth++;}
        private static void BeforeAi(Projectile __instance,out int __state)
        {BeforeTransient(out __state);var self=current;if(self!=null && self.Ready && self.host.Capture && __instance.type==949)self.host.Geometry.BeginDamage(__instance);}
        private static void EndTransient(int __state){transientDepth=__state;}
        private static void BeforeMovement(Projectile __instance,out MovementScope __state)
        {
            __state=new MovementScope{Depth=transientDepth,Owner=movementOwner,InitialBounce=initialBounce,Sampled=movementSampled};var self=current;
            movementOwner=null;movementSampled=false;
            if(self==null || !self.Ready || !self.host.Capture)return;
            transientDepth++;if(__instance.type==502){movementOwner=__instance;initialBounce=__instance.ai[0];}
        }
        private static void BeforePosition(Projectile __instance)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture || !ReferenceEquals(movementOwner,__instance) || movementSampled || __instance.owner==Main.myPlayer || __instance.ai[0]<=initialBounce)return;
            movementSampled=true;
            // Collision may have moved a projectile along a slope before the
            // impact. This natural boundary is after that correction and the
            // temporary Damage window, but before final velocity integration.
            // The fifth impact has already killed the projectile here.
            try{self.host.Geometry.CatImpact(__instance);}catch{self.host.CollisionFailed();}
        }
        private static void EndMovement(MovementScope __state)
        {transientDepth=__state.Depth;movementOwner=__state.Owner;initialBounce=__state.InitialBounce;movementSampled=__state.Sampled;}
        private static void RemoteCrystal(Projectile __instance)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture || __instance.owner==Main.myPlayer)return;
            try{self.host.Geometry.Crystal(__instance);}catch{self.host.CollisionFailed();}
        }
        private static void SelfHurt(Projectile __instance)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture)return;
            try{self.host.Geometry.SelfHurt(__instance);}catch{self.host.CollisionFailed();}
        }
        private static void BeforeItem(Player __instance,out Player __state)
        {__state=itemOwner;var self=current;itemOwner=self!=null && self.Ready && self.host.Capture?__instance:null;}
        private static void EndItem(Player __state){itemOwner=__state;}
        private static void Hitbox(Projectile __instance,Rectangle __result)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture || !ReferenceEquals(damageOwner,__instance) || __instance.type==949)return;
            // Torch God uses the earlier SelfHurt window. Its ordinary Damage
            // runs even when harmless, after movement; it is not a hurt sample.
            try{self.host.Geometry.Projectile(__instance,__result,damageTransient);}catch{self.host.CollisionFailed();}
        }
        private static void Melee(Player __instance,Item __0,Rectangle __1,int __2)
        {var self=current;if(self==null || !self.Ready || !self.host.Capture)return;try{self.host.Geometry.Melee(__instance,__0,__1,__2,__instance.whoAmI==Main.myPlayer?(bool?)self.volcanoPending(__instance):null);}catch{self.host.CollisionFailed();}}
        private static void AfterMelee(Player __instance,Item __0,Rectangle __1,int __2)
        {
            var self=current;if(self==null || !self.Ready || !self.host.Capture || __0.type!=121 || __instance.whoAmI!=Main.myPlayer)return;
            // The first struck NPC can consume the pending volcano inside the
            // native loop. The later rectangle is then a real phase of this
            // same swing; both phases share the always-active capsule.
            try{if(!self.volcanoPending(__instance))self.host.Geometry.Melee(__instance,__0,__1,__2,false);}catch{self.host.CollisionFailed();}
        }
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
        {Ready=false;foreach(var method in methods)try{harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);}catch(Exception e){if(Error==null)Error=e;}methods.Clear();if(ReferenceEquals(current,this))current=null;damageOwner=movementOwner=null;itemOwner=null;killDepth=transientDepth=0;damageTransient=movementSampled=false;}
    }
}
