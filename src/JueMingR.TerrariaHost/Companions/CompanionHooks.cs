using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        internal static readonly Harmony Harmony=new Harmony("JueMingR.Companions");
        private static HostCompanions host;
        private static bool installed;
        [ThreadStatic] internal static Player Spawning;
        [ThreadStatic] private static Scope current;
        private static readonly MethodInfo enemyDamage=AccessTools.Method(typeof(Projectile),"Damage_EVP");
        private static readonly MethodInfo canDamage=AccessTools.Method(typeof(Projectile),"Damage_CanDealDamage");
        private static readonly MethodInfo hurtTile=AccessTools.Method(typeof(Player),"GetHurtTile"),touchDamage=AccessTools.Method(typeof(Player),"ApplyTouchDamage"),shimmer=AccessTools.Method(typeof(Player),"TryToShimmerUnstuck");
        internal static void Install(HostCompanions value)
        {
            host=value;
            if(installed)return;
            Patch(typeof(Player),"Update",nameof(PlayerBefore),nameof(PlayerAfter),nameof(PlayerFinal),nameof(UpdateGates),new[]{typeof(int)});
            Patch(typeof(Main),"UpdateWorld_NPCs",postfix:nameof(NpcWorldAfter));
            Patch(typeof(Player),"ItemCheck",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Harmony.Patch(AccessTools.PropertyGetter(typeof(PlayerInput),"ShouldFastUseItem"),postfix:Hook(nameof(FastUse)));
            Patch(typeof(PlayerInput),"TryEndingFastUse",nameof(EndFastUse));
            Patch(typeof(Main),"LocalPlayerHasPendingInventoryActions",nameof(PendingInventory));
            Patch(typeof(Player),"Hurt",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"CheckDrowning",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"UpdateLifeRegen",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"KillMe",nameof(DeathBefore),finalizer:nameof(DeathFinal));
            Patch(typeof(Player),"Spawn",transpiler:nameof(SpawnLocals));
            Patch(typeof(Player),"PickupItem",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"GrabItems",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(WorldItem),"UpdateItem",postfix:nameof(ItemOwner));
            Patch(typeof(Projectile),"AI",nameof(ProjectileBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Projectile),"Damage",nameof(ProjectileBefore),nameof(ProjectileDamageAfter),nameof(ScopeFinal));
            Patch(typeof(Projectile),"Kill",nameof(ProjectileBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(NetMessage),"SendPacketToServer",nameof(Send));
            Patch(typeof(MessageBuffer),"GetData",nameof(Packet),nameof(PacketAfter));
            Patch(typeof(NPC),"GetPendingDamage",nameof(ObservationPending));
            Patch(typeof(NPC),"HitEffect",nameof(ObservationVisual));
            Patch(typeof(NPC),"TransformVisuals",nameof(ObservationVisual));
            Patch(typeof(NPC),"OnSpawn",nameof(ObservationVisual));
            Patch(typeof(CombatText),"NewText",nameof(ObservationVisual),args:new[]{typeof(Rectangle),typeof(Color),typeof(int),typeof(bool),typeof(bool)});
            Patch(typeof(WorldGen),"saveToonWhilePlaying",nameof(Save));
            Patch(typeof(WorldGen),"SaveAndQuit",nameof(Leaving),args:new[]{typeof(Action)});

            Patch(typeof(NPC),"PushPendingDamage",postfix:nameof(PredictionPending));
            Patch(typeof(Projectile),"Damage_PVE_Inner",nameof(ProjectilePredictionBefore),finalizer:nameof(PredictionFinal));
            Patch(typeof(Player),"ProcessHitAgainstNPC",nameof(MeleePredictionBefore),finalizer:nameof(PredictionFinal),transpiler:nameof(PredictionTarget));
            Patch(typeof(Player),"ApplyDamageToNPC",nameof(DirectPredictionBefore),finalizer:nameof(PredictionFinal),args:new[]{typeof(NPC),typeof(int),typeof(float),typeof(int),typeof(bool),typeof(PlayerNPCHitSource)});
            installed=true;
        }
        private static void Patch(Type t,string method,string prefix=null,string postfix=null,string finalizer=null,string transpiler=null,Type[] args=null)
        {
            var m=args==null?AccessTools.Method(t,method):AccessTools.Method(t,method,args);if(m==null)throw new MissingMethodException(t.FullName,method);
            Harmony.Patch(m,Hook(prefix),Hook(postfix),Hook(transpiler),Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(CompanionHooks),name);}
        internal static void Uninstall(){Harmony.UnpatchAll(Harmony.Id);installed=false;host=null;}
        internal static void ProjectileBorn(Player player,Projectile projectile){host?.ProjectileBorn(player,projectile);}
        internal static Scope Enter(HostCompanions.Actor actor,Vector2 point,bool inventory=false){return new Scope(actor,point,inventory);}
        internal sealed class Scope : IDisposable
        {
            internal readonly HostCompanions.Actor Actor;private readonly Scope previous;
            private readonly int oldPlayer,x,y,tileX,tileY;private readonly bool map;private readonly Item mouse;private bool ended;
            private readonly int life,mana;private readonly bool inventory;private readonly ulong resources;
            private readonly NPC[] npcTable;
            private static ulong Resources(Player p){unchecked{ulong value=1469598103934665603;for(int i=0;i<p.inventory.Length;i++){var item=p.inventory[i];value=(value^(uint)(item.type*31+item.stack))*1099511628211;}return value;}}
            internal Scope(HostCompanions.Actor actor,Vector2 aim,bool inventory)
            {
                this.inventory=inventory;life=actor.Body.statLife;mana=actor.Body.statMana;if(inventory)resources=Resources(actor.Body);
                npcTable=host.EnterNpcObservation();
                Actor=actor;previous=current;current=this;oldPlayer=Main.myPlayer;x=Main.mouseX;y=Main.mouseY;tileX=Player.tileTargetX;tileY=Player.tileTargetY;map=Main.mapFullscreen;mouse=Main.mouseItem;
                Main.myPlayer=actor.Body.whoAmI;Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(actor.Body.gravDir<0?Main.screenHeight-aim.Y+Main.screenPosition.Y:aim.Y-Main.screenPosition.Y);
                Player.tileTargetX=(int)(aim.X/16);Player.tileTargetY=(int)(aim.Y/16);
            }
            public void Dispose(){if(ended)return;ended=true;if(life!=Actor.Body.statLife || mana!=Actor.Body.statMana || inventory && resources!=Resources(Actor.Body))Actor.Dirty=true;host.LeaveNpcObservation(npcTable);Main.myPlayer=oldPlayer;Main.mouseX=x;Main.mouseY=y;Player.tileTargetX=tileX;Player.tileTargetY=tileY;Main.mapFullscreen=map;Main.mouseItem=mouse;current=previous;}
        }
        private sealed class PlayerStep {internal int X,Y;internal NPC[] Npcs;}
        private static bool PlayerBefore(Player __instance,ref PlayerStep __state)
        {
            var a=host?.Owned(__instance);if(a==null)return true;
            if(!a.Ready || a.Closing || a.Faulted)return false;
            if(a.LastUpdate==Main.GameUpdateCount)return false;
#if JMR_COMPANION_FAULT_DIAGNOSTIC
            CompanionUpdateFaultTrace.Phase(a,"before-body/observation");
#endif
            __state=new PlayerStep{X=Player.tileTargetX,Y=Player.tileTargetY,Npcs=host.EnterNpcObservation()};
            a.LastUpdate=Main.GameUpdateCount;
#if JMR_COMPANION_FAULT_DIAGNOSTIC
            CompanionUpdateFaultTrace.Phase(a,"before-body/control");
#endif
            host.Control(a);
#if JMR_COMPANION_FAULT_DIAGNOSTIC
            CompanionUpdateFaultTrace.Phase(a,"native-player-update");
#endif
            return true;
        }
        private static void PlayerAfter(Player __instance,PlayerStep __state)
        {var a=host?.Owned(__instance);if(__state!=null && a!=null && a.Ready && !a.Closing){
#if JMR_COMPANION_FAULT_DIAGNOSTIC
            CompanionUpdateFaultTrace.Phase(a,"after-body/publish");
#endif
            host.AfterBody(a);}}
        private static void NpcWorldAfter(){host?.AdvanceNpcObservation();}
        private static Exception PlayerFinal(Exception __exception,Player __instance,PlayerStep __state)
        {
            if(__state!=null){host.LeaveNpcObservation(__state.Npcs);Player.tileTargetX=__state.X;Player.tileTargetY=__state.Y;}
            if(__exception!=null){var a=host?.Owned(__instance);if(a!=null){
#if JMR_COMPANION_FAULT_DIAGNOSTIC
                CompanionUpdateFaultTrace.Capture(a,__exception);
#endif
                a.Status="人物更新失败："+__exception.Message;a.Faulted=true;a.Body.controlUseItem=false;a.Body.channel=false;return null;}}return __exception;
        }
        private static void UseBefore(Player __instance,MethodBase __originalMethod,ref Scope __state)
        {var a=host?.Owned(__instance);if(a!=null && a.Ready && !a.Closing){bool use=__originalMethod.Name=="ItemCheck";if(use)host.PrepareUse(a);__state=Enter(a,a.Aim,__originalMethod.Name=="PickupItem" || use && (__instance.controlUseItem || __instance.itemAnimation>0 || __instance.channel));}}
        private static void FastUse(ref bool __result){if(current!=null)__result=false;}
        private static bool EndFastUse(){return current==null;}
        private static bool PendingInventory(ref bool __result){if(current==null)return true;__result=false;return false;}
        private static Exception ScopeFinal(Exception __exception,Scope __state){__state?.Dispose();return __exception;}
        private static void DeathBefore(Player __instance,ref Scope __state){var a=host?.Owned(__instance);if(a!=null && a.Ready && !a.Closing)__state=Enter(a,a.Aim);}
        private static Exception DeathFinal(Exception __exception,Player __instance,Scope __state)
        {try{if(__state!=null)host.ObserveDeath(__state.Actor);}finally{__state?.Dispose();}return __exception;}
        private static void ProjectileBefore(Projectile __instance,ref Scope __state)
        {if(host==null || !host.HasActors)return;var a=host.ProjectileActor(__instance);if(a!=null && a.Ready && !a.Closing)__state=Enter(a,host.ProjectileAim(a,__instance));}
        private static void ProjectileDamageAfter(Projectile __instance)
        {
            if(host==null || !host.HasActors || !__instance.active || !__instance.hostile || __instance.damage<=0 || !(bool)canDamage.Invoke(__instance,null))return;
            var hitbox=__instance.Damage_GetHitbox();
            foreach(var a in host.ActiveActors)
            {
                if(!a.Ready || a.Closing || !a.Body.active || a.Body.dead || !__instance.Colliding(hitbox,a.Body.Hitbox))continue;
                using(Enter(a,a.Aim))enemyDamage.Invoke(__instance,new object[]{hitbox});
            }
        }
        private static bool Send(byte[] data)
        {
            if(Main.netMode==1 && current==null && data!=null && data.Length>=3 && data[2]==38)host?.HumanPassword(data);
            if(Main.netMode==1 && current==null && data!=null && data.Length>=3 && data[2]==154)host?.PrimaryPingSent();

            if(Main.netMode!=1 || current==null || current.Actor.Connection==null)return true;
            int length=data[0]|data[1]<<8;var copy=new byte[length];Buffer.BlockCopy(data,0,copy,0,length);current.Actor.Connection.Send(copy);return false;
        }
        private static bool Packet(MessageBuffer __instance,int start,int length,ref int messageType)
        {if(Main.netMode!=1 || host==null)return true;if(host.Decoding==null && length>0){if(__instance.readBuffer[start]==3)host.HumanAccepted();if(__instance.readBuffer[start]==154 && host.PrimaryPingReceived()){messageType=154;return false;}}if(host.AcceptPrimary(__instance.readBuffer,start,length))return true;messageType=__instance.readBuffer[start];if(Netplay.Connection!=null)Netplay.Connection.TimeOutTimer=0;return false;}
        private static void PacketAfter(MessageBuffer __instance,int start,int length,int messageType,bool __runOriginal)
        {if(__runOriginal && Main.netMode==1 && host!=null && !host.ReadingObservation && host.Decoding==null && (messageType==23 || messageType==28 || messageType==54))host.PrimaryNpcApplied(__instance.readBuffer,start,length);}
        private static bool ObservationPending(ref int damage,ref bool phaseChange)
        {if(host==null || !host.ReadingObservation)return true;damage=0;phaseChange=false;return false;}
        private static bool ObservationVisual(){return host==null || !host.ReadingObservation;}
        private static bool Save()
        {
            if(current==null)return true;
            // KillMe's request saves this role through its own archive. The
            // human save outlet remains fully active outside this exact scope.
            host.ObserveDeath(current.Actor);return false;
        }
        private static void Leaving(){host?.OnSessionEnded();}
        private static void ItemOwner(WorldItem __instance)
        {
            if(host==null || !host.HasActors || Main.netMode!=0 || !__instance.active || __instance.instanced || __instance.timeToKeepReservation>0 || __instance.shimmerTime>0 || __instance.grabDelayTime>0 && __instance.grabDelayPlayer==255)return;
            // Single-player vanilla reserves every drop for its one human.
            // Use the same native nearest/space eligibility for the additional
            // local bodies. No inventory or world stack is copied here.
            int chosen=Main.myPlayer;float best=Single.MaxValue;
            var real=Main.LocalPlayer;
            if(real!=null && real.active && !real.dead && real.CanPullItem(__instance,real.ItemSpace(__instance)))best=Math.Abs(real.Center.X-__instance.Center.X)+Math.Abs(real.Center.Y-__instance.position.Y-__instance.height);
            foreach(var a in host.ActiveActors)
            {
                var p=a.Body;if(!a.Ready || a.Closing || !p.active || p.dead || __instance.grabDelayTime>0 && __instance.grabDelayPlayer==p.whoAmI)continue;
                if(!p.CanPullItem(__instance,p.ItemSpace(__instance)))continue;
                float distance=Math.Abs(p.Center.X-__instance.Center.X)+Math.Abs(p.Center.Y-__instance.position.Y-__instance.height);
                if(distance<best){best=distance;chosen=p.whoAmI;}
            }
            __instance.playerIndexTheItemIsReservedFor=chosen;
        }
        private static int OwnerForUse(Player p){return host?.Owned(p)!=null?p.whoAmI:Main.myPlayer;}
        private static void ReplaceOwner(List<CodeInstruction> code,int at)
        {
            var prior=code[at];var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(prior.labels);load.blocks.AddRange(prior.blocks);code[at]=load;code.Insert(at+1,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CompanionHooks),nameof(OwnerForUse))));
        }
        private static IEnumerable<CodeInstruction> UpdateGates(IEnumerable<CodeInstruction> input)
        {
            var c=input.ToList();var field=AccessTools.Field(typeof(Main),"myPlayer");int falls=0,lavas=0,contacts=0;
            for(int i=c.Count-1;i>=0;i--)if(c[i].LoadsField(field))
            {
                int branch=-1;for(int j=i+1;j<Math.Min(c.Count,i+7);j++)if(c[j].opcode.FlowControl==FlowControl.Cond_Branch && c[j].operand is Label){branch=j;break;}
                if(branch<0)continue;var label=(Label)c[branch].operand;int end=c.FindIndex(branch+1,x=>x.labels.Contains(label));if(end<=branch)continue;
                bool damage=false,fall=false,lava=false,other=false;
                for(int j=branch+1;j<end;j++)
                {
                    var f=c[j].operand as FieldInfo;if(f!=null){fall|=f.Name=="fallStart2";lava|=f.Name=="lavaTime";}
                    var method=c[j].operand as MethodInfo;if(method==null)continue;string name=method.Name;
                    damage|=name=="Hurt";
                    other|=name=="CopyInto" || name=="ResetControls" || name=="SmartCursorLookup" || name=="ItemCheck" || name=="Update_NPCCollision" || name=="InputTextSign" || name=="InputTextChest";
                }
                // These two original body-only blocks own fall and lava
                // damage. The much larger local UI/NPC block is never widened.
                if(damage && !other && (fall || lava)){ReplaceOwner(c,i);if(fall)falls++;if(lava)lavas++;}
            }
            foreach(var instruction in c)if(instruction.Calls(AccessTools.Method(typeof(Player),"GrappleMovement"))){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(CompanionHooks),nameof(ContactThenGrapple));contacts++;}
            if(falls!=1 || lavas!=1 || contacts!=1)throw new InvalidOperationException("Companion native survival layout changed: fall="+falls+" lava="+lavas+" contact="+contacts);return c;
        }
        private static void ContactThenGrapple(Player p)
        {
            var a=host?.Owned(p);
            if(a!=null && a.Ready && !a.Closing && !p.dead)
                using(Enter(a,a.Aim))
                {p.Update_NPCCollision();if(!p.shimmering){var tile=(Collision.HurtTile)hurtTile.Invoke(p,null);if(tile.type>=0)touchDamage.Invoke(p,new object[]{tile.type,tile.x,tile.y});}shimmer.Invoke(p,null);}
            p.GrappleMovement();
        }
        private static Player SpawnLocal(){return Spawning??Main.LocalPlayer;}
        private static IEnumerable<CodeInstruction> SpawnLocals(IEnumerable<CodeInstruction> input)
        {var getter=AccessTools.PropertyGetter(typeof(Main),"LocalPlayer");foreach(var c in input){if(c.Calls(getter))c.operand=AccessTools.Method(typeof(CompanionHooks),nameof(SpawnLocal));yield return c;}}
        internal static bool AllowItemPacket(HostCompanions.Actor a,byte[] packet){return host!=null && host.AcceptItemPacket(a,packet);}
    }
}
