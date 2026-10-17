using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // One ordinary melee use, its currently observed NPC array, and only its
    // own finite OnHit children. Facing is the sole new input. Child velocity
    // is never aimed at the selected NPC: four native lotteries use legal
    // representatives; Volcano's center/velocity/once gate are deterministic.
    internal static class HostMeleeHitFamilyAttack
    {
        private static readonly FieldInfo volcano=typeof(Player).GetField("_spawnVolcanoExplosion",BindingFlags.Instance|BindingFlags.NonPublic),muramasa=typeof(Player).GetField("_spawnMuramasaCut",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Weapon(int type){return HostMeleeHitChildAttack.Weapon(type) || type==1123;}
        private static bool Family(int type){return HostMeleeHitChildAttack.Family(type) || HostBeeAttack.Handles(type);}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal HostBeeAttack.State Bee;internal bool Source;}
        private static Projectile Born(int type,Player p,Vector2 center,Vector2 velocity,int damage,float kb)
        {
            // Cloning a content prototype avoids SetDefaults' unrelated RNG.
            var q=HostNoteAttack.Copy(ContentSamples.ProjectilesByType[type]);q.active=true;q.owner=p.whoAmI;q.Center=center;q.velocity=velocity;q.damage=q.originalDamage=damage;q.knockBack=kb;
            q.armorPenetration+=p.HeldItem.armorPenetration;q.bonusTagDamage+=p.HeldItem.bonusTagDamage;
            q.ai=new float[3];q.localAI=new float[3];q.localNPCImmunity=new int[Main.maxNPCs];q.direction=velocity.X<0?-1:1;return q;
        }
        // Native Player.cs 446xx/448xx. Midpoint choices below are members of
        // the original random domains, not estimates of live Main.rand. The
        // original pre/post DamageVar distinction is retained for each role.
        internal static Projectile[] Births(Player p,Item item,Rectangle hand,MotionRect victim,Vector2 victimVelocity,int originalDamage,int damage,int randomized,float kb,bool rewards,bool onHitFlag,ref bool once)
        {
            Vector2 center=new Vector2(victim.CenterX,victim.CenterY);Projectile q;
            if(item.type==1166)
                return new[]{Born(1111,p,new Vector2(hand.X+hand.Width/2,hand.Y+Math.Max(0,hand.Height/4)),new Vector2(p.direction*3.5f,-2.5f),(int)(originalDamage*.75),kb)};
            if(item.type==3211)
                return new[]{Born(524,p,(hand.Center.ToVector2()+center*2)/3,new Vector2(p.direction*3.5f,0),(int)(damage*.5),kb*.7f)};
            if(item.type==1123 && onHitFlag)
            {
                // count=2, normal bee, +0 damage are all legal, even with
                // strongBees equipped. Never call beeType/beeDamage (RNG).
                var result=new Projectile[2];for(int i=0;i<result.Length;i++){q=Born(181,p,hand.Center.ToVector2(),new Vector2(p.direction*.4f,0),randomized/3,0);q.melee=true;result[i]=q;}return result;
            }
            if(item.type==121 && once && rewards && p.whoAmI==Main.myPlayer)
            {once=false;q=Born(978,p,center,new Vector2(0,-p.gravDir),(int)(damage*.75f),kb);q.ai[1]=2;return new[]{q};}
            if(item.type==155 && once && onHitFlag)
            {
                once=false;var rectangle=new Rectangle((int)victim.X,(int)victim.Y,(int)victim.Width,(int)victim.Height);rectangle.Inflate(30,16);rectangle.Y-=8;
                // The center is itself a legal point in NextVector2FromRectangle;
                // its zero delta takes native's (direction,gravity) fallback.
                Vector2 position=rectangle.Center.ToVector2(),velocity=new Vector2(p.direction,p.gravDir)*8;
                float curve=(float)Math.PI/5f+(float)Math.PI*4f/5f*.5f;curve*=.5f;velocity=velocity.RotatedBy(MathHelper.PiOver4);
                for(int i=0;i<15;i++){position-=velocity;velocity=velocity.RotatedBy(-curve/30);}
                position+=victimVelocity*5;q=Born(977,p,position,velocity,(int)(randomized*.5f),0);q.ai[0]=curve;return new[]{q};
            }
            return Array.Empty<Projectile>();
        }
        private static bool HandOverlap(Player body,Item item,Rectangle hand,MotionRect bounds,bool once)
        {
            var rectangle=new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height);bool overlap=hand.Intersects(rectangle);
            if(item.type!=121)return overlap;
            Vector2 a,b,v;body.GetPointOnSwungItemPath(70,70,0,body.GetAdjustedItemScale(item),out a,out v);body.GetPointOnSwungItemPath(70,70,.9f,body.GetAdjustedItemScale(item),out b,out v);
            return Utils.LineRectangleDistance(rectangle,a,b)<=16 || !once && overlap;
        }
        private static bool Clear(Player body,MotionRect target,bool noTile,PredictionTerrain terrain)
        {if(noTile)return true;bool clear;PredictionStop stop;return terrain.CanHit(new MotionRect(body.position.X,body.position.Y,body.width,body.height),target,out clear,out stop) && (clear || HostSwingAttack.Lines(body,target,terrain));}
        internal static AttackContact Solve(Player player,Item item,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,Rectangle texture,float mount,bool naturalPose,bool newHand,int steps,out HostAttackObstacles resultRoot)
        {
            resultRoot=null;if(clock.Phase==HostAttackPhase.Projectiles || clock.Age<0 || !Weapon(item.type))return null;
            var prototype=Born(item.type==1123?181:item.type==1166?1111:item.type==3211?524:item.type==155?977:978,player,player.Center,Vector2.Zero,player.GetWeaponDamage(item),0);
            var root=new HostAttackObstacles(Main.player[player.whoAmI],prototype,path,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
            root.CaptureMeleeFamily(player,item,newHand);root.CaptureArcOwner(Main.player[player.whoAmI],item);root.CaptureHomingInputs(Main.player[player.whoAmI],false,true);
            var ambient=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))ambient.Add(p);
            root.CaptureMeleeFamily(player,item,newHand).CaptureDamageMembers(ambient);
            root.CaptureOwnedProjectileFamily(ambient.ToArray(),player.whoAmI,new[]{1111,524,977,978,181,566});root.CaptureBirthSlots();
            HostProjectileTargets targets=null;if(item.type==1123 || ambient.Exists(p=>HostBeeAttack.Handles(p.type))){root.CaptureBeeOwner();targets=HostBeeAttack.CaptureTargets(prototype,root);}
            AttackContact best=null;int tries=newHand?2:1;
            for(int candidate=0;candidate<tries;candidate++)
            {
                var branch=root.ForkCandidate();branch.CaptureArcOwner(Main.player[player.whoAmI],item);branch.BeginCandidate();if(newHand)branch.ResetArcMeleeHistory();if(targets!=null)branch.CaptureBeeOwner();var state=branch.CaptureMeleeFamily(player,item,newHand);state.Facing=candidate==0?player.direction:-player.direction;
                var contact=Replay(player,item,path,clock,terrain,frame,texture,mount,naturalPose,newHand,steps,branch,state,ambient,targets);
                if(contact!=null && (best==null || contact.Tick<best.Tick || contact.Tick==best.Tick && contact.Subupdate<best.Subupdate)){best=contact;resultRoot=branch;}
            }
            return best;
        }
        private static AttackContact Replay(Player player,Item item,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,Rectangle texture,float mount,bool naturalPose,bool newHand,int steps,HostAttackObstacles root,HostAttackObstacles.MeleeFamilyState state,List<Projectile> ambient,HostProjectileTargets targets)
        {
            var body=HostMeleeAttack.PlayerCopy(player);body.direction=state.Facing;body.meleeNPCHitCooldown=(int[])player.meleeNPCHitCooldown.Clone();var motion=frame.Motion;motion.UmbrellaHold=false;
            var slots=root.CaptureBirthSlots().Copy();var members=new List<Member>();
            foreach(var source in ambient){var q=HostNoteAttack.Copy(source);members.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Bee=new HostBeeAttack.State(q)});}
            bool once=item.type==121?(newHand || (bool)volcano.GetValue(player)):item.type==155 && (newHand || (bool)muramasa.GetValue(player));
            var hit=new bool[Main.maxNPCs];int attack=newHand?0:player.attackCD,initialDamage=player.GetWeaponDamage(item);float kb=player.GetWeaponKnockback(item,item.knockBack);bool parry=player.parryDamageBuff;
            Vector2 aim=player.Center+new Vector2(state.Facing*100,0);
            int limit=Math.Min(120,Math.Min(steps,path.Count-clock.FirstTick));
            for(int action=0;action<limit;action++)
            {
                CombatPredictionWork.Check();int advance=action+(clock.NextWorld?1:0),handTick=clock.Age+action,tick=clock.FirstTick+action,outer=action+1;
                if(advance>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);body.direction=state.Facing;
                if(advance>0)state.AdvanceTimerCrit(action==0?state.CapturedUsing:body.UsingOrReusingItem,body.velocity);
                if(action==0)state.CaptureNativeStartTimer();
                body.reuseDelay=Math.Max(0,player.reuseDelay-advance);body.pendingItemReuse=false;
                // StartActualUse arms a full animation, then ItemCheck decrements
                // it BEFORE the first hand hit. That decrement is independent
                // of whether this capture preceded the current Player prefix.
                body.itemAnimation=player.itemAnimation-action-(newHand || clock.NextWorld?1:0);
                if(body.itemAnimation>0 && handTick>=0 && handTick<path.Count && attack<=advance)
                {
                    if(!(naturalPose && action==0 && !clock.NextWorld)){body.itemLocation=HostSwingAttack.Pose(body,item,texture,mount);body.itemRotation=HostSwingAttack.Rotation(body,item);}
                    bool blocked;Rectangle hand;body.ItemCheck_GetMeleeHitbox(item,texture,out blocked,out hand);
                    if(!blocked)foreach(var row in state.Rows)
                    {
                        int slot=row.Identity.Slot;bool isSelected=slot==path.Identity.Slot;
                        var bounds=isSelected?path[handTick].ReceiveBounds:row.Bounds;bool noTile=isSelected?path[handTick].NoTileCollide:row.NoTile;
                        if(state.Life[slot]<=0 || hit[slot] || !root.MeleeFamilyHandAllows(slot,clock.LaunchDelay+action,row.OwnerImmune) || !newHand && row.MeleeCooldown>clock.LaunchDelay+advance+(naturalPose?1:0))continue;
                        if(row.DontTake || !row.Qualified)
                        {if(NPCID.Sets.ZappingJellyfish[row.Npc.type] && hand.Intersects(new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height)) && Clear(body,bounds,noTile,terrain))return null;continue;}
                        if(row.Friendly && !(row.Npc.type==22 && body.killGuide || row.Npc.type==54 && body.killClothier) || isSelected && !path[handTick].CanReceive)continue;
                        if(!HandOverlap(body,item,hand,bounds,once) || !Clear(body,bounds,noTile,terrain))continue;
                        // Return the primary before an unrelated later lottery
                        // or unsupported child can erase its real receive.
                        if(isSelected)return AttackIntercept.MeleeShapeContact(path,handTick,true,aim.X,aim.Y,bounds.CenterX,bounds.CenterY);
                        int damage=body.ApplyRapidAttackBonus(initialDamage,item.type,!row.Immortal);ItemID.BannerEffect banner;if(body.GetBannerBuffEffect(row.Npc,out banner))damage=(int)(damage*banner.DamageDealt.Sample(Main.Difficulty));if(parry && item.melee){damage*=5;parry=false;}
                        int randomized=state.HandRandomized(damage)+row.Npc.checkArmorPenetration(body.GetArmorPenetration(item.melee),0);
                        // DamageVar's center and a noncritical strike are legal
                        // representatives. Retain the native armor calculation;
                        // future other-NPC motion/status/knockback remains the
                        // explicit stationary-receiver condition of this root.
                        int dealt=(int)Math.Max(1,Main.CalculateDamageNPCsTake(randomized,row.Defense)*(state.Critical(body.GetWeaponCrit(item)>=100)?2:1));if(row.Npc.RedHatSkeletronAdjustmentsEnabled())dealt=Math.Max(1,(int)(dealt*.7f));if(row.Taken>1)dealt=(int)(dealt*row.Taken);
                        state.Damage(slot,dealt);
                        bool onHit=!row.Immortal || row.Npc.type==488 && Terraria.Testing.DebugOptions.LetProjectilesAimAtTargetDummies;
                        var children=Births(body,item,hand,bounds,row.Velocity,initialDamage,damage,randomized,kb,row.Rewards,onHit,ref once);
                        foreach(var child in children){int at;if(!slots.TryAllocate(out at))return null;child.whoAmI=at;members.Add(new Member{Shot=child,Receive=root.ForkForSpawn(child),Bee=new HostBeeAttack.State(child),Source=true});}
                        hit[slot]=true;attack=advance+Math.Max(1,(int)(player.itemAnimationMax*.33));break;
                    }
                }
                members.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
                foreach(var m in members)
                {
                    var p=m.Shot;if(!p.active)continue;if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return null;
                    int updates=(HostBeeAttack.Handles(p.type)?m.Bee.Extra:p.extraUpdates)+1;
                    for(int sub=0;sub<updates && p.active;sub++)
                    {
                        CombatPredictionWork.Check();bool bee=HostBeeAttack.Handles(p.type);
                        if(bee){if(!HostBeeAttack.StepForMeleeFamily(p,targets,path,tick,terrain,ref m.Bee,slot=>state.Life[slot]>0))return null;p.position=m.Bee.Position;p.velocity=m.Bee.Velocity;p.penetrate=m.Bee.Penetration;p.active=m.Bee.Alive;}
                        else if(!HostMeleeHitChildAttack.Step(p,terrain))return null;
                        if(!p.active)break;int prior=m.Receive.SelectedHits;
                        bool pass=true;
                        bool zero=bee && m.Bee.Penetration==0;
                        // Bee's tile response pays penetration BEFORE Damage.
                        // A zero at that boundary still admits one unlimited
                        // Damage stage, followed by the native tail Kill.
                        if(p.friendly && p.damage>0)
                        {
                            Func<int,int,bool> onHit=(slot,remaining)=>{if(p.type==524)p.ai[0]+=50;state.Damage(slot,state.ProjectileDamage(slot,p));return true;};
                            pass=bee?m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,zero,m.Bee.Penetration,true,onHit):m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>HostMeleeHitChildAttack.Collides(p,b),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true,onHit);p.penetrate=zero?0:m.Receive.Remaining;
                            if(m.Source && m.Receive.SelectedHits>prior)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,item.type==121?AttackConfidence.Conditional:AttackConfidence.Representative);
                            // Muramasa retains a harmless body until its AI
                            // retires after spending penetration. It still owns
                            // that pool slot when another hand child is born.
                            if(!pass && p.penetrate==0 && p.stopsDealingDamageAfterPenetrateHits){p.penetrate=-1;p.damage=0;pass=true;}
                            if(!pass && p.penetrate!=0)return null;
                        }
                        if(bee){m.Bee.Penetration=p.penetrate;HostBeeAttack.Tail(ref m.Bee);p.active=m.Bee.Alive;p.timeLeft=m.Bee.Left;}
                        else if(!pass || --p.timeLeft<=0)p.active=false;
                    }
                    if(!p.active)slots.Release(p.whoAmI);
                }
                if(body.itemAnimation<=1 && !members.Exists(m=>m.Shot.active))break;
            }
            return null;
        }
    }
}
