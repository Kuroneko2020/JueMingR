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
            if(!phaseblade && !HostHarpoonAttack.Handles(player.HeldItem.shoot))return null;
            FlightPreview best=null;flightPreviewTerrain.Reset();var clock=new HostAttackClock(timeline,phase);
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
        private static AttackContact OnlyFlight(PreviewPlan plan,Player player,NpcTrajectory timeline,FlightPreview flight)
        {plan.Clear();plan.SetMembers(player,player.HeldItem);plan.Timeline=timeline;plan.Flight=flight;plan.Contact=flight?.Contact;return plan.Contact;}
        private sealed class FlightPreview
        {
            internal readonly AttackContact Contact;
            private readonly Player player;private readonly Projectile shot;private readonly int key,type,owner,time,extra,penetration;
            private readonly Vector2 position,velocity;private readonly float ai0,ai1,local0;
            private readonly bool use,wet,honey,lava,shimmer,tile;
            internal FlightPreview(Player player,Projectile shot,AttackContact contact)
            {
                this.player=player;this.shot=shot;Contact=contact;key=(int)shot.key;type=shot.type;owner=shot.owner;time=shot.timeLeft;extra=shot.extraUpdates;penetration=shot.penetrate;
                position=shot.position;velocity=shot.velocity;ai0=shot.ai[0];ai1=shot.ai[1];local0=shot.localAI[0];use=player.controlUseItem;wet=shot.wet;honey=shot.honeyWet;lava=shot.lavaWet;shimmer=shot.shimmerWet;tile=shot.tileCollide;
            }
            // Draw checks the bound member, never scans the projectile pool.
            internal bool Current {get{return shot.active && (int)shot.key==key && shot.type==type && shot.owner==owner && shot.timeLeft==time && shot.extraUpdates==extra && shot.penetrate==penetration && shot.position==position && shot.velocity==velocity && shot.ai[0]==ai0 && shot.ai[1]==ai1 && shot.localAI[0]==local0 && player.controlUseItem==use && shot.wet==wet && shot.honeyWet==honey && shot.lavaWet==lava && shot.shimmerWet==shimmer && shot.tileCollide==tile;}}
        }
    }
}
