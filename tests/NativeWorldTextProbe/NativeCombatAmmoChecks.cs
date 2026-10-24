using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAmmoChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var tools=Get(context,"Tools");var input=Get(context,"Input");
            ResolvedCatalog(context);
            if(string.Equals(Environment.GetEnvironmentVariable("JMR_WEAPON_CATALOG_ONLY"),"1",StringComparison.Ordinal))return;
            // One native outcome matrix; every row resolves its actual .8 Item
            // fields, ammo selection and projectile defaults. No old-version
            // identity or guessed per-tick speed stands in for these values.
            foreach(var row in new[]{new[]{95,278,981},new[]{95,3104,14},new[]{1201,1334,278},new[]{905,71,158},new[]{905,72,159},new[]{905,73,160},new[]{905,74,161},new[]{120,40,2},new[]{682,40,117},new[]{725,40,120},new[]{2223,40,357},new[]{3052,40,495},new[]{1254,97,242},new[]{1254,278,981},new[]{759,771,134},new[]{758,771,133},new[]{281,283,51},new[]{986,1310,267},new[]{1835,1836,323}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,row[0],0,0);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;Main.screenPosition=new Vector2(600,500);
                p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(row[0]==682 || row[0]==725 || row[0]==3052?1400:1100,646);n.life=n.lifeMax=10000;n.target=0;n.defense=0;
                // SetDefaults retains the slot's owner-immunity array. These
                // independent stationary rows do not tick NPCs; start eligible
                // instead of inheriting the preceding piercing shot's cooldown.
                Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                // This is the native-outcome matrix, not the natural cold-use
                // supply check. Its immutable121 fixture and
                // finite isolated mechanism scope cannot qualify runtime costs;
                // actual Shoot retains the normal input/operation protection.
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);
                if(row[0]==95 && row[1]==278)NativeCombatAimChecks.SolutionCheckpoint(context,attack,"ammo95/278-prepared");
                // Default representative aiming supplies a real direction and
                // receive index, without manufacturing a full lifecycle Contact.
                // This controlled stationary scene still requires its exact
                // first native Damage after the same legal enhanced input.
                var plan=NativeCombatAimChecks.RequireSolution(attack,p,"ammo solution admits its actual enhanced input: weapon="+row[0]+" ammo="+row[1]);
                var captured=Get(attack,"ammo");Require((int)Get(captured,"Projectile")==row[2],"deterministic post-ammo shoot conversion order");float raw=(float)Get(captured,"Speed");var delta=plan.Point-(p.RotatedRelativePoint(p.MountedCenter)+(row[0]==281 || row[0]==986?new Vector2(6*p.direction,-6*p.gravDir):Vector2.Zero));delta.Normalize();var expected=delta*raw;
                var models=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels");var args=new object[]{captured,default(AttackMotion)};Require((bool)models.GetMethod("TryRead",Flags).Invoke(null,args),"resolved member has an explicit model");var motion=(AttackMotion)args[1];
                Require((row[2]==117 || row[2]==120 || row[2]==495 || row[2]==2 || row[2]==133 || row[2]==278 || row[2]==51 || row[2]==267)?motion.Gravity>0:motion.Gravity==0,"straight members cannot fall through into a later arrow's gravity branch");
                if(ContentSamples.ProjectilesByType[row[2]].aiStyle==1)while(expected.X>=16 || expected.X<=-16 || expected.Y>=16 || expected.Y< -16)expected*=.97f;
                int mouseX=Main.mouseX,mouseY=Main.mouseY;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
                if(row[0]==95 && row[1]==278)NativeCombatAimChecks.SolutionCheckpoint(context,attack,"ammo95/278-shot");
                var shots=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();
                if(row[0]==95 && row[1]==278)Console.WriteLine("AMMO-CHECKPOINT nativeBirth count="+shots.Length+" shots="+string.Join(";",shots.Select(q=>"type="+q.type+" center="+q.Center+" velocity="+q.velocity+" updates="+(q.extraUpdates+1)+" damage="+q.damage))+" ammoStack="+p.inventory[54].stack);
                Require(shots.Length==1 && shots[0].type==row[2],"native final projectile identity agrees with pure snapshot");var shot=shots[0];Require(Vector2.DistanceSquared(shot.velocity,expected)<.0001f,"native component birth limit preserves the modeled velocity");Require(Main.mouseX==mouseX && Main.mouseY==mouseY && shot.damage>0,"natural damage and physical cursor ownership remain valid");
                if(row[0]==905)Require(Math.Abs(raw-(10+row[1]-70))<.0001f && shot.extraUpdates==1,"coin's ammo speed is per subupdate, despite zero weapon damage");
                int firstDamage=0;for(int tick=1;tick<=plan.NativeTick && shot.active;tick++){shot.Update(shot.whoAmI);if(n.life<10000){firstDamage=tick;break;}}
                if(row[0]==95 && row[1]==278)Console.WriteLine("AMMO-CHECKPOINT firstDamage="+firstDamage+" nativeTick="+plan.NativeTick+" receive="+plan.ReceiveTick+" first="+plan.FirstTick+" shot="+shot.Hitbox+" target="+n.Hitbox+" life="+n.life+" active="+shot.active);
                Require(firstDamage==plan.NativeTick,"first native Damage matches solution clock: expected="+plan.NativeTick+" actual="+firstDamage+" weapon="+row[0]+" receive="+plan.ReceiveTick+" first="+plan.FirstTick+" phase="+plan.Phase+" route="+plan.Route);
                Console.WriteLine("PASS ammo -> native ShotOutcome/Damage: weapon="+row[0]+" ammo="+row[1]+" projectile="+shot.type+" speed="+raw+" birth="+expected+" updates="+(shot.extraUpdates+1)+" firstDamage="+firstDamage+" receive="+plan.ReceiveTick+" first="+plan.FirstTick+" phase="+plan.Phase+" route="+plan.Route+" contact="+(plan.Contact!=null));
                Array.Clear(n.immune,0,n.immune.Length);
            }
            MotionMembers(context);
            HellwingClock(context);
            HellwingContact(context);
            PewmaticContact(context);
            AiTwoWind(context);
            AiTwoThrows(context);
            VampireDamageWindow(context);
            QuiverClocks(context);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void HellwingClock(object context)
        {
            var combat=Get(context,"Combat");var models=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);bool savedWind=Main.windPhysics;int cases=0;
            try{foreach(float launchX in new[]{-18f,-8.5f,0f,8.5f,18f})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3019,0,0);Main.windPhysics=false;for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                var args=new object[]{278,Math.Abs(launchX)+1,3019,default(AttackMotion)};Require((bool)models.GetMethod("TryResolved",Flags).Invoke(null,args),"Hellwing actual arrow has launch-clock facts");var motion=(AttackMotion)args[3];
                Require(motion.GravityClockUsesLaunchX && motion.WithConfidence(AttackConfidence.Conditional).GravityClockUsesLaunchX,"confidence changes preserve launch-clock facts");motion=motion.ForLaunch(launchX);float vx=launchX,vy=0;motion.Launch(ref vx,ref vy);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),new Vector2(launchX,0),278,50,0,p.whoAmI,launchX,0);var shot=Main.projectile[slot];float x=shot.Center.X,y=shot.Center.Y;
                for(int tick=1;tick<=30;tick++){for(int sub=0;sub<motion.Updates;sub++)motion.Advance(ref x,ref y,ref vx,ref vy,(tick-1)*motion.Updates+sub+1);shot.Update(slot);Require(shot.active && Vector2.DistanceSquared(shot.Center,new Vector2(x,y))<.02f,"Hellwing unbounded birth X advances actual AI001 gravity clock: "+launchX+"/"+tick+" native="+shot.Center+" model="+x+","+y);}
                cases++;
            }}finally{Main.windPhysics=savedWind;}
            Console.WriteLine("PASS native Hellwing launch-X gravity-clock motor: cases="+cases+" outerUpdates=150");
        }
        private static void HellwingContact(object context)
        {
            var combat=Get(context,"Combat");var models=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);bool savedWind=Main.windPhysics;int cases=0;
            try{foreach(int direction in new[]{-1,1})foreach(int distance in new[]{160,450})foreach(int vertical in new[]{-80,0,80})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3019,0,0);Main.windPhysics=false;for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.life=n.lifeMax=10000;n.position=new Vector2(950+direction*distance,350+vertical);n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};
                var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                var args=new object[]{278,12f,3019,default(AttackMotion)};Require((bool)models.GetMethod("TryResolved",Flags).Invoke(null,args),"Hellwing representative resolves actual arrow clock");var motion=(AttackMotion)args[3];
                var contact=AttackIntercept.Solve(950,350,motion,timeline,0,0);Require(contact!=null,"direction-dependent launch clock has a finite contact: "+direction+"/"+distance+"/"+vertical);
                Vector2 launch=new Vector2(contact.AimX-950,contact.AimY-350);launch.Normalize();launch*=motion.Speed;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),launch,278,50,0,p.whoAmI,launch.X,launch.Y);var shot=Main.projectile[slot];int first=0;
                for(int tick=1;tick<=contact.Tick && shot.active;tick++){shot.Update(slot);if(n.life<10000){first=tick;break;}}
                Require(first==contact.Tick,"launch-clock refined left/right near/far representative agrees with actual native Damage: "+direction+"/"+distance+"/"+vertical+" actual="+first+" contact="+contact.Tick);cases++;
            }}finally{Main.windPhysics=savedWind;}
            Console.WriteLine("PASS Hellwing representative directional clock contact: cases="+cases+" native first-Damage exact; random births remain Representative");
        }
        internal static void RunReflection(object context){PewmaticContact(context);}
        private static void PewmaticContact(object context)
        {
            var combat=Get(context,"Combat");var models=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);bool savedWind=Main.windPhysics;int cases=0;
            try{foreach(int direction in new[]{-1,1})foreach(int distance in new[]{160,450})foreach(int vertical in new[]{-80,0,80})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),5117,0,0);Main.windPhysics=false;for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                var origin=(Vector2)models.GetMethod("LaunchOrigin",Flags,null,new[]{typeof(Player),typeof(Item)},null).Invoke(null,new object[]{p,p.HeldItem});Require(origin.Y==p.position.Y+p.height/3,"Pewmatic representative keeps native modified birth origin");
                var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.life=n.lifeMax=10000;n.position=new Vector2(950+direction*distance,350+vertical);n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                var args=new object[]{968,12f,5117,default(AttackMotion)};Require((bool)models.GetMethod("TryResolved",Flags).Invoke(null,args),"Pewmatic native motion resolves explicit delayed horizontal damping");var motion=(AttackMotion)args[3];
                var contact=AttackIntercept.Solve(950,350,motion,timeline,0,0);
                if(direction==1 && distance==450 && vertical==-80)
                {
                    // AI002's speed-12 circle, delayed X damping and actual
                    // receiver Y interval have maximum centre X=1383.471 at
                    // step55. Native integer body needs X>=1394; this is a
                    // true negative, not a missing inverse direction seed.
                    Require(contact==null,"Pewmatic finite upper/far receiver is outside the native speed budget");
                    float bestY=(317-350-.2f*36*37)/55;var bestLaunch=new Vector2((float)Math.Sqrt(144-bestY*bestY),bestY);
                    int negativeSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),bestLaunch,968,50,0,p.whoAmI);var negativeShot=Main.projectile[negativeSlot];for(int tick=1;tick<=120 && negativeShot.active;tick++)negativeShot.Update(negativeSlot);
                    Require(n.life==10000,"native extremal upper/far Pewmatic launch cannot fabricate Damage");cases++;continue;
                }
                Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"Pewmatic ellipse inverse has finite representative contact: "+direction+"/"+distance+"/"+vertical);
                Vector2 launch=new Vector2(contact.AimX-950,contact.AimY-350);launch.Normalize();launch*=motion.Speed;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),launch,968,50,0,p.whoAmI);var shot=Main.projectile[slot];int first=0;
                for(int tick=1;tick<=contact.Tick && shot.active;tick++){shot.Update(slot);if(n.life<10000){first=tick;break;}}
                Require(first==contact.Tick,"Pewmatic delayed damping representative matches native first Damage: "+direction+"/"+distance+"/"+vertical+" actual="+first+" model="+contact.Tick);cases++;
            }}finally{Main.windPhysics=savedWind;}
            Console.WriteLine("PASS Pewmatic delayed horizontal damping inverse: directional native contacts="+cases+"; native random births remain Representative");
        }
        private static void AiTwoWind(object context)
        {
            var models=Get(context,"Combat").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);bool savedWind=Main.windPhysics;float savedSpeed=Main.windSpeedCurrent,savedStrength=Main.windPhysicsStrength;
            try{foreach(int type in new[]{304,968,3,21,48,54,318,330,520,599,1012,1085})foreach(bool wall in new[]{false,true})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),5117,0,0);Main.windPhysics=true;Main.windSpeedCurrent=-.1f;Main.windPhysicsStrength=1;for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();if(wall)for(int tx=5;tx<115;tx++)for(int ty=5;ty<60;ty++)Main.tile[tx,ty].wall=1;
                var args=new object[]{type,12f,5117,default(AttackMotion)};Require((bool)models.GetMethod("TryResolved",Flags).Invoke(null,args),"explicit AI002 member retains both wind stages");var motion=((AttackMotion)args[3]).WithEnvironment(1,wall?0:-.1f).WithConfidence(AttackConfidence.Conditional);
                Require(motion.PreMotorWindX==-.1f,"AI002 pre-motor wind survives environment/confidence copies");int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),1100,350,12,0,type,50,0,p.whoAmI);var shot=Main.projectile[slot];float x=shot.Center.X,y=shot.Center.Y,vx=shot.velocity.X,vy=shot.velocity.Y;
                for(int tick=1;tick<=40;tick++){motion.Advance(ref x,ref y,ref vx,ref vy,tick);shot.Update(slot);Require(shot.active && Vector2.DistanceSquared(shot.Center,new Vector2(x,y))<.02f && Vector2.DistanceSquared(shot.velocity,new Vector2(vx,vy))<.0001f,"AI002 separate unconditional and wall-gated wind agrees with native: "+type+"/"+wall+"/"+tick);}
            }}finally{Main.windPhysics=savedWind;Main.windSpeedCurrent=savedSpeed;Main.windPhysicsStrength=savedStrength;}
            Console.WriteLine("PASS native AI002 unconditional plus gated wind: 24 cases/960 outer updates");
        }
        private static void QuiverClocks(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var snapshots=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);var models=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);var env=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);bool wind=Main.windPhysics;int cases=0,motor=0;
            var audit=new Harmony("JueMingR.Tests.QuiverEquipment");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{foreach(int accessory in new[]{1321,4002,4006,6181})foreach(int ammoType in new[]{40,1334})
            {
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(accessory);p.controlUseItem=false;p.Update(p.whoAmI);Main.windPhysics=false;p.inventory[54].SetDefaults(ammoType);p.inventory[54].stack=999;
                Require(p.magicQuiver && (accessory!=6181 || p.accHarpyCharm && p.hasPhoenixQuiver && p.hasMoltenQuiver),"natural Player.Update equips the complete real quiver accessory effects");var captured=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var args=new object[]{captured,default(AttackMotion)};Require((bool)models.GetMethod("TryRead",Flags).Invoke(null,args),"native equipped quiver has explicit resolved arrow");var motion=(AttackMotion)args[1];int type=(int)Get(captured,"Projectile");var sample=ContentSamples.ProjectilesByType[type];Require(motion.Updates==2 && motion.FirstUpdates==(sample.extraUpdates<1?1:0),"birth/next outer clock uses native sample and real equipment");Require(motion.WithEnvironment(1,0).WithConfidence(AttackConfidence.Conditional).WithLifetime(motion.Lifetime).ForLaunch(3).FirstUpdates==motion.FirstUpdates,"all finite motion copies retain native first outer fact");
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,motion.Speed,0,type,0,0,p.whoAmI);var shot=Main.projectile[slot];float xPos=shot.Center.X,yPos=shot.Center.Y,vx=shot.velocity.X,vy=shot.velocity.Y;int k=0;
                for(int outer=1;outer<=25;outer++)
                {int updates=shot.extraUpdates+1;for(int sub=0;sub<updates;sub++){k++;Require(motion.OuterAtSubupdate(k)==outer && motion.SubupdateInOuter(k)==sub,"outer/substep clock agrees before real native update");motion.Advance(ref xPos,ref yPos,ref vx,ref vy,k);}shot.Update(slot);Require(shot.active && Vector2.DistanceSquared(shot.Center,new Vector2(xPos,yPos))<.002f && Vector2.DistanceSquared(shot.velocity,new Vector2(vx,vy))<.0001f && shot.extraUpdates==1 && motion.SubupdatesForOuters(outer)==k,"natural equipped quiver full native motor agrees: "+accessory+"/"+type+"/"+outer);motor+=updates;}
                shot.active=false;var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.defense=0;n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var origin=p.RotatedRelativePoint(p.MountedCenter);var solve=new object[]{sample,terrain,origin,motion,timeline,0,0,1,new Func<int,bool>(outer=>true),null,null,null};var plan=(AttackContact)env.GetMethod("Solve",Flags).Invoke(null,solve);Require(plan!=null,"real quiver inverse receives a native interval");var velocity=(new Vector2(plan.AimX,plan.AimY)-origin).SafeNormalize(Vector2.UnitX)*motion.Speed;slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,50,0,p.whoAmI);shot=Main.projectile[slot];int first=0;
                // Receiver distance is <400: this ordinary clock oracle ends
                // before any future Harpy player stage can admit the primary.
                for(int outer=1;outer<=40 && shot.active;outer++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);shot.Update(slot);if(n.life<10000){first=outer;break;}}Require(first==plan.Tick,"real equipped quiver native first Damage agrees with discrete inversion: "+accessory+"/"+type+" expected="+plan.Tick+" native="+first);cases++;
            }}finally{audit.UnpatchAll(audit.Id);Main.windPhysics=wind;foreach(var item in Main.LocalPlayer.armor)item.TurnToAir();Main.LocalPlayer.ResetEffects();}
            Console.WriteLine("PASS real equipped magic/molten/stalker/Phoenix arrow clocks: "+cases+" first Damage cases/"+motor+" native motor substeps");
        }
        private static void AiTwoThrows(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var models=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);var env=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);bool wind=Main.windPhysics;int cases=0;
            try{foreach(int type in new[]{3,21,48,54,318,330,520,599,1012,1085,69,70,621,497})foreach(int direction in new[]{-1,1})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),42,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.position=new Vector2(950+direction*350-16,375);n.life=n.lifeMax=10000;n.defense=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
                var args=new object[]{type,12f,0,default(AttackMotion)};Require((bool)models.GetMethod("TryResolved",Flags).Invoke(null,args),"audited AI002 throw family has explicit scalar model");var motion=(AttackMotion)args[3];bool flask=type==69 || type==70 || type==621;int start=flask?10:type==497?30:20;Require(motion.Acceleration==(flask || type==497?.99f:.97f) && motion.Gravity==(flask?.25f:type==497?.5f:.4f) && motion.GravityStart==start && motion.AccelerationStart==start && motion.HorizontalAccelerationOnly && motion.MaxFallSpeed==32,"throw variants preserve their own threshold, X-only drag and terminal fall cap");
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var solve=new object[]{ContentSamples.ProjectilesByType[type],terrain,new Vector2(950,350),motion,timeline,0,0,1,new Func<int,bool>(outer=>true),null,null,null};var contact=(AttackContact)env.GetMethod("Solve",Flags).Invoke(null,solve);Require(contact!=null,"throw family inverse finds native damage interval: "+type+"/"+direction);
                var velocity=(new Vector2(contact.AimX,contact.AimY)-new Vector2(950,350)).SafeNormalize(Vector2.UnitX)*12;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),velocity,type,50,0,p.whoAmI);var shot=Main.projectile[slot];int first=0;for(int tick=1;tick<=120 && shot.active;tick++){shot.Update(slot);if(n.life<10000){first=tick;break;}}Require(first==contact.Tick,"actual AI002 body first Damage agrees: "+type+"/"+direction+" expected="+contact.Tick+" first="+first);cases++;
            }}finally{Main.windPhysics=wind;}
            Console.WriteLine("PASS ordinary AI002 throw family real collision/body contacts: "+cases+" directional cases");
        }
        private static void VampireDamageWindow(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var snapshots=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);var models=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);bool wind=Main.windPhysics;int cases=0;
            try{foreach(int damage in new[]{1,4,42,200})foreach(bool beyond in new[]{false,true})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1569,0,0);Main.windPhysics=false;p.HeldItem.damage=damage;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                var snapshot=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var args=new object[]{snapshot,default(AttackMotion)};Require((bool)models.GetMethod("TryRead",Flags).Invoke(null,args),"VampireKnives captures a positive finite damage window");var motion=(AttackMotion)args[1];int initial=(int)Get(snapshot,"Damage"),last=29,value=initial;
                for(int k=30;k<=54;k++){value=(int)(value*.9f);if(value<=0)break;last=k;}Require(motion.Lifetime==last && motion.WithLifetime(last).PreMotorWindX==motion.PreMotorWindX,"captured integer damage fade limits contact independently of visual lifetime");
                int desired=last+(beyond?1:0);var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.life=n.lifeMax=10000;n.defense=0;n.netOffset=Vector2.Zero;n.position=new Vector2(950+12*desired+14,330);Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                var contact=AttackIntercept.Replay(2000,350,950,350,12,0,motion,timeline,0);Require(beyond?contact==null:contact!=null && contact.Tick==desired,"zero-damage visual continuation cannot publish a contact");
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,12,0,304,initial,0,p.whoAmI);var shot=Main.projectile[slot];int first=0;for(int tick=1;tick<=60 && shot.active;tick++){shot.Update(slot);if(n.life<10000){first=tick;break;}}
                Require(beyond?first==0:first==desired,"native damage truncation and visual retirement respect exact contact window: damage="+initial+" last="+last+" beyond="+beyond+" first="+first);
                p.HeldItem.damage++;Require(!(bool)snapshots.GetMethod("MembersMatch",Flags).Invoke(snapshot,new object[]{p,p.HeldItem}),"changed damage immediately invalidates the frozen VampireKnives window");cases++;
            }}finally{Main.windPhysics=wind;}
            Console.WriteLine("PASS native VampireKnives integer damage window: "+cases+" finite positive/zero-damage negative cases");
        }
        private static void MotionMembers(object context)
        {
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");
            var models=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);
            bool savedWind=Main.windPhysics;try
            {
            foreach(int type in new[]{81,82,103,172,225,278,282,474,819,94,1114,304,955,20,104,265,287,355,246,311,312,336,345,1006,968,51,267,323,69,70,621,497})
            {
                var p=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);p.position=new Vector2(700,646);Main.windPhysics=false;
                // Reset supplies a gameplay floor at tileY43. This oracle
                // isolates the native motor before any tile-hit transition.
                for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                foreach(var n in Main.npc)n.active=false;
                var args=new object[]{type,12f,95,default(AttackMotion)};Require((bool)models.GetMethod("TryResolved",Flags).Invoke(null,args),"explicit related member has a finite motion: "+type);var motion=(AttackMotion)args[3];
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),1100,type==1006?1400:350,12,type==81?15.8f:type==1006?-15.8f:0,type,50,0,p.whoAmI);var shot=Main.projectile[slot];
                float x=shot.Center.X,y=shot.Center.Y,vx=shot.velocity.X,vy=shot.velocity.Y;int compared=0;
                for(int tick=0;tick<60 && shot.active;tick++)
                {
                    for(int sub=0;sub<motion.Updates;sub++)motion.Advance(ref x,ref y,ref vx,ref vy,tick*motion.Updates+sub+1);
                    shot.Update(slot);if(!shot.active)break;
                    Require(Vector2.DistanceSquared(shot.Center,new Vector2(x,y))<.02f,"full original AI/movement agrees across gravity/damping threshold: "+type+"/"+tick+" native="+shot.Center+" model="+x+","+y);compared++;
                }
                Require(compared>=Math.Min(30,motion.Lifetime/motion.Updates-1),"finite member receives a real future interval: "+type);
                if(type==81)Require(shot.velocity.Y==16,"native AI001 gravity reaches and preserves the fall-speed cap");
                if(type==1006)Require(shot.velocity.Y==-16,"native ShimmerArrow reaches and preserves its distinct rise-speed cap");
                Console.WriteLine("WEAPON MOTION member="+type+" originalOuterUpdates="+compared+" finiteSubsteps="+motion.Lifetime);
            }
            }finally{Main.windPhysics=savedWind;}
        }
        private static void ResolvedCatalog(object context)
        {
            var combat=Get(context,"Combat");var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),95,0,0);var assembly=combat.GetType().Assembly;
            Func<string,Type> type=name=>assembly.GetType("JueMingR.TerrariaHost.Combat."+name,true);
            var eligible=type("HostAttackAim").GetMethod("Eligible",Flags);var capture=type("AttackAmmoSnapshot").GetMethod("Capture",Flags);var model=type("HostAttackModels").GetMethod("TryRead",Flags);
            var ammoGroups=ContentSamples.ItemsByType.Values.Where(a=>a.type>0 && a.ammo>0).GroupBy(a=>a.ammo).ToDictionary(g=>g.Key,g=>g.Select(a=>a.type).Distinct().OrderBy(a=>a).ToArray());
            int weapons=0,resolved=0,unknown=0,missingAmmo=0,excludedHandStages=0,mixedPairs=0,scourgePairs=0;
            foreach(var weapon in ContentSamples.ItemsByType.Values.Where(w=>w.type>0).OrderBy(w=>w.type))
            {
                // The full native inventory is the starting set. Eligibility
                // here is the production boundary, not a list of reported
                // regressions. Every admitted ammo conversion is classified;
                // model presence is explicitly NOT an actual hit result.
                bool admitted=(bool)eligible.Invoke(null,new object[]{weapon});
                if(!admitted)
                {
                    // A held cast/placement is a hand stage even when its
                    // later autonomous minion AI is outside aiming. Record
                    // real excluded weapons independently of the aim gate.
                    if(weapon.useStyle>0 && weapon.useTime>0 && (weapon.damage>0 || weapon.type==905) && (weapon.shoot>0 || !weapon.noMelee) && weapon.pick==0 && weapon.axe==0 && weapon.hammer==0)
                    {
                        excludedHandStages++;string excluded=weapon.sentry?"SENTRY_PLACEMENT_UNMODELED":weapon.summon?"SUMMON_HAND_STAGE_UNMODELED":"HAND_STAGE_UNMODELED";
                        Console.WriteLine("WEAPON CATALOG item="+weapon.type+" baseProjectile="+weapon.shoot+" status=EXCLUDED_TRUE_WEAPON stage="+excluded+" autonomousAiOutsideAim="+(weapon.sentry || weapon.summon));
                    }
                    continue;
                }
                weapons++;p.inventory[0].SetDefaults(weapon.type);
                if((bool)type("HostPlacementAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{p.HeldItem})){resolved++;Console.WriteLine("WEAPON CATALOG item="+weapon.type+" ammo=0 projectile="+weapon.shoot+" stage="+(weapon.type==6161?"DIRECTED_BIRTH_INPUT":"PLACEMENT_INPUT")+" boundary=hand-input-only; no-contact; autonomous-future-outside-aim");continue;}
                bool melee=(bool)type("HostSwingAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{p.HeldItem});
                bool nativeMelee=(p.HeldItem.useStyle==1 || p.HeldItem.useStyle==3) && !p.HeldItem.noMelee && p.HeldItem.damage>0 && !p.HeldItem.summon;
                // A mixed weapon's hand pose does not classify its projectile,
                // ammo conversion or later release. Only a genuinely shotless
                // item can end here; the shot stage is audited independently.
                if(melee && p.HeldItem.shoot<=0){resolved++;Console.WriteLine("WEAPON CATALOG item="+weapon.type+" ammo=0 projectile=0 stage=MELEE_POSE");continue;}
                int[] ammunition;if(weapon.useAmmo==0)ammunition=new[]{0};else if(!ammoGroups.TryGetValue(weapon.useAmmo,out ammunition)){missingAmmo++;Console.WriteLine("WEAPON CATALOG item="+weapon.type+" status=NO_AMMO_DEFINITION");continue;}
                foreach(int ammoType in ammunition)
                {
                    for(int slot=54;slot<=57;slot++)p.inventory[slot].SetDefaults(0);p.inventory[54].SetDefaults(ammoType);if(ammoType!=0)p.inventory[54].stack=999;
                    var snapshot=capture.Invoke(null,new object[]{p,p.HeldItem});if(snapshot==null){unknown++;Console.WriteLine("WEAPON CATALOG item="+weapon.type+" ammo="+ammoType+" status=UNRESOLVED_SNAPSHOT");continue;}
                    int projectile=(int)Get(snapshot,"Projectile");Projectile sample;string kind="UNKNOWN";
                    if((bool)type("HostPhasebladeAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{projectile}))kind="PRESS_MELEE_RELEASE_FLIGHT";
                    else if((bool)type("HostHeldAttack").GetMethod("Weapon",Flags).Invoke(null,new object[]{weapon.type}))kind="CONTROLLER_STAGE";
                    else if((bool)type("HostYoyoNavigation").GetMethod("Weapon",Flags).Invoke(null,new object[]{p.HeldItem}))kind="YOYO_NAVIGATION";
                    else if((bool)type("HostWhipAttack").GetMethod("Weapon",Flags).Invoke(null,new object[]{p.HeldItem}))kind="WHIP_DERIVED";
                    else if((bool)type("HostSkyAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{weapon.type}))kind="SKY_REPRESENTATIVE";
                    else if(ContentSamples.ProjectilesByType.TryGetValue(projectile,out sample) && (bool)type("HostMeleeAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{sample}))kind="MELEE_OR_DERIVED_POSE";
                    else if(projectile==93)kind="FINITE_RANDOM_GRAVITY_WINDOW_REPRESENTATIVE";
                    else if((bool)type("HostScourgeAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{projectile})){kind="PRIMARY_CONTACT_AND_CHILD307_REPRESENTATIVE";scourgePairs++;}
                    else if((bool)type("HostEffectAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{projectile}))kind="EFFECT_REPRESENTATIVE";
                    else{var args=new object[]{snapshot,default(AttackMotion)};if((bool)model.Invoke(null,args))kind=((AttackMotion)args[1]).Confidence==AttackConfidence.Representative?"BALLISTIC_REPRESENTATIVE":"BALLISTIC_CONDITIONAL";}
                    if(kind=="UNKNOWN")unknown++;else resolved++;
                    if(melee)mixedPairs++;
                    ContentSamples.ProjectilesByType.TryGetValue(projectile,out sample);Console.WriteLine("WEAPON CATALOG item="+weapon.type+" ammo="+ammoType+" projectile="+projectile+" aiStyle="+(sample==null?-1:sample.aiStyle)+" handStage="+(melee?"MELEE_POSE":nativeMelee?"UNMODELED_MELEE_POSE":"NONE")+" stage="+kind);
                }
            }
            Require(weapons>100 && resolved>0,"coverage catalog starts from the actual full locked native item collection");
            Require(mixedPairs>0 && scourgePairs>0,"mixed hand poses retain their real shot classification and native 306 death family is registered independently");
            Console.WriteLine("WEAPON CATALOG SUMMARY eligibleWeapons="+weapons+" resolvedWeaponAmmoPairs="+resolved+" unknownPairs="+unknown+" missingAmmoGroups="+missingAmmo+" excludedTrueWeaponHandStages="+excludedHandStages+" boundary=model-registration-only; UNKNOWN-is-unfinished-not-PASS");
        }
    }
}
