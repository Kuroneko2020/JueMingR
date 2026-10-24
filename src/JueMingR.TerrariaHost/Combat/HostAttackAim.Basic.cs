using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum HostAttackInputKind { Basic, Enhanced, Sealed }
    internal readonly struct HostAttackInput
    {
        internal readonly HostAttackInputKind Kind;
        internal readonly Vector2 Point;
        internal readonly NpcIdentity Target;
        internal readonly Item Weapon;
        internal readonly Projectile Source;
        internal readonly HostAttackPhase Phase;
        internal readonly bool ReverseGravity,TileTarget;
        internal readonly AttackPointSemantics PointSemantics;
        private readonly HostAttackAim owner;
        internal HostAttackInput(HostAttackAim owner,HostAttackInputKind kind,Vector2 point,NpcIdentity target,Item weapon,Projectile source,HostAttackPhase phase,bool reverseGravity,bool tileTarget,AttackPointSemantics pointSemantics=AttackPointSemantics.Unknown)
        {this.owner=owner;Kind=kind;Point=point;Target=target;Weapon=weapon;Source=source;Phase=phase;ReverseGravity=reverseGravity;TileTarget=tileTarget;PointSemantics=pointSemantics;}
        internal CombatCursorScope Begin()
        {var scope=TileTarget?CombatCursorScope.BeginTileTarget(Point,ReverseGravity):CombatCursorScope.Begin(Point,ReverseGravity);if(scope!=null)owner?.RecordInput(this);return scope;}
    }

    internal sealed partial class HostAttackAim
    {
        private readonly PredictionTerrain basicYoyoTerrain=new PredictionTerrain();
        private HostAttackInput consumedInput;
        private Vector2 consumedOrigin;
        private uint consumedInputStep;
        private bool hasConsumedInput;
        // This same-world-step fact belongs to a real native cursor consumer.
        // Read-only candidate/preview queries never publish it or acquire input.
        internal void RecordInput(HostAttackInput input)
        {consumedInput=input;consumedOrigin=input.Source?.Center??HostAttackModels.LaunchOrigin(combat.Player,input.Weapon);consumedInputStep=Main.GameUpdateCount;hasConsumedInput=true;}
        internal void RecordControlInput(Vector2 point,Projectile source,HostAttackInputKind kind,AttackPointSemantics pointSemantics=AttackPointSemantics.WorldPoint)
        {RecordInput(new HostAttackInput(this,kind,point,observation.Selection.Target,combat.Player.HeldItem,source,source==null?HostAttackPhase.BeforeNpc:HostAttackPhase.Projectiles,true,false,pointSemantics));}
        // A prediction fault retires enhanced information, not a current legal
        // consumer. This gate retains the actual focus/UI/source/use contract;
        // it has no terrain, future path, ammo prediction or damage premise.
        internal bool BasicPermission {get{return observation.SelectionAvailable && observation.Settings.CanRun && observation.Options.Aim && combat.Player!=null && combat.Admitted(combat.Player) &&
            (combat.Left || combat.Player.controlUseItem || combat.Player.channel || combat.Use.Active || Control.Pending || ReleaseHandCurrent || HostPhasebladeAttack.Handles(combat.Player.HeldItem.shoot) && combat.Player.itemAnimation>0);}}
        internal bool TryBasicInput(Player player,Item item,Projectile source,HostAttackPhase phase,out HostAttackInput input)
        {return TryBasicInputCore(player,item,source,phase,true,out input);}
        internal bool TryBasicPreviewInput(Player player,Item item,Projectile source,HostAttackPhase phase,out HostAttackInput input)
        {return TryBasicInputCore(player,item,source,phase,false,out input);}
        private bool TryBasicInputCore(Player player,Item item,Projectile source,HostAttackPhase phase,bool consumer,out HostAttackInput input)
        {
            input=default(HostAttackInput);
            if(player==null || item==null || !ReferenceEquals(player,combat.Player) || !ReferenceEquals(item,player.HeldItem) || !(consumer?BasicPermission:PreviewDemand) || !CurrentReceiver || !Eligible(item))return false;
            var npc=Main.npc[observation.Selection.Target.Slot];var point=npc.Center+npc.netOffset;
            if(HostYoyoNavigation.Weapon(item) && !HostYoyoNavigation.BasicPoint(player,item,source,point,basicYoyoTerrain,out point))return false;
            // These native controllers clamp world intent themselves. Keeping
            // that same value space also covers their real AI/Kill consumers.
            if(source!=null && HostGuidedAttack.Handles(source.type))player.LimitPointToPlayerReachableArea(ref point);
            bool reverse=source!=null || !HostSkyAttack.RawCursor(item.type) && !HostMeleeAttack.RawCursor(item.type) && !HostCloudAttack.RawCursor(item.type);
            var captured=source==null?AttackAmmoSnapshot.Capture(player,item):null;
            int projectile=captured?.Projectile??item.shoot;
            input=new HostAttackInput(this,HostAttackInputKind.Basic,point,observation.Selection.Target,item,source,phase,reverse,source==null && (projectile==442 || projectile==80),AttackPointSemantics.WorldPoint);
            return true;
        }
        internal bool TryOrdinaryInput(Player player,Item item,HostAttackPhase phase,out HostAttackInput input)
        {
            input=default(HostAttackInput);
            if(player==null || item==null || !ReferenceEquals(player,combat.Player) || !ReferenceEquals(item,player.HeldItem) || !BasicPermission || !CurrentReceiver)return false;
            if(ReferenceEquals(item,weapon) && Valid(true))
            {
                if(balancedOrdinary)
                {
                    var solution=Main.GameUpdateCount==prepared?currentSolution:nextSolution;
                    bool reverse=!HostSkyAttack.RawCursor(item.type) && !HostMeleeAttack.RawCursor(item.type) && !HostCloudAttack.RawCursor(item.type);
                    input=new HostAttackInput(this,HostAttackInputKind.Enhanced,solution.Point,solution.Timeline.Identity,item,null,phase,reverse,ammo.Projectile==442 || ammo.Projectile==80,solution.PointSemantics);
                }
                else input=ContactInput(Main.GameUpdateCount==prepared?current:next,item,null,phase);
                return true;
            }
            return TryBasicInput(player,item,null,phase,out input);
        }
        internal HostAttackInput ContactInput(AttackContact contact,Item item,Projectile source,HostAttackPhase phase)
        {
            bool reverse=source!=null || !HostSkyAttack.RawCursor(item.type) && !HostMeleeAttack.RawCursor(item.type) && !HostCloudAttack.RawCursor(item.type);
            int projectile=ammo?.Projectile??item.shoot;
            return new HostAttackInput(this,HostAttackInputKind.Enhanced,new Vector2(contact.AimX,contact.AimY),contact.Timeline.Identity,item,source,phase,reverse,source==null && (projectile==442 || projectile==80),contact.PointSemantics);
        }
        private CombatCursorScope BeginBasic(Player player,Item item,Projectile source,HostAttackPhase phase)
        {HostAttackInput input;return TryBasicInput(player,item,source,phase,out input)?input.Begin():null;}
        internal void StartedHand(Player player,Item item)
        {
            StartedReleaseHand(player,item);
            // Called only after vanilla StartActualUse has admitted and started
            // the animation. Pure swung weapons consume direction, not cursor;
            // changing a borrowed cursor here would have no native consumer.
            if(item==null || item.shoot>0 || !HostSwingAttack.Handles(item) || player?.itemAnimation<=0)return;
            Vector2 familyPoint;HostAttackInput input;
            if(TryPreparedMeleeFacing(player,out familyPoint))input=ContactInput(current,item,null,HostAttackPhase.BeforeNpc);
            else if(!TryBasicInput(player,item,null,HostAttackPhase.BeforeNpc,out input))return;
            float x=input.Point.X-player.MountedCenter.X;
            if(x!=0){player.ChangeDir(x>0?1:-1);RecordInput(input);}
        }
        private bool TryPreparedMeleeFacing(Player player,out Vector2 point)
        {
            point=Vector2.Zero;
            if(player==null || !ReferenceEquals(player,combat.Player) || !HasOwnMeleeHitFamily(player.HeldItem) || Main.GameUpdateCount!=prepared || current==null || obstacles==null || !ValidCore(true,true))return false;
            point=new Vector2(current.AimX,current.AimY);
            // An already progressing native swing has a sealed facing. Only
            // the legal new-hand proof may choose the other facing; ordinary
            // Basic/current-source semantics remain the fallback on rejection.
            return player.itemAnimation<=0 || player.ItemAnimationJustStarted || obstacles.MeleeFamilyStartsNow || (point.X-player.MountedCenter.X)*player.direction>=0;
        }
    }
}
