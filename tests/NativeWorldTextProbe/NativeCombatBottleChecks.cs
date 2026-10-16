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
    internal static class NativeCombatBottleChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile motor,receiving,parent;private static NPC target;
        private static int kills,births,solves,steps,entrySlot,entryDamage;private static long first,birthTick;private static bool natural,positiveDamagePass;private static object ammo;private static AttackContact known;private static Vector2 raw,deathCenter,deathVelocity;
        private static readonly List<Projectile> children=new List<Projectile>(),birthCopies=new List<Projectile>();private static readonly List<int> hitSlots=new List<int>(),rawDamage=new List<int>(),entryDamageAtStrike=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Work(){solves++;}private static void MotorWork(){steps++;}
        private static void BeforeKill(Projectile __instance)
        {if(ReferenceEquals(__instance,motor) || ReferenceEquals(__instance,parent)){kills++;deathCenter=__instance.Center;deathVelocity=__instance.velocity;}}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;positiveDamagePass=__instance.damage>0;}private static void AfterDamage(){receiving=null;positiveDamagePass=false;}
        // AI68/Ale changes the persistent projectile damage BEFORE Strike.
        // Native dmg was read at receiver entry; preserve both observations
        // instead of treating a Strike postfix as that earlier input state.
        private static void BeforeInner(Projectile __instance,int __0)
        {if(ReferenceEquals(__instance,receiving)){entrySlot=__0;entryDamage=__instance.damage;}}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.owner!=Main.myPlayer || !positiveDamagePass || __result<=0 || !(receiving.type==399 || receiving.type==669 || receiving.type>=400 && receiving.type<=402))return;
            Require(entrySlot==__instance.whoAmI,"Strike belongs to the captured native receiver entry");hitSlots.Add(__instance.whoAmI);rawDamage.Add(receiving.damage);entryDamageAtStrike.Add(entryDamage);if(ReferenceEquals(__instance,target) && first<0)first=Main.GameUpdateCount;
        }
        private static Player Reset(object context,int item=2590)
        {
            natural=false;target=null;parent=motor=receiving=null;positiveDamagePass=false;kills=births=0;first=-1;known=null;children.Clear();birthCopies.Clear();hitSlots.Clear();rawDamage.Clear();entryDamageAtStrike.Clear();entrySlot=-1;entryDamage=0;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);if(item==3821){p.inventory[54].SetDefaults(353);p.inventory[54].stack=999;}else p.HeldItem.stack=999;
            Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40,int type=3)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(type);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static Projectile Shot(Player p,int type,Vector2 center,Vector2 v,int damage=50)
        {int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,v,type,damage,0,p.whoAmI);return Main.projectile[slot];}
        // Native Kill passes num484/num485 as float NewProjectile arguments.
        // x86 may retain extended precision on the oracle expression side;
        // force the same IEEE single storage boundary, then compare exactly.
        private static float StoredBirthComponent(float value)
        {return BitConverter.ToSingle(BitConverter.GetBytes(value),0);}
        private static void Born(IEntitySource __0,int __result)
        {
            if(__result<0)return;var q=Main.projectile[__result];var origin=__0 as EntitySource_Parent;
            if(q.type>=400 && q.type<=402 && origin?.Entity is Projectile)
            {
                var source=(Projectile)origin.Entity;Require(ReferenceEquals(source,parent) && source.type==399,"each Molotov flame has its actual still-occupied parent source");children.Add(q);birthCopies.Add((Projectile)Static(model,"Copy",q));
                bool legalX=false,legalY=false;for(int f=20;f<50 && !legalX;f++)for(int j=-20;j<=20;j++)if(q.velocity.X==StoredBirthComponent(-deathVelocity.X*f*.01f+j*.4f)){legalX=true;break;}
                for(int f=30;f<50 && !legalY;f++)for(int j=-20;j<5;j++)if(q.velocity.Y==StoredBirthComponent(-Math.Abs(deathVelocity.Y)*f*.01f+j*.4f)){legalY=true;break;}
                bool legal=legalX && legalY && q.damage==(int)((double)source.damage*.5) && q.knockBack==0 && q.owner==source.owner && source.active && Vector2.DistanceSquared(q.Center,deathCenter+q.velocity)<.0001f;
                if(!legal)Console.WriteLine("BOTTLE birth mismatch type="+q.type+" slot="+q.whoAmI+" V="+q.velocity+" parentV="+deathVelocity+" legalXY="+legalX+","+legalY+" center="+q.Center+" deathCenter="+deathCenter+" centerDelta2="+Vector2.DistanceSquared(q.Center,deathCenter+q.velocity)+" damage="+q.damage+" parentDamage="+source.damage+" knockback="+q.knockBack+" owner="+q.owner+" parentOwner="+source.owner+" parentActive="+source.active);
                Require(legal,"all six actual random births obey legal type/velocity/origin/halfDamage and allocate before parent release");
            }
            if(!natural || q.type!=399 && q.type!=669)return;parent=q;births++;birthTick=Main.GameUpdateCount;var p=Main.LocalPlayer;var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var originPoint=p.RotatedRelativePoint(p.MountedCenter);var v=cursor-originPoint;float length=(float)Math.Sqrt(v.X*v.X+v.Y*v.Y);v*=((float)Get(ammo,"Speed"))/length;
            var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));
            Console.WriteLine("BOTTLE born type="+q.type+" slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" pen="+q.penetrate+" ranged="+q.ranged+" melee="+q.melee+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
            Require(!(origin?.Entity is Projectile) && births==1 && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(q.Center,originPoint)<.0001f && Vector2.DistanceSquared(q.velocity,v)<.0001f && Vector2.Dot(q.velocity,raw-originPoint)<0 && (q.type==399?q.ranged && !q.melee:q.melee && !q.ranged),"actual bottle hand consumes assisted integer cursor and native opener damage class");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBottleAttack",true);var audit=new Harmony("JueMingR.Tests.Bottle");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(Work)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(MotorWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage_PVE_Inner",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(BeforeInner)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBottleChecks),nameof(Strike)));
            var componentHand=new NativeCombatHandFixture(context,()=>natural,()=>target);
            try{string only=Environment.GetEnvironmentVariable("JMR_BOTTLE_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="birth")Births(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);Require(string.IsNullOrEmpty(only) || only=="motor" || only=="birth" || only=="receive" || only=="natural","bottle leaf selector");}
            finally{componentHand.Dispose();natural=false;target=null;receiving=parent=motor=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Compare(Projectile q,Projectile copy,int type,int scene,int outer)
        {
            bool equal=q.active==copy.active && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.ai.SequenceEqual(copy.ai) && q.width==copy.width && q.height==copy.height && q.rotation==copy.rotation && q.direction==copy.direction && q.spriteDirection==copy.spriteDirection && q.knockBack==copy.knockBack && q.tileCollide==copy.tileCollide && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.lavaWet==copy.lavaWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f;
            Require(equal,"bottle full native motor "+type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.width+"/"+q.height+"/"+q.timeLeft+"/"+q.alpha+"/"+q.rotation+"/"+q.direction+"/"+q.spriteDirection+"/"+q.knockBack+"/"+q.tileCollide+"/wet="+q.wet+","+q.honeyWet+","+q.lavaWet+","+q.shimmerWet+" model="+copy.position+"/"+copy.velocity+"/"+copy.width+"/"+copy.height+"/"+copy.timeLeft+"/"+copy.alpha+"/"+copy.rotation+"/"+copy.direction+"/"+copy.spriteDirection+"/"+copy.knockBack+"/"+copy.tileCollide+"/wet="+copy.wet+","+copy.honeyWet+","+copy.lavaWet+","+copy.shimmerWet);
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;foreach(int type in new[]{399,669,400,401,402})foreach(int scene in Enumerable.Range(0,type==399 || type==669?9:5))
            {
                var p=Reset(context);bool opener=type==399 || type==669;
                if(scene>=1 && scene<=3 && opener || scene>=1 && scene<=2 && !opener)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(opener && scene==4 || !opener && scene==3)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(!opener && scene==4)for(int x=40;x<115;x++)NativeToolsChecks.Tile(x,23,1);
                if(scene==7 || scene==8)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                var q=Shot(p,type,scene==6?new Vector2(3,350):new Vector2(950,350),new Vector2(opener?scene==4?24:6:scene==3?24:6,scene==7?2:scene==8?-2:!opener && scene==4?24:0),0);if(scene==5)q.timeLeft=1;parent=type==399?q:null;motor=q;var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=80 && q.active;outer++){var args=new object[]{copy,terrain,null,null};Require((bool)Static(model,"Step",args),"bottle finite movement dependencies");if(copy.active)Static(model,"Tail",copy,null,null);q.Update(q.whoAmI);actions++;Compare(q,copy,type,scene,outer);}if(scene==6)Require(kills==0 && children.Count==0,"world retirement is not bottle Kill");cases++;
            }
            Console.WriteLine("PASS BOTTLE motor="+cases+" actions="+actions);
        }
        private static void Births(object context)
        {
            int cases=0;foreach(bool lower in new[]{false,true})
            {
                var p=Reset(context);if(lower)for(int i=0;i<7;i++){Main.projectile[i].active=true;Main.projectile[i].timeLeft=1000;}parent=Shot(p,399,new Vector2(950,350),Vector2.Zero,51);parent.timeLeft=1;if(lower)for(int i=0;i<6;i++)Main.projectile[i].active=false;
                // Ascending native family visit: freshly allocated higher
                // slots run in this outer; lower slots wait until the next.
                for(int slot=0;slot<Main.maxProjectiles;slot++){var shot=Main.projectile[slot];if(shot.active && (ReferenceEquals(shot,parent) || children.Contains(shot)))shot.Update(slot);}
                Require(children.Count==6 && children.Select(q=>q.whoAmI).Distinct().Count()==6 && children.All(q=>lower?q.whoAmI<parent.whoAmI:q.whoAmI>parent.whoAmI) && children.All(q=>q.penetrate==3 && q.timeLeft==(lower?360:359)),"six actual Molotov children allocate before parent release and retain the original slot-order update clock");
                var terrain=Terrain();for(int i=0;i<children.Count;i++){var copy=birthCopies[i];if(!lower){Require((bool)Static(model,"Step",copy,terrain,null,null),"actual higher-slot newborn motor dependencies");Static(model,"Tail",copy,null,null);}Compare(children[i],copy,children[i].type,-1,1);}
                target=Target(2,children[0].Center,40,60);NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,children.ToArray(),path,Clock(path),Terrain(),Obstacles(p,children[0],path));long sample=path.SampleTick;
                for(int outer=1;outer<=12 && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);foreach(var q in children.OrderBy(q=>q.whoAmI))if(q.active)q.Update(q.whoAmI);}
                Require(first>=0 && known!=null && first-sample==known.Tick && known.Confidence==AttackConfidence.Conditional,"observed actual random six-child family has useful ordered native Damage equal to known replay");Console.WriteLine("BOTTLE actual family lower="+lower+" slots="+string.Join(",",children.Select(q=>q.whoAmI))+" first="+(first-sample)+" known="+known.Tick);cases++;
            }
            foreach(int type in new[]{399,669})
            {
                var p=Reset(context);var v=new Vector2(24,-18);var q=Shot(p,type,new Vector2(950,350),v);
                Require(q.aiStyle==68 && q.velocity==v,"both actual AI68 births retain components beyond16; AI001-only .97 clamp does not apply");
            }
            Console.WriteLine("PASS BOTTLE native six-child families="+cases);
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int mode in Enumerable.Range(0,9))
            {
                var p=Reset(context,3821);int type=mode==6 || mode==7?400:669;target=Target(2,new Vector2(mode==3 || mode==4?1002:950,350),18,40,mode==7?13:3);if(mode==1)target.immune[p.whoAmI]=200;
                NPC front=mode==2 || mode==5 || mode==8?Target(1,new Vector2(950,350),22,40):null;var q=Shot(p,type,new Vector2(950,350),Vector2.Zero,mode==8?1:50);if(mode==3 || mode==4)q.timeLeft=1;if(mode==4)target.Center=new Vector2(1031,350);if(mode==5)target.Center=new Vector2(950,350);if(mode==6 || mode==7)q.penetrate=q.maxPenetrate=2;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                if(mode==0)Require(known!=null && Static(model,"KnownFamily",p,new[]{q},path,Clock(path,"Projectiles"),Terrain(),Obstacles(p,q,path))==null,"a whole-array positive cannot be borrowed by a half-visited projectile epoch whose lower-slot update may already have happened");
                for(int outer=1;outer<=12 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);q.Update(q.whoAmI);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("BOTTLE receive mode="+mode+" native="+actual+" known="+known?.Tick+" slots="+string.Join(",",hitSlots)+" entryDamage="+string.Join(",",entryDamageAtStrike)+" rawDamage="+string.Join(",",rawDamage)+" remaining="+q.penetrate+" owner="+target.immune[p.whoAmI]+" body="+q.width+"x"+q.height+" left="+q.timeLeft);
                bool negative=mode==1 || mode==4;Require((actual==0)==negative && (known==null)==negative && (known==null || known.Tick==actual),"actual bottle body/fuse/144burst/owner gates and ordered known contact agree");
                if(mode==2 || mode==5)Require(hitSlots.Take(2).SequenceEqual(new[]{1,2}) && entryDamageAtStrike.Take(2).SequenceEqual(new[]{50,40}) && rawDamage.Take(2).SequenceEqual(new[]{40,32}) && q.damage==32,"Ale higher NPC slots receive reduced damage before expiry burst, without double-hit of prior receivers");
                if(mode==3)Require(q.width==144 && q.damage==40 && q.timeLeft==1,"Ale Kill itself damages enlarged body, can run OnHit left1 and damage*.8 before active false");
                if(mode==6)Require(q.penetrate==1 && target.immune[p.whoAmI]==10,"ordinary pen2 flame writes owner10 then normal decrement");
                if(mode==7)Require(q.penetrate==0 && target.immune[p.whoAmI]==0,"Eater segment extra decrement precedes owner write and final ordinary decrement");cases++;
                if(mode==8)Require(hitSlots.Take(2).SequenceEqual(new[]{1,2}) && entryDamageAtStrike.Take(2).SequenceEqual(new[]{1,0}) && rawDamage.Take(2).SequenceEqual(new[]{0,0}) && q.damage==0 && hitSlots.Count==2,"Ale damage1 becomes0 after lower slot but the already-admitted Damage loop still Strikes selected; the next Kill Damage is rejected");
            }
            Console.WriteLine("PASS BOTTLE receive="+cases);
        }
        private static void Natural(object context)
        {
            foreach(int item in new[]{2590,3821})
            {
                var p=Reset(context,item);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,45,40);raw=target.Center-new Vector2(390,0);solves=steps=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                Require(GetOptional(attack,"ExpectedPreview")==null && solves==0 && steps==0 && !Main.projectile.Any(q=>q.active),"withdrawn idle bottle preview never solves or fires");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"actual pressed bottle has a prepared finite damage contact");Call(attack,"BindPresentation",plan);
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
                int oldSolves=solves,oldSteps=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int i=0;i<20;i++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PresentationCurrent",plan),"bottle current scalar presentation lease");}Require(oldSolves==solves && oldSteps==steps,"twenty Draws perform zero bottle Solve/Step");
                natural=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual bottle hand consumes its prepared cursor");for(int i=0;i<50 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);Console.WriteLine("BOTTLE natural item="+item+" births="+births+" first="+(first<0?0:first-birthTick+1)+" known="+known?.Tick+" representative="+plan?.Tick+" rawDamage="+string.Join(",",rawDamage));
                Require(births==1 && first>=birthTick && known!=null && known.Confidence!=AttackConfidence.Representative && first-birthTick+1==known.Tick,"both deterministic bottle primaries give useful actual Strike on genuine immortal488 after real one-press/release, equal to known source");natural=false;
            }
        }
    }
}
