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
    internal static class NativeCombatThornChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;
        private static bool observe,natural;private static int solves,steps,kills,handBirths,childBirths,naturalMotorActions;private static long first,birthTick;private static object ammo;private static AttackContact known;private static Vector2 raw;
        private sealed class UpdateInput {internal Projectile Shot;internal int BornAt;}
        private static readonly HashSet<ProjectileKey> sourceKeys=new HashSet<ProjectileKey>();
        private static readonly List<Projectile> newborns=new List<Projectile>();private static readonly List<int> strikeSlots=new List<int>();
        private static readonly List<Projectile> newbornRefs=new List<Projectile>();private static readonly List<ProjectileKey> newbornKeys=new List<ProjectileKey>();private static readonly List<float> birthAi1=new List<float>();
        private static bool Type(int type){return type==7 || type==8 || type>=150 && type<=152 || type==493 || type==494;}
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeKill(Projectile __instance){if(Type(__instance.type))kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(receiving==null || !Type(receiving.type) || !sourceKeys.Contains(receiving.key) || __result<=0)return;strikeSlots.Add(__instance.whoAmI);if(ReferenceEquals(__instance,target) && first<0)first=Main.GameUpdateCount;}
        private static Player Reset(object context,int item=64)
        {
            observe=natural=false;target=null;receiving=null;sourceKeys.Clear();newborns.Clear();newbornRefs.Clear();newbornKeys.Clear();birthAi1.Clear();strikeSlots.Clear();first=-1;kills=handBirths=childBirths=naturalMotorActions=0;known=null;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);p.fullRotation=0;p.gfxOffY=0;Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles,Wet=n.wet};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static Projectile Copy(Projectile p){return (Projectile)Static(model,"Copy",p);}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!observe || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(!Type(q.type))return;
            bool child=__0 is EntitySource_Parent s && s.Entity is Projectile;if(child){childBirths++;if(sourceKeys.Contains(((Projectile)((EntitySource_Parent)__0).Entity).key))sourceKeys.Add(q.key);}else {handBirths++;sourceKeys.Add(q.key);birthTick=Main.GameUpdateCount;}
            newborns.Add(Copy(q));newbornRefs.Add(q);newbornKeys.Add(q.key);birthAi1.Add(q.ai[1]);
            if(natural && !child)
            {
                var p=Main.LocalPlayer;var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var origin=p.RotatedRelativePoint(p.MountedCenter);var v=(Vector2)Static(model,"BirthVelocity",p,origin,cursor,(float)Get(ammo,"Speed"));
                Console.WriteLine("THORN hand item="+p.HeldItem.type+" slot="+q.whoAmI+" type="+q.type+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" alpha="+q.alpha+" raw="+raw+" borrowed="+cursor);
                Require(handBirths==1 && q.aiStyle==4 && q.damage==(int)Get(ammo,"Damage") && q.Center==origin && Vector2.DistanceSquared(q.velocity,v)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"one original hand birth uses the actual borrowed cursor/origin/damage, not a private spawn");
                var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));
            }
        }
        private static void BeforeUpdate(Projectile __instance,out UpdateInput __state)
        {__state=natural && Type(__instance.type)?new UpdateInput{Shot=Copy(__instance),BornAt=newborns.Count}:null;}
        private static bool ChildSame(Projectile child,Projectile actual)
        {return child.type==actual.type && child.position==actual.position && child.velocity==actual.velocity && child.ai[1]==actual.ai[1] && child.damage==actual.damage && child.knockBack==actual.knockBack && child.armorPenetration==actual.armorPenetration && child.bonusTagDamage==actual.bonusTagDamage && child.bonusCritChance==actual.bonusCritChance && child.tagEffectType==actual.tagEffectType;}
        private static void AfterUpdate(Projectile __instance,UpdateInput __state)
        {
            if(__state==null)return;CaptureChildAssignments(__state.BornAt);bool match=false;var terrain=Terrain();for(int variant=0;variant<4;variant++)
            {
                var expected=Copy(__state.Shot);var args=new object[]{expected,terrain,variant,null};Require((bool)Static(model,"Step",args),"natural Thorn private dependencies continue");var child=(Projectile)args[3];Static(model,"Finish",expected);
                bool childMatch=child==null?newborns.Count==__state.BornAt:newborns.Count==__state.BornAt+1 && ChildSame(child,newborns[__state.BornAt]);if(Same(__instance,expected) && childMatch)match=true;
            }
            Require(match,"current-input pure full Thorn action agrees with the one original Update, including actual branch/child/retirement type="+__state.Shot.type+" depth="+__state.Shot.ai[1]+" native="+__instance.position+"/"+__instance.alpha+" expected prior="+__state.Shot.position+"/"+__state.Shot.alpha);naturalMotorActions++;
        }
        private static void CaptureChildAssignments(int from)
        {
            // Native7/150 writes child.ai[1] AFTER NewProjectile returns;
            // 494 passes it into NewProjectile. Observe that real store at
            // the parent Update's exit, before a later child slot runs AI.
            // Preserve the raw birth value separately; no live value is set.
            for(int i=from;i<newborns.Count;i++)
            {var actual=newbornRefs[i];Require(actual.key.Equals(newbornKeys[i]) && ReferenceEquals(Main.projectile[actual.whoAmI],actual),"native child post-birth assignment retains the actual generation");newborns[i].ai[1]=actual.ai[1];}
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true);var audit=new Harmony("JueMingR.Tests.Thorn");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(Strike)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(BeforeUpdate)),postfix:new HarmonyMethod(typeof(NativeCombatThornChecks),nameof(AfterUpdate)));
            try
            {
                string only=Environment.GetEnvironmentVariable("JMR_THORN_LEAF");Require(string.IsNullOrEmpty(only) || only=="motor" || only=="receive" || only=="natural","valid narrow Thorn leaf");
                if(string.IsNullOrEmpty(only) || only=="motor"){Births(context);Motors(context);}if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);
            }
            finally{observe=natural=false;target=null;receiving=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static bool Same(Projectile actual,Projectile expected)
        {
            return actual.type==expected.type && actual.active==expected.active && actual.position==expected.position && actual.velocity==expected.velocity && actual.width==expected.width && actual.height==expected.height && actual.ai[0]==expected.ai[0] && actual.ai[1]==expected.ai[1] && actual.localAI[0]==expected.localAI[0] && actual.alpha==expected.alpha && actual.timeLeft==expected.timeLeft && actual.rotation==expected.rotation && actual.direction==expected.direction && actual.damage==expected.damage && actual.knockBack==expected.knockBack && actual.armorPenetration==expected.armorPenetration;
        }
        private static void Births(object context)
        {
            int cases=0;var shoot=typeof(Player).GetMethod("ItemCheck_Shoot",Flags);
            foreach(int item in new[]{64,788,3051})foreach(float gravity in new[]{1f,-1f})
            {
                var p=Reset(context,item);p.position=new Vector2(650.25f,600.5f);p.gravDir=gravity;p.fullRotation=.2f;Main.screenPosition=new Vector2(500.25f,400.5f);Main.screenWidth=800;Main.screenHeight=600;Main.mouseX=430;Main.mouseY=250;
                var origin=p.RotatedRelativePoint(p.MountedCenter);var consumed=new Vector2(Main.mouseX+Main.screenPosition.X,gravity==-1?Main.screenPosition.Y+Main.screenHeight-Main.mouseY:Main.mouseY+Main.screenPosition.Y);
                var v=(Vector2)Static(model,"BirthVelocity",p,origin,consumed,p.HeldItem.shootSpeed);shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shots=Main.projectile.Where(q=>q.active).ToArray();
                Require(shots.Length==1 && shots[0].type==p.HeldItem.shoot && shots[0].aiStyle==4 && shots[0].Center==origin && Vector2.DistanceSquared(shots[0].velocity,v)<.0001f && shots[0].damage==p.GetWeaponDamage(p.HeldItem) && p.HeldItem.noMelee,"actual ItemCheck_Shoot normal/reverse gravity gives one original AI4 hand role, native float velocity and no phantom hand melee item="+item+" gravity="+gravity+" V="+shots[0].velocity+" expected="+v);cases++;
            }
            {
                var p=Reset(context);Main.screenPosition=Vector2.Zero;p.position=new Vector2(640,600);var origin=p.RotatedRelativePoint(p.MountedCenter);Main.mouseX=(int)origin.X;Main.mouseY=(int)origin.Y;
                Require(new Vector2(Main.mouseX,Main.mouseY)==origin,"coincident native cursor has exact integer source");var v=(Vector2)Static(model,"BirthVelocity",p,origin,origin,p.HeldItem.shootSpeed);shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require(Main.projectile.Single(q=>q.active).velocity==v && v==new Vector2(-32,0),"zero ray uses the direction selected before native speed multiplication");cases++;
            }
            Console.WriteLine("PASS THORN original hand birth cases="+cases);
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0,births=0;
            foreach(int type in new[]{7,8,150,151,152,493,494})foreach(int mode in Enumerable.Range(0,4))
            {
                var p=Reset(context);Vector2 center=mode==3?new Vector2(10,350):new Vector2(950,350);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,new Vector2(32,-.3f),type,20,2,p.whoAmI);var q=Main.projectile[slot];
                if(mode==0)q.ai[1]=2;else {q.ai[0]=1;q.ai[1]=7;q.alpha=mode==1?252:70;}if(mode==2)q.timeLeft=1;
                q.armorPenetration+=3;observe=true;
                for(int outer=1;outer<=12 && q.active;outer++)
                {
                    var prior=Copy(q);var terrain=Terrain();newborns.Clear();newbornRefs.Clear();newbornKeys.Clear();birthAi1.Clear();q.Update(slot);CaptureChildAssignments(0);actions++;
                    bool match=false;for(int variant=0;variant<4;variant++)
                    {
                        var expected=Copy(prior);var args=new object[]{expected,terrain,variant,null};Require((bool)Static(model,"Step",args),"known Thorn scalar motor continues");var child=(Projectile)args[3];Static(model,"Finish",expected);
                        bool childMatch=child==null?newborns.Count==0:newborns.Count==1 && ChildSame(child,newborns[0]);
                        if(Same(q,expected) && childMatch)match=true;
                    }
                    Require(match,"original AI004 alpha/phase/position/lifetime/child inherited stats match a finite branch type="+type+" mode="+mode+" action="+outer+" native="+q.position+"/"+q.velocity+" phase="+q.ai[0]+","+q.ai[1]+" alpha="+q.alpha);births+=newborns.Count;
                    if(prior.ai[0]!=0)Require(q.position==prior.position,"AI004 never integrates its apparent velocity");
                }
                if(mode==3)Require(kills==0 && q.timeLeft==3600,"world-edge exit is not Kill or lifetime decrement");else if(mode==1 || mode==2)Require(!q.active && kills==1,"fade and lifetime terminate through actual Kill");cases++;
            }
            // Both real suffix alternatives are admissible without consuming
            // native RNG; the parent is an observed state, not a new weapon.
            foreach(int variant in new[]{0,2})
            {var p=Reset(context);var q=new Projectile();q.SetDefaults(494);q.owner=p.whoAmI;q.active=true;q.damage=21;q.knockBack=3;q.ai[1]=7;q.alpha=50;Projectile child;var args=new object[]{q,variant,null};Require((bool)Static(model,"Ai",args),"Crystal depth7 finite branch");child=(Projectile)args[2];Require(child.type==(variant==0?493:494) && child.damage==(variant==0?26:21) && child.ai[1]==8,"random threshold changes the tip's role/damage while deterministic prefix remains available");}
            Console.WriteLine("PASS THORN motor cases="+cases+" actions="+actions+" originalChildBirths="+births+" finiteCrystalBranches=2");
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int mode in Enumerable.Range(0,5))
            {
                var p=Reset(context);var center=new Vector2(950,350);target=Target(2,center,6,20);if(mode==1)target.immune[p.whoAmI]=200;if(mode==3)target.dontTakeDamage=true;
                if(mode==2){int ambient=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,Vector2.Zero,8,30,0,p.whoAmI);Main.projectile[ambient].ai[0]=1;Main.projectile[ambient].alpha=0;}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,new Vector2(32,0),7,30,0,p.whoAmI);var q=Main.projectile[slot];if(mode==4)target.reflectsProjectiles=true;sourceKeys.Add(q.key);observe=true;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var obstacles=Obstacles(p,q,path);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),obstacles);long sample=path.SampleTick;
                if(mode==0)
                {
                    var lateClock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"Projectiles"),0},null);
                    Require(Static(model,"KnownFamily",p,new[]{q},path,lateClock,Terrain(),Obstacles(p,q,path))==null,"a partial projectile-array phase cannot pretend earlier captured family members have not run");
                    q.ai[1]++;Require(!(bool)Get(obstacles,"Unchanged"),"real chain-depth change revokes the prepared family lease");q.ai[1]--;Require((bool)Get(obstacles,"Unchanged"),"restored exact chain facts retain the lease");
                    q.active=false;Require(!(bool)Get(obstacles,"Unchanged"),"actual occupied-slot retirement revokes the captured family/pool condition");q.active=true;Require((bool)Get(obstacles,"Unchanged"),"restored occupancy preserves the original slot/generation lease");
                }
                for(int outer=1;outer<=120 && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active && Type(Main.projectile[i].type))Main.projectile[i].Update(i);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("THORN receive mode="+mode+" actualSourceFirst="+actual+" representative="+known?.Tick+" ownerImmune="+target.immune[p.whoAmI]);bool negative=mode==1 || mode==2 || mode==3;
                Require((actual==0)==negative && (known==null)==negative && (known==null || known.Tick==actual),"native owner immunity, preceding ambient segment and invulnerability agree; AI004 is outside reflective Damage AI whitelist");if(mode==0)Require(actual==1 && newborns.Count==0,"alpha255 root is already a real Damage body");cases++;
            }
            foreach(bool early in new[]{false,true})
            {
                var p=Reset(context);for(int i=0;i<10;i++){int allocated=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,500,0,0,8,0,0,p.whoAmI);Require(allocated==i,"ten actual terminal segments occupy native low slots");Main.projectile[allocated].ai[0]=1;Main.projectile[allocated].alpha=0;}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,32,0,7,30,0,p.whoAmI);Require(slot==10,"original first-free slot allocates the parent after ten occupied slots");
                // Release one earlier slot only after the real parent birth.
                // NewProjectile itself must then pick that hole or slot11;
                // never relocate the parent or fabricate its generation key.
                if(early)Main.projectile[0].Kill();var q=Main.projectile[slot];q.ai[1]=2;q.alpha=25;target=Target(2,q.Center+new Vector2(32,0),6,20);sourceKeys.Add(q.key);observe=true;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                for(int action=0;action<2;action++){if(action>0)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active && Type(Main.projectile[i].type))Main.projectile[i].Update(i);}
                Require(first-sample==(early?2:1) && known!=null && known.Tick==first-sample,"original later-slot child damages this action and earlier-slot child damages next action");cases++;
            }
            Console.WriteLine("PASS THORN receive cases="+cases);
        }
        private static void Natural(object context)
        {
            int cases=0;string selector=Environment.GetEnvironmentVariable("JMR_THORN_ITEM");int[] items=string.IsNullOrEmpty(selector)?new[]{64,788,3051}:new[]{int.Parse(selector)};Require(items.All(i=>i==64 || i==788 || i==3051),"valid Thorn item selector");
            foreach(int item in items)
            {
                var p=Reset(context,item);p.position=new Vector2(650,646);p.ResetFallDamage();p.releaseUseItem=true;p.statMana=p.statManaMax2=200;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);raw=target.Center-new Vector2(430,0);solves=steps=0;
                var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null && !Main.projectile.Any(q=>q.active),"idle real Thorn hand has a useful preview without firing");ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
                int captureSteps=steps,captureSolves=solves;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int d=0;d<20;d++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"same-update Thorn preview is current");}Require(captureSteps==steps && captureSolves==solves && solves>0,"twenty CPU Capture/Project/PreviewCurrent passes do not solve/replay Thorn segments; actual World.Draw/pixels remain unverified");
                natural=observe=true;long start=Main.GameUpdateCount;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);for(int i=0;i<155;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                Console.WriteLine("THORN natural item="+item+" hand="+handBirths+" child="+childBirths+" first="+(first<0?0:first-start)+" known="+known?.Tick+" idleRepresentative="+plan.Tick+" actualTypes="+string.Join(",",newborns.Select(q=>q.type))+" rawBirthDepths="+string.Join(",",birthAi1)+" completedBirthDepths="+string.Join(",",newborns.Select(q=>q.ai[1]))+" kills="+kills+" pureCurrentActions="+naturalMotorActions+" solves="+solves+" steps="+steps);
                Require(handBirths==1 && childBirths>=6 && first>=birthTick && known!=null && first-birthTick+1==known.Tick,"one original click gives a genuine derived-chain positive Strike against the native training dummy, before random-only length divergence");
                Require(newborns.Any(q=>q.type==(item==64?8:item==788?152:493)) && !Main.projectile.Any(q=>q.active && Type(q.type)) && kills==handBirths+childBirths,"the real terminal role is born and every original segment naturally retires");
                Require(newborns.Skip(1).All(q=>q.ai[1]>=2) && newborns.Select(q=>q.whoAmI).Distinct().Count()==newborns.Count,"actual finite chain has separate native slots and depth facts, not duplicate hand registration");natural=observe=false;cases++;
            }
            Console.WriteLine("PASS THORN natural items="+string.Join(",",items)+" cases="+cases+" actionsPerItem=156");
        }
    }
}
