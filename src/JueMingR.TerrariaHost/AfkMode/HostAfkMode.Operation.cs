using System.Collections.Generic;
using JueMingR.Platform.Combat;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.AfkMode
{
    internal sealed partial class HostAfkMode
    {
        private sealed class Body
        {
            internal Projectile Shot;internal int Key,Type;internal bool Aim;
            internal Rectangle Goal;internal uint AimStep,ProgressStep;internal int Stagnant;
            internal Vector2 Last;internal float Distance;
        }
        // Only this actual native action's causal primary bodies live here.
        // Independent bullets and persistent summons never delay completion.
        private readonly List<Body> bodies=new List<Body>(2);
        private bool operating,released,hadBody,hasCompletion,handEnded;
        private uint completionStep,startStep,handEndedStep;
        private int inCheck;
#if DEBUG
        internal int SourceReads,BodyReads,Starts,Completions;
        internal uint LastStart {get{return startStep;}}
        internal uint LastCompletion {get{return completionStep;}}
#endif
        private bool GapReady {get{return !hasCompletion || unchecked(Main.GameUpdateCount-completionStep)>=(uint)(((long)GapMilliseconds*60+999)/1000);}}
        private void ResetOperation(){bodies.Clear();operating=released=hadBody=hasCompletion=handEnded=false;inCheck=0;}
        private bool Valid(Body body)
        {
            var p=body.Shot;
            return player!=null && p.active && (int)p.key==body.Key && (p.type==body.Type || body.Type==948 && p.type==947) &&
                p.owner==player.whoAmI && p.whoAmI>=0 && p.whoAmI<Main.maxProjectiles && ReferenceEquals(Main.projectile[p.whoAmI],p);
        }
        private Body Find(Projectile shot)
        {for(int i=0;i<bodies.Count;i++)if(ReferenceEquals(bodies[i].Shot,shot) && Valid(bodies[i]))return bodies[i];return null;}
        internal void Started(Player p,Item item)
        {
            if(inCheck==0 || token==0 || !ReferenceEquals(p,player) || !Identity() || !ReferenceEquals(item,p.HeldItem) || manual || quickRequested)return;
            // Only an accepted native start opens an operation. The ready
            // boundary is intercepted before TryStartUse, not retroactively.
            operating=everStarted=true;released=hadBody=handEnded=false;bodies.Clear();startStep=Main.GameUpdateCount;
#if DEBUG
            Starts++;
#endif
        }
        internal void Created(Player p,Projectile shot)
        {
            if(inCheck==0 || !operating || token==0 || !ReferenceEquals(p,player) || shot==null || !shot.active || shot.owner!=p.whoAmI || manual)return;
            if(shot.type!=source.shoot && !(source.shoot==26 && shot.type==35 || source.shoot==35 && shot.type==26))return;
            if(shot.minion || shot.sentry || source.summon || source.sentry)return;
            bool controls=HostYoyoNavigation.Handles(shot) || shot.aiStyle==15 || shot.aiStyle==19 ||
                shot.aiStyle==3 && !HostPhasebladeAttack.Handles(shot.type) || source.channel;
            if(!controls)return;
            if(Find(shot)!=null)return;
            bodies.Add(new Body{Shot=shot,Key=(int)shot.key,Type=shot.type,Last=shot.Center,ProgressStep=Main.GameUpdateCount});hadBody=true;
        }
        internal void AfterTimers(Player p)
        {if(operating && ReferenceEquals(p,player) && handEnded && handEndedStep==Main.GameUpdateCount)ObserveCompletion(p,true);}
        private void ObserveCompletion(Player p,bool afterTimers=false)
        {
            if(!operating || !ReferenceEquals(p,player))return;
#if DEBUG
            SourceReads++;
#endif
            for(int i=bodies.Count-1;i>=0;i--)
            {
#if DEBUG
                BodyReads++;
#endif
                var body=bodies[i];
                // Glacier's native release transfers this body into an ordinary
                // independent flight. Flint and returning bodies retain hand
                // ownership until their real retirement; there is no Kill.
                if(!Valid(body) || type==6153 && body.Shot.ai[1]==1 || HostYoyoNavigation.Handles(body.Shot) && body.Shot.ai[0]==-2)bodies.RemoveAt(i);
            }
            // A manual same-slot use may have started a NEW animation before
            // this call decrements the OLD item's last cooldown. The exact
            // native after-timer outlet preserves the old completion timestamp
            // instead of postponing it until the manual action returns.
            bool handReady=WeaponCatalog.Ready(p) || afterTimers && !source.channel && p.itemTime<=0 && p.reuseDelay<=0 && !p.delayUseItem;
            if(bodies.Count!=0 || !handReady || source.channel && !hadBody && p.channel)return;
            operating=false;hasCompletion=true;completionStep=Main.GameUpdateCount;released=false;yieldRequested=false;
            ReleaseLease();
#if DEBUG
            Completions++;
#endif
        }
        private bool DecidePress()
        {
            if(!operating)return GapReady && !quickRequested && !automaticRequested;
            if(yieldRequested || released)return false;
            // AutoReuse may retire animation 1 before the same ItemCheck
            // decrements the last itemTime. A positive post-completion gap
            // must stop that early next start while the OLD call finishes its
            // real cooldown. With zero gap ordinary native held reuse remains
            // one uninterrupted consumption chain; do not manufacture a
            // completion receipt from the still-positive timer or add a frame.
            if(!source.channel && player.itemAnimation<=0 && (GapMilliseconds>0 || bodies.Count>0))return false;
            if(ItemID.Sets.ShootsOnUseRelease[type] && player.itemAnimation>1 && player.itemTime==0 && Main.GameUpdateCount!=startStep)
            {released=true;return false;}
            for(int i=0;i<bodies.Count;i++)
            {
                var body=bodies[i];if(!Valid(body))continue;var shot=body.Shot;
                if(type==5462 && shot.ai[1]>=30 || type==6153 && (shot.ai[0]>=60 || shot.ai[1]==1) || shot.aiStyle==15 && Main.GameUpdateCount!=startStep)
                {released=true;return false;}
            }
            // A non-channel returning operation cannot auto-spawn another
            // allowed parallel body when its hand timers become ready.
            return source.channel || bodies.Count==0 || !WeaponCatalog.Ready(player);
        }
        internal Scope BeginProjectile(Projectile shot)
        {
            var body=Find(shot);
            if(body==null || !CanOutput(player) || !operating || token==0)return null;
            return BeginScope(player,false);
        }
        internal void AfterProjectile(Projectile shot)
        {
            // The native AI may have just killed this exact body. Preserve its
            // captured identity without requiring active, but do no completion
            // work for unrelated world AI callbacks (including our bullets).
            Body body=null;
            for(int i=0;i<bodies.Count;i++)if(ReferenceEquals(bodies[i].Shot,shot) && (int)shot.key==bodies[i].Key){body=bodies[i];break;}
            if(body==null)return;
            if(Valid(body) && HostYoyoNavigation.Handles(shot) && !released && !manual && !quickRequested && shot.ai[0]>=0)
                ObserveYoyo(body);
            ObserveCompletion(player);
        }
        // This is called from the existing final aim consumer, after its own
        // priority/identity gates. The NPC receive box is the logical endpoint;
        // a navigation corner supplied as the actual cursor is not arrival.
        internal void ConsumedAim(Projectile shot,NpcIdentity target)
        {
            var body=Find(shot);if(body==null || !CombatSelection.Valid(target,target.Session))return;
            body.Goal=CombatSelection.ReceiveBounds(Main.npc[target.Slot]);body.Aim=true;body.AimStep=Main.GameUpdateCount;
        }
        private void ObserveYoyo(Body body)
        {
            var shot=body.Shot;float range,speed;HostYoyoNavigation.Parameters(player,shot.type,false,out range,out speed);
            bool aimed=body.Aim && body.AimStep==Main.GameUpdateCount;
            Vector2 goal=aimed?new Vector2(body.Goal.Center.X,body.Goal.Center.Y):point;
            Vector2 delta=goal-player.Center;float length=delta.Length();
            bool clamped=length>range && length>0;
            if(clamped)goal=player.Center+delta*(range/length);
            float arrival=5+speed/2;
            Rectangle bodyBox=shot.Hitbox;bodyBox.Inflate((int)arrival,(int)arrival);
            bool arrived=aimed && !clamped?bodyBox.Intersects(body.Goal):Vector2.DistanceSquared(shot.Center,goal)<=arrival*arrival;
            float distance=Vector2.Distance(shot.Center,goal);
            if(body.ProgressStep!=Main.GameUpdateCount)
            {
                // Only an actually motionless, non-progressing body times out.
                // Moving the player/target never recreates this operation or
                // its gap, and transient navigation preparation is irrelevant.
                uint elapsed=unchecked(Main.GameUpdateCount-body.ProgressStep);
                bool progressed=Vector2.DistanceSquared(body.Last,shot.Center)>.25f || distance<body.Distance-.25f;
                body.Stagnant=progressed?0:body.Stagnant+(int)System.Math.Min(elapsed,180u);
                body.Last=shot.Center;body.Distance=distance;body.ProgressStep=Main.GameUpdateCount;
            }
            if(arrived || body.Stagnant>=180)released=true;
        }
    }
}
