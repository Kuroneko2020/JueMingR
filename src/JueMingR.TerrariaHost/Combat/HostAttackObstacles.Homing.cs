using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HomingLease homingInputs;
        internal void CaptureHomingInputs(Player value,bool players,bool controls=false)
        {if(homingInputs==null)homingInputs=new HomingLease(Main.player[value.whoAmI],players,controls);}
        private bool HomingInputsCurrent {get{return homingInputs==null || homingInputs.MembersCurrent;}}
        private bool HomingDependenciesCurrent {get{return homingInputs==null || homingInputs.Current;}}
        private sealed class HomingLease
        {
            private readonly Player owner;private readonly int slot,weapon,eventideStage;private readonly float gravity;
            private readonly Vector2 position,mounted,rotationOrigin;private readonly Vector2? hand;private readonly float rotation;
            private readonly Vector2 inputVelocity;private readonly bool left,right,jump,down,up,releaseJump,releaseUp,wet,honey,lava,shimmer;
            private readonly int jumpTicks,forcedGravity;
            private readonly List<PlayerRow> players;
            private readonly int difficulty;
            private readonly JueMingR.Platform.Combat.PredictionPlayerMotion? motion;
            private readonly struct PlayerRow
            {
                internal readonly Player Player;internal readonly int Slot,Dash;internal readonly Rectangle Bounds;internal readonly bool Dead;
                internal PlayerRow(Player p,int slot){Player=p;Slot=slot;Dash=p.dashDelay;Bounds=p.Hitbox;Dead=p.dead;}
                internal bool Current(Player p){return ReferenceEquals(Player,p) && p.Hitbox==Bounds && p.dead==Dead && p.dashDelay==Dash;}
            }
            internal HomingLease(Player p,bool includePlayers,bool controls)
            {
                owner=p;slot=p.whoAmI;weapon=p.HeldItem.type;eventideStage=Stage(p);gravity=p.gravDir;position=p.position;mounted=p.MountedCenter;hand=p.HandPosition;rotation=p.fullRotation;rotationOrigin=p.fullRotationOrigin;difficulty=(int)Main.Difficulty;
                if(controls){motion=NpcPredictionSource.ReadPlayer(p);inputVelocity=p.velocity;left=p.controlLeft;right=p.controlRight;jump=p.controlJump;down=p.controlDown;up=p.controlUp;releaseJump=p.releaseJump;releaseUp=p.releaseUp;jumpTicks=p.jump;forcedGravity=p.forcedGravity;wet=p.wet;honey=p.honeyWet;lava=p.lavaWet;shimmer=p.shimmerWet;}
                if(includePlayers){players=new List<PlayerRow>();for(int i=0;i<255;i++)if(Main.player[i]!=null && Main.player[i].active)players.Add(new PlayerRow(Main.player[i],i));}
            }
            private static int Stage(Player p)
            {return p.itemAnimation<=0?0:(p.itemAnimationMax-p.itemAnimation)/2;}
            internal bool MembersCurrent
            {
                get
                {
                    // ItemAnimation/miscCounter only choose visual ai1 for
                    // 931/1116. Facing is recomputed from the candidate cursor
                    // by the actual hand. Eventide's integer Shoot ordinal is
                    // a damaging branch: opening0 and native started0 agree,
                    // but moving to the central ordinal2 always retires it.
                    if(slot<0 || slot>=Main.player.Length || !ReferenceEquals(Main.player[slot],owner) || owner.HeldItem.type!=weapon || weapon==4953 && Stage(owner)!=eventideStage || owner.gravDir!=gravity || owner.position!=position || owner.MountedCenter!=mounted || players!=null && owner.HandPosition!=hand || owner.fullRotation!=rotation || owner.fullRotationOrigin!=rotationOrigin || (int)Main.Difficulty!=difficulty)return false;
                    // Presentation compares captured scalars/members only.
                    // ReadPlayer may query terrain; discovering newly active
                    // players likewise belongs to Prepare/Consume below.
                    if(motion.HasValue && (owner.velocity!=inputVelocity || owner.controlLeft!=left || owner.controlRight!=right || owner.controlJump!=jump || owner.controlDown!=down || owner.controlUp!=up || owner.releaseJump!=releaseJump || owner.releaseUp!=releaseUp || owner.jump!=jumpTicks || owner.forcedGravity!=forcedGravity || owner.wet!=wet || owner.honeyWet!=honey || owner.lavaWet!=lava || owner.shimmerWet!=shimmer))return false;
                    if(players!=null)foreach(var row in players){var current=Main.player[row.Slot];if(current==null || !current.active || !row.Current(current))return false;}return true;
                }
            }
            internal bool Current
            {
                get
                {
                    if(!MembersCurrent || motion.HasValue && !motion.Value.SameSample(NpcPredictionSource.ReadPlayer(owner)))return false;
                    if(players!=null){int at=0;for(int i=0;i<255;i++)if(Main.player[i]!=null && Main.player[i].active){if(at>=players.Count || players[at].Slot!=i || !players[at++].Current(Main.player[i]))return false;}if(at!=players.Count)return false;}return true;
                }
            }
        }
    }
}
