using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked 1.4.5.8 AI113 hand roles only. These value copies never run AI,
    // Damage, Kill, birth or random. An attachment disables Damage, not
    // UpdatePosition: native Center=NPC.Center-velocity*2 then moves once.
    internal static class HostAttachedShotAttack
    {
        private static readonly int[] sceneTypes={598,636,953,1024};
        internal static bool Handles(int type){return type==598 || type==636 || type==1024;}
        internal struct State
        {
            internal Vector2 Position,Velocity,ResetCenter,DeathCenter,Half;
            internal float Phase,Clock,Curve,AttachedClock,Rotation;
            internal int Left,Alpha;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Tile,IgnoreWater,Killed,ResetDamage;
            internal State(Projectile q)
            {Half=q.Size*.5f;Position=q.position;Velocity=q.velocity;Phase=q.ai[0];Clock=q.ai[1];Curve=q.ai[2];AttachedClock=q.localAI[0];Rotation=q.rotation;Left=q.timeLeft;Alpha=q.alpha;Alive=q.active;Wet=q.wet;Honey=q.honeyWet;Lava=q.lavaWet;Shimmer=q.shimmerWet;Tile=q.tileCollide;IgnoreWater=q.ignoreWater;Killed=ResetDamage=false;ResetCenter=DeathCenter=Vector2.Zero;}
            internal State(Projectile sample,Vector2 center,Vector2 velocity,float curve):this(sample)
            {Position=center-sample.Size*.5f;Velocity=velocity;Phase=Clock=AttachedClock=0;Curve=curve;Alive=true;Rotation=velocity.ToRotation()+(float)Math.PI/2;if(sample.type==1024)Rotation-=(float)Math.PI/2;}
        }
        internal static void Attach(ref State s,int slot,MotionRect target,int width,int height)
        {s.Phase=1;s.Clock=slot;s.Velocity=(new Vector2(target.CenterX,target.CenterY)-(s.Position+new Vector2(width,height)*.5f))*.75f;}
        internal static void Recall(ref State s,int width,int height)
        {s.Phase=2;s.Velocity=Vector2.Zero;s.ResetDamage=true;s.ResetCenter=s.Position+new Vector2(width,height)*.5f;}
        private static void Kill(ref State s){if(s.Alive){s.DeathCenter=s.Position+s.Half;s.Alive=false;s.Killed=true;s.Left=0;}}
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State s,MotionRect? attachedBody,MotionRect? ownerBody,bool ownerActive,bool ownerDead)
        {
            s.ResetDamage=false;if(!s.Alive)return true;
            if(s.Position.X<=Main.leftWorld || s.Position.X+sample.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+sample.height>=Main.bottomWorld){s.Alive=false;return true;}
            Vector2 half=sample.Size*.5f,wetVelocity=s.Velocity;
            s.Alpha=Math.Max(0,s.Alpha-25);
            if(s.Phase==0)
            {
                s.Clock+=1f;
                if(s.Clock>=(sample.type==1024?60:45))
                {s.Velocity.X*=sample.type==636?.995f:.98f;s.Velocity.Y+=sample.type==636?.15f:.35f;}
                if(sample.type==1024 && s.Velocity.Y>12)s.Velocity.Y=12;
                s.Rotation=s.Velocity.ToRotation()+(float)Math.PI/2;
                if(sample.type==1024)
                {
                    s.Rotation-=(float)Math.PI/2;
                    float angle=(float)Math.PI/30f,amplitude=40f*s.Curve;
                    double bend=(double)(amplitude*angle*angle)*(0.0-Math.Sin(s.Clock*angle));
                    if(s.Clock==1f)bend+=(double)(amplitude*angle);
                    s.Velocity+=Vector2.UnitY.RotatedBy(s.Rotation)*(float)bend;
                }
            }
            if(s.Phase==1)
            {
                s.IgnoreWater=true;s.Tile=false;s.AttachedClock+=1f;
                bool retire=s.AttachedClock>=60*(sample.type==598?15:5*sample.MaxUpdates);
                if(sample.type==1024 && (int)s.Clock==-1)
                {bool support;if(!TileSupport(sample,terrain,s.Position,out support))return false;if(support)s.Velocity=Vector2.Zero;else retire=true;}
                else if(!retire)
                {
                    if(!attachedBody.HasValue)retire=true;
                    else{s.Position=new Vector2(attachedBody.Value.CenterX,attachedBody.Value.CenterY)-s.Velocity*2-half;}
                }
                if(retire)
                {if(sample.type==1024)Recall(ref s,sample.width,sample.height);else Kill(ref s);}
            }
            if(s.Phase==2)
            {
                s.Tile=false;
                if(!ownerBody.HasValue)return false;
                var body=new Rectangle((int)s.Position.X,(int)s.Position.Y,sample.width,sample.height);
                var owner=ownerBody.Value;
                if(!ownerActive || ownerDead || body.Intersects(new Rectangle((int)owner.X,(int)owner.Y,(int)owner.Width,(int)owner.Height)))Kill(ref s);
                else{s.Velocity=(new Vector2(owner.CenterX,owner.CenterY)-(s.Position+half)).SafeNormalize(Vector2.Zero)*16;s.Rotation=s.Velocity.ToRotation()+(float)Math.PI/2;if(sample.type==1024)s.Rotation-=(float)Math.PI/2;}
            }
            if(!ownerActive)Kill(ref s);
            Vector2 center=s.Position+half;float wind;
            if(!HostProjectileEnvironment.Impulse(sample,terrain,center.X,center.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            if(!s.IgnoreWater)
            {
                bool wet,lava;byte liquid;
                if(!terrain.ProjectileWet(center.X,center.Y,sample.width,sample.height,out wet,out liquid,true) || !terrain.ProjectileLava(center.X,center.Y,sample.width,sample.height,out lava,true))return false;
                if(lava)s.Lava=true;if(wet && liquid==2)s.Honey=true;if(wet && liquid==3)s.Shimmer=true;s.Wet=wet;
                if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            }
            if(sample.type==1024 && s.Shimmer)
            {
                int x=(int)(center.X/16),y=(int)(s.Position.Y/16);bool full=false;
                if(x>=10 && y>=11 && x<Main.maxTilesX-10 && y<Main.maxTilesY-10)
                {PredictionTile cell,above;PredictionStop stop;if(!terrain.Tile(x,y,out cell,out stop) || !terrain.Tile(x,y-1,out above,out stop))return false;full=cell.Liquid==255 && cell.ShimmerLiquid && above.Liquid>0 && above.ShimmerLiquid;}
                if(full)Kill(ref s);
                else if(s.Velocity.Y>0){s.Velocity.Y*=-1;s.Shimmer=s.Wet=false;}
            }
            Vector2 prior=s.Velocity;bool hit=false;
            if(s.Tile && !terrain.ProjectileTileMotion(sample,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetVelocity,out hit,shimmer:s.Shimmer,nativeClip:true))return false;
            if(hit)
            {
                if(sample.type!=1024){s.Position+=s.Velocity;Kill(ref s);}
                else{if(s.Phase==0)s.Position+=s.Velocity;if(s.Phase<2){s.Phase=1;s.Clock=-1;s.Velocity=prior.SafeNormalize(Vector2.Zero);}}
            }
            s.Position+=s.Wet?wetVelocity:s.Velocity;return true;
        }
        private static bool TileSupport(Projectile sample,PredictionTerrain terrain,Vector2 position,out bool support)
        {
            support=false;
            int left=Math.Max(0,Math.Min(Main.maxTilesX,(int)(position.X/16)-1)),right=Math.Max(0,Math.Min(Main.maxTilesX,(int)((position.X+sample.width)/16)+2));
            int top=Math.Max(0,Math.Min(Main.maxTilesY,(int)(position.Y/16)-1)),bottom=Math.Max(0,Math.Min(Main.maxTilesY,(int)((position.Y+sample.height)/16)+2));
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {PredictionTile cell;PredictionStop stop;if(!terrain.Tile(x,y,out cell,out stop))return false;if(cell.Active && cell.Solid && !cell.SolidTop && position.X+sample.width+7>x*16 && position.X-7<x*16+16 && position.Y+sample.height+7>y*16 && position.Y-7<y*16+16)support=true;}
            return true;
        }
        internal static void Finish(ref State s){if(s.Alive && --s.Left<=0)Kill(ref s);}
        internal static Rectangle DamageBox(int type,Vector2 position,int width,int height)
        {var b=new Rectangle((int)position.X,(int)position.Y,width,height);if(type==1024)b.Inflate(6,6);return b;}
        internal static bool Collides(int type,Rectangle shot,MotionRect target)
        {var b=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);if((type==598 || type==636) && b.Width>8 && b.Height>8)b.Inflate(-b.Width/8,-b.Height/8);return shot.Intersects(b);}

        private sealed class Member
        {
            internal Projectile Shot;internal State State;internal HostAttackObstacles Receivers;
            internal int Slot,BornOuter;internal bool Primary,DeathHandled;internal float BurstClock,BurstScale;
        }
        private sealed class Group
        {
            internal readonly List<Member> Members=new List<Member>();
            internal Player Player;internal NpcTrajectory Timeline;internal PredictionTerrain Terrain;internal HostAttackObstacles Root;
            internal HostProjectileTargets Targets;internal HostProjectileBirthSlots Slots;internal Vector2 Aim;
            internal HostAttackPlayerFrame? PlayerFrame;
            internal bool Unknown;internal AttackConfidence Confidence;
            internal void Add(Member row)
            {int at=Members.FindIndex(m=>m.Slot>=row.Slot);if(at<0)Members.Add(row);else if(Members[at].Slot==row.Slot)Members[at]=row;else Members.Insert(at,row);}
            internal Member Find(int slot){return Members.Find(m=>m.Slot==slot);}
        }
        private static readonly System.Reflection.MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        private static Projectile Copy(Projectile shot){return (Projectile)clone.Invoke(shot,null);}
        private static Member Make(Group group,Projectile shot,State state,int slot,bool primary,int bornOuter=1)
        {return new Member{Shot=shot,State=state,Slot=slot,Primary=primary,BornOuter=bornOuter,Receivers=group.Root.ForkForSpawn(shot),BurstClock=shot.ai[0],BurstScale=shot.ai[1]};}
        private static Group Capture(Player player,NpcTrajectory timeline,PredictionTerrain terrain,HostAttackObstacles obstacles,Vector2 aim,Projectile[] family,HostProjectileTargets targets,HostProjectileBirthSlots slots)
        {
            obstacles.BeginCandidate();var group=new Group{Player=player,Timeline=timeline,Terrain=terrain,Root=obstacles,Aim=aim,Targets=targets,Slots=slots.Copy(),Confidence=AttackConfidence.Conditional};
            foreach(var source in family)if(source.active){var shot=Copy(source);group.Add(Make(group,shot,new State(shot),shot.whoAmI,false));}
            return group;
        }
        private static Projectile[] Observe(Player player,HostAttackObstacles obstacles,Projectile sample)
        {
            var family=new List<Projectile>();foreach(var q in Main.projectile)if(q.owner==player.whoAmI && (Handles(q.type) || q.type==953) && (q.active || q.type==1024))family.Add(q);
            // Include inactive retained cards: ItemCheck_Shoot's reset/query
            // has no active gate. Every real full key and AI field is leased.
            var observed=family.ToArray();obstacles.CaptureOwnedProjectileFamily(observed,player.whoAmI,sceneTypes);return observed;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay,HostAttackPlayerFrame? preparedPlayer=null)
        {
            var sample=new Projectile();sample.SetDefaults(ammo.Projectile);sample.owner=player.whoAmI;sample.damage=ammo.Damage;
            var family=Observe(player,obstacles,sample);var targets=obstacles.CaptureAttachmentTargets(sample);var slots=obstacles.CaptureBirthSlots();
            int mode=sample.type==1024?ammo.CardFireType:0;
            if(mode==3)
            {
                var group=Capture(player,timeline,terrain,obstacles,origin,family,targets,slots);
                if(!BeforeLaunch(group,player,preparedPlayer,age,beforeNpc,delay))return null;
                bool outstanding=false;
                foreach(var actual in family)if(actual.type==1024)
                {var member=group.Find(actual.whoAmI);if(member==null?actual.ai[0]!=2:member.Shot.type==1024 && member.State.Phase!=2)outstanding=true;}
                if(outstanding)
                {
                    // The native Player pass includes inactive retained cards,
                    // in ascending slot order, before any NPC movement.
                    foreach(var actual in family)if(actual.type==1024)
                    {
                        var member=group.Find(actual.whoAmI);if(member==null){var q=Copy(actual);member=Make(group,q,new State(q),q.whoAmI,true);group.Add(member);}
                        if(member.Shot.type!=1024)continue; // A real copied birth replaced the retained slot.
                        member.Primary=true;Recall(ref member.State,member.Shot.width,member.Shot.height);member.Receivers.ResetLocalHitHistory();
                        AttackContact hit;bool pass=Damage(group,member,age,0,delay+1,true,out hit);if(!pass)return null;if(hit!=null)return hit;
                    }
                    return Run(group,age+1,delay+1,true);
                }
                mode=0; // Original resets mode3 before Shoot when no old A0<2.
            }
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;
            for(int seed=0;seed<9;seed++)
            {
                var body=timeline[Math.Min(timeline.Count-1,age+1+seed*8)].ProjectileReceiveBounds;
                var unit=(new Vector2(body.CenterX,body.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);var aim=origin+unit*10000;
                int patterns=sample.type==1024?2:1;
                for(int pattern=0;pattern<patterns;pattern++)
                {
                    var group=Capture(player,timeline,terrain,obstacles,aim,family,targets,slots);int count=mode==1?2:mode==2?6:1;
                    if(!BeforeLaunch(group,player,preparedPlayer,age,beforeNpc,delay))continue;
                    if(sample.type==1024)group.Confidence=AttackConfidence.Representative;
                    for(int member=0;member<count;member++)
                    {
                        int slot;if(!group.Slots.TryAllocate(out slot)){group.Unknown=true;break;}
                        var q=new Projectile();q.SetDefaults(sample.type);q.owner=player.whoAmI;q.damage=ammo.Damage;q.active=true;
                        Vector2 velocity=unit*ammo.Speed;if(mode==1 && member==1)velocity*=-1;if(mode==2)velocity=velocity.RotatedBy(((float)Math.PI/4f/5f)*(-2.5f+member));
                        float sign=mode==2?(pattern==0 || member%2==0?-1:1):(pattern==0?-1:1);
                        var state=new State(q,origin,velocity,sample.type==1024?sign*.4f:0);group.Add(Make(group,q,state,slot,true));
                    }
                    if(group.Unknown)continue;var contact=Run(group,age+1,delay+1,true);if(contact!=null)return contact;
                }
            }
            return null;
        }
        private static bool BeforeLaunch(Group group,Player launch,HostAttackPlayerFrame? prepared,int age,bool beforeNpc,int delay)
        {
            if(delay>0 && group.Members.Count>0)
            {
                // Existing attacks have already spent the cooldown actions
                // before a future hand may emit/recall. Advance their copied
                // motors, cap deaths, slots and immunity from the original
                // player epoch, not from the already advanced launch clone.
                var original=Main.player[launch.whoAmI];group.Player=original;group.PlayerFrame=HostAttackPlayerFrame.Capture(original,group.Timeline);
                Run(group,age-delay+1,1,beforeNpc,delay);if(group.Unknown)return false;
            }
            group.Player=launch;group.PlayerFrame=prepared??HostAttackPlayerFrame.Capture(launch,group.Timeline);return true;
        }
        private static AttackContact Stop(Group group){group.Unknown=true;return null;}
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(!Handles(shot.type) || !shot.active || shot.damage<=0)return null;
            var family=Observe(player,obstacles,shot);var targets=obstacles.CaptureAttachmentTargets(shot);var slots=obstacles.CaptureBirthSlots();
            var group=Capture(player,timeline,terrain,obstacles,shot.Center+shot.velocity.SafeNormalize(Vector2.UnitX)*10000,family,targets,slots);
            var selected=group.Find(shot.whoAmI);if(selected==null)return null;selected.Primary=true;
            if(clock.Phase==HostAttackPhase.Projectiles)
                foreach(var member in group.Members)member.BornOuter=member.Slot<shot.whoAmI?1:0;
            // The actual born ai2 is retained. All earlier real members can
            // write owner immunity or produce953 before this member's slot.
            // At this member's Update prefix, lower slots have ALREADY run
            // this outer; replay them starting at the next array pass only.
            return Run(group,clock.FirstTick,clock.LaunchDelay+(clock.Phase==HostAttackPhase.Projectiles?0:1),!clock.NextWorld);
        }
        // Read-only existing-flight preparation is one family solve, never
        // one complete pool capture per card. Draw checks captured scalars;
        // full receiver/terrain/scene validation remains Prepare/Consume.
        internal sealed class FlightState
        {
            private sealed class Row
            {
                internal readonly Projectile Actual,Snapshot;internal readonly State Motion;internal readonly int Key;
                internal Row(Projectile q){Actual=q;Snapshot=Copy(q);Motion=new State(q);Key=(int)q.key;}
                internal bool Current
                {
                    get
                    {
                        var q=Actual;var p=Snapshot;var s=Motion;
                        return ReferenceEquals(Main.projectile[p.whoAmI],q) && (int)q.key==Key && q.active==s.Alive && q.type==p.type && q.owner==p.owner && q.position==s.Position && q.velocity==s.Velocity && q.width==p.width && q.height==p.height && q.timeLeft==s.Left && q.extraUpdates==p.extraUpdates && q.damage==p.damage && q.penetrate==p.penetrate && q.maxPenetrate==p.maxPenetrate && q.friendly==p.friendly && q.hostile==p.hostile && q.ai[0]==s.Phase && q.ai[1]==s.Clock && q.ai[2]==s.Curve && q.localAI[0]==s.AttachedClock && q.alpha==s.Alpha && q.tileCollide==s.Tile && q.ignoreWater==s.IgnoreWater && q.wet==s.Wet && q.honeyWet==s.Honey && q.lavaWet==s.Lava && q.shimmerWet==s.Shimmer;
                    }
                }
            }
            private readonly Player player;private readonly int heldType;private readonly Row[] rows;
            private readonly HostAttackObstacles obstacles;private readonly PredictionTerrain terrain;private readonly HostProjectileEnvironment.State environment;
            internal FlightState(Player player,Projectile[] observed,HostAttackObstacles obstacles,PredictionTerrain terrain)
            {this.player=player;heldType=player.HeldItem.shoot;this.obstacles=obstacles;this.terrain=terrain;environment=new HostProjectileEnvironment.State(true);rows=Array.ConvertAll(observed,q=>new Row(q));}
            internal bool MembersCurrent {get{if(player.HeldItem.shoot!=heldType)return false;foreach(var row in rows)if(!row.Current)return false;return true;}}
            internal bool Current {get{return MembersCurrent && environment.Current && obstacles.Unchanged && terrain.Unchanged;}}
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] observed,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out FlightState state)
        {
            state=null;Projectile first=null;foreach(var q in observed)if(q.active && q.owner==player.whoAmI && q.type==player.HeldItem.shoot && Handles(q.type) && q.damage>0){first=q;break;}if(first==null)return null;
            var obstacles=new HostAttackObstacles(player,first,timeline,0,clock.BeforeNpc,clock.NextWorld);obstacles.CaptureOwnedProjectileFamily(observed,player.whoAmI,sceneTypes);
            var targets=obstacles.CaptureAttachmentTargets(first);var slots=obstacles.CaptureBirthSlots();state=new FlightState(player,observed,obstacles,terrain);
            var group=Capture(player,timeline,terrain,obstacles,first.Center+first.velocity.SafeNormalize(Vector2.UnitX)*10000,observed,targets,slots);
            foreach(var member in group.Members){member.Primary=member.Shot.type==player.HeldItem.shoot && Handles(member.Shot.type);if(clock.Phase==HostAttackPhase.Projectiles)member.BornOuter=0;}
            return Run(group,clock.FirstTick,clock.LaunchDelay+(clock.Phase==HostAttackPhase.Projectiles?0:1),!clock.NextWorld);
        }
        private static bool Attachment(Group group,Member member,int tick,out MotionRect? target)
        {
            target=null;if(member.State.Phase!=1 || (int)member.State.Clock<0)return true;
            int slot=(int)member.State.Clock;
            if(slot==group.Timeline.Identity.Slot)
            {
                // CanReceive false does not identify dontTakeDamage versus
                // every other native receive reason. Do not invent a retire
                // from an ambiguous future selected qualification.
                if(!group.Timeline[tick].CanReceive)return false;target=group.Timeline[tick].Bounds;return true;
            }
            for(int i=0;i<group.Targets.Count;i++)if(group.Targets.Slot(i)==slot){target=group.Targets.Bounds(i);return true;}return true;
        }
        private static AttackContact Run(Group group,int firstTick,int firstOuter,bool playerCompleted,int maximumFrames=120)
        {
            var pc=group.PlayerFrame??HostAttackPlayerFrame.Capture(group.Player,group.Timeline);var player=pc.Motion;PredictionStop stop;int playerSteps=0;
            for(int frame=0;frame<maximumFrames && firstTick+frame<group.Timeline.Count;frame++)
            {
                int tick=firstTick+frame,outer=firstOuter+frame;
                for(int at=0;at<group.Members.Count;)
                {
                    var row=group.Members[at];int slot=row.Slot;
                    if(row.State.Alive && row.BornOuter<=outer)
                    {
                        if(!row.Receivers.BeforeNpc(tick,outer,row.State.Position.X+row.Shot.width*.5f,row.State.Position.Y+row.Shot.height*.5f))return Stop(group);
                        for(int sub=0;sub<=row.Shot.extraUpdates && row.State.Alive;sub++)
                        {
                            if(row.Shot.type==953)
                            {AttackContact burst;if(!BurstStep(group,row,tick,sub,outer,out burst))return Stop(group);if(burst!=null)return burst;continue;}
                            MotionRect? attached;if(!Attachment(group,row,tick,out attached))return Stop(group);
                            var prior=row.State;var owner=new MotionRect(player.X,player.Y,player.Width,player.Height);
                            if(!Step(row.Shot,group.Terrain,ref row.State,attached,owner,group.Player.active,group.Player.dead))return Stop(group);
                            if(row.Shot.type==1024 && row.State.Phase==2 && group.Player.active && !group.Player.dead)
                            {
                                // Backfill only when a return action really
                                // consumes the player. Do not replace future
                                // owner geometry with a frozen live center.
                                int required=frame+(playerCompleted?0:1);
                                while(playerSteps<required){if(!PlayerMotionContinuation.Advance(ref player,pc.Environment,group.Terrain,out stop))return Stop(group);playerSteps++;}
                                owner=new MotionRect(player.X,player.Y,player.Width,player.Height);row.State=prior;
                                if(!Step(row.Shot,group.Terrain,ref row.State,attached,owner,group.Player.active,group.Player.dead))return Stop(group);
                            }
                            if(row.State.ResetDamage)
                            {
                                row.Receivers.ResetLocalHitHistory();AttackContact immediate;var position=row.State.Position;row.State.Position=row.State.ResetCenter-row.Shot.Size*.5f;
                                bool pass=Damage(group,row,tick,sub,outer,false,out immediate);row.State.Position=position;if(!pass)return Stop(group);if(immediate!=null)return immediate;
                            }
                            if(row.State.Alive && row.State.Phase!=1){AttackContact hit;if(!Damage(group,row,tick,sub,outer,false,out hit))return Stop(group);if(hit!=null)return hit;}
                            if(row.State.Alive)Finish(ref row.State);
                            if(row.State.Killed && !Death(group,row,outer,slot,row.Primary))return Stop(group);
                            if(!row.State.Alive && !row.State.Killed)group.Slots.Release(slot); // Native world-bounds removal has no Kill derivative.
                        }
                    }
                    // NewProjectile may insert a lower slot into this copied
                    // list. It has missed this native array pass, not an extra
                    // AI action. Advance past all <=current slots once.
                    while(at<group.Members.Count && group.Members[at].Slot<=slot)at++;
                }
                if(group.Unknown || !group.Members.Exists(m=>m.State.Alive))return null;
            }
            return null;
        }
        private static bool Damage(Group group,Member row,int tick,int sub,int outer,bool playerStage,out AttackContact contact)
        {
            contact=null;if(row.Shot.damage<=0 || !row.Shot.friendly || row.Shot.hostile)return true;
            var box=DamageBox(row.Shot.type,row.State.Position,row.Shot.width,row.Shot.height);int prior=row.Receivers.SelectedHits;
            bool pass=row.Receivers.PassCustomOuter(2,tick,outer,box.Center.X,box.Center.Y,row.Shot.width,row.Shot.height,b=>Collides(row.Shot.type,box,b),consumeSelected:true,onHit:(slot,remaining)=>
            {
                if(row.State.Phase<2 && row.Shot.type!=953)
                {
                    MotionRect body=default(MotionRect);bool found=slot==group.Timeline.Identity.Slot;
                    if(found)body=group.Timeline[tick].Bounds;
                    else for(int i=0;i<group.Targets.Count;i++)if(group.Targets.Slot(i)==slot){body=group.Targets.Bounds(i);found=true;break;}
                    if(!found)return false;Attach(ref row.State,slot,body,row.Shot.width,row.Shot.height);
                }
                if(row.Shot.type==598 || row.Shot.type==636)
                {
                    int cap=row.Shot.type==598?6:8,count=0;Member oldest=null;
                    foreach(var candidate in group.Members)
                    {
                        if(candidate.Slot==row.Slot || !candidate.State.Alive || candidate.Shot.type!=row.Shot.type || candidate.State.Phase!=1 || (int)candidate.State.Clock!=slot)continue;
                        if(oldest==null || candidate.State.Left<oldest.State.Left)oldest=candidate;if(++count==cap)break;
                    }
                    if(count==cap){Kill(ref oldest.State);if(!Death(group,oldest,outer,row.Slot,row.Primary))return false;}
                }
                return true;
            });
            if(row.Primary && row.Receivers.SelectedHits>prior)
            {
                contact=playerStage?AttackIntercept.MeleeShapeContact(group.Timeline,tick,true,group.Aim.X,group.Aim.Y,box.Center.X,box.Center.Y):AttackIntercept.BodyContact(group.Timeline,tick,sub,group.Aim.X,group.Aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,group.Confidence);
            }
            // An already qualified selected Strike precedes cap Kill/birth
            // and later NPC slots. A missing later derivative cannot undo it.
            return contact!=null || pass;
        }
        private static bool Death(Group group,Member parent,int outer,int executingSlot,bool fromInput)
        {
            if(parent.DeathHandled)return true;parent.DeathHandled=true;
            if(parent.Shot.type==636)
            {
                int slot;if(!group.Slots.TryAllocate(out slot)){group.Unknown=true;return false;}
                var child=new Projectile();child.SetDefaults(953);child.owner=parent.Shot.owner;child.damage=parent.Shot.damage;child.active=true;child.Center=parent.State.DeathCenter;
                // .85 is an explicit legal Kill representative, not another
                // random draw. Its first-AI44px box is also inside every legal
                // larger scale, but later receive history remains conditional.
                child.ai[1]=.85f;group.Confidence=AttackConfidence.Representative;
                group.Add(Make(group,child,new State(child),slot,fromInput,slot>executingSlot?outer:outer+1));
            }
            group.Slots.Release(parent.Slot);return true;
        }
        private static bool BurstStep(Group group,Member row,int tick,int sub,int outer,out AttackContact contact)
        {
            contact=null;var s=row.State;var shot=row.Shot;
            if(s.Position.X<=Main.leftWorld || s.Position.X+shot.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+shot.height>=Main.bottomWorld){row.State.Alive=false;group.Slots.Release(row.Slot);return true;}
            row.BurstScale+=.01f;row.BurstClock+=1;
            if(row.BurstClock>=3*Main.projFrames[953]){Kill(ref row.State);return Death(group,row,outer,row.Slot,false);}
            if(row.BurstClock==1)
            {
                var center=s.Position+shot.Size*.5f;shot.width=shot.height=(int)(52f*row.BurstScale);s.Position=center-shot.Size*.5f;s.Half=shot.Size*.5f;row.State=s;
                if(!Damage(group,row,tick,sub,outer,false,out contact) || contact!=null)return contact!=null;
            }
            row.State.Position+=row.State.Velocity;
            if(!Damage(group,row,tick,sub,outer,false,out contact) || contact!=null)return contact!=null;
            Finish(ref row.State);if(row.State.Killed)return Death(group,row,outer,row.Slot,false);return true;
        }
    }
}
