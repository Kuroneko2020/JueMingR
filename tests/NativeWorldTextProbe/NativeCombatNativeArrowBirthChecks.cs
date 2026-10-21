using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Public representative input, native birth adapters and sealed ordered
    // ammo replay are separate obligations. Components control legal primitive
    // samples; actual-use opportunities leave ordinary native RNG untouched.
    internal static class NativeCombatNativeArrowBirthChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static object context;private static bool lottery;private static int depth,rng,step,first,bounceHits,charmHits,firstType;private static Projectile damaging;private static bool ordinary,watchBirth;private static int ordinaryWorld,ordinaryStarts,ordinaryEnhanced,ordinaryBasic,ordinaryStrikes,ordinaryRecovery,ordinaryEnhancedTiles;private static Projectile[] preparedBirth;private static Vector2 preparedOrigin,preparedAim;private static Player preparedPose;private static int modelCalls,birthCalls,modelPath,ordinaryEnhancedStrikes,isolatedCalls,completeCalls,firstWidth,preparedStage,nativeStage,nativeStageCalls;private static bool watchNativeStage,modelBudget;private static AttackContact modelContact;private static string modelStop;private static bool modelPaused;private static bool trackFan;private static readonly Dictionary<string,string> ordinaryKinds=new Dictionary<string,string>();private static readonly List<Projectile> actualFan=new List<Projectile>(),privateFan=new List<Projectile>();
        private static readonly int[] Ammo={40,41,47,51,265,516,545,988,1235,1334,1341,3003,3103,3568,5348};
        private static object Static(string type,string method,params object[] args){return host.GetType("JueMingR.TerrariaHost.Combat."+type,true).GetMethod(method,Flags).Invoke(null,args);}
        private static bool NextInt(object[] __args,ref int __result)
        {if(depth>0){rng++;return true;}if(!lottery)return true;int min=__args.Length==2?(int)__args[0]:0,max=__args.Length==0?int.MaxValue:(int)__args.Last();__result=min==75 && max==150?100:min+(max-min)/2;return false;}
        private static int Draw(Terraria.Utilities.UnifiedRandom random,int max){int n=0;return NextInt(new object[]{max},ref n)?random.Next(max):n;}
        private static int Draw(Terraria.Utilities.UnifiedRandom random,int min,int max){int n=0;return NextInt(new object[]{min,max},ref n)?random.Next(min,max):n;}
        private static bool NextDouble(ref double __result){if(depth>0){rng++;return true;}if(!lottery)return true;__result=1073741824d/int.MaxValue;return false;}
        private static double DrawDouble(Terraria.Utilities.UnifiedRandom random){double value=0;return NextDouble(ref value)?random.NextDouble():value;}
        private static float DrawFloat(Terraria.Utilities.UnifiedRandom random){if(depth>0){rng++;return random.NextFloat();}return lottery?.5f:random.NextFloat();}
        private static IEnumerable<CodeInstruction> Inputs(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var ins in instructions)
            {
                var method=ins.operand as MethodInfo;MethodInfo replacement=null;
                if((ins.opcode==OpCodes.Call || ins.opcode==OpCodes.Callvirt) && method!=null)
                {
                    if(method.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && method.Name=="Next" && method.GetParameters().Length>0)replacement=typeof(NativeCombatNativeArrowBirthChecks).GetMethod(nameof(Draw),Flags,null,new[]{typeof(Terraria.Utilities.UnifiedRandom)}.Concat(method.GetParameters().Select(p=>p.ParameterType)).ToArray(),null);
                    else if(method.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && method.Name=="NextDouble")replacement=typeof(NativeCombatNativeArrowBirthChecks).GetMethod(nameof(DrawDouble),Flags);
                    else if(method.DeclaringType==typeof(Utils) && method.Name=="NextFloat" && method.GetParameters().Length==1)replacement=typeof(NativeCombatNativeArrowBirthChecks).GetMethod(nameof(DrawFloat),Flags);
                }
                if(replacement!=null){ins.opcode=OpCodes.Call;ins.operand=replacement;}yield return ins;
            }
        }
        private static void Damage(Projectile __instance){damaging=__instance;}private static void DamageDone(){damaging=null;}
        private static void Strike(NPC __instance,int __result){if(ordinary && __result>0 && __instance.whoAmI==2 && damaging!=null){ordinaryStrikes++;string kind;if(ordinaryKinds.TryGetValue(damaging.key.ToString(),out kind) && kind=="Enhanced"){ordinaryEnhancedStrikes++;if(damaging.type==225 && damaging.ai[1]>0)ordinaryEnhancedTiles++;}}if(__result>0 && __instance.whoAmI==2 && damaging!=null && first==0){first=step;firstType=damaging.type;firstWidth=damaging.width;if(new Projectile.AI_001_Features(damaging.ai[2]).HarpyCharmed)charmHits++;if(damaging.type==225 && damaging.ai[1]>0)bounceHits++;}}
        private static Player Scene(int weapon,int ammunition,int direction=1,bool harpy=false)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);
            p.position=new Vector2(850,1000);p.velocity=Vector2.Zero;p.ResetEffects();p.direction=direction;p.gravDir=1;p.inventory[54].SetDefaults(ammunition);p.inventory[54].stack=999;p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.itemTime=0;p.accHarpyCharm=harpy;p.accHarpyCharmCooldown=0;p.magicQuiver=false;p.phantasmTime=0;Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=3;x<Main.maxTilesX-3;x++)for(int y=3;y<Main.maxTilesY-3;y++)Main.tile[x,y].ClearEverything();first=step=bounceHits=charmHits=firstType=firstWidth=0;return p;
        }
        private static NPC Target(Vector2 center,bool receive=true)
        {var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=center;n.width=n.height=34;n.life=n.lifeMax=100000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.realLife=-1;n.friendly=n.immortal=false;n.dontTakeDamage=!receive;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Path(NPC n)
        {var id=(NpcIdentity)Static("CombatSelection","Identity",n,1L);var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=!n.dontTakeDamage,NoTileCollide=true};var rows=Enumerable.Range(0,121).Select(t=>{var point=state;point.X+=n.velocity.X*t;point.Y+=n.velocity.Y*t;point.Vx=n.velocity.X;point.Vy=n.velocity.Y;return new NpcTrajectoryPoint(t,point);}).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,rows,121,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Root(Player p,object ammo,NpcTrajectory path){return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,ContentSamples.ProjectilesByType[(int)Get(ammo,"Projectile")],path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc")},null);}
        private static object Capture(Player p){return Static("AttackAmmoSnapshot","Capture",p,p.HeldItem);}
        private static Projectile[] Births(Player p,object ammo,Vector2 aim,int stage)
        {depth++;try{return (Projectile[])Static("HostNativeArrowBirthAttack","Births",p,ammo,Static("HostAttackModels","LaunchOrigin",p,p.HeldItem),aim,stage,Terrain());}finally{depth--;}}
        private static void Shoot(Player p,Vector2 aim,bool representative)
        {Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(p.gravDir==-1 && !(bool)Static("HostSkyAttack","RawCursor",p.HeldItem.type)?Main.screenHeight-(aim.Y-Main.screenPosition.Y):aim.Y-Main.screenPosition.Y);lottery=representative;try{typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});}finally{lottery=false;}}
        private static void Advance(Player p,int limit)
        {for(step=1;step<=limit && first==0;step++){NativeQuickItemChecks.BeginWorldStep();if(step>1)typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int i=0;i<1000;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;try{Main.projectile[i].Update(i);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        private static string Tuple(Projectile p){return "type="+p.type+" owner="+p.owner+" slot="+p.whoAmI+" center="+p.Center+" V="+p.velocity+" dmg="+p.damage+" KB="+p.knockBack.ToString("R")+" ai="+string.Join(",",p.ai)+" extra="+p.extraUpdates+" noDrop="+p.noDropItem+" aiStyle="+p.aiStyle+" arrow="+p.arrow+" left="+p.timeLeft+" pen="+p.penetrate;}
        private static void BirthMatrix()
        {
            int count=0;foreach(int weapon in new[]{1229,2624,3029,4953})foreach(int ammunition in Ammo)foreach(int direction in new[]{-1,1})for(int stage=0;stage<(weapon==4953?5:1);stage++)
            {
                var p=Scene(weapon,ammunition,direction);p.fullRotation=.4f;p.magicQuiver=ammunition==1235;p.itemAnimation-=stage*2;var ammo=Capture(p);var aim=p.Center+new Vector2(250*direction,100);var expected=Births(p,ammo,aim,stage);Require(expected!=null,"native birth read set available");Shoot(p,aim,true);var actual=Main.projectile.Where(q=>q.active).OrderBy(q=>q.whoAmI).ToArray();Require(actual.Length==expected.Length,"native simultaneous count/ordinal class "+weapon+"/"+ammunition+"/"+stage);
                for(int i=0;i<actual.Length;i++){var a=actual[i];var b=expected[i];Require(a.type==b.type && a.owner==b.owner && a.damage==b.damage && a.knockBack==b.knockBack && Vector2.Distance(a.Center,b.Center)<.002f && Vector2.Distance(a.velocity,b.velocity)<.002f && a.ai[0]==b.ai[0] && a.ai[1]==b.ai[1] && a.extraUpdates==b.extraUpdates && a.noDropItem==b.noDropItem,"exact native birth scalar "+weapon+"/"+ammunition+"/"+stage+"/"+i+" native=["+Tuple(a)+"] model=["+Tuple(b)+"]");}
                count++;
            }Console.WriteLine("NATIVE ARROW birth hands="+count+" actual selected ammo15/facing2/real4953ordinal5/native count+all scalar, rotation.4/quiver");
        }
        private static void ParameterBirths()
        {
            foreach(int weapon in new[]{1229,2624,3029,4953})
            {
                var p=Scene(weapon,40);p.magicQuiver=p.hasMoltenQuiver=p.archery=p.accSharpBarb=p.kbBuff=p.shroomiteStealth=true;p.stealth=.25f;var ammo=Capture(p);var aim=p.Center+new Vector2(230,100);var expected=Births(p,ammo,aim,0);Shoot(p,aim,true);var actual=Main.projectile.Where(q=>q.active).OrderBy(q=>q.whoAmI).ToArray();Require(actual.Length==expected.Length,"parameter hand has exact native count");
                for(int i=0;i<actual.Length;i++)Require(actual[i].type==expected[i].type && actual[i].damage==expected[i].damage && actual[i].knockBack==expected[i].knockBack && Vector2.Distance(actual[i].velocity,expected[i].velocity)<.002f,"native complete PickAmmo Quiver/Molten/SharpBarb/Archery and player knockback order "+weapon+" native=["+Tuple(actual[i])+"] model=["+Tuple(expected[i])+"]");
                p.kbBuff=false;Require(!(bool)Call(ammo,"MembersMatch",p,p.HeldItem),"sameGUC effective player knockback change revokes native birth");p.kbBuff=true;float old=p.inventory[54].knockBack;p.inventory[54].knockBack+=1;Require(!(bool)Call(ammo,"MembersMatch",p,p.HeldItem),"sameGUC selected ammo knockback edit revokes native birth");p.inventory[54].knockBack=old;
            }
            Console.WriteLine("NATIVE ARROW PickAmmo state/order actualHands=4 ammo/bonus knockback revocations=8");
        }
        private static void Bounce(int weapon,bool harpy)
        {
            var p=Scene(weapon,1235,1,harpy);var aim=weapon==3029?new Vector2(1000,1000):new Vector2(1300,1021);var n=Target(weapon==3029?new Vector2(1140,720):new Vector2(990,900));
            if(weapon==3029)for(int x=53;x<69;x++)NativeToolsChecks.Tile(x,55,1);else for(int y=48;y<73;y++)NativeToolsChecks.Tile(69,y,1);
            var path=Path(n);var ammo=Capture(p);var root=Root(p,ammo,path);var terrain=Terrain();var born=Births(p,ammo,aim,0);AttackContact contact;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)Static("HostHeldAmmoFamilyAttack","ExplicitBirthBatch",p,born,path,Clock(path),terrain,root,aim,0,null,null,true);}finally{depth--;}
            Require(contact!=null,"special native225 birth has a complete tile-retarget receive "+weapon+"/"+harpy);Shoot(p,aim,true);Advance(p,120);Require(first==contact.Tick && first>0 && (bounceHits>0 || harpy && charmHits>0),"actual special225 tile reflection/LOS retarget reaches selected at exact firstStrike "+weapon+"/"+harpy+" native="+first+" model="+contact.Tick);Console.WriteLine("NATIVE ARROW bounce weapon="+weapon+" harpy="+harpy+" nativeFirst="+first+" model="+contact.Tick+" actualTileHits="+bounceHits+" actualCharmHits="+charmHits);
        }
        private static void KillStages(int type,bool harpy)
        {
            int ammunition=type==91?516:type==41?265:3568;var p=Scene(4953,ammunition,1,harpy);for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);for(int y=40;y<74;y++)NativeToolsChecks.Tile(69,y,1);
            if(harpy)p.accHarpyCharmCooldown=90;var n=Target(type==639?new Vector2(1020,930):type==41?new Vector2(1120,1056):new Vector2(1090,350));if(type==639)n.velocity=new Vector2(0,3);var aim=new Vector2(1300,1021);var path=Path(n);var ammo=Capture(p);Require((int)Get(ammo,"Projectile")==type,"selected ammo resolves expected Kill family");var root=Root(p,ammo,path);var born=Births(p,ammo,aim,0);AttackContact contact;
            depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)Static("HostHeldAmmoFamilyAttack","ExplicitBirthBatch",p,born,path,Clock(path),Terrain(),root,aim,0,null,null,true);}finally{depth--;}
            Require(contact!=null,"actual special birth has a useful future Kill family stage type="+type+" harpy="+harpy);Shoot(p,aim,true);lottery=true;try{Advance(p,120);}finally{lottery=false;}
            int child=type==91?92:type==41?41:640;Require(first>0 && firstType==child && first==contact.Tick && (type!=41 || firstWidth==64),"native special hand -> AI/tile Kill -> child-only selected firstStrike type="+type+" harpy="+harpy+" first="+first+" hitType="+firstType+" plan="+contact.Tick);
            Console.WriteLine("NATIVE ARROW Kill family="+type+" Harpy="+harpy+" child="+child+" first="+first+" predicted="+contact.Tick);
        }
        private static void PublicHands(bool inverted=false)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            foreach(int weapon in new[]{1229,2624,3029,4953})foreach(bool harpy in new[]{false,true})for(int ordinal=0;ordinal<(weapon==4953?2:1);ordinal++)
            {
                var p=Scene(weapon,1235,1,harpy);p.gravDir=inverted?-1:1;if(inverted)p.fullRotation=.4f;if(ordinal==1)p.itemAnimation-=4;var n=Target(p.Center+new Vector2(220,0));var physical=p.Center-new Vector2(300,100);NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),physical,true);Call(combat,"Sample");
                depth++;try{NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0,phaseName:"BeforeNpc");}finally{depth--;}
                if(weapon==3029 && !harpy)SkySteeringCheckpoint(attack,p);
                var solution=NativeCombatAimChecks.RequireSolution(attack,p,"special native-birth public preparation supplies its current representative point "+weapon+"/"+harpy);Require(!(bool)Get(attack,"hasConsumedInput"),"native-arrow query does not consume a real hand");Shoot(p,physical,true);
                NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"real original Shoot consumes this special native-birth Enhanced input "+weapon+"/"+harpy);
                var shots=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI).ToArray();object root;var contact=Known(p,shots,Path(n),out root);Require(contact!=null,"sealed actual special-arrow births retain a complete useful replay");Advance(p,120);Require(first>0 && first==contact.Tick,"actual Enhanced special native birth firstDamage equals sealed replay "+weapon+"/"+harpy+" first="+first+" known="+contact.Tick);
                Console.WriteLine("NATIVE ARROW public Enhanced weapon="+weapon+" Harpy="+harpy+" inverted="+inverted+" ordinal="+(ordinal==1?2:0)+" first="+first+" known="+contact.Tick+" representative="+solution.NativeTick+" mechanismScope1000=True");
            }
        }
        private static void SkySteeringCheckpoint(object attack,Player player)
        {
            NativeCombatAimChecks.SolutionCheckpoint(context,attack,"native-sky-steering");var solution=NativeCombatAimChecks.ReadSolution(attack);var ammo=Get(attack,"ammo");var origin=(Vector2)Static("HostAttackModels","LaunchOrigin",player,player.HeldItem);
            var births=Births(player,ammo,solution.Point,0);var shot=births[0];var targets=Static("HostProjectileTargets","Capture",shot,solution.Timeline.Identity.Session);
            var args=new object[]{ammo,default(AttackMotion)};var representative=host.GetType("JueMingR.TerrariaHost.Combat.HostRepresentativeAttack",true);
            Require((bool)representative.GetMethods(Flags).Single(m=>m.Name=="TryMotion" && m.GetParameters().Length==2).Invoke(null,args),"sky steering regression has the actual resolved motion");
            var replay=representative.GetMethod("Replay",Flags);var absent=new object[]{player,shot,solution.Timeline,solution.FirstTick,Terrain(),args[1],0,0f,null};var without=replay.Invoke(null,absent);
            var present=new object[]{player,Births(player,ammo,solution.Point,0)[0],solution.Timeline,solution.FirstTick,Terrain(),args[1],0,0f,targets};var with=replay.Invoke(null,present);
            Console.WriteLine("NATIVE SKY STEERING current="+solution.Reachability+" targets="+Get(targets,"Count")+" omitted="+without+" captured="+with+" receive="+present[6]+" rawPoint="+solution.Point+" origin="+origin);
            Require((int)Get(targets,"Count")>0 && without.ToString()=="Unknown" && with.ToString()=="Reachable","same legal native sky birth needs its captured steering targets, not an unknown-target exemption");
        }
        private static NPC WallTarget(int weapon)
        {
            var n=Target(weapon==3029?new Vector2(1140,720):new Vector2(990,900));
            if(weapon==3029){for(int x=53;x<69;x++)NativeToolsChecks.Tile(x,55,1);for(int y=26;y<38;y++)NativeToolsChecks.Tile(68,y,1);}
            else{for(int y=48;y<73;y++)NativeToolsChecks.Tile(69,y,1);for(int x=53;x<61;x++)NativeToolsChecks.Tile(x,60,1);}
            return n;
        }
        private static void PublicWallHands()
        {
            int publicRows=0,dedicatedRows=0;
            foreach(int weapon in new[]{4953,3029})foreach(bool harpy in new[]{false,true})
            {
                var p=Scene(weapon,1235,1,harpy);var n=WallTarget(weapon);
                var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var physical=p.Center-new Vector2(300,100);NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),physical,true);Call(combat,"Sample");
                depth++;try{NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0,phaseName:"BeforeNpc");}finally{depth--;}
                NativeCombatAimChecks.SolutionCheckpoint(context,attack,"native-wall-"+weapon+"/"+harpy);var solution=NativeCombatAimChecks.ReadSolution(attack);var available=NativeCombatAimChecks.QueryInput(attack,p);Require(solution.HasPoint && available.Allowed && available.Kind=="Enhanced" && available.Point==solution.Point && available.Target.Equals(solution.Timeline.Identity),"wall witness consumes the actual representative input before its native-result gate");Require(!(bool)Get(attack,"hasConsumedInput"),"native-arrow wall query does not consume a real hand");Shoot(p,physical,true);NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"composed public wall route consumes this actual Enhanced input");var shots=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI).ToArray();Require(shots.Length>0,"public wall current input creates real native sources");object root;var contact=Known(p,shots,Path(n),out root);Advance(p,120);
                Console.WriteLine("NATIVE ARROW public wall weapon="+weapon+" Harpy="+harpy+" first="+first+" known="+contact?.Tick+" representative="+solution.NativeTick+" reach="+solution.Reachability+" tile="+bounceHits+" charm="+charmHits+" mechanismScope1000=True");
                // Finite public candidates own their actual direction and
                // receive result; Blocked does not promise a different bounce
                // route. A zero receive is recorded, never called useful.
                Require(first==0?contact==null:contact!=null && first==contact.Tick,"public wall actual births preserve exact complete Known/real Damage including a naturally absent receive weapon="+weapon+" Harpy="+harpy);publicRows++;

                // Preserve the separate useful reflection obligation on the
                // SAME wall/platform. These physical inputs already belong to
                // Bounce above; no search of current representative outputs.
                p=Scene(weapon,1235,1,harpy);n=WallTarget(weapon);var aim=weapon==3029?new Vector2(1000,1000):new Vector2(1300,1021);var path=Path(n);var ammo=Capture(p);var declaredRoot=Root(p,ammo,path);var born=Births(p,ammo,aim,0);AttackContact declared;
                depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))declared=(AttackContact)Static("HostHeldAmmoFamilyAttack","ExplicitBirthBatch",p,born,path,Clock(path),Terrain(),declaredRoot,aim,0,null,null,true);}finally{depth--;}
                Shoot(p,aim,true);shots=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI).ToArray();Require(shots.Length==born.Length,"AimOFF same-wall physical hand retains its declared native birth count");contact=Known(p,shots,path,out root);Advance(p,120);
                Console.WriteLine("NATIVE ARROW dedicated same-wall weapon="+weapon+" Harpy="+harpy+" aim="+aim+" actualTargetCenter="+n.Center+" first="+first+" declared="+declared?.Tick+" known="+contact?.Tick+" tile="+bounceHits+" charm="+charmHits+" AimOFF=True");
                Require(first>0 && declared!=null && contact!=null && first==declared.Tick && first==contact.Tick && (bounceHits>0 || harpy && charmHits>0),"predeclared physical input retains useful same-wall native tile/Charm exact receive weapon="+weapon+" Harpy="+harpy+" native="+first+" declared="+declared?.Tick+" known="+contact?.Tick);dedicatedRows++;
            }
            Require(publicRows==4 && dedicatedRows==4,"all four public directions and independent dedicated same-wall positives retain their fixed denominators");
        }
        private static object RootShot(Player p,Projectile shot,NpcTrajectory path)
        {return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,true,false},null);}
        private static AttackContact Known(Player p,Projectile[] shots,NpcTrajectory path,out object root)
        {root=RootShot(p,shots[0],path);depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)Static("HostHeldAmmoFamilyAttack","KnownFamily",p,shots,path,Clock(path),Terrain(),root);}finally{depth--;}}
        private static Projectile Spawn(Player p,int type,Vector2 center,Vector2 velocity,int damage=80)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,damage,0,p.whoAmI)];}
        private static void StreakCollider()
        {
            var p=Scene(4953,40);var q=Spawn(p,932,new Vector2(1000,900),new Vector2(8,0));q.rotation=.7f;int count=0;
            for(int x=-80;x<=80;x+=4)for(int y=-80;y<=80;y+=4)
            {var rectangle=new Rectangle((int)q.Center.X+x,(int)q.Center.Y+y,3,3);bool native=q.Colliding(q.Hitbox,rectangle);bool model=(bool)Static("HostHomingEffectAttack","EventideCollides",q,new MotionRect(rectangle.X,rectangle.Y,rectangle.Width,rectangle.Height));Require(native==model,"932 original broad+line collision grid x="+x+" y="+y);count++;}
            Console.WriteLine("NATIVE ARROW932 original collider grid="+count);
        }
        private static void AmbientFamilies()
        {
            int count=0,positive=0;
            foreach(int type in new[]{932,469,485,710,181,566,117})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(4953,40);for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);var n=Target(new Vector2(1100,980),!blocked);
                var q=Spawn(p,type,new Vector2(1000,980),new Vector2(8,0));if(type==117)q.timeLeft=3;object root;var path=Path(n);var plan=Known(p,new[]{q},path,out root);
                lottery=true;try{Advance(p,100);}finally{lottery=false;}
                Console.WriteLine("NATIVE ARROW ambient type="+type+" blocked="+blocked+" native="+first+" hitType="+firstType+" plan="+plan?.Tick);
                Require(first==0?plan==null:plan!=null && first==plan.Tick,"same complete native ambient family exact receive type="+type+" blocked="+blocked+" native="+first+" plan="+plan?.Tick);if(first>0)positive++;count++;
            }
            Require(positive>=5,"new finite ambient comparison includes real positive receivers");Console.WriteLine("NATIVE ARROW ambient families="+count+" positive="+positive);
        }
        private static void MixedFlight()
        {
            foreach(bool harpy in new[]{false,true})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(4953,40,1,harpy);for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);var n=Target(new Vector2(1100,980),!blocked);
                var old=Spawn(p,14,new Vector2(1070,980),new Vector2(8,0));var q=Spawn(p,932,new Vector2(1000,980),new Vector2(8,0));var path=Path(n);object root;isolatedCalls=completeCalls=0;var complete=Known(p,new[]{old,q},path,out root);
                Require(isolatedCalls==0 && completeCalls==1,"independent mixed932 oracle executes one complete ordered batch without isolated932 replay");
                var attack=Get(Get(context,"Combat"),"Attack");var args=new object[]{p,path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),false};object flight;isolatedCalls=completeCalls=0;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=attack.GetType().GetMethod("ReadFlight",Flags).Invoke(attack,args);}finally{depth--;}
                // Ordinary representative preparation retires this public
                // world preview; the independent born-family oracle still
                // owns ordered immunity, collision and exact native damage.
                Require((bool)Get(attack,"BalancedWeapon") && flight==null && !(bool)args[3] && isolatedCalls==0 && completeCalls==0,"balanced mixed932 public reader retires Flight without replaying either complete or isolated worlds");
                if(!blocked){Require(complete!=null,"mixed932 positive has an independent complete receive");float local=old.localAI[1];try{old.localAI[1]=local+1;Require(!(bool)Get(root,"Unchanged"),"ambient sameGUC scalar edit revokes complete mixed family");}finally{old.localAI[1]=local;}int immunity=q.localNPCImmunity[2];try{q.localNPCImmunity[2]=immunity+1;Require(!(bool)Get(root,"Unchanged"),"live932 sameGUC local history edit revokes complete mixed family");}finally{q.localNPCImmunity[2]=immunity;}}
                Advance(p,60);Require(blocked?first==0 && complete==null:first>0 && complete!=null && first==complete.Tick,"independent mixed932 receive agrees with actual ordered bullet/streak owner history and collider");Console.WriteLine("NATIVE ARROW mixed932 publicRetired=True Harpy="+harpy+" blocked="+blocked+" native="+first+" plan="+complete?.Tick);
            }
        }
        private static void MixedRetirement()
        {
            foreach(bool harpy in new[]{false,true})foreach(bool blocked in new[]{false,true})
            {
            var p=Scene(4953,516,1,harpy);var n=Target(new Vector2(1005,300),!blocked);var low=Spawn(p,14,new Vector2(800,980),Vector2.Zero,0);low.timeLeft=1;var parent=Spawn(p,91,new Vector2(1000,980),new Vector2(8,0));parent.timeLeft=1;var path=Path(n);object root;isolatedCalls=completeCalls=0;var complete=Known(p,new[]{parent},path,out root);Require(completeCalls==1 && isolatedCalls==0,"independent retirement oracle owns one complete ordered ammo world");try{low.timeLeft=2;Require(!(bool)Get(root,"Unchanged"),"complete mixed scene retains low nonarrow lifetime dependency");}finally{low.timeLeft=1;}
            var singleRoot=RootShot(p,parent,path);var singleArgs=new object[]{p,new[]{parent},path,Clock(path),Terrain(),singleRoot,null};AttackContact isolated;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))isolated=(AttackContact)Static("HostHarpyAttack","KnownPrepared",singleArgs);}finally{depth--;}
            var attack=Get(Get(context,"Combat"),"Attack");var args=new object[]{p,path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),false};object flight;isolatedCalls=completeCalls=0;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=attack.GetType().GetMethod("ReadFlight",Flags).Invoke(attack,args);}finally{depth--;}
            Require((bool)Get(attack,"BalancedWeapon") && flight==null && !(bool)args[3] && isolatedCalls==0 && completeCalls==0,"balanced public preview retires without adopting isolated Harpy or complete ammo world after low nonarrow retirement");
            lottery=true;try{Advance(p,60);}finally{lottery=false;}Console.WriteLine("NATIVE ARROW mixed-no932 Harpy="+harpy+" blocked="+blocked+" lowBulletRetires slot="+low.whoAmI+" parent="+parent.whoAmI+" native="+first+" type="+firstType+" complete="+complete?.Tick+" isolated="+isolated?.Tick+" publicRetired=True");
            Require(blocked?first==0 && complete==null: first>0 && firstType==92 && complete!=null && first==complete.Tick,"original Holy expiry child respects low nonarrow retirement and parent-occupied allocation");
            }
        }
        private static void BirthObserved(Player player,Vector2 origin,Vector2 aim,int stage,Projectile[] __result)
        {if(ordinary)birthCalls++;if(watchBirth){preparedBirth=__result==null?null:__result.Select(PrivateCopy).ToArray();preparedStage=stage;preparedOrigin=origin;preparedAim=aim;preparedPose=player;}}
        private static void SingleBirthObserved(Player player,Vector2 origin,Vector2 aim,int stage,Projectile __result)
        {
            // Current public preparation reads one main birth. These finite
            // origin/ordinal oracles observe that real adapter, without
            // restoring the retired full-batch/world Contact requirement.
            // Replay advances this same private object in place. Snapshot at
            // the birth exit; retaining its reference would observe flight
            // state while claiming to compare native birth geometry.
            if(!watchBirth)return;preparedBirth=__result==null?null:new[]{PrivateCopy(__result)};preparedStage=stage;preparedOrigin=origin;preparedAim=aim;preparedPose=player;
        }
        private static void SolveObserved(NpcTrajectory path){depth++;if(ordinary){modelCalls++;modelPath=path.Count;modelStop=path.Stop.ToString();modelPaused=path.BudgetPaused;}}
        private static Exception SolveDone(AttackContact __result,Exception __exception){try{if(ordinary){modelContact=__result;modelBudget|=__exception!=null && __exception.GetType().Name.Contains("BudgetExceeded");}return __exception;}finally{depth--;}}
        private static void NativeHandObserved()
        {
            if(watchNativeStage){nativeStageCalls++;nativeStage=(Main.LocalPlayer.itemAnimationMax-Main.LocalPlayer.itemAnimation)/2;}if(!ordinary)return;var attack=Get(Get(context,"Combat"),"Attack");ordinaryStarts++;string kind=(bool)Get(attack,"hasConsumedInput")?Convert.ToString(Get(Get(attack,"consumedInput"),"Kind")):"none";
            foreach(var shot in Main.projectile)if(shot.active && shot.owner==Main.LocalPlayer.whoAmI && !ordinaryKinds.ContainsKey(shot.key.ToString()))ordinaryKinds.Add(shot.key.ToString(),kind);if(kind=="Enhanced")ordinaryEnhanced++;if(kind=="Basic")ordinaryBasic++;if(ordinaryWorld>=120)ordinaryRecovery++;
            Console.WriteLine("NATIVE ARROW ORDINARY hand world="+ordinaryWorld+" kind="+kind+" facing="+Main.LocalPlayer.direction+" ammo="+Main.LocalPlayer.inventory[54].stack+" modelCalls="+modelCalls+" modelPath="+modelPath+" candidates="+birthCalls+" Contact="+modelContact?.Tick+" modelBudget="+modelBudget+" SourceStop="+modelStop+" SourcePaused="+modelPaused+" workMs="+Get(Get(context,"CombatObservation"),"PredictionWork").GetType().GetProperty("UsedMilliseconds",Flags).GetValue(Get(Get(context,"CombatObservation"),"PredictionWork")));
        }
        private static void LaunchPrefixes()
        {
            int count=0;foreach(int weapon in new[]{3029,4953})foreach(int delay in new[]{0,5})foreach(bool advanced in new[]{false,true})
            {
                var p=Scene(weapon,1235);p.velocity=new Vector2(3,0);p.controlRight=true;var n=Target(new Vector2(1150,1000));var path=Path(n);var terrain=Terrain();var frame=Static("HostAttackPlayerFrame","Capture",p,path);var prefix=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerPrefix",true),Flags,null,new object[]{frame,terrain,121},null);var args=new object[]{delay+1,null};Require((bool)Call(prefix,"TryRead",args),"future launch motion exists");
                var future=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);Static("HostAttackPlayerFrame","ApplyPose",future,args[1]);var oldOrigin=(Vector2)Static("HostAttackModels","LaunchOrigin",p,p.HeldItem);var newOrigin=(Vector2)Static("HostAttackModels","LaunchOrigin",future,future.HeldItem);var ammo=Capture(p);var root=Root(p,ammo,path);var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),delay},null);
                watchBirth=true;preparedBirth=null;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Static("HostNativeArrowBirthAttack","Solve",p,ammo,path,advanced?newOrigin:oldOrigin,clock,terrain,root,advanced);}finally{depth--;watchBirth=false;}
                Require(preparedBirth!=null && preparedBirth.Length==1 && Vector2.Distance(preparedOrigin,newOrigin)<.001f && preparedPose.gravDir==future.gravDir && preparedPose.position==future.position,"CW current/next and delayed preview read one main birth, apply future pose and advance launch exactly once weapon="+weapon+" delay="+delay+" advanced="+advanced);
                // The public adapter owns only ordinal zero here. Independently
                // seal every native ordinal at that exact pose/origin/aim;
                // watchBirth is closed so this cannot replace the observation.
                var sealedBatch=Births(preparedPose,ammo,preparedAim,preparedStage);
                Require(sealedBatch!=null && sealedBatch.Length==(weapon==3029?3:1),"independent future-pose batch retains every native ordinal");
                Require(preparedBirth[0].type==sealedBatch[0].type && Vector2.Distance(preparedBirth[0].Center,sealedBatch[0].Center)<.002f && Vector2.Distance(preparedBirth[0].velocity,sealedBatch[0].velocity)<.002f,"public main birth equals its exact independent ordinal zero weapon="+weapon+" delay="+delay+" advanced="+advanced+" point="+preparedAim+" public=["+Tuple(preparedBirth[0])+"] sealed=["+Tuple(sealedBatch[0])+"]");
                var originalPosition=p.position;var originalVelocity=p.velocity;int originalAnimation=p.itemAnimation;
                try
                {
                    Static("HostAttackPlayerFrame","ApplyPose",p,args[1]);p.itemAnimation=Math.Max(0,p.itemAnimation-delay-1);Shoot(p,preparedAim,true);var actual=Main.projectile.Where(q=>q.active).OrderBy(q=>q.whoAmI).ToArray();Console.WriteLine("NATIVE ARROW prefix actual weapon="+weapon+" delay="+delay+" advanced="+advanced+" publicMainBirths="+preparedBirth.Length+" sealedBirths="+sealedBatch.Length+" nativeBirths="+actual.Length+" stage="+preparedStage+" origin="+preparedOrigin+" actualOrigin="+Static("HostAttackModels","LaunchOrigin",p,p.HeldItem));Require(actual.Length==sealedBatch.Length,"future pose native count retained");
                    for(int i=0;i<actual.Length;i++){var a=actual[i];var b=sealedBatch[i];Require(a.type==b.type && a.owner==b.owner && a.damage==b.damage && a.knockBack==b.knockBack && a.ai[0]==b.ai[0] && a.ai[1]==b.ai[1] && a.extraUpdates==b.extraUpdates && a.noDropItem==b.noDropItem && Vector2.Distance(a.Center,b.Center)<.002f && Vector2.Distance(a.velocity,b.velocity)<.002f,"sealed future pose actual native ordinal and scalar formula weapon="+weapon+" delay="+delay+" advanced="+advanced+" ordinal="+i+" native=["+Tuple(a)+"] model=["+Tuple(b)+"] origin="+preparedOrigin+" futurePosition="+future.position+" aim="+preparedAim);}
                }
                finally{p.position=originalPosition;p.velocity=originalVelocity;p.itemAnimation=originalAnimation;}
                count++;
            }
            Console.WriteLine("NATIVE ARROW CW current/advanced-next/delay0+5 native sealed births="+count+" no synthetic fullPlayer-delay claim");
        }
        private static void AmbientDeaths()
        {
            foreach(int type in new[]{469,710,117})
            {
                var p=Scene(4953,40);for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);var n=Target(new Vector2(type==117?960:type==710?1030:1040,980));var bee=Spawn(p,181,new Vector2(800,980),Vector2.Zero,0);bee.timeLeft=1;var oldBeeKey=bee.key;var q=Spawn(p,type,new Vector2(1000,980),new Vector2(8,0));q.timeLeft=1;var path=Path(n);object root;var contact=Known(p,new[]{q},path,out root);lottery=true;try{Advance(p,100);}finally{lottery=false;}
                Console.WriteLine("NATIVE ARROW ambient death type="+type+" native="+first+" hitType="+firstType+" plan="+contact?.Tick+" hitWidth="+firstWidth+" lowBeeActive="+bee.active);
                Require(first==0?contact==null:contact!=null && first==contact.Tick,"ambient native expiry births keep parent-occupied history and release low bee slot type="+type);Require(!bee.active || !bee.key.Equals(oldBeeKey),"expired low-slot native bee retires or its slot acquires a distinct native child key");
            }
        }
        private static void IsolatedObserved(Projectile[] sources){if(sources!=null && sources.Any(p=>p.type==932))isolatedCalls++;}
        private static void CompleteObserved(){completeCalls++;}
        private static Projectile PrivateCopy(Projectile p){var q=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();return q;}
        private static void FanBorn(IEntitySource __0,int __result)
        {var source=__0 as EntitySource_Parent;if(trackFan && source?.Entity is Projectile && ((Projectile)source.Entity).type==710 && __result>=0 && Main.projectile[__result].type==710)actualFan.Add(PrivateCopy(Main.projectile[__result]));}
        private static void FanPrivate(Projectile child){if(trackFan && child.type==710)privateFan.Add(PrivateCopy(child));}
        private static void PhoenixFan()
        {
            foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(4953,40);for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);var n=Target(new Vector2(1008,1060));var other=Main.npc[3];other.SetDefaults(3);other.active=true;other.whoAmI=3;other.Center=new Vector2(1012,980);other.velocity=Vector2.Zero;other.aiStyle=-1;other.noGravity=other.noTileCollide=true;other.dontTakeDamage=blocked;other.life=other.lifeMax=10000;other.knockBackResist=0;Array.Clear(other.immune,0,other.immune.Length);
                var q=Spawn(p,710,new Vector2(1000,980),new Vector2(8,0));var path=Path(n);object root;actualFan.Clear();privateFan.Clear();trackFan=true;AttackContact plan;try{plan=Known(p,new[]{q},path,out root);Advance(p,80);}finally{trackFan=false;}
                Require(first==0?plan==null:plan!=null && first==plan.Tick,"710 ambient OnHit five native children share same receive/slot world blocked="+blocked+" native="+first+" plan="+plan?.Tick);
                Require(actualFan.Count==(blocked?0:5) && privateFan.Count==actualFan.Count,"710 original native OnHit fan count and blocked receiver prevent fake descendants");for(int i=0;i<actualFan.Count;i++)Require(actualFan[i].damage==privateFan[i].damage && actualFan[i].ai[1]==-1 && privateFan[i].ai[1]==-1 && actualFan[i].velocity==privateFan[i].velocity && Vector2.Distance(actualFan[i].Center,privateFan[i].Center)<.002f,"710 native five-child formula stays original and nonrecursive");
                if(!blocked)Require(first>0,"710 OnHit-derived positive reaches selected through another receiver");Console.WriteLine("NATIVE ARROW710 native OnHit fan blocked="+blocked+" count="+actualFan.Count+" first="+first+" plan="+plan?.Tick);
            }
        }
        private static void QuiverStreak()
        {
            foreach(bool quiver in new[]{false,true})
            {
                var p=Scene(4953,40);p.magicQuiver=quiver;var n=Target(new Vector2(1120,980));var q=Spawn(p,932,new Vector2(1000,980),new Vector2(8,0));Require(q.extraUpdates==0,"932 native starts with zero extra even with magicQuiver equipped");object root;var plan=Known(p,new[]{q},Path(n),out root);p.magicQuiver=!quiver;Require(!(bool)Get(root,"Unchanged"),"native quiver promotion premise edit withdraws frozen scene");p.magicQuiver=quiver;Advance(p,40);Require(plan!=null && first==plan.Tick && first>0 && q.extraUpdates==(quiver?1:0),"932 first-world promotion after movement retains exact native first receive");Console.WriteLine("NATIVE ARROW932 quiver="+quiver+" first="+first+" plan="+plan.Tick);
            }
        }
        private static void DelayedOrdinal()
        {
            foreach(bool harpy in new[]{false,true})
            {
                var p=Scene(4953,1235,1,harpy);p.itemAnimationMax=10;p.itemAnimation=7;p.itemTime=3;p.controlUseItem=true;p.releaseUseItem=false;for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);var n=Target(p.Center+new Vector2(220,0));var path=Path(n);var attack=Get(Get(context,"Combat"),"Attack");object bound;preparedBirth=null;watchBirth=true;depth++;AttackContact contact;try{contact=NativeCombatUsageChecks.ReadMechanism(Get(context,"CombatObservation"),attack,n,out bound,path);}finally{depth--;watchBirth=false;}
                var solution=Get(bound,"Solution");Require((bool)Get(bound,"Balanced") && (bool)Get(solution,"HasPoint") && Get(solution,"Reachability").ToString()=="Reachable" && preparedBirth!=null,"delayed ordinal component owns the actual representative later hand source without requiring a world Contact");int expectedStage=preparedStage;nativeStageCalls=0;watchNativeStage=true;try{for(int i=0;i<5 && nativeStageCalls==0;i++)NativeCombatSelectionPreviewChecks.Step(context,n.Center,true);}finally{watchNativeStage=false;}
                Require(nativeStageCalls==1 && nativeStage==expectedStage,"delayed Eventide private preview ordinal equals actual native ItemCheck after cooldown Harpy="+harpy+" predicted="+expectedStage+" native="+nativeStage+" planDelay="+Get(bound,"Delay"));Console.WriteLine("NATIVE ARROW delayed native ordinal Harpy="+harpy+" stage="+nativeStage+" delay="+Get(bound,"Delay"));
            }
        }
        private static void GravityBirths()
        {
            int count=0;foreach(int weapon in new[]{1229,2624,3029,4953})foreach(int gravity in new[]{1,-1})foreach(int direction in new[]{-1,1})
            {
                var p=Scene(weapon,1235,direction);p.gravDir=gravity;p.fullRotation=.4f;Main.screenPosition=new Vector2(300.3f,400.1f);var aim=p.Center+new Vector2(250*direction+.3f,100.2f);var expected=Births(p,Capture(p),aim,0);Shoot(p,aim,true);var actual=Main.projectile.Where(q=>q.active).OrderBy(q=>q.whoAmI).ToArray();Require(actual.Length==expected.Length,"native gravity birth count");for(int i=0;i<actual.Length;i++)Require(actual[i].type==expected[i].type && actual[i].damage==expected[i].damage && actual[i].knockBack==expected[i].knockBack && Vector2.Distance(actual[i].Center,expected[i].Center)<.002f && Vector2.Distance(actual[i].velocity,expected[i].velocity)<.002f,"native raw screen versus gravity-world special birth weapon="+weapon+" gravity="+gravity+" facing="+direction+" native=["+Tuple(actual[i])+"] model=["+Tuple(expected[i])+"]");count++;
            }Console.WriteLine("NATIVE ARROW native gravity/facing formula cases="+count);
        }
        private static void SourceGeometry()
        {
            var p=Scene(3029,1235);var n=Target(p.Center+new Vector2(920,-350));var ammo=Capture(p);var origin=(Vector2)Static("HostAttackModels","LaunchOrigin",p,p.HeldItem);var position=p.position;var velocity=p.velocity;int direction=p.direction,animation=p.itemAnimation,time=p.itemTime,stack=p.inventory[54].stack,mx=Main.mouseX,my=Main.mouseY;var random=Main.rand;int hint;
            depth++;try{hint=(int)Static("HostNativeArrowBirthAttack","PreferredRange",p,ammo,n);}finally{depth--;}
            Require(p.position==position && p.velocity==velocity && p.direction==direction && p.itemAnimation==animation && p.itemTime==time && p.inventory[54].stack==stack && Main.mouseX==mx && Main.mouseY==my && ReferenceEquals(random,Main.rand) && !Main.projectile.Any(q=>q.active),"actual-born desired source hint is read-only and allocates no native actor");
            var aim=n.Center+n.netOffset;var expected=Births(p,ammo,aim,0);Shoot(p,aim,true);var actual=Main.projectile.Where(q=>q.active).OrderBy(q=>q.whoAmI).ToArray();Require(actual.Length==3 && Vector2.Distance(actual[0].Center,expected[0].Center)<.002f,"source geometry is checked against original ItemCheck_Shoot birth formula");
            float speed=(float)Get(ammo,"Speed");while(speed>=16)speed*=.97f;float closing=Math.Max(1,speed*2-n.velocity.Length());var oldOrigin=new Vector2(origin.X,p.MountedCenter.Y-600);int oldHint=Math.Min(120,Math.Max(16,(int)Math.Ceiling(Vector2.Distance(oldOrigin,aim)/closing)+8));int fromNative=Math.Min(120,Math.Max(16,(int)Math.Ceiling(Vector2.Distance(actual[0].Center,aim)/closing)+8));
            Console.WriteLine("NATIVE ARROW sourceGeometry handSky="+oldOrigin+" actualNativeSky="+actual[0].Center+" receiver="+aim+" oldDesired="+oldHint+" actualBirthDesired="+fromNative+" productDesired="+hint+" desiredOnlyNotContact=True");Require(hint==fromNative && hint<oldHint,"distant horizontal target schedules from actual native sky X rather than hand X, retaining original bounds/progression");
        }
        private static void OrdinaryWindows(bool adjacent=false,bool moving=false,bool wall=false,bool skyOnly=false)
        {
            foreach(int weapon in skyOnly?new[]{3029}:adjacent?new[]{1229,2624}:new[]{3029,4953})foreach(bool harpy in moving || wall?new[]{false}:new[]{false,true})
            {
                var p=Scene(weapon,1235);p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;p.fullRotation=0;for(int x=3;x<Main.maxTilesX-3;x++)NativeToolsChecks.Tile(x,70,1);foreach(var armor in p.armor)armor.TurnToAir();if(harpy)p.armor[3].SetDefaults(6166);if(wall){if(weapon==3029){for(int x=53;x<69;x++)NativeToolsChecks.Tile(x,55,1);for(int y=26;y<38;y++)NativeToolsChecks.Tile(68,y,1);}else{for(int y=48;y<73;y++)NativeToolsChecks.Tile(69,y,1);for(int x=53;x<61;x++)NativeToolsChecks.Tile(x,60,1);}}var target=wall?Target(weapon==3029?new Vector2(1140,720):new Vector2(990,900)):NativeCombatControlPreviewChecks.Dummy(2,72,67);if(moving){var center=target.Center;target.SetDefaults(1);target.whoAmI=2;target.Center=center;target.life=target.lifeMax=100000;target.knockBackResist=0;}var initialTarget=target.position;int movingWorlds=0;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(false,true,false,true,true,0,false,true));ordinaryKinds.Clear();ordinaryStarts=ordinaryEnhanced=ordinaryBasic=ordinaryStrikes=ordinaryRecovery=ordinaryEnhancedStrikes=ordinaryEnhancedTiles=0;ordinary=true;
                try{for(ordinaryWorld=0;ordinaryWorld<180;ordinaryWorld++){bool press=(ordinaryWorld<90 || ordinaryWorld>=120) && ordinaryWorld%40<20;modelCalls=birthCalls=modelPath=0;modelContact=null;modelBudget=modelPaused=false;modelStop=null;NativeCombatSelectionPreviewChecks.Step(context,target.Center,press,right:moving && ordinaryWorld%60<25);if(p.velocity.LengthSquared()>0)movingWorlds++;}}finally{ordinary=false;}
                Console.WriteLine("NATIVE ARROW ORDINARY summary weapon="+weapon+" Harpy="+harpy+" worlds=180 release90..119 starts="+ordinaryStarts+" E="+ordinaryEnhanced+" Basic="+ordinaryBasic+" nativeStrikes="+ordinaryStrikes+" EnhancedNativeStrikes="+ordinaryEnhancedStrikes+" recovery="+ordinaryRecovery+" budget6ms/noPublish/noWarmup/nativeRngUnmodified moving="+moving+" ownerMovingWorlds="+movingWorlds+" nativeTargetDelta="+(target.position-initialTarget)+" wall="+wall+" EnhancedTileStrikes="+ordinaryEnhancedTiles);if(wall)Require(ordinaryEnhancedTiles>0,"normal6ms actual Enhanced special native birth reaches selected after original225 tile retarget");if(moving)Require(movingWorlds>0 && target.position!=initialTarget,"real Player input and native Slime.Update both advance during ordinary opportunity window");Require(ordinaryStarts>0 && ordinaryEnhanced>0 && ordinaryStrikes>0 && ordinaryEnhancedStrikes>0 && ordinaryRecovery>0,"normal budget supplies real special-birth Enhanced opportunity and native Strike with release/recovery weapon="+weapon+" Harpy="+harpy);
            }
        }
        private static void InputNegatives()
        {
            foreach(int edit in new[]{0,1,2,3,4,5,6,7})
            {
                var p=Scene(4953,1235,1,true);var n=Target(p.Center+new Vector2(220,0));var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,false,true));var physical=p.Center-new Vector2(250,120);NativeToolExecutionChecks.Sample(context,Get(context,"Input"),physical,true);Call(combat,"Sample");depth++;try{NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0,phaseName:"BeforeNpc");}finally{depth--;}
                var solution=NativeCombatAimChecks.RequireSolution(attack,p,"negative begins with an actually prepared special-birth input");uint originalTick=Main.GameUpdateCount;bool originalChat=Main.drawingPlayerChat;var originalVelocity=p.velocity;
                if(edit==0)NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,false,false));if(edit==1)Main.drawingPlayerChat=true;if(edit==2)n.dontTakeDamage=true;if(edit==3)p.magicQuiver=!p.magicQuiver;if(edit==4)p.inventory[54].SetDefaults(40);if(edit==5)Spawn(p,14,p.Center-new Vector2(30,0),Vector2.UnitX,0);
                if(edit==6)p.velocity+=Vector2.UnitX;if(edit==7)typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,unchecked(Main.GameUpdateCount+2));
                try
                {
                    Require(!(bool)Get(attack,"hasConsumedInput"),"native-arrow negative begins before any actual hand consumption");
                    Shoot(p,physical,true);
                    bool enhanced=(bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced";
                    // A foreign zero-damage bullet is no longer an ordinary hand
                    // dependency. Aim/UI/target/ammo/gear/motion/age still are.
                    Require(enhanced==(edit==5),"special-birth input follows its actual permission and dependency boundary edit="+edit);if(edit==5)NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"ambient birth does not withdraw the current representative hand");Require(Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.damage>0),"negative never suppresses native original hand birth");Console.WriteLine("NATIVE ARROW actual input edit="+edit+" Enhanced="+enhanced+" nativeBirthTrue");
                }
                finally{Main.drawingPlayerChat=originalChat;typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,originalTick);p.velocity=originalVelocity;}
            }
        }
        private static string RandomSequence(Terraria.Utilities.UnifiedRandom random)
        {return string.Join(",",Enumerable.Range(0,24).Select(i=>random.Next(-500,501)+"/"+random.NextDouble().ToString("R")+"/"+random.NextFloat().ToString("R")));}
        internal static void Run(object value)
        {
            context=value;host=Get(value,"Combat").GetType().Assembly;var audit=new Harmony("JueMingR.Tests.NativeArrowBirth");bool wind=Main.windPhysics;var originalRandom=Main.rand;string ordinarySequence=RandomSequence(new Terraria.Utilities.UnifiedRandom(17));rng=depth=0;
            try
            {
                Require(RandomSequence(new Terraria.Utilities.UnifiedRandom(17))==ordinarySequence,"unmodified native random baseline is reproducible");
                audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostHomingEffectAttack",true).GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(IsolatedObserved)));
                audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostHeldAmmoFamilyAttack",true).GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(CompleteObserved)));
                audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostHeldAmmoFamilyAttack",true).GetMethod("Add",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(FanPrivate)));
                audit.Patch(typeof(Projectile).GetMethod("NewProjectile",Flags,null,new[]{typeof(IEntitySource),typeof(float),typeof(float),typeof(float),typeof(float),typeof(int),typeof(int),typeof(float),typeof(int),typeof(float),typeof(float),typeof(float),typeof(NewProjectileModifier)},null),postfix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(FanBorn)));
                foreach(string achievement in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(achievement,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
                foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && m.Name=="Next"))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(NextInt)));
                audit.Patch(typeof(Terraria.Utilities.UnifiedRandom).GetMethod("NextDouble",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(NextDouble)));
                audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),transpiler:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(Inputs)));
                audit.Patch(typeof(Utils).GetMethod("RotatedByRandom",Flags,null,new[]{typeof(Vector2),typeof(double)},null),transpiler:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(Inputs)));
                audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true).GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(SolveObserved)),finalizer:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(SolveDone)));
                audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true).GetMethod("Births",Flags),postfix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(BirthObserved)));
                audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true).GetMethod("Birth",Flags),postfix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(SingleBirthObserved)));
                audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),postfix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(NativeHandObserved)),transpiler:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(Inputs)));
                audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(DamageDone)));
                audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatNativeArrowBirthChecks),nameof(Strike)));
                if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="source-geometry"){SourceGeometry();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="mixed-retirement"){MixedRetirement();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="wall"){PublicWallHands();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ambient"){StreakCollider();AmbientFamilies();MixedFlight();MixedRetirement();AmbientDeaths();PhoenixFan();QuiverStreak();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="new-stages"){foreach(bool harpy in new[]{false,true})KillStages(41,harpy);PublicHands();InputNegatives();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="gravity-public"){PublicHands(true);}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinal"){DelayedOrdinal();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="gravity"){GravityBirths();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="prefix"){LaunchPrefixes();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinary"){OrdinaryWindows();}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinary-adjacent"){OrdinaryWindows(true);}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinary-moving"){OrdinaryWindows(false,true);}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinary-wall"){OrdinaryWindows(false,false,true);}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinary-sky"){OrdinaryWindows(false,false,false,true);}else if(Environment.GetEnvironmentVariable("JMR_NATIVE_ARROW_SECTION")=="ordinary-sky-moving"){OrdinaryWindows(false,true,false,true);}else{BirthMatrix();ParameterBirths();SourceGeometry();foreach(int weapon in new[]{4953,3029})foreach(bool harpy in new[]{false,true})Bounce(weapon,harpy);foreach(int type in new[]{41,91,639})foreach(bool harpy in new[]{false,true})KillStages(type,harpy);PublicHands();PublicWallHands();StreakCollider();AmbientFamilies();MixedFlight();MixedRetirement();AmbientDeaths();PhoenixFan();QuiverStreak();LaunchPrefixes();DelayedOrdinal();GravityBirths();PublicHands(true);InputNegatives();}Require(!lottery && depth==0 && ReferenceEquals(originalRandom,Main.rand) && RandomSequence(new Terraria.Utilities.UnifiedRandom(17))==ordinarySequence,"ordinary native random forwarding and Main.rand identity preserved across scoped primitive representatives");Require(rng==0,"all private native-birth adapters and full ammo scene consume zero live RNG");Console.WriteLine("PASS NATIVE ARROW COMPOSITION RNG0");
            }
            finally{audit.UnpatchAll(audit.Id);lottery=false;damaging=null;Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;context=null;Require(!Harmony.GetAllPatchedMethods().Any(m=>Harmony.GetPatchInfo(m).Owners.Contains(audit.Id)),"all native primitive/birth/receive observer hooks uninstall");}
        }
    }
}
