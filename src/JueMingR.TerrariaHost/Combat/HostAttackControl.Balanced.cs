using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackControl
    {
        internal int BalancedLaunchDelay
        {get{int delay=0;foreach(var entry in shots.Values)if(FiniteHeld(entry.Type) && Observed(entry))delay=Math.Max(delay,Math.Max(0,(int)Math.Ceiling(entry.Shot.ai[1])-1));return delay;}}
        internal HostAttackSolution PrepareBalancedPreview(NpcTrajectory timeline,HostAttackPhase phase,int delay,PredictionTerrain cells)
        {
            var player=combat.Player;
            foreach(var entry in shots.Values)
            {
                if(!FiniteHeld(entry.Type) || !Observed(entry))continue;
                if(entry.Prepared==Main.GameUpdateCount && entry.Solution.Timeline==timeline && preparedPhase==phase && entry.Solution.HasPoint)return entry.Solution;
                var ammo=AttackAmmoSnapshot.CaptureController(player,player.HeldItem,entry.Type==705?12:14);
                return HostHeldAttack.PrepareRepresentative(player,entry.Shot,ammo,timeline,new HostAttackClock(timeline,phase),cells,HostAttackPlayerFrame.Capture(player,timeline));
            }
            // A candidate opening is private and cannot adopt a live body or
            // gain an input receipt. It uses the same main-shot birth adapter.
            var item=player.HeldItem;var sample=new Projectile();sample.SetDefaults(item.shoot);sample.owner=player.whoAmI;sample.scale=1;
            var selected=AttackAmmoSnapshot.CaptureController(player,item,item.shoot==705?12:14);
            return HostHeldAttack.PrepareRepresentative(player,sample,selected,timeline,new HostAttackClock(timeline,phase,delay),cells,HostAttackPlayerFrame.Capture(player,timeline));
        }
    }
}
