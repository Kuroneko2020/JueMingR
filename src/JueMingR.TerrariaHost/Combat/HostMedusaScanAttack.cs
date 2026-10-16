using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Testing;

namespace JueMingR.TerrariaHost.Combat
{
    // Medusa has no useful cursor candidate. Observe the real channel body:
    // its old anchor gates a nearest-three scan, and only synchronous536
    // Damage at each victim can receive. Later decorative rays have damage0.
    internal static class HostMedusaScanAttack
    {
        internal static bool Handles(int type){return type==535;}
        internal sealed class State
        {
            internal sealed class Resource
            {
                internal readonly Item Item;internal readonly int Type,Stack,Heal,Mana;internal readonly bool Potion;
                internal Resource(Item item){Item=item;Type=item.type;Stack=item.stack;Heal=item.healMana;Mana=item.mana;Potion=item.potion;}
                internal bool Current(Item item){return ReferenceEquals(item,Item) && item.type==Type && item.stack==Stack && item.healMana==Heal && item.mana==Mana && item.potion==Potion;}
            }
            private readonly Player player;private readonly Item weapon;private readonly bool channel,flower,cursed,cc,dead,slow,pending,voidBag,manaV2,potionMode;private readonly int mana,maximum,manaDelay,potionDelay,weaponMana,mouseX;private readonly float manaCost,screenX;private readonly Rectangle frame;
            private readonly HostMeleeAttack.SpearFlightState pose;
            internal readonly Resource[] Inventory,Void;internal readonly float PhysicalMouseX;
            internal State(Player player,Projectile[] sources)
            {
                this.player=player;weapon=player.HeldItem;channel=player.channel;flower=player.manaFlower;cursed=player.cursed;cc=player.CCed;dead=player.dead;slow=player.slowMagicUse;mana=player.statMana;maximum=player.statManaMax2;manaDelay=player.manaPotionDelay;potionDelay=player.potionDelay;weaponMana=weapon.mana;manaCost=player.manaCost;mouseX=Main.mouseX;screenX=Main.screenPosition.X;PhysicalMouseX=screenX+mouseX;frame=player.bodyFrame;pending=Main.LocalPlayerHasPendingInventoryActions();voidBag=player.useVoidBag();manaV2=DebugOptions.ManaV2;potionMode=DebugOptions.ManaPotionDelay;pose=new HostMeleeAttack.SpearFlightState(player,sources);
                Inventory=new Resource[58];for(int i=0;i<Inventory.Length;i++)Inventory[i]=new Resource(player.inventory[i]);if(voidBag){Void=new Resource[player.bank4.maxItems];for(int i=0;i<Void.Length;i++)Void[i]=new Resource(player.bank4.item[i]);}
            }
            internal bool MembersCurrent
            {get{return ReferenceEquals(player.HeldItem,weapon) && player.channel==channel && player.manaFlower==flower && player.cursed==cursed && player.CCed==cc && player.dead==dead && player.slowMagicUse==slow && player.statMana==mana && player.statManaMax2==maximum && player.manaPotionDelay==manaDelay && player.potionDelay==potionDelay && weapon.mana==weaponMana && player.manaCost==manaCost && Main.mouseX==mouseX && Main.screenPosition.X==screenX && player.bodyFrame==frame && DebugOptions.ManaV2==manaV2 && DebugOptions.ManaPotionDelay==potionMode && pose.Current;}}
            internal bool Current
            {get{if(!MembersCurrent || Main.LocalPlayerHasPendingInventoryActions()!=pending || player.useVoidBag()!=voidBag)return false;for(int i=0;i<Inventory.Length;i++)if(!Inventory[i].Current(player.inventory[i]))return false;if(Void!=null)for(int i=0;i<Void.Length;i++)if(!Void[i].Current(player.bank4.item[i]))return false;return true;}}
            internal Budget NewBudget(){return new Budget(this);}
            internal sealed class Budget
            {
                private readonly State state;private readonly int[] counts;private readonly Resource[] resources;private int quickDelay,potionWait;
                internal Budget(State state){this.state=state;var all=new List<Resource>(state.Inventory);if(state.Void!=null)all.AddRange(state.Void);resources=all.ToArray();counts=Array.ConvertAll(resources,r=>r.Stack);quickDelay=state.manaDelay;potionWait=state.potionDelay;}
                internal void NextWorld(){if(quickDelay>0)quickDelay--;if(potionWait>0)potionWait--;}
                internal bool Pay(Player privateOwner,int elapsed)
                {
                    int cost=(int)(privateOwner.HeldItem.mana*privateOwner.manaCost);
                    if(privateOwner.statMana<cost && privateOwner.manaFlower && !state.pending && !privateOwner.cursed && !privateOwner.CCed && !privateOwner.dead && quickDelay<=0 && (DebugOptions.ManaV2 || privateOwner.statMana!=privateOwner.statManaMax2))
                    {
                        for(int i=0;i<resources.Length;i++)
                        {
                            var r=resources[i];if(counts[i]<=0 || r.Type<=0 || r.Heal<=0 || r.Mana!=0 || r.Potion && potionWait>0)continue;
                            privateOwner.statMana=Math.Min(privateOwner.statManaMax2,privateOwner.statMana+r.Heal);counts[i]--;if(DebugOptions.ManaPotionDelay)quickDelay=480;
                            // Every native life/mana hybrid's potion delay,
                            // including StrangeBrew's minimum with pStone,
                            // exceeds this finite120-action observation.
                            if(r.Potion)potionWait=int.MaxValue;break;
                        }
                    }
                    // With quick mana disabled this original path only changes
                    // fields on our private owner (including ManaV2's2.5 slow
                    // flag). No live inventory, mount, sound or operation runs.
                    return privateOwner.CheckMana(privateOwner.HeldItem.mana,true,true);
                }
            }
        }
        private sealed class Node{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;internal int Key,Parent;}
        internal static void Anchor(Projectile p,Player body,float physicalMouseX)
        {
            var zero=new Vector2(body.direction*6,body.gravDir*-14);p.direction=physicalMouseX==body.Center.X?body.direction:Math.Sign(physicalMouseX-body.Center.X);p.velocity=new Vector2(p.direction,0);
            var hand=Main.OffsetsPlayerOnhand[body.bodyFrame.Y/56]*2;if(body.direction!=1)hand.X=body.bodyFrame.Width-hand.X;if(body.gravDir!=1)hand.Y=body.bodyFrame.Height-hand.Y;hand-=new Vector2(body.bodyFrame.Width-body.width,body.bodyFrame.Height-42)/2;
            p.Center=(body.RotatedRelativePoint(body.MountedCenter-new Vector2(20,42)/2+hand+zero)-p.velocity).Floor()+p.velocity;body.direction=p.direction;p.timeLeft=1;
        }
        private static bool Eligible(Projectile p,HostProjectileTargets targets,int index,NpcTrajectory path,int tick,Vector2 sight,PredictionTerrain terrain,out bool legal)
        {
            var b=targets.Bounds(index,path,tick);legal=false;if(targets.Slot(index)==path.Identity.Slot && !path[tick].CanReceive || Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY))>=320)return true;
            PredictionStop stop;return terrain.CanHitLine(b,new MotionRect(sight.X,sight.Y,0,0),out legal,out stop);
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && (p.type==535 || p.type==536))scene.Add(p);root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{535,536});var state=root.CaptureMedusaOwner(player,sources);root.CaptureHomingInputs(player,false,true);var budget=state.NewBudget();var targets=root.CaptureChaseTargets(sources[0]);root.BeginCandidate();var slots=root.CaptureBirthSlots().Copy();var rows=new List<Node>();foreach(var p in scene){var q=HostNebulaBlazeAttack.Copy(p);rows.Add(new Node{Shot=q,Receive=q.type==535?root.ForkForSpawn(q):null,Source=Array.IndexOf(sources,p)>=0,Key=(int)p.key,Parent=q.type==536?(int)q.ai[0]:0});}
            var body=HostMeleeAttack.PlayerCopy(player);var prefix=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(player,path),terrain,121);
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                int tick=clock.FirstTick+action,outer=action+1;PredictionPlayerMotion motion;if(!prefix.TryRead(action+(clock.NextWorld?1:0),out motion))return null;HostAttackPlayerFrame.ApplyPose(body,motion);if(clock.MovePlayer(action))budget.NextWorld();
                foreach(var row in rows)if(row.Shot.active && row.Receive!=null && !row.Receive.BeforeNpc(tick,outer,row.Shot.Center.X,row.Shot.Center.Y,row.Shot.width,row.Shot.height))return null;
                int after=-1;for(int visit=0;visit<1000;visit++)
                {
                    CombatPredictionWork.Check();Node row=null;foreach(var candidate in rows)if(candidate.Shot.active && candidate.Shot.whoAmI>after && (row==null || candidate.Shot.whoAmI<row.Shot.whoAmI))row=candidate;if(row==null)break;var p=row.Shot;after=p.whoAmI;
                    if(p.type==536){bool ownerAlive=rows.Exists(n=>n.Shot.active && n.Shot.type==535 && n.Key==row.Parent);if(++p.localAI[0]>=20 || !ownerAlive){p.active=false;slots.Release(p.whoAmI);}continue;}
                    if(!body.channel || !body.active){p.active=false;slots.Release(p.whoAmI);continue;}
                    float interval=(int)(60*body.GetSlowMagicScalar())/4;bool waiting=p.ai[0]>0;if(waiting)p.ai[0]--;
                    if(!waiting || p.ai[0]%interval==0)
                    {
                        bool has=false;var sight=body.Center+new Vector2(body.direction*4,body.gravDir*2);
                        for(int i=0;i<targets.Count;i++){bool legal;if(!Eligible(p,targets,i,path,tick,sight,terrain,out legal))return null;if(legal){has=true;break;}}
                        if(has && budget.Pay(body,action+(clock.NextWorld?1:0)))
                        {
                            var ordered=new List<Tuple<int,float>>();for(int i=0;i<targets.Count;i++){bool legal;if(!Eligible(p,targets,i,path,tick,p.Center,terrain,out legal))return null;if(legal){var b=targets.Bounds(i,path,tick);ordered.Add(Tuple.Create(i,Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY))));}}ordered.Sort((a,b)=>a.Item2.CompareTo(b.Item2));
                            for(int index=0;index<Math.Min(3,ordered.Count);index++)
                            {
                                int slot;if(!slots.TryAllocate(out slot))return null;var b=targets.Bounds(ordered[index].Item1,path,tick);var child=new Projectile();child.SetDefaults(536);child.active=true;child.owner=p.owner;child.whoAmI=slot;child.Center=new Vector2(b.CenterX,b.CenterY);child.damage=p.damage;var receive=root.ForkForSpawn(child);if(!receive.BeforeNpc(tick,outer,child.Center.X,child.Center.Y,10,10))return null;int hits=receive.SelectedHits;bool passed=receive.PassCustomOuter(2,tick,outer,child.Center.X,child.Center.Y,10,10,null,consumeSelected:true);
                                if(row.Source && receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,0,p.Center.X,p.Center.Y,child.Center.X,child.Center.Y,10,10,AttackConfidence.Conditional);
                                if(!passed)return null;child.damage=0;child.Center=p.Center;rows.Add(new Node{Shot=child,Parent=row.Key});
                            }
                            for(int decorative=0;decorative<4;decorative++){int slot;if(!slots.TryAllocate(out slot))return null;var child=new Projectile();child.SetDefaults(536);child.active=true;child.whoAmI=slot;child.owner=p.owner;child.damage=0;rows.Add(new Node{Shot=child,Parent=row.Key});}p.ai[0]=60;
                        }
                    }
                    if(!HostProjectileEnvironment.Dry(p,terrain,p.Center.X,p.Center.Y,p.velocity.X))return null;Anchor(p,body,state.PhysicalMouseX);
                }
            }
            return null;
        }
    }
}
