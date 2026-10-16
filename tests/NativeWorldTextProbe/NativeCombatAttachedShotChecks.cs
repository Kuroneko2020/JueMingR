using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual locked NewProjectile/Update/Damage/positive Strike are the
    // oracle. A representative hand preview is not a claim that the game's
    // unseen card signs equal the representative; Known uses each real ai2.
    internal static class NativeCombatAttachedShotChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model,stateType;private static NPC target;private static Projectile receiving;
        private static bool hand;private static int born,positive,explosions,solves;private static object ammo;private static Vector2 raw;
        private static void SolveWork(){solves++;}
        private static readonly HashSet<int> modes=new HashSet<int>();
        // Native pools can reuse the object itself. Keep each birth key with its
        // Known clock so a later generation never matches an earlier sample.
        private static readonly List<Tuple<Projectile,AttackContact,long,int>> known=new List<Tuple<Projectile,AttackContact,long,int>>();
        private static readonly List<Projectile> pending=new List<Projectile>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Player Reset(object context,int item)
        {
            hand=false;receiving=null;target=null;born=positive=explosions=0;modes.Clear();known.Clear();pending.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);p.inventory[0].stack=999;p.statMana=p.statManaMax2=10000;Set(p,"killingCardFireType",0);Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=50,int height=60)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);
            var s=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,Active=true,NoTileCollide=true};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.damage<=0 || __result<=0 || receiving.owner!=Main.myPlayer)return;
            if(receiving.type==598 || receiving.type==636 || receiving.type==1024 || receiving.type==953)
            {positive++;Console.WriteLine("ATTACHED realStrike type="+receiving.type+" slot="+receiving.whoAmI+" target="+__instance.whoAmI+" result="+__result+" phase="+receiving.ai[0]+" tick="+Main.GameUpdateCount);
                var k=known.FirstOrDefault(v=>ReferenceEquals(v.Item1,receiving) && v.Item4==(int)receiving.key);if(k!=null && k.Item2!=null && receiving.ai[0]!=2)Require(k.Item2.Tick==(long)Main.GameUpdateCount-k.Item3,"actual known card/javelin positive first Strike consumes the observed ai2 path");}
        }
        private static void Born(IEntitySource __0,int __result)
        {
            if(__result<0 || __result>=Main.projectile.Length)return;var q=Main.projectile[__result];if(q.type==953){explosions++;Require((__0 as EntitySource_Parent)?.Entity is Projectile,"native Daybreak death creates953 from its real parent");return;}
            if(!hand || q.type!=598 && q.type!=636 && q.type!=1024)return;
            born++;var p=Main.LocalPlayer;modes.Add(q.type==1024?(int)Get(p,"killingCardFireType"):0);Require(!((__0 as EntitySource_Parent)?.Entity is Projectile),"AI113 hand birth is an actual item use");
            var origin=p.RotatedRelativePoint(p.MountedCenter);if(q.type!=1024)origin.Y=p.position.Y+p.height/3;
            Require(q.damage==(int)Get(ammo,"Damage") && q.damage>0 && Vector2.DistanceSquared(q.Center,origin)<.0001f,"native3378/3543 height-third and5461 mounted origins retain positive actual damage");
            if(q.type==1024)Require(q.ai[2]==-.4f || q.ai[2]==.4f,"real card curve sign is a legal native draw");
            pending.Add(q);Console.WriteLine("ATTACHED hand type="+q.type+" mode="+Get(p,"killingCardFireType")+" key="+q.key+" center="+q.Center+" V="+q.velocity+" ai2="+q.ai[2]);
        }
        private static void BeforeUpdate(Projectile __instance)
        {
            // Shoot may birth six cards. Capture only after the complete real
            // Player action, before this member's original Update; a callback
            // on the first NewProjectile must not omit later born siblings.
            if(!hand || !pending.Remove(__instance))return;var q=__instance;var p=Main.LocalPlayer;var path=Timeline(target);
            var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"Projectiles"),0},null);
            var obstacle=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,false},null);
            var contact=(AttackContact)Static(model,"Known",p,q,path,clock,Terrain(),obstacle);known.Add(Tuple.Create(q,contact,(long)Main.GameUpdateCount-1,(int)q.key));Console.WriteLine("ATTACHED known key="+q.key+" observedAi2="+q.ai[2]+" contact="+contact?.Tick);
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttachedShotAttack",true);stateType=model.GetNestedType("State",BindingFlags.NonPublic);bool wind=Main.windPhysics;
            var audit=new Harmony("JueMingR.Tests.AttachedShots");foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(SolveWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(AfterDamage)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(BeforeUpdate)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_ATTACHED_SHOT_ONLY");Console.WriteLine("ATTACHED actual leaf="+(leaf??"motor,lifecycle,natural"));
                Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="lifecycle" || leaf=="natural","attached shot selector is explicit");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);
                if(string.IsNullOrEmpty(leaf) || leaf=="lifecycle")Lifecycle(context);
                if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{hand=false;receiving=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static object NewState(Projectile q){return Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);}
        private static object Advance(Projectile q,object terrain,object state,MotionRect? attached,Player p)
        {
            var owner=new MotionRect(p.position.X,p.position.Y,p.width,p.height);
            for(int sub=0;sub<=q.extraUpdates && (bool)Get(state,"Alive");sub++)
            {var args=new object[]{q,terrain,state,attached,(MotionRect?)owner,p.active,p.dead};Require((bool)Static(model,"Step",args),"AI113 observed terrain/attachment/owner are readable");state=args[2];if((bool)Get(state,"Alive")){var tail=new[]{state};Static(model,"Finish",tail);state=tail[0];}}
            return state;
        }
        private static void Compare(Projectile q,object s,string leaf,int outer)
        {
            Require(q.active==(bool)Get(s,"Alive") && q.timeLeft==(int)Get(s,"Left") && q.ai[0]==(float)Get(s,"Phase") && q.ai[1]==(float)Get(s,"Clock") && q.localAI[0]==(float)Get(s,"AttachedClock") && q.alpha==(int)Get(s,"Alpha") && q.tileCollide==(bool)Get(s,"Tile") && q.ignoreWater==(bool)Get(s,"IgnoreWater") && q.wet==(bool)Get(s,"Wet") && q.honeyWet==(bool)Get(s,"Honey") && q.lavaWet==(bool)Get(s,"Lava") && q.shimmerWet==(bool)Get(s,"Shimmer") && Vector2.DistanceSquared(q.position,(Vector2)Get(s,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(s,"Velocity"))<.0001f,"native AI113 full step "+leaf+"/"+outer+" type="+q.type+" actual="+q.position+"/"+q.velocity+"/"+q.ai[0]+"/"+q.ai[1]+"/"+q.localAI[0]+"/"+q.timeLeft+" model="+Get(s,"Position")+"/"+Get(s,"Velocity")+"/"+Get(s,"Phase")+"/"+Get(s,"Clock")+"/"+Get(s,"AttachedClock")+"/"+Get(s,"Left"));
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;
            // Native receiver shrink is independent of both the 4/10px tile
            // profile and the card's inflated Damage body. Keep the touching
            // edges that would otherwise turn a miss into an attachment.
            foreach(int type in new[]{598,636,1024})foreach(int width in new[]{8,40})foreach(int x in new[]{15,16,20,21})
            {
                var q=new Projectile();q.SetDefaults(type);var shot=new Rectangle(0,0,16,16);var targetBox=new Rectangle(x,0,width,40);
                Require(q.Colliding(shot,targetBox)==(bool)Static(model,"Collides",type,shot,new MotionRect(targetBox.X,targetBox.Y,targetBox.Width,targetBox.Height)),"AI113 native receiver edge and shrink type="+type+" width="+width+" x="+x);
            }
            foreach(int type in new[]{598,636,1024})foreach(int variant in new[]{0,1,2})
            {
                var p=Reset(context,type==598?3378:type==636?3543:5461);
                if(variant==1)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(type==598?0:type==636?2:3);}
                if(variant==2)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,350),new Vector2(7,.25f),type,0,0,p.whoAmI,0,0,type==1024?-.4f:0);var q=Main.projectile[slot];object s=NewState(q),terrain=Terrain();
                Vector2 offset;int w,h;q.GetCollisionParams(out offset,out w,out h);Require(w==(type==1024?4:10) && h==w && q.Damage_GetHitbox().Width==(type==1024?28:16),"AI113 separates native tile and damage bodies");
                for(int frame=1;frame<=96 && q.active;frame++){s=Advance(q,terrain,s,null,p);q.Update(slot);Compare(q,s,"flight/liquid/tile"+variant,frame);actions++;}
                if(variant==2 && type==1024)Require(q.ai[0]==1 && q.ai[1]==-1 && q.velocity==Vector2.Zero,"actual card tile collision settles on native expanded-solid support");cases++;
            }
            Console.WriteLine("PASS attached motor cases="+cases+" actions="+actions);
        }
        private static void Lifecycle(object context)
        {
            int cases=0;
            foreach(int type in new[]{598,636,1024})
            {
                var p=Reset(context,type==598?3378:type==636?3543:5461);target=Target(4,new Vector2(950,350));
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center-new Vector2(8,0),Vector2.Zero,type,50,0,p.whoAmI);var q=Main.projectile[slot];q.Update(slot);
                Require(positive>0 && q.ai[0]==1 && q.ai[1]==target.whoAmI,"actual positive Strike creates NPC attachment");
                object s=NewState(q),terrain=Terrain();var rect=new MotionRect(target.position.X,target.position.Y,target.width,target.height);int before=positive;
                for(int frame=1;frame<=34;frame++){s=Advance(q,terrain,s,rect,p);q.Update(slot);Compare(q,s,"receiving-attachment",frame);}
                Require(before==positive,"attached stage cannot manufacture ordinary Damage");
                if(type==1024)
                {
                    int positiveBeforeReturn=positive,frames=0;
                    while(q.active && q.ai[0]==1 && frames++<300){s=Advance(q,terrain,s,rect,p);q.Update(slot);Compare(q,s,"natural-attached-expiry",frames);}
                    Require(q.ai[0]==2 && positive>positiveBeforeReturn,"real attachment clock expiry clears local immunity and deals immediate positive return Damage");
                    frames=0;while(q.active && frames++<80){s=Advance(q,terrain,s,null,p);q.Update(slot);Compare(q,s,"natural-return-owner-contact",frames);}
                    Require(!q.active,"actual returning card retires on owner contact");
                    // A separate actual birth receives again before testing
                    // a newly observed invulnerable NPC and dead player.
                    slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center-new Vector2(8,0),Vector2.Zero,type,50,0,p.whoAmI);q=Main.projectile[slot];q.Update(slot);Require(q.ai[0]==1,"fresh card really reattaches after prior return retirement");
                }
                // This is a newly observed native receiving boundary: body
                // becomes invulnerable, then the original next AI retires or
                // returns. No solver-private phase input is injected.
                target.dontTakeDamage=true;s=NewState(q);terrain=Terrain();s=Advance(q,terrain,s,null,p);q.Update(slot);Compare(q,s,"invulnerable-owner",1);
                Require(type==1024?q.ai[0]==2 && q.active:!q.active,"native attachment loss has role-specific retirement");
                if(type==1024){p.dead=true;s=NewState(q);s=Advance(q,terrain,s,null,p);q.Update(slot);Compare(q,s,"return-dead-owner",1);Require(!q.active,"original return action retires on dead owner");p.dead=false;}
                if(type==636){Require(explosions==1,"real Daybreak Kill emits953");var child=Main.projectile.Single(v=>v.active && v.type==953);Require(child.ai[1]>=.85f && child.ai[1]<2 && child.damage==50,"actual derived scale/damage is legal native birth");}
                cases++;
            }
            foreach(int type in new[]{598,636,1024})
            {
                var p=Reset(context,type==598?3378:type==636?3543:5461);target=Target(4,new Vector2(950,350));
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,type,0,0,p.whoAmI);var q=Main.projectile[slot];var path=Timeline(target);
                Require(Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path))==null,"zero-damage actual body cannot manufacture an attached-shot contact");q.Update(slot);
                Require(positive==0 && q.ai[0]==0,"native zero-damage update cannot create attachment");cases++;
            }
            {
                var p=Reset(context,3378);target=Target(4,new Vector2(950,350));
                // AI113's javelin cap branch does not write owner immunity.
                // Establish this negative premise with an actual ordinary
                // multi-penetration shuriken receive, rather than inventing
                // a counter or treating an attached hit as that producer.
                int life=target.life;int first=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,3,20,0,p.whoAmI);Main.projectile[first].Update(first);Require(target.life<life && target.immune[p.whoAmI]>0,"real shuriken receive establishes native owner immunity before the javelin action");
                int second=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,598,20,0,p.whoAmI);var q=Main.projectile[second];var path=Timeline(target);
                var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));int before=positive;q.Update(second);
                Require(positive==before && q.ai[0]==0 && (contact==null || contact.Tick>1),"an actual earlier receive blocks same-action second attachment and its claimed contact");cases++;
            }
            foreach(int type in new[]{598,636})
            {
                var p=Reset(context,type==598?3378:3543);target=Target(4,new Vector2(950,350));
                int a=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,type,20,0,p.whoAmI);
                int b=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,type,20,0,p.whoAmI);
                var first=Main.projectile[a];var second=Main.projectile[b];var path=Timeline(target);var history=Obstacles(p,first,path);
                Call(history,"BeginCandidate");var sibling=Call(history,"ForkForSpawn",second,false);
                // Both native slot actions consume one shared owner's history.
                // Keep the gates captured before either real receive; k=2
                // prevents a child pass from resetting the preceding writes.
                foreach(var row in new[]{Tuple.Create(first,history),Tuple.Create(second,sibling)})
                    Require((bool)Call(row.Item2,"PassCustomOuter",2,1,1,row.Item1.Center.X,row.Item1.Center.Y,row.Item1.width,row.Item1.height,null,false,0,true,null,null),"same-action javelin receiver pass remains readable");
                Require((int)Get(history,"SelectedHits")==1 && (int)Get(sibling,"SelectedHits")==1,"javelin cap handling preserves owner history without introducing a ten-action cooldown");
                first.Update(a);second.Update(b);
                Require(positive==2 && first.ai[0]==1 && second.ai[0]==1 && target.immune[p.whoAmI]==0,"two original same-action javelin receives both attach and leave owner immunity unchanged");
                Console.WriteLine("PASS attached native cap immunity type="+type+" slots="+a+","+b+" owner="+target.immune[p.whoAmI]);cases++;
            }
            // Native KillOldest uses native NPC slot and timeLeft order,
            // independent of current projectile pool allocation order.
            foreach(int type in new[]{598,636})
            {
                var p=Reset(context,type==598?3378:3543);target=Target(4,new Vector2(950,350));int cap=type==598?6:8;Projectile oldest=null;
                for(int i=0;i<=cap;i++)
                {
                    if(i>0)for(int f=0;f<11;f++){NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(4);foreach(var shot in Main.projectile.Where(v=>v.active).ToArray())shot.Update(shot.whoAmI);}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center-new Vector2(8,0),Vector2.Zero,type,20,0,p.whoAmI);var q=Main.projectile[slot];if(i==0)oldest=q;q.Update(slot);Require(q.ai[0]==1,"each cap participant is a real qualified positive receive");
                }
                Require(!oldest.active && Main.projectile.Count(v=>v.active && v.type==type && v.ai[0]==1 && v.ai[1]==4)==cap,"actual seventh/ninth qualified hit kills the minimum native lifetime");if(type==636)Require(explosions>0,"cap death keeps its real953 derivative");cases++;
            }
            LeaseOwnership(context);DerivedBurst(context);cases+=2;
            Console.WriteLine("PASS attached receiving/lifecycle="+cases);
        }
        private static void LeaseOwnership(object context)
        {
            var p=Reset(context,3378);target=Target(4,new Vector2(950,350));
            int localSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,350),new Vector2(7,0),598,20,0,p.whoAmI);
            int foreignSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(350,350),Vector2.Zero,598,0,0,1);
            Require(Main.projectile[foreignSlot].active && Main.projectile[foreignSlot].owner!=p.whoAmI,"same-type foreign body really exists before local family capture");
            var local=Main.projectile[localSlot];var path=Timeline(target);var args=new object[]{p,new[]{local},path,Clock(path),Terrain(),null};Static(model,"KnownFamily",args);var lease=args[5];
            Require(lease!=null && (bool)Get(lease,"Current"),"existing foreign same-type body does not immediately retire the precisely owned local family lease");
            int added=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(400,350),Vector2.Zero,1024,0,0,p.whoAmI,0,0,.4f);
            Require(Main.projectile[added].active && !(bool)Get(lease,"Current"),"a new local different attached-family type retires the old census before consumption");
            Console.WriteLine("PASS attached owned census local="+localSlot+" foreign="+foreignSlot+" addedLocalCard="+added);
        }
        private static void DerivedBurst(object context)
        {
            var p=Reset(context,3543);for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
            target=Target(4,new Vector2(975,350),8,20);
            int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,350),new Vector2(12,0),636,50,0,p.whoAmI);var parent=Main.projectile[slot];var path=Timeline(target);
            var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"Projectiles"),0},null);
            var obstacle=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,parent,path,0,false,false},null);
            var contact=(AttackContact)Static(model,"Known",p,parent,path,clock,Terrain(),obstacle);
            Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"observed Daybreak predicts the legal minimum-scale death derivative beside a tile wall");
            var familyArgs=new object[]{p,new[]{parent},path,clock,Terrain(),null};var familyContact=(AttackContact)Static(model,"KnownFamily",familyArgs);var familyLease=familyArgs[5];
            Require(familyContact!=null && familyContact.Tick==contact.Tick && (bool)Get(familyLease,"MembersCurrent"),"one observed family flight solve retains the same native derivative contact and scalar presentation lease");
            long tick=0;while(positive==0 && tick++<18)
            {
                if(tick>1)NativeQuickItemChecks.BeginWorldStep();
                // Original array order: a child in a greater slot executes
                // this outer; it is not postponed by a ToArray snapshot.
                for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active)Main.projectile[i].Update(i);
            }
            Require(!parent.active && parent.ai[0]==0 && explosions==1 && positive>0,"real tile retirement produces an actual953 positive Strike without a prior parent receive");
            Require(!(bool)Get(familyLease,"MembersCurrent"),"actual parent retirement withdraws its old captured family presentation");
            Require(contact.Tick==tick,"actual953 positive tick consumes the predicted native birth-slot order");
            var child=Main.projectile.First(q=>q.active && q.type==953);Require(child.width>=44 && child.ai[1]>=.86f && child.damage==50,"actual random-scale burst includes the certified44px representative body");
            Console.WriteLine("PASS attached actual tile Kill ->953 positive receive tick="+tick+" representative="+contact.Tick+" scale="+child.ai[1]+" width="+child.width);
        }
        private static void Natural(object context)
        {
            foreach(int item in new[]{3378,3543,5461})
            {
                var p=Reset(context,item);p.position=new Vector2(650,646);p.ResetFallDamage();p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,43,40);raw=target.Center-new Vector2(390,0);
                var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                solves=0;for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && p.HeldItem.noMelee && solves==0,"withdrawn AI113 idle preview neither solves nor births an attack or invented melee");
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);hand=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));
                bool ongoing=false,enhanced=false;int recallDamage=0;
                for(int frame=0;frame<(item==5461?100:40);frame++)
                {
                    int previousMode=(int)Get(p,"killingCardFireType"),previousPositive=positive,previousBorn=born;
                    // Keep actual input cadence. Consumable3378 receives a
                    // second lightpress; auto-reuse weapons hold naturally.
                    bool use=item==3378?frame==0 || frame==27:true;
                    AttackContact prepared=null;
                    if(use)
                    {
                        NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");
                        NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);prepared=(AttackContact)GetOptional(attack,"ExpectedImpact");
                        if(frame==0)Require(prepared!=null,"actual AI113 first pressed hand prepares under1000ms mechanism allowance");
                    }
                    NativeCombatSelectionPreviewChecks.Step(context,raw,use);
                    if(born>previousBorn && (bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced")enhanced=true;
                    if(Main.projectile.Any(q=>q.active) && prepared!=null)ongoing=true;
                    if(item==5461 && previousMode==3 && (int)Get(p,"killingCardFireType")==0)recallDamage+=positive-previousPositive;
                }
                Require(born>0 && positive>0 && known.Any(v=>v.Item2!=null) && ongoing && enhanced,"actual Enhanced hand birth/observed Known replay/positive Strike and sustained pressed preparation are exercised for item"+item);
                if(item==5461)Require(modes.Contains(0) && modes.Contains(1) && modes.Contains(2) && recallDamage>0,"real single/reverse/fan/recall cycle includes immediate positive recall Damage without invented birth");
                Console.WriteLine("PASS attached natural item="+item+" births="+born+" positive="+positive+" known="+known.Count(v=>v.Item2!=null)+" ongoing="+ongoing+" recall="+recallDamage+" modes="+string.Join(",",modes));hand=false;
            }
        }
    }
}
