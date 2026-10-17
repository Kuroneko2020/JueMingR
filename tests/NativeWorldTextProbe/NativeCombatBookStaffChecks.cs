using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Testing;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBookStaffChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched;private static bool record,slow;
        private static Vector2 raw;private static long first;private static int firstSub,solves,knownCalls,depth,steps,scans,rng,kills,capturedSolves,capturedKnown,lightRejects,captureFailures,abortedSolves,abortedKnown;private static Player shootEntry;
        private sealed class Birth
        {internal Projectile Shot;internal int Key,Slot,Type,Damage,Animation,Maximum,Time,Direction,Sub=-1;internal long Tick,First=-1;internal bool CursorInput;internal Vector2 Center,Velocity,Cursor,Origin;internal AttackContact Known;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> hitSlots=new List<int>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static bool Role(Projectile q){return q!=null && (q.type==712 || q.type==704);}
        private static void SolveWork(out int __state){__state=scans;solves++;depth++;}
        private static void SolveDone(int __state,AttackContact __result,Exception __exception)
        {
            // Observe each actual call, without reproducing its birth guard.
            // A legal light rejection may exit before any scene capture.
            int delta=scans-__state;if(delta==1)capturedSolves++;else if(delta==0 && __exception!=null)abortedSolves++;else if(delta==0 && __result==null)lightRejects++;else captureFailures++;depth--;
        }
        private static void KnownWork(out int __state){__state=scans;knownCalls++;depth++;}
        private static void KnownDone(int __state,Exception __exception)
        {if(scans-__state==1)capturedKnown++;else if(scans==__state && __exception!=null)abortedKnown++;else captureFailures++;depth--;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rng++;}
        private static void BeforeKill(Projectile __instance){if(Role(__instance) && __instance.active)kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void BeforeShoot(Player __instance,Item __1)
        {
            if(!record || __instance.whoAmI!=Main.myPlayer || __1.type!=3852)return;
            shootEntry=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(__instance,null);
            if(naturalContext==null || !Main.mouseLeft && !Main.mouseRight)return;
            // Native ItemCheck has already selected primary/alt and its exact
            // locked pose. NPCs are still at T-1; preserve that real phase.
            var host=Get(naturalContext,"CombatObservation");var attack=Get(Get(naturalContext,"Combat"),"Attack");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0,phaseName:"BeforeNpc");
            var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"each real Book Shoot entry has an actual preparation under1000ms mechanism allowance");
            preparedHands++;if(preparedHands<=2)Draws(host,attack,plan);
        }
        private static object naturalContext;private static int preparedHands;
        private static void Strike(NPC __instance,int __result)
        {
            if(!Role(receiving) || receiving.owner!=Main.myPlayer || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;int sub=receiving.extraUpdates-receiving.numUpdates-1;
            if(first<0){first=Main.GameUpdateCount;firstSub=sub;}foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0){b.First=Main.GameUpdateCount;b.Sub=sub;}
        }
        private static Player Reset(object context)
        {
            record=false;slow=false;receiving=watched=null;shootEntry=null;target=null;first=-1;firstSub=-1;kills=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3852,0,0);p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=p.releaseUseTile=true;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Path(NPC n,bool completed=false)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-(completed?0:1),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var value=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(value,"Reset");return value;}
        private static object Receivers(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Receivers(p,q,path));}
        private static Projectile Shot(Player p,int type,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,damage,0,p.whoAmI)];}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!record || __result<0)return;var q=Main.projectile[__result];if(!Role(q))return;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;bool input=(bool)Static(model,"CursorInput",p,0),locked=(bool)Static(model,"LockedDirection",p,0);
            Require(shootEntry!=null,"actual Book Shoot entry has its pre-write stored itemRotation");
            Vector2 center,velocity;var args=new object[]{locked?shootEntry:p,origin,cursor,p.HeldItem.shootSpeed,locked,null,null};model.GetMethod("Birth",Flags).Invoke(null,args);center=(Vector2)args[5];velocity=(Vector2)args[6];
            if(q.type==712)velocity=(Vector2)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSonicBoomAttack",true),"LimitLaunch",velocity);
            var b=new Birth{Shot=q,Slot=q.whoAmI,Key=(int)q.key,Type=q.type,Damage=q.damage,Tick=Main.GameUpdateCount,Animation=p.itemAnimation,Maximum=p.itemAnimationMax,Time=p.itemTime,Direction=p.direction,CursorInput=input,Origin=origin,Center=q.Center,Velocity=q.velocity,Cursor=cursor,Known=Known(p,q,Path(target))};born.Add(b);
            if(naturalContext!=null && input)
            {
                var attack=Get(Get(naturalContext,"Combat"),"Attack");
                Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual cursor-reading Book birth consumes its prepared Enhanced receipt");
            }
            int damage=p.GetWeaponDamage(p.HeldItem);if(q.type==704)damage=(int)((float)damage*1.75f);
            Console.WriteLine("BOOK born type="+q.type+" tick="+b.Tick+" slot/key="+b.Slot+"/"+b.Key+" anim/max/time="+b.Animation+"/"+b.Maximum+"/"+b.Time+" input="+input+" alt/slow="+p.altFunctionUse+"/"+p.slowMagicUse+" center/V="+b.Center+"/"+b.Velocity+" cursor="+cursor+" raw="+raw+" damage="+q.damage+" known="+b.Known?.Tick+"/"+b.Known?.Subupdate);
            Require(!(__0 is EntitySource_Parent parent && parent.Entity is Projectile) && (int)Static(model,"BirthType",p)==q.type && q.damage==damage && q.magic && Vector2.DistanceSquared(center,q.Center)<.000001f && Vector2.DistanceSquared(velocity,q.velocity)<.000001f,"real Book hand origin, stored-angle/inverted-gravity consumer, legal type and damage match without model RNG");
            Require((bool)Static(model,"CanBirth",p.itemAnimation,p.itemAnimationMax,p.itemTimeMax,q.type==704),"every real native burst/alt birth satisfies its exact animation gate");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBookStaffAttack",true);var audit=new Harmony("JueMingR.Tests.BookStaff");bool wind=Main.windPhysics,manaV2=DebugOptions.ManaV2;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(BeforeShoot)){priority=Priority.First+1000});
            // Finalizers only close the work observer on every exit. They do
            // not replace or suppress a normal production budget cancellation.
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(SolveWork)),finalizer:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(SolveDone)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(KnownWork)),finalizer:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(KnownDone)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBookStaffChecks),nameof(Strike)));
            try{string leaf=Environment.GetEnvironmentVariable("JMR_BOOK_STAFF_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","Book leaf selector");if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);}
            finally{naturalContext=null;record=slow=false;receiving=watched=null;target=null;DebugOptions.ManaV2=manaV2;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;foreach(int type in new[]{712,704})for(int scene=0;scene<(type==712?8:6);scene++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1750,500));if(type==712 && scene>=1 && scene<=3)for(int x=5;x<115;x++)for(int y=10;y<40;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(type==712 && scene==4)for(int y=10;y<40;y++)NativeToolsChecks.Tile(60,y,1);
                if(type==712 && (scene==5 || scene==6))for(int x=5;x<115;x++){Main.tile[x,21].liquid=128;Main.tile[x,21].liquidType(3);}
                if(type==704 && scene==1)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,25,1);
                if(type==704 && scene==2)for(int x=5;x<115;x++)for(int y=24;y<27;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                var q=Shot(p,type,type==704 && scene==5?new Vector2(29,350):new Vector2(950,350),new Vector2(8,type==712 && scene==5?2:type==712 && scene==6?-2:0));if(type==712 && scene==7)q.timeLeft=2;if(type==704 && scene==3)q.ai[0]=299;if(type==704 && scene==4)q.localAI[0]=16;
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();for(int outer=1;outer<=40 && q.active;outer++)
                {
                    int updates=copy.extraUpdates+1;for(int sub=0;sub<updates && copy.active;sub++){Require((bool)Static(model,"Step",copy,terrain),"Book motor retains actual finite solid/liquid dependencies");actions++;if(copy.active)Static(model,"Finish",copy);}q.Update(q.whoAmI);
                    Console.WriteLine("BOOK motor "+type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+" ai/local="+q.ai[0]+"/"+q.localAI[0]+" alpha/left/active="+q.alpha+"/"+q.timeLeft+"/"+q.active+" model="+copy.position+"/"+copy.velocity+" ai/local="+copy.ai[0]+"/"+copy.localAI[0]+" alpha/left/active="+copy.alpha+"/"+copy.timeLeft+"/"+copy.active);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.ai[0]==copy.ai[0] && q.localAI[0]==copy.localAI[0] && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && (!q.active || q.alpha==copy.alpha && q.scale==copy.scale && Vector2.DistanceSquared(q.position,copy.position)<.0001f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.000001f),"AI001712 and solid/wet-sensitive AI145704 agree; retired visuals are not a new damage phase");
                }
                if(type==704 && scene==5)Require(!q.active && kills==0,"storm world edge retires without sound-only Kill");if(type==704 && scene==4)Require(q.ai[0]>=285,"actual obstruction counter enters the native finite fade clock");cases++;
            }
            Console.WriteLine("PASS BOOK motors="+cases+" actions="+actions);
        }
        private static void Step(object context,Vector2 mouse,bool left,bool right)
        {
            var input=Get(context,"Input");Call(input,"BeginUpdate");var pixel=Vector2.Transform(mouse-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);PlayerInput.MouseInfo=new MouseState((int)pixel.X,(int)pixel.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,right?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released);
            var tokens=new List<string>();if(left)tokens.Add("Mouse1");if(right)tokens.Add("Mouse2");PlayerInput.Triggers.Reset();Call(input,"AfterNativeMouse",tokens);foreach(string name in tokens)PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].Processkey(PlayerInput.Triggers.Current,name,InputMode.Keyboard);PlayerInput.Triggers.Update();Main.mouseLeft=PlayerInput.Triggers.Current.MouseLeft;Main.mouseRight=PlayerInput.Triggers.Current.MouseRight;PlayerInput.MouseX=(int)pixel.X;PlayerInput.MouseY=(int)pixel.Y;PlayerInput.UpdateMainMouse();PlayerInput.CacheZoomableValues();Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState();Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"Shell"),"ProcessInput");Call(Get(context,"Combat"),"Sample");PlayerInput.SetZoom_World();Call(Get(context,"CombatObservation"),"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"Book natural input consumer restores physical mouse");
        }
        private static Player Scene(object context,bool alt=false)
        {
            var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center;return p;
        }
        private static void Births(object context)
        {
            foreach(bool alt in new[]{false,true})foreach(bool slowed in new[]{false,true})
            {
                var p=Scene(context,alt);slow=slowed;DebugOptions.ManaV2=true;if(slowed)p.statMana=0;record=true;
                for(int frame=0;frame<70;frame++)Step(context,frame==0?raw:raw-new Vector2(380,0),!alt && frame==0,alt && frame==0);record=false;
                int expected=alt?1:4;Console.WriteLine("BOOK burst alt/slow="+alt+"/"+slowed+" born="+born.Count+" stages="+string.Join(",",born.Select(b=>b.Animation+"/"+b.Maximum+":"+b.Time+":"+b.CursorInput)));
                Require(born.Count==expected && born[0].CursorInput && born.Skip(1).All(b=>!b.CursorInput),"real press/release has four native main stages or one alt stage; only first stage can consume cursor");
                Require(born[0].Time==(slowed?7:3) && born[0].Maximum==(slowed?63:26),"native low-mana CheckMana selects the actual2.5 multiplier without fixture flag injection");
                if(!alt){int time=born[0].Time;Require(born.Zip(born.Skip(1),(a,b)=>b.Tick-a.Tick).All(t=>t==time) && born.Skip(1).All(b=>Vector2.DistanceSquared(b.Velocity,born[0].Velocity)<.000001f),"changing physical cursor after first use does not redirect the later native stored-angle burst");}
            }
            InvertedBirths(context);Console.WriteLine("PASS BOOK actual main/alt and slow-magic births4 plus inverted-Y consumers2");
        }
        private static void InvertedBirths(object context)
        {
            foreach(int sign in new[]{-1,1})
            {
                var p=Scene(context);p.gravDir=-1;p.itemAnimation=20;p.itemAnimationMax=26;p.itemTime=0;p.itemTimeMax=3;p.itemRotation=0;p.direction=1;p.controlUseItem=false;p.altFunctionUse=0;
                var origin=p.RotatedRelativePoint(p.MountedCenter);raw=origin+new Vector2(-160,sign*30);Main.mouseX=(int)(raw.X-Main.screenPosition.X);Main.mouseY=Main.screenHeight-(int)(raw.Y-Main.screenPosition.Y);record=true;NativeQuickItemChecks.BeginWorldStep();p.ItemCheck();record=false;
                Require(born.Count==1 && born[0].CursorInput && born[0].Velocity.X>0 && Math.Sign(born[0].Velocity.Y)==sign,"native inverted-gravity later burst retains stored X while actually consuming the physical cursor Y");
                Console.WriteLine("BOOK inverted later input sign="+sign+" actual="+born[0].Velocity+" raw="+raw+" anim="+born[0].Animation+"/"+born[0].Maximum);p.gravDir=1;
            }
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int type in new[]{712,704})for(int mode=0;mode<5;mode++)
            {
                var p=Reset(context);target=Target(2,new Vector2(type==704?980:972,352));if(mode==1)target.immune[p.whoAmI]=200;if(mode==2)target.reflectsProjectiles=true;NPC front=mode==3?Target(1,target.Center):null;
                watched=Shot(p,type,new Vector2(950,350),new Vector2(11,0),mode==4?0:38);NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var plan=Known(p,watched,path);long sample=path.SampleTick;Require(Static(model,"Known",p,watched,path,Clock(path,"Projectiles"),Terrain(),Receivers(p,watched,path))==null,"Book family observation rejects a partial array epoch");
                for(int outer=1;outer<=8 && watched.active && watched.friendly;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);watched.Update(watched.whoAmI);}int actual=first<0?0:(int)(first-sample);
                bool negative=mode==4 || type==704 && mode==1 || type==712 && (mode==2 || mode==3);
                // Native Damage_PVE maxPenetrate=1 without local/static
                // bypasses owner immunity. Storm's local=-1 is a SEPARATE
                // receiving gate: owner200 still blocks it. AI145 is not an
                // AI001 reflection role, so that raw-reflect flag stays positive.
                Console.WriteLine("BOOK receive "+type+"/"+mode+" actual/known="+actual+"/"+plan?.Tick+" sub="+firstSub+"/"+plan?.Subupdate+" hits="+string.Join(",",hitSlots)+" owner/local="+target.immune[p.whoAmI]+"/"+watched.localNPCImmunity[2]);
                Require((actual==0)==negative && (plan==null)==negative && (plan==null || actual==plan.Tick && firstSub==plan.Subupdate),"native Book finite/local budget, raw reflection role and same-owner receiving agree");if(type==704 && mode==3)Require(hitSlots.Contains(1) && hitSlots.Contains(2),"infinite storm consumes lower and selected in the same actual NPC loop");cases++;
            }
            HandLease(context);Console.WriteLine("PASS BOOK receiving="+cases+" plus private-pose/live-owner hand scalar lease");
        }
        private static void HandLease(object context)
        {
            var p=Reset(context);target=Target(2,new Vector2(972,350));p.itemAnimation=20;p.itemAnimationMax=26;p.itemRotation=.125f;p.direction=1;
            var body=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);var q=Shot(p,712,new Vector2(950,350),new Vector2(11,0));var receivers=Receivers(p,q,Path(target));Call(receivers,"CaptureBookHand",body,true);
            Require((bool)Get(receivers,"BirthInputsCurrent"),"private Book hand captures the corresponding live owner scalar premise");
            float rotation=p.itemRotation;p.itemRotation+=.1f;Require(!(bool)Get(receivers,"BirthInputsCurrent"),"locked-direction itemRotation change retires the actual future hand input");p.itemRotation=rotation;Require((bool)Get(receivers,"BirthInputsCurrent"),"restoring itemRotation restores the exact locked hand premise");
            int direction=p.direction;p.direction=-direction;Require(!(bool)Get(receivers,"BirthInputsCurrent"),"locked direction flip retires that same hand scalar lease");p.direction=direction;
            p.altFunctionUse=2;Require(!(bool)Get(receivers,"BirthInputsCurrent"),"alt-use switch cannot reuse a712 hand as704");p.altFunctionUse=0;
            bool flag=DebugOptions.ManaV2;DebugOptions.ManaV2=!flag;Require(!(bool)Get(receivers,"BirthInputsCurrent"),"native slow-magic feature changes retire prepared birth timing");DebugOptions.ManaV2=flag;
            bool originalSlow=p.slowMagicUse;p.slowMagicUse=!originalSlow;Require(!(bool)Get(receivers,"BirthInputsCurrent"),"live slowMagicUse change cannot be hidden by an unchanged private pose");p.slowMagicUse=originalSlow;Require((bool)Get(receivers,"BirthInputsCurrent"),"restored actual live hand is current without scanning the scene");
        }
        private static void Draws(object host,object attack,AttackContact plan)
        {
            Call(attack,"BindPresentation",plan);int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PresentationCurrent",plan),"Book actual hand preparation remains current");}Require(a==solves && b==steps && c==scans,"twenty CPU Book presentations perform zero solve/motor/scene scans");
        }
        private static long ExistingFlight(object attack,Player player)
        {
            // Same completed-world timeline as the displayed plan. Only real
            // still-owned births contribute; the kernel captures their shared
            // history once rather than independently solving each source.
            var sources=born.Where(b=>b.Shot.active && b.Shot.friendly && !b.Shot.hostile && b.Key==(int)b.Shot.key).Select(b=>b.Shot).ToArray();
            Require(sources.Length>0,"Book frame6 contains actual still-owned flights");var path=Path(target,true);
            var receivers=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{player,sources[0],path,0,false,true},null);
            var live=(AttackContact)Static(model,"KnownFamily",player,sources,path,Clock(path,"CompletedWorld"),Terrain(),receivers);
            var args=new object[]{player,path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};object flight;
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=Call(attack,"ReadFlight",args);
            var observed=flight==null?null:(AttackContact)Get(flight,"Contact");
            Console.WriteLine("BOOK live tick/sample="+Main.GameUpdateCount+"/"+path.SampleTick+" slots/keys="+string.Join(",",sources.Select(q=>q.whoAmI+"/"+(int)q.key))+" public="+observed?.Tick+"/"+observed?.Subupdate+" known="+live?.Tick+"/"+live?.Subupdate);
            Require((bool)args[3] && live!=null && observed!=null && observed.Tick==live.Tick && observed.Subupdate==live.Subupdate,"public Book Flight consumes its complete actual family at the independently known first receive");
            return path.SampleTick+live.Tick;
        }
        private static void Natural(object context)
        {
            foreach(bool alt in new[]{false,true})
            {
                var p=Scene(context,alt);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=knownCalls=depth=steps=scans=rng=capturedSolves=capturedKnown=lightRejects=captureFailures=abortedSolves=abortedKnown=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)Step(context,raw,false,false);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0,"withdrawn idle Book preview neither solves nor fires");
                raw=alt?target.Center:target.Center-new Vector2(380,0);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));record=true;naturalContext=context;preparedHands=0;int previews=0;long observedAt=-1,flightAt=-1;
                for(int frame=0;frame<55;frame++)
                {
                    Step(context,raw,!alt,alt);
                    if(frame>=2)
                    {
                        int time=(int)Static(model,"UseTime",p);bool gate=(bool)Static(model,"CanBirth",p.itemAnimation,p.itemAnimationMax,time,alt);
                        int flights=born.Count(b=>b.Shot.active && b.Shot.friendly && b.Key==(int)b.Shot.key);
                        Console.WriteLine("BOOK continuous alt/frame="+alt+"/"+frame+" tick="+Main.GameUpdateCount+" animation/max/time="+p.itemAnimation+"/"+p.itemAnimationMax+"/"+p.itemTime+" burstGate="+gate+" actualFlights="+flights);
                        Require(GetOptional(attack,"ExpectedPreview")==null,"all53 stable Book frames keep the retired hypothetical preview absent");previews++;
                    }
                    if(frame==6){observedAt=Main.GameUpdateCount;flightAt=ExistingFlight(attack,p);}
                }
                Step(context,raw,false,false);record=false;naturalContext=null;for(int frame=0;frame<40 && born.Any(b=>b.First<0 && b.Shot.active && b.Key==(int)b.Shot.key);frame++)Step(context,raw,false,false);
                foreach(var b in born){int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("BOOK natural alt="+alt+" type="+b.Type+" tick="+b.Tick+" slot/key="+b.Slot+"/"+b.Key+" input="+b.CursorInput+" center/V="+b.Center+"/"+b.Velocity+" first/known="+actual+"/"+b.Known?.Tick+" sub="+b.Sub+"/"+b.Known?.Subupdate);Require(actual>0 && b.Known!=null && actual==b.Known.Tick && b.Sub==b.Known.Subupdate,"actual Book source has positive immortal488 Strike at its own known first clock");if(!alt)Require(Vector2.Dot(b.Velocity,raw-b.Origin)<0,"real primary first-use assistance and stored burst point opposite physical cursor");}
                var next=born.FirstOrDefault(b=>b.Tick>observedAt);Require(next!=null && next.Known!=null,"a real subsequent native Book hand supplies the independent next-use comparator");long nextAt=next.Tick+next.Known.Tick-1;
                Console.WriteLine("BOOK existing-before-hand alt="+alt+" observed="+observedAt+" existingFirst="+flightAt+" nextBorn="+next.Tick+" nextKnownFirst="+nextAt+" nextNativeFirst="+next.First);
                Require(flightAt<nextAt && next.First==nextAt,"the observed Book family really precedes the next actual legal hand's independently witnessed damage");
                Console.WriteLine("BOOK natural counters alt="+alt+" births="+born.Count+" observations="+previews+" solve/known/capture/motor="+solves+"/"+knownCalls+"/"+scans+"/"+steps+" rng="+rng+" capturedSolve/known/lightReject/failure="+capturedSolves+"/"+capturedKnown+"/"+lightRejects+"/"+captureFailures+" abortedBeforeCapture="+abortedSolves+"/"+abortedKnown);
                Require(born.Count>=(alt?2:8) && preparedHands>=2 && previews==53,"first and continuous genuine Book uses retain both actual prepared hands and all53 stable native observations");
                Require(rng==0,"Book model preparation consumes no native RNG");
                Require(captureFailures==0 && depth==0 && capturedSolves>0 && capturedKnown+abortedKnown==knownCalls && solves==capturedSolves+lightRejects+abortedSolves && scans==capturedSolves+capturedKnown,"every Book call captures once or exits before capture, including separately observed normal budget cancellations; no observer scope escapes into native RNG");
                Console.WriteLine("PASS BOOK natural alt="+alt+" births="+born.Count+" previews="+previews+" solve/known/capture/motor="+solves+"/"+knownCalls+"/"+scans+"/"+steps);
            }
        }
    }
}
