using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private readonly PredictionTerrain flightPreviewTerrain=new PredictionTerrain();
        // A sealed AI013/phaseblade flight can be observed after enabling the path or
        // returning to its weapon. This does not manufacture a birth receipt
        // or grant any cursor consumer authority over that existing attack.
        private FlightPreview ReadFlight(Player player,NpcTrajectory timeline,HostAttackPhase phase,out bool live)
        {
            live=false;bool phaseblade=HostPhasebladeAttack.Handles(player.HeldItem.shoot);
            bool gem=HostGemStaffAttack.Handles(player.HeldItem.shoot);
            bool attached=HostAttachedShotAttack.Handles(player.HeldItem.shoot);
            bool arc=HostArcSwingAttack.Weapon(player.HeldItem.type);
            if(!phaseblade && !gem && !attached && !arc && !HostHarpoonAttack.Handles(player.HeldItem.shoot))return null;
            FlightPreview best=null;flightPreviewTerrain.Reset();var clock=new HostAttackClock(timeline,phase);
            if(arc)
            {
                var sources=new List<Projectile>();
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {
                    var shot=Main.projectile[slot];if(shot==null || !shot.active || shot.owner!=player.whoAmI || shot.damage<=0 || !shot.friendly || shot.hostile || !ArcFlightRole(player.HeldItem.type,shot.type) || shot.type==1128 && shot.ai[1]>=1)continue;sources.Add(shot);
                }
                live=sources.Count>0;if(!live)return null;
                var primary=sources[0];var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
                var contact=HostArcSwingAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers,HostAttackPlayerFrame.Capture(player,timeline));
                return contact==null?null:new FlightPreview(player,primary,contact,arc:receivers);
            }
            if(attached)
            {
                // Native card recall also visits retained inactive cards.
                // Only an active held-role shot owns a visible flight mark;
                // the other captured local stages supply causal dependencies.
                var observed=new List<Projectile>();Projectile primary=null;
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {
                    var shot=Main.projectile[slot];if(shot==null || shot.owner!=player.whoAmI || !HostAttachedShotAttack.Handles(shot.type) && shot.type!=953 || !shot.active && shot.type!=1024)continue;
                    observed.Add(shot);if(shot.active && shot.type==player.HeldItem.shoot && shot.damage>0 && shot.friendly && !shot.hostile && primary==null)primary=shot;
                }
                live=primary!=null;if(!live)return null;
                HostAttachedShotAttack.FlightState state;var contact=HostAttachedShotAttack.KnownFamily(player,observed.ToArray(),timeline,clock,flightPreviewTerrain,out state);
                return contact==null?null:new FlightPreview(player,primary,contact,attached:state);
            }
            if(gem)
            {
                // One real pool scan captures local gem competition. Only
                // the held role contributes a mark; other local gem roles
                // remain ordered owner-immunity dependencies, not inputs.
#if DEBUG
                HostGemStaffAttack.FlightPoolScans++;
#endif
                var observed=new List<Projectile>();Projectile primary=null;
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {var shot=Main.projectile[slot];if(shot==null || !shot.active || shot.owner!=player.whoAmI || !HostGemStaffAttack.Handles(shot.type))continue;observed.Add(shot);if(shot.type==player.HeldItem.shoot && shot.friendly && !shot.hostile && primary==null)primary=shot;}
                live=primary!=null;if(!live)return null;
                HostGemStaffAttack.FlightState state;var contact=HostGemStaffAttack.KnownFamily(player,observed.ToArray(),timeline,clock,flightPreviewTerrain,out state);
                return contact==null?null:new FlightPreview(player,primary,contact,state);
            }
            for(int i=0;i<Main.maxProjectiles;i++)
            {
                var shot=Main.projectile[i];if(shot==null || !shot.active || shot.owner!=player.whoAmI || shot.type!=player.HeldItem.shoot)continue;
                live=true;HostAttackObstacles receivers;
                AttackContact contact;
                if(phaseblade)
                {receivers=new HostAttackObstacles(player,shot,timeline,0,clock.BeforeNpc,clock.NextWorld);contact=HostPhasebladeAttack.Known(player,shot,timeline,clock,flightPreviewTerrain,receivers);}
                else contact=HostHarpoonAttack.Known(player,shot,timeline,clock,flightPreviewTerrain,out receivers);
                if(contact!=null && Earlier(contact,best?.Contact))best=new FlightPreview(player,shot,contact);
                if(best!=null && best.Contact.Tick==clock.FirstTick && best.Contact.Subupdate==0)break;
            }
            return best;
        }
        private static bool Earlier(AttackContact a,AttackContact b)
        {return a!=null && (b==null || a.Tick<b.Tick || a.Tick==b.Tick && a.Subupdate<b.Subupdate);}
        private static bool ArcFlightRole(int item,int type)
        {return item==273?type==972:item==674?type==983:item==675?type==972 || type==973:item==757?type==984 || type==985:item==1226 && (type==1126 || type==1128 || type==1129);}
        private static AttackContact OnlyFlight(PreviewPlan plan,Player player,NpcTrajectory timeline,FlightPreview flight)
        {plan.Clear();plan.SetMembers(player,player.HeldItem);plan.Timeline=timeline;plan.Flight=flight;plan.Contact=flight?.Contact;return plan.Contact;}
        private sealed class FlightPreview
        {
            internal readonly AttackContact Contact;
            private readonly Player player;private readonly Projectile shot;private readonly int key,type,owner,time,extra,penetration;
            private readonly Vector2 position,velocity;private readonly float ai0,ai1,local0;
            private readonly bool use,wet,honey,lava,shimmer,tile;
            private readonly HostGemStaffAttack.FlightState gem;
            private readonly HostAttachedShotAttack.FlightState attached;
            private readonly HostAttackObstacles arc;
            internal FlightPreview(Player player,Projectile shot,AttackContact contact,HostGemStaffAttack.FlightState gem=null,HostAttachedShotAttack.FlightState attached=null,HostAttackObstacles arc=null)
            {
                this.player=player;this.shot=shot;this.gem=gem;this.attached=attached;this.arc=arc;Contact=contact;key=(int)shot.key;type=shot.type;owner=shot.owner;time=shot.timeLeft;extra=shot.extraUpdates;penetration=shot.penetrate;
                position=shot.position;velocity=shot.velocity;ai0=shot.ai[0];ai1=shot.ai[1];local0=shot.localAI[0];use=player.controlUseItem;wet=shot.wet;honey=shot.honeyWet;lava=shot.lavaWet;shimmer=shot.shimmerWet;tile=shot.tileCollide;
            }
            // Draw checks the bound member, never scans the projectile pool.
            internal bool Current {get{return (gem==null || gem.MembersCurrent) && (attached==null || attached.MembersCurrent) && (arc==null || arc.FamilyMembersCurrent) && shot.active && (int)shot.key==key && shot.type==type && shot.owner==owner && shot.timeLeft==time && shot.extraUpdates==extra && shot.penetrate==penetration && shot.position==position && shot.velocity==velocity && shot.ai[0]==ai0 && shot.ai[1]==ai1 && shot.localAI[0]==local0 && player.controlUseItem==use && shot.wet==wet && shot.honeyWet==honey && shot.lavaWet==lava && shot.shimmerWet==shimmer && shot.tileCollide==tile;}}
        }
    }
}
