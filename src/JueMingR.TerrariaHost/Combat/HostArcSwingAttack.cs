using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Actual item roles, not every AI190/191 projectile. A private family
    // follows native projectile slots; newly allocated earlier slots wait for
    // the next outer action. No native AI, NewProjectile or RNG is called.
    internal static class HostArcSwingAttack
    {
        private const int MaximumMembers=128;
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal Player ForeignOwner;internal bool Source,Representative;}
        internal static bool Handles(int type){return type==982 || type==997 || type==972 || type==973 || type==983 || type==985 || type==1126;}
        internal static bool Weapon(int type){return type==368 || type==1826 || type==273 || type==674 || type==675 || type==757 || type==1226;}
        internal static bool MemberType(int type){return type==321 || type==997 || type==972 || type==973 || type>=982 && type<=985 || type==1126 || type==1128 || type==1129;}
        internal static Projectile Copy(Projectile p)
        {var q=HostThornAttack.Copy(p);q.ownerHitCheckDistance=p.ownerHitCheckDistance;q.stopsDealingDamageAfterPenetrateHits=p.stopsDealingDamageAfterPenetrateHits;return q;}
        private static void Kill(Projectile p){if(p.active){p.active=false;p.timeLeft=0;}}
        private static bool Retired(Projectile p)
        {return p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y);}
        internal static bool Wet(Projectile p,PredictionTerrain terrain,bool birth=false)
        {
            if(p.ignoreWater && !birth)return true;
            bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            if(birth){p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;return !p.shimmerWet;}
            if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(p.wet!=wet && p.wetCount==0)p.wetCount=10;p.wet=wet;if(!p.wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;return !p.shimmerWet;
        }
        private static bool PointClear(Vector2 from,Vector2 to,PredictionTerrain terrain,out bool clear)
        {PredictionStop stop;return terrain.CanHit(new MotionRect(from.X,from.Y,0,0),new MotionRect(to.X,to.Y,0,0),out clear,out stop);}
        private static bool SolidPoint(Vector2 point,PredictionTerrain terrain,out bool solid)
        {
            solid=true;int x=(int)(point.X/16),y=(int)(point.Y/16);if(x<0 || x>=Main.maxTilesX || y<0 || y>=Main.maxTilesY-40)return true;
            PredictionTile cell;PredictionStop stop;if(!terrain.Tile(x,y,out cell,out stop))return false;solid=cell.Active && cell.RawSolid && !cell.SolidTop;return true;
        }
        private static bool Rays(Projectile p,float length,bool centerGate,PredictionTerrain terrain,out bool clear)
        {
            clear=false;if(centerGate){bool solid;if(!SolidPoint(p.Center,terrain,out solid))return false;if(solid)return true;}
            float rotation=p.velocity.ToRotation();for(float i=-1;i<=1;i+=.5f)
            {var ray=(rotation+i*((float)Math.PI/4)*.25f).ToRotationVector2();bool rayClear;if(!PointClear(p.Center+ray*length*.5f*p.scale,p.Center+ray*length*p.scale,terrain,out rayClear))return false;if(rayClear){clear=true;break;}}return true;
        }
        internal static bool Ai(Projectile p,Player body,PredictionTerrain terrain,Projectile[] scene,int representative,out List<Projectile> children)
        {
            children=new List<Projectile>();if(!p.active)return true;
            if(p.aiStyle==190)
            {
                if(p.ai[1]<=0 || p.ai[2]<=0)return false;
                if(p.localAI[0]==0 && p.type==1126){p.localAI[2]=p.velocity.Y==0?1:0;p.velocity.Y=0;}
                p.localAI[0]++;float phase=p.localAI[0]/p.ai[1],sign=p.ai[0];p.rotation=(float)Math.PI*sign*phase+p.velocity.ToRotation()+sign*(float)Math.PI+body.fullRotation;
                float rate=p.type==983?1:p.type==982 || p.type==997 || p.type==984 || p.type==1126?.6f:.2f,scale=p.type==983?1.2f:p.type==1126?.85f:1;
                p.Center=body.RotatedRelativePoint(body.MountedCenter)-p.velocity;p.scale=scale+phase*rate;
                if(p.type==972 && Math.Abs(sign)<.2f){p.rotation+=(float)Math.PI*4*sign*10*phase;float flight=Utils.Remap(p.localAI[0],10,p.ai[1]-5,0,1);p.position+=p.velocity.SafeNormalize(Vector2.Zero)*(45*flight);p.scale+=flight*.4f;}
                if(p.type==1126 && !Chlorophyte(p,body,terrain,representative,children))return false;
                p.scale*=p.ai[2];if(phase>=1)Kill(p);return true;
            }
            if(p.type==973 || p.type==985)
            {
                float fade=p.ai[1]+(p.type==973?50:0),end=fade+(p.type==973?15:25);if(p.ai[1]<=0 || p.ai[2]<=0)return false;
                p.localAI[0]++;if(p.type==985 && p.localAI[1]==1)p.localAI[0]+=2;if(p.type==973 && p.damage==0 && p.localAI[0]<MathHelper.Lerp(fade,end,.5f))p.localAI[0]+=6;
                p.Opacity=Utils.Remap(p.localAI[0],0,p.ai[1],0,1)*Utils.Remap(p.localAI[0],fade,end,1,0);if(p.localAI[0]>=end){p.localAI[1]=1;Kill(p);return true;}
                float phase=p.localAI[0]/p.ai[1],turn=Utils.Remap(p.localAI[0],p.ai[1]*.4f,end,0,1);p.direction=p.spriteDirection=(int)p.ai[0];
                if(p.type==973)
                {
                    if(p.damage!=0 && p.localAI[0]>=80)p.damage=0;if(p.damage!=0){bool clear;if(!Rays(p,80,true,terrain,out clear))return false;if(!clear)p.damage=0;}
                    p.localAI[1]++;turn=Utils.Remap(p.localAI[1],p.ai[1]*.4f,end,0,1);p.Center=body.RotatedRelativePoint(body.MountedCenter)-p.velocity+p.velocity*turn*turn*77;
                    p.rotation+=p.ai[0]*((float)Math.PI*2)*(4+p.Opacity*4)/90;p.scale=Utils.Remap(p.localAI[0],p.ai[1]+2,end,1.12f,1)*p.ai[2];return true;
                }
                p.ownerHitCheck=p.localAI[0]<=6;if(p.localAI[0]>=MathHelper.Lerp(fade,end,.65f))p.damage=0;float ease=1-(1-turn)*(1-turn);p.rotation=p.velocity.ToRotation();p.scale=Utils.Remap(ease,0,1,1.5f,1)*p.ai[2];
                p.Opacity=Utils.Remap(p.localAI[0],0,p.ai[1]*.5f,0,1)*Utils.Remap(p.localAI[0],end-12,end,1,0);
                if(p.velocity.Length()>8){p.velocity*=.94f;if(p.localAI[1]==0){bool clear;if(!Rays(p,110,false,terrain,out clear))return false;if(!clear)p.localAI[1]=1;}if(p.localAI[1]==1 && p.velocity.Length()>8)p.velocity*=.8f;if(p.localAI[1]==1)p.velocity*=.88f;}
                if(p.damage==0){p.localAI[0]+=3;p.velocity*=.76f;}if(p.localAI[0]<10 && (p.localAI[1]==1 || p.damage==0)){p.localAI[0]++;p.velocity*=.85f;}return true;
            }
            if(p.type==1129){p.width=(int)p.ai[1];p.height=(int)p.ai[2];p.ai[0]++;if(p.ai[0]>20)Kill(p);return true;}
            return p.type==1128 && Cloud(p,terrain,scene);
        }
        private static Projectile Child(Projectile parent,int type,Vector2 center,Vector2 velocity,int damage,float ai1=0,float ai2=0)
        {
            // NewProjectile retains Entity.direction=1 until its first Update tail.
            var p=new Projectile();p.SetDefaults(type);
            p.owner=parent.owner;p.active=true;p.Center=center;p.velocity=velocity;p.damage=damage;p.ai[1]=ai1;p.ai[2]=ai2;p.armorPenetration+=parent.armorPenetration;p.bonusTagDamage+=parent.bonusTagDamage;p.bonusCritChance+=parent.bonusCritChance;p.tagEffectType=parent.tagEffectType;return p;}
        private static bool Impact(int x,int y,PredictionTerrain terrain,out bool solid)
        {
            // Framing.GetTileSafely gives an empty tile outside the world.
            solid=false;if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return true;PredictionTile t;PredictionStop stop;if(!terrain.Tile(x,y,out t,out stop))return false;solid=t.Active && t.RawSolid;return true;
        }
        internal static bool FindGround(Projectile p,ref Vector2 position,PredictionTerrain terrain,out bool found)
        {
            found=false;float nearest=float.MaxValue;var initial=position;int x=(int)(position.X/16),y=(int)(position.Y/16),left=Math.Sign(p.velocity.X)>0?-5:0,right=Math.Sign(p.velocity.X)>0?0:5;
            for(int dx=left;dx<=right;dx++)for(int dy=-2;dy<=2;dy++){bool ground,above;if(!Impact(x+dx,y+dy,terrain,out ground) || !Impact(x+dx,y+dy-1,terrain,out above))return false;if(!ground || above)continue;float distance=(initial-new Vector2((x+dx)*16+8,(y+dy)*16-8)).Length();if(distance<nearest){position=new Vector2(x*16+8,(y+dy)*16-8);nearest=distance;found=true;}}return true;
        }
        private static bool Chlorophyte(Projectile p,Player body,PredictionTerrain terrain,int representative,List<Projectile> children)
        {
            float phase=p.localAI[0]/p.ai[1],radius=81*p.scale;
            if(p.localAI[2]==1 && phase>=.8f && (p.localAI[0]-1)/p.ai[1]<.8f)
            {
                float baseAngle=Utils.Remap(.8f,0,1,0,(float)Math.PI/4);var point=p.Center+(p.rotation+baseAngle*p.ai[0]).ToRotationVector2()*(radius-4);bool found;if(!FindGround(p,ref point,terrain,out found))return false;
                float step=(float)Math.PI/64,left=step*20,right=step*12,angle=0;
                while(!found && (angle<=left || angle<=right)){angle+=step;for(int side=-1;side<=1;side+=2){if(side<0?angle>left:angle>right)continue;point=p.Center+(p.rotation+(baseAngle+angle*side)*p.ai[0]).ToRotationVector2()*(radius-4);if(!FindGround(p,ref point,terrain,out found))return false;if(found)break;}}
                if(found && p.owner==Main.myPlayer)
                {
                    int x=(int)point.X/16,y=(int)point.Y/16,leftColumn=Math.Sign(p.velocity.X)>0?-5:0,rightColumn=Math.Sign(p.velocity.X)>0?0:5;bool any=false;
                    for(int dx=leftColumn;dx<=rightColumn;dx++)
                    {
                        int surfaces=0;for(int dy=-2;dy<=2;dy++){bool ground,above;if(!Impact(x+dx,y+dy,terrain,out ground) || !Impact(x+dx,y+dy-1,terrain,out above))return false;if(ground && !above)surfaces++;}
                        for(int dy=-2;dy<=2;dy++){bool ground,above;if(!Impact(x+dx,y+dy,terrain,out ground) || !Impact(x+dx,y+dy-1,terrain,out above))return false;if(!ground || above)continue;any=true;for(int n=0;n<(surfaces==1?2:1);n++)
                            // Probability is always positive, so emission is a
                            // legal representative at every captured count.
                            // Exact random coordinates are promised only for
                            // already observed children, never this suffix.
                            children.Add(Child(p,1128,new Vector2((x+dx)*16+10,(y+dy)*16-(4*n-4)),new Vector2(body.direction*2,-5.29f),(int)(p.damage*.75f)));
                        }
                    }
                    if(any){var direction=(p.rotation+baseAngle*p.ai[0]).ToRotationVector2();var center=p.Center+direction*(radius-16);center.Y-=48;if(direction.X>0)center.X-=112;center.X+=direction.X>0?48:-48;children.Add(Child(p,1129,center,Vector2.Zero,p.damage,128,96));}
                }
            }
            if(phase<=.8f && p.owner==Main.myPlayer)
            {
                // Two lawful representatives: the ordinary random test fails,
                // or succeeds. Explicit forced emissions cannot be omitted.
                bool forced=(int)p.localAI[0]==(int)p.localAI[1]/3 || (int)p.localAI[0]==(int)(p.localAI[1]/3)*2;
                if(forced || representative!=0){float radiusNow=81*p.scale,offset=Utils.Remap(phase/.8f,0,1,-(float)Math.PI/4,0)+Utils.Remap(phase,0,1,0,(float)Math.PI/4);var center=p.Center+(p.rotation+offset*p.ai[0]).ToRotationVector2()*MathHelper.Lerp(radiusNow*.5f,radiusNow,phase/.8f);var velocity=body.velocity*.77f+(p.rotation+p.ai[0]*((float)Math.PI/2)).ToRotationVector2()*5;children.Add(Child(p,1128,center,velocity,(int)(p.damage*.75f),120));}
            }
            return true;
        }
        private static bool Cloud(Projectile p,PredictionTerrain terrain,Projectile[] scene)
        {
            p.tileCollide=false;p.ai[0]++;if(p.ai[1]>=1)p.ai[0]+=2;float end=p.ai[1]==0?220:p.ai[1];if(end<=0)return false;if(p.ai[0]>end){Kill(p);p.ai[0]=end;return true;}
            float fraction=p.ai[0]/end;p.scale=Utils.Remap(fraction,0,.1f,.25f,1.1f);var center=p.Center;p.width=(int)(30*p.scale/1.1f);p.height=p.width;p.Center=center;p.Opacity=Utils.Remap(fraction,0,.25f,0,1)*Utils.Remap(fraction,.5f,1,1,0)*.75f;p.velocity.Y-=.01f;
            p.localAI[0]+=p.direction;p.rotation=p.whoAmI*.4002029f+p.localAI[0]*((float)Math.PI*2)/480;p.velocity*=.97f;
            if(scene==null)return false;foreach(var q in scene){if(q==p || !q.active || q.type!=1128 || q.whoAmI==p.whoAmI || !p.Hitbox.Intersects(q.Hitbox))continue;var delta=q.Center-p.Center;if(delta==Vector2.Zero)delta=q.whoAmI<p.whoAmI?new Vector2(-1,1):new Vector2(1,-1);float distance=delta.Length();delta.Normalize();var impulse=delta*(Math.Max(p.width-distance,0)/p.width)*.005f;p.velocity-=impulse;q.velocity+=impulse;}
            var unit=p.velocity.SafeNormalize(Vector2.Zero);var probe=p.Center+unit*16;bool solid;if(!terrain.ProjectileWorldPointSolid(probe,out solid))return false;if(!solid)return true;
            bool found=false;if(Math.Abs(unit.X)>Math.Abs(unit.Y)){var reflected=new Vector2(unit.X,-unit.Y);var first=p.Center+reflected*16;if(!Redirect(p,first,reflected,true,terrain,out found))return false;for(int i=0;i<4 && !found;i++)if(!Redirect(p,probe+new Vector2(0,new[]{-16,16,-32,32}[i]),Vector2.Zero,false,terrain,out found))return false;}
            if(!found){var reflected=new Vector2(-unit.X,unit.Y);probe=p.Center+reflected*16;if(!Redirect(p,probe,reflected,true,terrain,out found))return false;for(int i=0;i<2 && !found;i++)if(!Redirect(p,probe+new Vector2(i==0?-16:16,0),Vector2.Zero,false,terrain,out found))return false;}
            if(!found)p.velocity=Vector2.Lerp(p.velocity,p.velocity-unit,.5f);return true;
        }
        private static bool Redirect(Projectile p,Vector2 point,Vector2 direction,bool direct,PredictionTerrain terrain,out bool found)
        {bool solid;found=false;if(!terrain.ProjectileWorldPointSolid(point,out solid))return false;if(solid)return true;var v=direct?direction:point-p.Center;if(!direct)v.Normalize();v*=p.velocity.Length();p.velocity=v;found=true;return true;}
        internal static bool Step(Projectile p,Player body,PredictionTerrain terrain,Projectile[] scene,int representative,out List<Projectile> children)
        {
            children=null;if(!p.active)return true;if(Retired(p)){p.active=false;return true;}
            if(!MemberType(p.type) || p.aiStyle!=(p.type==973 || p.type==985?191:p.type==1128?92:p.type==1129?209:190) || p.extraUpdates!=0 || p.correctSlopeCollision || p.shimmerWet || p.manualDirectionChange)return false;
            var wetVelocity=p.velocity;if(!Ai(p,body,terrain,scene,representative,out children))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;if(p.owner<255 && !body.active)Kill(p);
            if(!Wet(p,terrain))return false;var position=p.position;var velocity=p.velocity;bool hit=false;var lastVelocity=p.velocity;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,false,true))return false;p.position=position;p.velocity=velocity;
            if(hit){if(p.type!=985)return false;p.position+=p.velocity;p.velocity=lastVelocity*.01f;p.localAI[1]=1;}
            p.position+=p.wet?wetVelocity:p.velocity;if(p.velocity.X<0)p.direction=-1;else p.direction=1;return true;
        }
        internal static void Finish(Projectile p){if(!p.active)return;p.timeLeft--;if(p.timeLeft<=0 || p.penetrate==0)Kill(p);}
        internal static bool Collides(Projectile p,MotionRect box,PredictionTerrain terrain)
        {
            var target=new Rectangle((int)box.X,(int)box.Y,(int)box.Width,(int)box.Height);if(p.aiStyle==190){float radius=94*p.scale,angle=p.rotation+((float)Math.PI*2)/25*p.ai[0],fade=Utils.Remap(p.localAI[0],p.ai[1]*.3f,p.ai[1]*.5f,1,0);return target.IntersectsConeSlowMoreAccurate(p.Center,radius,angle,(float)Math.PI/4) || fade>0 && target.IntersectsConeSlowMoreAccurate(p.Center,radius,angle-(float)Math.PI/4*p.ai[0]*fade,(float)Math.PI/4);}
            if(p.type==973 || p.type==985){bool overlap=p.type==973?(target.ClosestPointInRect(p.Center)-p.Center).Length()<100*p.scale:target.IntersectsConeFastInaccurate(p.Center,90*p.scale,p.rotation,(float)Math.PI/4);bool clear;return overlap && PointClear(p.Center,target.Center.ToVector2(),terrain,out clear) && clear;}return p.Hitbox.Intersects(target);
        }
        internal static Projectile[] BirthHand(Player body,Item item,AttackAmmoSnapshot ammo,Vector2 aim,int counter)
        {
            // Shoot uses the rotated mounted point for its aim, while the actual
            // paired births use the unrotated MountedCenter. Keep both stores.
            var aimOrigin=body.RotatedRelativePoint(body.MountedCenter);int direction=HostBetsyFlameAttack.Facing(body,aimOrigin,aim);body.direction=direction;float scale=body.GetAdjustedItemScale(item);int duration=HostMeleeAttack.AnimationDuration(body,item);float sign=direction*body.gravDir;var origin=body.MountedCenter;var velocity=HostThornAttack.BirthVelocity(body,aimOrigin,aim,ammo.Speed);
            int primary=item.type==675?972:item.type==757?984:ammo.Projectile;var p=Child(new Projectile{owner=body.whoAmI},primary,origin,new Vector2(direction,item.type==1226 && counter>0?1:0),item.type==1226 && counter<=0?(int)(ammo.Damage*1.5f):ammo.Damage,duration,scale);p.ai[0]=sign;p.knockBack=body.GetWeaponKnockback(item,item.knockBack)*(item.type==1226 && counter<=0?1.25f:1);p.armorPenetration+=item.armorPenetration;p.bonusTagDamage+=item.bonusTagDamage;
            if(item.type==1226 || item.type==368 || item.type==1826)return new[]{p};int secondary=item.type==273?972:item.type==674?982:item.type==675?973:985;var q=Child(p,secondary,origin,item.type==674?new Vector2(direction,0):velocity*(item.type==757?5:1),item.type==674?0:item.type==675?ammo.Damage/2:ammo.Damage,item.type==273?30:item.type==674?duration:item.type==675?32:18,scale);q.ai[0]=item.type==273?sign*.1f:sign;q.knockBack=body.GetWeaponKnockback(item,item.knockBack);return new[]{p,q};
        }
        private static Projectile[] Capture(HostAttackObstacles root)
        {
            var scene=new List<Projectile>();var a=new List<Projectile>();var b=new List<Projectile>();var c=new List<Projectile>();var d=new List<Projectile>();var e=new List<Projectile>();var f=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && MemberType(p.type)){if(p.type==321){if(p.owner==Main.myPlayer)f.Add(p);}else if(p.type<=973)a.Add(p);else if(p.type<=985)b.Add(p);else if(p.type==1126)c.Add(p);else if(p.type==997)e.Add(p);else d.Add(p);if(p.owner==Main.myPlayer || p.type==1128)scene.Add(p);}
            root.CaptureProjectileFamily(a.ToArray(),972,973);root.CaptureProjectileFamily(b.ToArray(),982,985);root.CaptureProjectileFamily(c.ToArray(),1126,1126);root.CaptureProjectileFamily(d.ToArray(),1128,1129);root.CaptureProjectileFamily(e.ToArray(),997,997);root.CaptureOwnedProjectileFamily(f.ToArray(),Main.myPlayer,new[]{321});return scene.ToArray();
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene){bool source=false;if(sources!=null)foreach(var s in sources)if(ReferenceEquals(p,s)){source=true;break;}var q=Copy(p);rows.Add(new Member{Shot=q,Source=source,ForeignOwner=q.owner<255 && q.owner!=Main.myPlayer?(Player)clone.Invoke(Main.player[q.owner],null):null,Receive=q.owner==Main.myPlayer?root.ForkForSpawn(q):null});}return rows;}
        private static bool Birth(Member parent,Projectile child,List<Member> rows,HostProjectileBirthSlots pool,PredictionTerrain terrain)
        {int slot;if(rows.Count>=MaximumMembers || !pool.TryAllocate(out slot))return false;child.whoAmI=slot;if(!Wet(child,terrain,true))return false;
            // A hypothetical spore can push an already observed cloud. Its
            // later contact then shares that representative suffix; the hand
            // arc motor itself has no crowd feedback and keeps its proof.
            if(child.type==1128)foreach(var row in rows)if(row.Shot.type==1128)row.Representative=true;
            if(child.type==321)foreach(var row in rows)row.Representative=true;
            rows.Add(new Member{Shot=child,Source=parent.Source,Representative=parent.Representative || child.type==321 || child.type==1128 || child.type==1129,Receive=parent.Receive?.ForkForSpawn(child)});return true;}
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots pool,Player body,NpcTrajectory timeline,PredictionTerrain terrain,int tick,int outer,Vector2 aim,int representative,out AttackContact contact,HostProjectileTargets pumpkinTargets=null,HostPumpkinEffectAttack.Inputs pumpkinInputs=null)
        {
            contact=null;foreach(var r in rows)if(r.Shot.active && r.Receive!=null && !r.Receive.BeforeNpc(tick,outer,r.Shot.Center.X,r.Shot.Center.Y,r.Shot.width,r.Shot.height))return false;
            int after=-1;Projectile[] scene=null;for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var r in rows)if(r.Shot.active && r.Shot.whoAmI>after && (m==null || r.Shot.whoAmI<m.Shot.whoAmI))m=r;if(m==null)return true;after=m.Shot.whoAmI;
                for(int sub=0;sub<=m.Shot.extraUpdates;sub++)
                {
                    var p=m.Shot;if(scene==null || scene.Length!=rows.Count){scene=new Projectile[rows.Count];for(int i=0;i<scene.Length;i++)scene[i]=rows[i].Shot;Array.Sort(scene,(a,b)=>a.whoAmI.CompareTo(b.whoAmI));}List<Projectile> children=null;var owner=m.ForeignOwner??body;if(p.type==321){if(pumpkinTargets==null || !HostPumpkinEffectAttack.Step(p,pumpkinTargets,timeline,tick,terrain))return false;}else if(!Step(p,owner,terrain,scene,representative,out children))return false;if(children!=null)foreach(var q in children)if(!Birth(m,q,rows,pool,terrain))return false;
                    if(p.active && p.damage>0 && p.friendly && !p.hostile && !float.IsNaN(p.position.X) && !float.IsNaN(p.position.Y) && !(p.type==1128 && p.ai[1]>=1) && m.Receive!=null)
                    {
                        int hits=m.Receive.SelectedHits;bool failed=false;Func<int,int,bool> hit=(slot,remaining)=>{if(p.type==985)p.damage=(int)((double)p.damage*.75);if(p.type==997 && pumpkinInputs!=null && pumpkinInputs.Proc(slot)){var child=HostPumpkinEffectAttack.Birth(p,owner,pumpkinInputs.Center(slot,timeline,tick),slot,representative,pumpkinInputs);if(!Birth(m,child,rows,pool,terrain)){failed=true;return false;}}return true;};
                        bool passed=p.type==321?m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true):m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b,terrain),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),Math.Max(0,body.itemAnimation),true,hit);
                        p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){var target=timeline[tick].ProjectileReceiveBounds;contact=AttackIntercept.ProjectileShapeContact(timeline,tick,sub,true,aim.X,aim.Y,target.CenterX,target.CenterY,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return true;}
                        if(failed)return false;if(!passed && p.penetrate==0){if(p.stopsDealingDamageAfterPenetrateHits){p.penetrate=-1;p.damage=0;}else Kill(p);}else if(!passed)return false;
                    }
                    Finish(p);if(!p.active){pool.Release(p.whoAmI);break;}
                }
            }
            return false;
        }
        internal static AttackContact SolvePrepared(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,int delay,HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            if(!Weapon(item.type) || !Handles(ammo.Projectile) || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=timeline.Count || obstacles==null)return null;
            // The prepared pose can be a private delayed clone. Capture the
            // real Host owner by slot; opaque PlayerToken is never dereferenced.
            var owner=Main.player[player.whoAmI];if(owner==null || !ReferenceEquals(frame.Motion.PlayerToken,owner))return null;obstacles.CaptureShotFacing(owner);obstacles.CaptureHomingInputs(owner,false,true);obstacles.CaptureArcOwner(owner,item);var captured=Capture(obstacles);if(captured.Length>=MaximumMembers)return null;var pumpkinInputs=item.type==1826 || Array.Exists(captured,p=>p.type==997 || p.type==321)?obstacles.CapturePumpkinInputs():null;var pumpkinTargets=pumpkinInputs==null?null:obstacles.CaptureChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[321]);var pool=obstacles.CaptureBirthSlots();var directions=new Vector2[9];int count=0;
            for(int seed=0;seed<9;seed++)
            {
                var target=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var origin=player.MountedCenter;var direction=(new Vector2(target.CenterX,target.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;var aim=origin+direction*10000;
                for(int variant=0;variant<(item.type==1826?4:item.type==1226?2:1);variant++)
                {
                    obstacles.BeginCandidate();var rows=Prepare(captured,null,obstacles);var slots=pool.Copy();bool valid=true;var body=(Player)clone.Invoke(owner,null);var waitFrame=delay==0?frame:HostAttackPlayerFrame.Capture(owner,timeline);var waitMotion=waitFrame.Motion;
                    for(int wait=0;wait<delay;wait++){if(!beforeNpc || wait>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref waitMotion,waitFrame.Environment,terrain,out stop)){valid=false;break;}}HostAttackPlayerFrame.ApplyPose(body,waitMotion);body.itemAnimation=Math.Max(0,owner.itemAnimation-wait);AttackContact ignored;if(!Outer(rows,slots,body,timeline,terrain,age-delay+wait+1,wait+1,aim,variant,out ignored,pumpkinTargets,pumpkinInputs)){valid=false;break;}}if(!valid)continue;
                    body=(Player)clone.Invoke(player,null);var motion=frame.Motion;HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimationMax=body.itemAnimation=HostMeleeAttack.AnimationDuration(body,item);
                    // ItemCheck decrements a positive counter before Shoot,
                    // including this birth action after the delayed prefix.
                    obstacles.ResetArcMeleeHistory();var newborn=BirthHand(body,item,ammo,aim,Math.Max(0,owner.chlorophyteBladeCounter-delay-1));foreach(var q in newborn){var source=new Member{Source=true,Receive=obstacles};if(!Birth(source,q,rows,slots,terrain)){valid=false;break;}}if(!valid)continue;
                    for(int step=0;step<Math.Min(120,timeline.Count-age-1);step++){if(!beforeNpc || step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))break;}HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=Math.Max(0,body.itemAnimationMax-step);AttackContact contact;if(!Outer(rows,slots,body,timeline,terrain,age+step+1,delay+step+1,aim,variant,out contact,pumpkinTargets,pumpkinInputs))break;if(contact!=null)return contact;}
                }
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            // Native UpdateMeleeHitCooldowns runs only inside a direct hand melee.
            // A switched active noMelee=false hand needs that separate input
            // consumer; do not freeze its array as though an arc still held it.
            if(!player.HeldItem.noMelee && player.itemAnimation>0)return null;
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var q in sources)if(q==null || !q.active || !MemberType(q.type) || q.owner!=player.whoAmI)return null;
            obstacles.CaptureHomingInputs(player,false,true);obstacles.CaptureArcOwner(player,player.HeldItem);var scene=Capture(obstacles);if(scene.Length>=MaximumMembers)return null;var pumpkinInputs=Array.Exists(scene,p=>p.type==997 || p.type==321)?obstacles.CapturePumpkinInputs():null;var pumpkinTargets=pumpkinInputs==null?null:obstacles.CaptureChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[321]);var pool=obstacles.CaptureBirthSlots();var aim=sources[0].Center+sources[0].velocity*10000;
            for(int variant=0;variant<(pumpkinInputs==null?2:4);variant++){obstacles.BeginCandidate();var rows=Prepare(scene,sources,obstacles);var slots=pool.Copy();var body=(Player)clone.Invoke(player,null);var motion=frame.Motion;for(int step=0;step<Math.Min(120,timeline.Count-clock.FirstTick);step++){if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))break;}HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=Math.Max(0,player.itemAnimation-step);AttackContact contact;if(!Outer(rows,slots,body,timeline,terrain,clock.FirstTick+step,step+1,aim,variant,out contact,pumpkinTargets,pumpkinInputs))break;if(contact!=null)return contact;}}return null;
        }
    }
}
