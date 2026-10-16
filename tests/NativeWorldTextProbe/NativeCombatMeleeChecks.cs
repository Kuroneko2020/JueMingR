using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMeleeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            if(Environment.GetEnvironmentVariable("JMR_MELEE_ORACLE_ONLY")=="boomerang-variants"){NativeCombatBoomerangVariantChecks.Run(context);return;}
            if(Environment.GetEnvironmentVariable("JMR_MELEE_ORACLE_ONLY")=="common-boomerang"){NativeCombatBoomerangCommonChecks.Run(context);return;}
            if(Environment.GetEnvironmentVariable("JMR_MELEE_ORACLE_ONLY")=="phase"){NativeCombatPhaseChecks.Run(context);return;}
            if(Environment.GetEnvironmentVariable("JMR_MELEE_ORACLE_ONLY")!="shape-table")
            {
            LineOracle();
            AnimationOracle(context,combat,input);
            SolarMotorOracle(context,combat,input);
            BoomerangMotorOracle(context,combat,input);
            BoomerangTerrainOracle(context,combat,input);
            BoomerangReflectedContact(context,combat,host,input);
            BoomerangOtherReceive(context,combat,host,attack,input);
            NativeCombatBoomerangVariantChecks.Run(context);
            NativeCombatBoomerangCommonChecks.Run(context);
            NativeCombatPhaseChecks.Run(context);
            OwnerOffsetOracle(context,combat,host,input);
            Starlight(context,combat,host,attack,input);
            Flail(context,combat,host,attack,input);
            NativeCombatReturnChecks.Run(context);
            }
            // Native AI161 at speed2.1 cannot reach the old x780 positive.
            // Retain that exact coordinate as an additional negative and use
            // x750 for the same eight actual shortsword positive families.
            foreach(var row in new[]{new[]{3507,750,0},new[]{3501,750,0},new[]{6,750,0},new[]{3495,750,0},new[]{3513,750,0},new[]{3489,750,0},new[]{3519,750,0},new[]{3483,750,0},new[]{3507,780,0},new[]{3507,1000,0},new[]{277,780,0},new[]{4911,800,0},new[]{284,1100,0},new[]{284,870,0},new[]{3473,900,0},new[]{55,1000,0},new[]{1825,870,0},new[]{1228,1040,0},new[]{1228,960,0},new[]{4061,1040,0},new[]{4061,1020,0},new[]{5669,1040,0},new[]{277,780,1},new[]{277,780,2}})
            {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.direction=1;
            // Run the real normal base-effects phase. Without it this fixture
            // has whipRange=0/maxTags=0 and native first tag indexes -1.
            p.ResetEffects();
            // This independent row starts a fresh native animation. The bare
            // direct-Shoot oracle does not run StartActualUse, whose preceding
            // reset clears a prior row's owner-melee immunity and attackCD.
            p.ResetMeleeHitCooldowns();p.attackCD=0;
            Main.screenPosition=new Vector2(600,500);Console.WriteLine("MELEE row start ownerCD="+p.meleeNPCHitCooldown[2]);
            // The direct fractal Shoot divides by itemTime. Keep its nonzero
            // ready-window boundary (1), rather than a full cooling-down timer
            // that correctly has no new hand input capability.
            if(row[0]==5669)p.itemTime=1;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(row[1],646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            if(row[2]>0){for(int ty=20;ty<=42;ty++){Main.tile[46,ty].active(true);Main.tile[46,ty].type=Terraria.ID.TileID.Stone;}n.noTileCollide=row[2]==2;}
            // This table supplies a complete synthetic motion prefix and exact
            // native animation; it checks shape/receive semantics, not the cold
            // ordinary-Main admission budget measured in its separate windows.
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,true);
            bool distant=row[0]==3507 && row[1]>=780 || row[0]==284 && row[1]==1100 || row[0]==1228 && row[1]==1040 || row[0]==4061 && row[1]==1040 || row[2]==1;var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Console.WriteLine("MELEE premise: weapon="+row[0]+" shoot="+p.HeldItem.shoot+" speed="+p.HeldItem.shootSpeed+" animation="+p.itemAnimation+" whipRange="+p.whipRangeMultiplier+" failed="+Get(attack,"Failed"));
            if(row[0]==3473 && contact!=null)Require(contact.Confidence==AttackConfidence.Representative,"random solar arc remains a representative result");
            // Whip's false flag is a real repeated-shot branch: it rotates
            // velocity randomly and changes damage/duration, not merely audio.
            // The ordinary deterministic collision model uses the normal shot.
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),row[0]==4911});var q=Main.projectile.Single(s=>s.active && s.owner==p.whoAmI);int first=0;
            var audit=new Harmony("JueMingR.Tests.MeleeDamageFault");var methods=typeof(Projectile).GetMethods(Flags).Where(m=>m.Name.StartsWith("Damage_",StringComparison.Ordinal)).ToArray();
            foreach(var method in methods)audit.Patch(method,finalizer:new HarmonyMethod(typeof(NativeCombatMeleeChecks).GetMethod(nameof(Fault),BindingFlags.Static|BindingFlags.NonPublic)));
            try{for(int tick=1;tick<=(distant || contact==null?60:row[0]==3473?29:contact.Tick+3) && Main.projectile.Any(s=>s.active && s.owner==p.whoAmI);tick++){if(tick>1)p.itemAnimation--;for(int ps=0;ps<Main.maxProjectiles;ps++){if(!Main.projectile[ps].active)continue;Main.ProjectileUpdateLoopIndex=ps;try{Main.projectile[ps].Update(ps);}finally{Main.ProjectileUpdateLoopIndex=-1;}}if(row[0]==4911 && contact!=null && tick>=contact.Tick-1)Console.WriteLine("WHIP difference: tick="+tick+" origin="+q.Center+" velocity="+q.velocity+" ai="+q.ai[0]+" owner="+n.immune[0]+" local="+q.localNPCImmunity[2]+" bounds="+n.Hitbox+" net="+n.netOffset+" life="+n.life+" animation="+p.itemAnimation+" max="+p.itemAnimationMax+" arm="+p.GetArmPosition());if(n.life<10000){first=tick;break;}}}
            finally{foreach(var method in methods)audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
            Console.WriteLine("MELEE native outcome: weapon="+row[0]+" first="+first+" model="+contact?.Tick+" final="+q.Center+" velocity="+q.velocity+" npc="+n.Hitbox);
            Require(distant?contact==null:contact!=null,"native shape/animation respects outbound reach and owner wall: weapon="+row[0]+" wall="+row[2]);
            Require(distant?first==0:contact.Confidence==AttackConfidence.Representative?first>0:first==contact.Tick,"first natural Damage agrees with declared pose/representative model: first="+first+" expected="+contact?.Tick+" weapon="+row[0]);
            Console.WriteLine("PASS native melee prepared shape/first Damage: weapon="+row[0]+" type="+q.type+" first="+first+" expected="+contact?.Tick);
            }
        }
        private static void AnimationOracle(object context,object combat,object input)
        {
            var duration=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true).GetMethod("AnimationDuration",BindingFlags.Static|BindingFlags.NonPublic);
            var apply=typeof(Player).GetMethod("ApplyItemAnimation",Flags);
            foreach(int weapon in new[]{277,4911,3507,3473})foreach(float factor in new[]{.6f,1f,1.25f})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.meleeSpeed=factor;p.summonerWeaponSpeedBonus=factor;p.whipUseTimeMultiplier=.9f;
                int expected=(int)duration.Invoke(null,new object[]{p,p.HeldItem});apply.Invoke(p,new object[]{p.HeldItem});
                Require(expected==p.itemAnimationMax,"unborn native animation duration multiplies actual melee/summoner factors: "+weapon+"/"+factor+" model="+expected+" native="+p.itemAnimationMax);
            }
            Console.WriteLine("PASS native ApplyItemAnimation non-unit melee/whip duration matrix: 12 cases");
        }
        private static void SolarMotorOracle(object context,object combat,object input)
        {
            var pose=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true).GetMethod("SolarPose",BindingFlags.Static|BindingFlags.NonPublic);
            int cases=0,compared=0;
            foreach(float arc in new[]{-.9f,-.3f,0f,.3f,.9f})foreach(int direction in new[]{-1,1})foreach(int elapsed in new[]{0,7,28})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3473,0,0);p.position=new Vector2(700,646);p.ResetEffects();
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter, new Vector2(8*direction,0),611,50,0,p.whoAmI,0,arc);var original=Main.projectile[slot];
                for(int k=0;k<elapsed;k++)original.Update(slot);
                var shadow=new Projectile();shadow.SetDefaults(611);shadow.owner=p.whoAmI;shadow.direction=original.direction;shadow.Center=original.Center;shadow.velocity=original.velocity;shadow.ai=(float[])original.ai.Clone();shadow.localAI=(float[])original.localAI.Clone();
                for(int k=elapsed;k<30 && original.active;k++)
                {
                    bool active=shadow.ai[0]+1<30;if(active)pose.Invoke(null,new object[]{shadow,p,Vector2.Zero,0});original.Update(slot);
                    Require(active==original.active,"known Solar finite remaining AI boundary: "+arc+"/"+direction+"/"+elapsed+"/"+k);
                    if(!active)break;
                    Require(Vector2.DistanceSquared(original.Center,shadow.Center)<.0001f && Vector2.DistanceSquared(original.velocity,shadow.velocity)<.0001f && original.ai[0]==shadow.ai[0] && original.localAI[0]==shadow.localAI[0] && original.direction==shadow.direction,"known Solar copies actual random arc/clock/owner arm: "+arc+"/"+direction+"/"+elapsed+"/"+k+" native="+original.Center+" model="+shadow.Center+" velocity="+original.velocity+"/"+shadow.velocity+" facing="+original.direction+"/"+shadow.direction);compared++;
                }
                cases++;
            }
            Console.WriteLine("PASS native Solar actual arc remaining-motor matrix: cases="+cases+" subupdates="+compared);
        }
        private static void BoomerangMotorOracle(object context,object combat,object input)
        {
            var motor=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostBoomerangAttack",true).GetMethod("Step",BindingFlags.Static|BindingFlags.NonPublic);
            var handles=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostBoomerangAttack",true).GetMethod("Handles",BindingFlags.Static|BindingFlags.NonPublic);
            Require(Terraria.ID.ContentSamples.ProjectilesByType[13].aiStyle==7 && !(bool)handles.Invoke(null,new object[]{13}),"grappling Hook is not admitted as an ordinary returning weapon");
            foreach(int type in new[]{6,19,33,52,106,113,272,333,867,902,1000})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,55,0,0);p.position=new Vector2(700,646);p.ResetEffects();
                // This standalone motor oracle excludes the Reset floor;
                // real terrain contacts remain in the other native cases.
                for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                foreach(var n in Main.npc)n.active=false;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter.X+80,p.MountedCenter.Y,8,2,type,50,0,p.whoAmI);var original=Main.projectile[slot];
                var shadow=new Projectile();shadow.SetDefaults(type);shadow.owner=p.whoAmI;shadow.position=original.position;shadow.velocity=original.velocity;
                bool active=true,returned=false;int compared=0;
                for(int tick=0;tick<120 && original.active;tick++)
                {
                    for(int sub=0;sub<shadow.extraUpdates+1 && active;sub++)active=(bool)motor.Invoke(null,new object[]{shadow,p});
                    original.Update(slot);returned|=shadow.ai[0]==1;
                    Require(active==original.active,"finite boomerang native catch/kill boundary agrees: "+type+"/"+tick);
                    if(!active)break;
                    Require(Vector2.DistanceSquared(original.Center,shadow.Center)<.0001f && Vector2.DistanceSquared(original.velocity,shadow.velocity)<.0001f,"full native outbound/return motor agrees: "+type+"/"+tick+" original="+original.Center+" shadow="+shadow.Center+" velocity="+original.velocity+"/"+shadow.velocity);compared++;
                }
                Require(returned && !active && compared>=15,"explicit family reaches actual native return and catch: "+type);
                Console.WriteLine("WEAPON BOOMERANG member="+type+" originalOuterUpdates="+compared+" returnAndCatch=true");
            }
        }
        private static void BoomerangTerrainOracle(object context,object combat,object input)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
            var assembly=combat.GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBoomerangAttack",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var advance=model.GetMethod("Advance",flags);var copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);bool wind=Main.windPhysics;int cases=0,steps=0,collisions=0;
            try{foreach(int type in new[]{6,19,33,52,106,113,272,320,333,867,902,1000})foreach(int scene in Enumerable.Range(0,10))
            {
                if(type==320 && (scene==0 || scene==2))continue; // Its airborne phase has no timed return; separately checked below.
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,55,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(scene==1 || scene>=3){int wallX=type==320?62:65;for(int y=15;y<=45;y++){Main.tile[wallX,y].active(true);Main.tile[wallX,y].type=1;}}
                if(scene==2)for(int x=40;x<=80;x++){Main.tile[x,25].active(true);Main.tile[x,25].type=19;Main.tile[x,25].frameY=0;}
                if(scene>=3 && scene<=5)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==3?0:scene==4?2:1);}
                if(scene>=6){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-5));}
                Vector2 origin=scene>=6?new Vector2(936,390):new Vector2(950,350),velocity=scene>=6?new Vector2(20,8):new Vector2(8,3);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,0,0,p.whoAmI);var actual=Main.projectile[slot];var shadow=(Projectile)copy.Invoke(actual,null);shadow.ai=(float[])actual.ai.Clone();shadow.localAI=(float[])actual.localAI.Clone();var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");bool alive=true,terrainHit=false,returned=false;
                for(int outer=1;outer<=180 && actual.active;outer++)
                {
                    int updates=actual.extraUpdates+1;for(int sub=0;sub<updates && alive;sub++){var before=shadow.velocity;float outbound=shadow.ai[0];var args=new object[]{shadow,p,terrain,alive};Require((bool)advance.Invoke(null,args),"boomerang terrain motor retains its actual finite dependencies: "+type+"/"+scene+"/"+outer);alive=(bool)args[3];if(outbound==0 && (shadow.velocity.X*before.X<0 || shadow.velocity.Y*before.Y<0))terrainHit=true;steps++;}actual.Update(slot);returned|=shadow.ai[0]==1;
                    Require(alive==actual.active,"boomerang native terrain return/catch boundary agrees: "+type+"/"+scene+"/"+outer);if(!alive)break;
                    Require(Vector2.DistanceSquared(actual.Center,shadow.Center)<.002f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.ai[0]==shadow.ai[0] && actual.ai[1]==shadow.ai[1] && actual.tileCollide==shadow.tileCollide && actual.wet==shadow.wet && actual.honeyWet==shadow.honeyWet && actual.lavaWet==shadow.lavaWet,"boomerang full native tile/liquid/slope outbound and return agree: "+type+"/"+scene+"/"+outer+" actual="+actual.Center+"/"+actual.velocity+" ai="+actual.ai[0]+","+actual.ai[1]+" model="+shadow.Center+"/"+shadow.velocity+" ai="+shadow.ai[0]+","+shadow.ai[1]);
                }
                Require(returned,"native terrain member retains a real return phase: "+type+"/"+scene);if(terrainHit)collisions++;cases++;Console.WriteLine("BOOMERANG TERRAIN type="+type+" scene="+scene+" reflection="+terrainHit+" caught="+!actual.active);
            }}finally{Main.windPhysics=wind;}
            Require(collisions>20,"native matrix really exercises axis reflections and outbound-to-return collision transitions");Console.WriteLine("PASS boomerang full native terrain: "+cases+" cases/"+steps+" substeps/"+collisions+" reflection cases");
            var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1825,0,0);player.position=new Vector2(700,646);player.ResetEffects();for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var npc in Main.npc)npc.active=false;int airborneSlot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),950,350,8,-2,320,0,0,player.whoAmI);var airborne=Main.projectile[airborneSlot];var privateShot=(Projectile)copy.Invoke(airborne,null);privateShot.ai=(float[])airborne.ai.Clone();privateShot.localAI=(float[])airborne.localAI.Clone();
            try{Main.windPhysics=false;for(int outer=1;outer<=40;outer++){Require((bool)model.GetMethod("Step",flags).Invoke(null,new object[]{privateShot,player}),"BloodyMachete airborne gravity remains active past ordinary return clock");airborne.Update(airborneSlot);Require(airborne.active && airborne.ai[0]==0 && privateShot.ai[0]==0 && airborne.ai[1]==privateShot.ai[1] && Vector2.DistanceSquared(airborne.Center,privateShot.Center)<.002f && Vector2.DistanceSquared(airborne.velocity,privateShot.velocity)<.0001f,"native BloodyMachete10-step gravity/X-drag/cap and absent30-step return agree: "+outer);}}finally{Main.windPhysics=wind;}
            Console.WriteLine("PASS BloodyMachete native airborne gravity stays outbound after step30: 40 updates");
        }
        private static void BoomerangReflectedContact(object context,object combat,object host,object input)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;var assembly=combat.GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBoomerangAttack",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);bool wind=Main.windPhysics;
            try{foreach(int type in new[]{33,106})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,55,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();for(int y=15;y<=45;y++){Main.tile[65,y].active(true);Main.tile[65,y].type=1;}
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(970,399);n.velocity=n.netOffset=Vector2.Zero;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;n.life=n.lifeMax=10000;n.defense=0;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,8,3,type,40,0,p.whoAmI);var actual=Main.projectile[slot];var shadow=(Projectile)copy.Invoke(actual,null);shadow.ai=(float[])actual.ai.Clone();shadow.localAI=(float[])actual.localAI.Clone();var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var key=new NpcIdentity((long)Get(host,"Session"),n,2,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true,Active=true};var points=Enumerable.Range(0,181).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);AttackContact contact=null;bool alive=true,reflected=false;int first=0;
                for(int outer=1;outer<=120 && actual.active;outer++)
                {
                    for(int sub=0;sub<actual.extraUpdates+1 && alive;sub++){float before=shadow.velocity.X;var args=new object[]{shadow,p,terrain,alive};Require((bool)model.GetMethod("Advance",flags).Invoke(null,args),"native reflected receiver retains finite terrain");alive=(bool)args[3];reflected|=before*shadow.velocity.X<0;if(alive && contact==null)contact=AttackIntercept.BodyContact(timeline,outer,sub,10000,350,shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height,AttackConfidence.Conditional);}
                    NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);actual.Update(slot);if(n.life<10000){first=outer;break;}
                }
                Require(reflected && first>0 && contact!=null && first==contact.Tick,"native post-wall axis reflection causes its actual first Damage at the predicted receive: "+type+" native="+first+" contact="+contact?.Tick);Console.WriteLine("PASS native reflected boomerang contact type="+type+" firstDamage="+first+" contact="+contact.Tick);
            }}finally{Main.windPhysics=wind;}
        }
        private static void BoomerangOtherReceive(object context,object combat,object host,object attack,object input)
        {
            foreach(bool immune in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4818,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);
                foreach(int slot in new[]{1,2}){var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=new Vector2(slot==1?820:930,646);n.velocity=n.netOffset=Vector2.Zero;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;n.life=n.lifeMax=10000;n.defense=0;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);}var front=Main.npc[1];var target=Main.npc[2];front.immune[p.whoAmI]=immune?100:0;
                // Select the rear receiver by the physical mouse. Player-
                // center nearest correctly selects the intervening body and
                // cannot serve as this explicit other-receiver experiment.
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,target.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0,true);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(immune?contact!=null:contact==null,"902 return comes from actual qualified earlier receiver, not an immune geometric overlap: "+immune);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shot=Main.projectile.Single(q=>q.active && q.owner==p.whoAmI);Require(shot.type==902,"actualTrimarang hand stage preserves native902");int first=0,frontAt=0,cooldown=0;bool returnAtFront=false;
                for(int outer=1;outer<=80 && shot.active;outer++){NativeQuickItemChecks.BeginWorldStep();front.UpdateNPC(1);target.UpdateNPC(2);shot.Update(shot.whoAmI);if(frontAt==0 && front.life<10000){frontAt=outer;cooldown=front.immune[p.whoAmI];returnAtFront=shot.ai[0]==1;}if(target.life<10000){first=outer;break;}}
                Require(immune?first>0 && frontAt==0 && first==contact.Tick:first==0 && frontAt>0 && cooldown==7 && returnAtFront,"native902 causal OnHit has seven owner immunity and return before selected receive: immune="+immune+" frontAt="+frontAt+" cooldown="+cooldown+" nativeFirst="+first+" contact="+contact?.Tick);Console.WriteLine("PASS native902 front receiver immune="+immune+" frontAt="+frontAt+" selectedFirst="+first+" predicted="+contact?.Tick);
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void LineOracle()
        {
            foreach(int type in new[]{3,414})
            {
                var key=new NpcIdentity(1,new object(),2,1,type,type);var state=new NpcMotionState{Identity=key,X=170.9f,Y=100.4f,NetOffsetX=-10.6f,NetOffsetY=-.5f,Width=18,Height=12,CanReceive=true};var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,new[]{new NpcTrajectoryPoint(0,state)},1,PredictionStrategy.RollingConditional);
                var b=timeline[0].ProjectileReceiveBounds;var rectangle=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);var delta=rectangle.ClosestPointInRect(new Vector2(100,100))-new Vector2(100,100);delta.Y/=.8f;bool expected=delta.Length()<=55;
                Require(BitConverter.ToInt32(BitConverter.GetBytes(timeline[0].OwnerBounds.X),0)==BitConverter.ToInt32(BitConverter.GetBytes(state.X+state.NetOffsetX),0) && BitConverter.ToInt32(BitConverter.GetBytes(timeline[0].OwnerBounds.Y),0)==BitConverter.ToInt32(BitConverter.GetBytes(state.Y+state.NetOffsetY),0),"compact timeline retains floating offset body distinct from integer damage rectangle");
                Require((AttackIntercept.EllipseContact(timeline,0,900,900,100,100,55,.8f,.4f)!=null)==expected,"flail ellipse uses original integer offset/type414 projectile receive frame: "+type);
            }
            int comparisons=0;
            foreach(float angle in new[]{0f,.2f,-.7f,1.57f})foreach(float length in new[]{5f,100f,700f})foreach(float offset in new[]{-22f,-8f,0f,7.9f,8f,16f,35f})
            {
                var origin=new Vector2(100,100);var end=origin+angle.ToRotationVector2()*length;var box=new Vector2(120,100+offset);var dimensions=new Vector2(18,12);float collision=0;
                bool expected=Collision.CheckAABBvLineCollision(box,dimensions,origin,end,16,ref collision);var key=new NpcIdentity(1,new object(),2,1,3,3);var point=new NpcTrajectoryPoint(0,new NpcMotionState{Identity=key,X=box.X,Y=box.Y,Width=18,Height=12,CanReceive=true});var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,new[]{point},1);
                var contact=AttackIntercept.LineContact(timeline,0,900,900,origin.X,origin.Y,end.X,end.Y,16);Require((contact!=null)==expected,"finite strip matches original pure geometry at rectangle edge: "+angle+"/"+length+"/"+offset);comparisons++;
            }
            Console.WriteLine("PASS original pure finite-strip geometry: comparisons="+comparisons);
        }
        private static void OwnerOffsetOracle(object context,object combat,object host,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,277,0,0);p.position=new Vector2(700,646);p.ResetEffects();
            var n=new NPC();n.SetDefaults(3);n.position=new Vector2(780.9f,646.4f);n.netOffset=new Vector2(-10.6f,-.5f);var q=new Projectile();q.SetDefaults(47);q.owner=0;q.Center=p.MountedCenter;q.ownerHitCheck=true;
            var key=new NpcIdentity(1,n,2,1,3,3);var point=new NpcTrajectoryPoint(0,new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y,Width=n.width,Height=n.height,CanReceive=true});
            var terrain=Activator.CreateInstance(combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain"),true);var allows=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack").GetMethod("OwnerAllows",BindingFlags.Static|BindingFlags.NonPublic);
            float distance=Vector2.Distance(q.Center,n.Center+n.netOffset);
            foreach(float margin in new[]{-.05f,.05f})
            {
                q.ownerHitCheckDistance=distance+margin;var position=n.position;bool native;try{n.position+=n.netOffset;native=q.CanHitWithMeleeWeapon(n);}finally{n.position=position;}
                Require((bool)allows.Invoke(null,new object[]{p,q,point.OwnerBounds,false,terrain})==native,"owner gate preserves exact floating offset body at distance boundary: "+margin);
            }
            Require(!(bool)allows.Invoke(null,new object[]{p,q,point.Bounds,false,terrain}),"unshifted motion body is a distinct incorrect owner-distance premise");Console.WriteLine("PASS original owner gate floating netOffset/distance, distinct from integer Colliding");
        }
        private static void Starlight(object context,object combat,object host,object attack,object input)
        {
            foreach(bool wall in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4923,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.itemAnimationMax=p.HeldItem.useAnimation;Main.screenPosition=new Vector2(600,500);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(790,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                if(wall)for(int y=20;y<=42;y++){Main.tile[46,y].active(true);Main.tile[46,y].type=Terraria.ID.TileID.Stone;}
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.type==927);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
                Require(wall?plan==null:plan!=null,"starlight contact uses its native short shape and owner wall qualification");
                int first=-1;for(int k=0;k<12;k++){q.Update(q.whoAmI);if(n.life<10000){first=k;break;}}
                Require(wall?first<0:first==plan.Tick,"starlight first natural Damage matches its declared contact: actual="+first+" expected="+plan?.Tick);
                p.channel=false;q.AI();Require(!q.active,"starlight release retains original channel exit");Console.WriteLine("PASS native starlight contact/wall/release: first="+first+" expected="+plan?.Tick+" wall="+wall);
            }
        }
        private static void Flail(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,162,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.aiStyle==15);q.Update(q.whoAmI);Require(n.life==10000,"initial flail spin cannot damage before original localAI warmup");
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;NativeCombatAimChecks.Prepare(host,attack,n,0,true);int x=Main.mouseX,y=Main.mouseY;var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            q.Update(q.whoAmI);Require(q.ai[0]==1 && q.velocity.X>0 && Main.mouseX==x && Main.mouseY==y,"flail's real release AI borrows prepared direction then returns physical coordinates");
            Require(plan!=null,"flail release has a finite native outbound contact");int first=n.life<10000?0:-1;for(int k=1;k<20 && first<0;k++){q.Update(q.whoAmI);if(n.life<10000)first=k;}
            Require(first==plan.Tick,"flail first native outbound Damage matches timeline: actual="+first+" expected="+plan.Tick);Console.WriteLine("PASS native flail warmup/release/contact: first="+first+" expected="+plan.Tick);
            foreach(var shot in Main.projectile)shot.active=false;p.channel=p.controlUseItem=true;n.position.X=755;n.life=10000;Array.Clear(n.immune,0,n.immune.Length);NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});q=Main.projectile.Single(s=>s.active && s.aiStyle==15);NativeCombatAimChecks.Prepare(host,attack,n,0,true);plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Console.WriteLine("FLAIL spin premise: plan="+plan?.Tick+" local="+q.localAI[1]+" state="+q.ai[0]+" channel="+p.channel+" mounted="+p.MountedCenter+" target="+n.Hitbox+" owner="+n.immune[0]+" localimmune="+q.localNPCImmunity[2]);Require(plan!=null && plan.Tick>=12,"spin contact respects original thirteen-step warmup");first=-1;
            for(int k=0;k<20 && first<0;k++){q.Update(q.whoAmI);if(n.life<10000)first=k;}
            Require(first==plan.Tick,"flail spin ellipse and actual warmup agree: first="+first+" expected="+plan.Tick);Console.WriteLine("PASS native flail warmup/ellipse contact: first="+first+" expected="+plan.Tick);
        }
        private static Exception Fault(Exception __exception,MethodBase __originalMethod)
        {if(__exception!=null)Console.WriteLine("MELEE original fault: "+__originalMethod.Name+" "+__exception);return __exception;}
    }
}
