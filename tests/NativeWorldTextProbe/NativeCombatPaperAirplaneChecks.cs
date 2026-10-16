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
    internal static class NativeCombatPaperAirplaneChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;
        private static bool natural,auditCurrent;private static Projectile aiBody,killBody;private static int births,positive,solves,steps,currentActions,dropRequests,killDamageCalls;
        private static int naturalAction,assistedBirths,manualBirths;private static bool physicalLeft;
        private static long first,birthTick;private static AttackContact known;private static Vector2 raw;
        private static Projectile firstBorn;private static object firstBornKey;private static int firstBornSlot;
        private sealed class Draw {internal int Minimum,Maximum,Value;}
        private sealed class Input {internal Projectile Before;internal object Terrain;internal float Wind;internal double Visual;internal readonly List<Draw> Draws=new List<Draw>();}
        private static Input current;
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static long BirthUlps(float a,float b)
        {
            if(a==b)return 0;if(float.IsNaN(a) || float.IsNaN(b) || float.IsInfinity(a) || float.IsInfinity(b) || Math.Sign(a)!=Math.Sign(b))return long.MaxValue;
            return Math.Abs((long)BitConverter.ToInt32(BitConverter.GetBytes(a),0)-BitConverter.ToInt32(BitConverter.GetBytes(b),0));
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p){return (Projectile)Static(model,"Copy",p);}
        private static bool Paper(Projectile p){return p!=null && (p.type==761 || p.type==762);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;if(Paper(killBody))killDamageCalls++;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(!Paper(receiving) || __result<=0 || !ReferenceEquals(__instance,target))return;
            positive++;
            if(first<0)
            {
                if(natural)
                {
                    Console.WriteLine("PAPER first Strike tick="+Main.GameUpdateCount+" receiver="+receiving.type+"/"+receiving.whoAmI+"/"+receiving.key+" firstBorn="+firstBornSlot+"/"+firstBornKey+" damage="+__result);
                    Require(ReferenceEquals(receiving,firstBorn) && receiving.whoAmI==firstBornSlot && receiving.key.Equals(firstBornKey),"actual first positive Strike belongs to the first original birth token/key, not a later paper throw");
                }
                first=Main.GameUpdateCount;
            }
        }
        private static void BeforeAi(Projectile __instance){aiBody=__instance;}
        private static void AfterAi(){aiBody=null;}
        private static void RandomDraw(int __0,int __1,int __result)
        {
            if(!Paper(aiBody) || current==null)return;
            Require(__result>=__0 && __result<__1,"native paper clock draw remains in its actual half-open range");
            current.Draws.Add(new Draw{Minimum=__0,Maximum=__1,Value=__result});
        }
        private static void BeforeKill(Projectile __instance,out Projectile __state){__state=killBody;if(Paper(__instance))killBody=__instance;}
        private static void AfterKill(Projectile __state){killBody=__state;}
        private static bool Drop(IEntitySource __0,Vector2 __1,int __2)
        {
            if(!Paper(killBody))return true;
            Require(killBody.owner==Main.myPlayer && !killBody.noDropItem && __2==(killBody.type==761?4343:4344),"original paper Kill requests only its matching recoverable item");
            dropRequests++;return false; // Only the isolated item outlet; original Kill still executes.
        }
        private static void BeforeUpdate(Projectile __instance)
        {
            if(!auditCurrent || !Paper(__instance) || !__instance.active)return;
            Require(__instance.extraUpdates==0,"this current-action oracle uses one native subaction; default paper has no extra update");
            current=new Input{Before=Copy(__instance),Terrain=Terrain(),Wind=Main.WindForVisuals,Visual=Main.timeForVisualEffects};
        }
        private static bool Step(Input input,Projectile q,out bool drew)
        {
            int initial=55,reset=360,turn=210;
            foreach(var d in input.Draws)
            {
                if(d.Minimum==30 && d.Maximum==80)initial=d.Value;
                else if(d.Minimum==120 && d.Maximum==600)reset=d.Value;
                else if(d.Minimum==120 && d.Maximum==300)turn=d.Value;
                else Require(false,"only AI159 clock draws are observed inside the exact native AI method");
            }
            var args=new object[]{q,input.Terrain,input.Wind,input.Visual,initial,reset,turn,false};bool valid=(bool)Static(model,"Step",args);drew=(bool)args[7];return valid;
        }
        private static void AfterUpdate(Projectile __instance)
        {
            if(current==null || !auditCurrent || !Paper(__instance))return;var input=current;current=null;var q=input.Before;bool drew;
            Require(Step(input,q,out drew),"paper motor has a complete captured terrain input");Static(model,"Finish",q);currentActions++;
            // Native draws are observed, not replayed or substituted. This
            // diagnoses exact current motor separately from Representative Solve.
            Require(drew==(input.Draws.Count>0),"pure current-action clock consumer matches actual native random call sites");
            Console.WriteLine("PAPER current type="+__instance.type+" actual="+__instance.position+"/"+__instance.velocity+" ai="+string.Join(",",__instance.ai)+" model="+q.position+"/"+q.velocity+" draws="+input.Draws.Count+" wind="+input.Wind+" visual="+input.Visual);
            Require(Vector2.DistanceSquared(q.position,__instance.position)<.002f && Vector2.DistanceSquared(q.velocity,__instance.velocity)<.00001f && q.ai.SequenceEqual(__instance.ai) && q.direction==__instance.direction && q.spriteDirection==__instance.spriteDirection && Math.Abs(q.rotation-__instance.rotation)<.00001f && q.wet==__instance.wet && q.honeyWet==__instance.honeyWet && q.shimmerWet==__instance.shimmerWet,"original AI159 motor, manual direction, actual clock and wet movement agree");
            if(!natural)Require(q.active==__instance.active && q.timeLeft==__instance.timeLeft,"native paper retirement and lifetime agree");
        }
        private static Player Reset(object context,int item=4343)
        {
            natural=auditCurrent=false;current=null;target=null;receiving=aiBody=killBody=null;births=positive=assistedBirths=manualBirths=0;first=-1;known=null;firstBorn=null;firstBornKey=null;firstBornSlot=-1;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);p.HeldItem.stack=999;Main.windPhysics=false;Main.windSpeedCurrent=0;Main.timeForVisualEffects=0;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=18;n.height=40;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n,bool beforeNpc=true)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-(beforeNpc?1:0),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path,bool before=true){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,before,!before},null);}
        // The finite CPU harness already owns original Player/NPC/Projectile
        // actions but does not run Main's render-facing prefix. Supply that
        // captured .8 visual clock progression explicitly; do not call model At
        // to manufacture the native input or claim original World.Draw.
        private static void AdvanceVisual(){Main.timeForVisualEffects+=1;if(Main.timeForVisualEffects>=216000)Main.timeForVisualEffects=0;}
        private static void NaturalStep(object context,Vector2 mouse,bool left,bool pure=false){AdvanceVisual();NativeCombatSelectionPreviewChecks.Step(context,mouse,left,pure);}
        private static object naturalContext;private static int preparedHands;
        private static void BeforeNaturalShoot(Player __instance)
        {
            if(!natural || !physicalLeft || naturalContext==null || !ReferenceEquals(__instance,Main.LocalPlayer))return;
            // AdvanceVisual and this real Player prefix have already run. Keep
            // their original GUC/visual relation and the still-T-1 NPC sample.
            var host=Get(naturalContext,"CombatObservation");var attack=Get(Get(naturalContext,"Combat"),"Attack");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0,phaseName:"BeforeNpc");
            Require(GetOptional(attack,"ExpectedImpact")!=null,"actual paper Shoot prepares under1000ms mechanism allowance at its native visual clock");preparedHands++;
        }
        private static void Born(IEntitySource __0,int __result)
        {
            if(!natural || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(!Paper(q))return;births++;
            var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            var expected=(Vector2)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"BirthVelocity",p,origin,cursor,p.HeldItem.shootSpeed);
            var scope=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true).GetField("owner",Flags).GetValue(null);bool borrowed=scope!=null && !(bool)Get(scope,"ended");
            if(borrowed)assistedBirths++;else manualBirths++;
            Console.WriteLine("PAPER actual birth action="+naturalAction+" physicalLeft="+physicalLeft+" controlUse="+p.controlUseItem+" borrowed="+borrowed+" tick="+Main.GameUpdateCount+" item="+p.HeldItem.type+" type="+q.type+" center="+q.Center+" V="+q.velocity+" cursor="+cursor+" raw="+raw+" stack="+p.HeldItem.stack+" anim/time="+p.itemAnimation+"/"+p.itemTime);
            Require(q.type==p.HeldItem.shoot && q.manualDirectionChange && q.aiStyle==159 && q.ai[1]==0 && q.damage==p.GetWeaponDamage(p.HeldItem) && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f,"actual paper hand uses native origin, consumed integer cursor, type and velocity");
            if(physicalLeft){Require(borrowed && (int)Get(scope,"ownX")==Main.mouseX && (int)Get(scope,"ownY")==Main.mouseY && Vector2.Dot(q.velocity,raw-origin)<0,"each still-authorized real paper birth consumes its current legal borrowed cursor opposite physical mouse");}
            if(births==1){firstBorn=q;firstBornKey=q.key;firstBornSlot=q.whoAmI;birthTick=Main.GameUpdateCount;var path=Timeline(target);known=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));}
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPaperAirplaneAttack",true);var audit=new Harmony("JueMingR.Tests.PaperAirplane");
            bool oldWind=Main.windPhysics;float speed=Main.windSpeedCurrent;double visual=Main.timeForVisualEffects;var priorSpelunker=Main.instance.SpelunkerProjectileHelper;Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(StepWork)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(BeforeNaturalShoot)){priority=Priority.First+1000});
            audit.Patch(typeof(Projectile).GetMethod("AI_159_PaperAirplanes",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(BeforeAi)),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(AfterAi)));
            audit.Patch(typeof(Terraria.Utilities.UnifiedRandom).GetMethod("Next",new[]{typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(RandomDraw)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(BeforeKill)),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(AfterKill)));
            audit.Patch(typeof(Item).GetMethod("RequestNewItem",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(Drop)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(BeforeUpdate)),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(AfterUpdate)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(AfterDamage)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatPaperAirplaneChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_PAPER_AIRPLANE_LEAF");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="receive" || leaf=="natural","known Paper leaf");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor"){Births(context);Motors(context);Leases(context);}
                if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);
                if(string.IsNullOrEmpty(leaf) || leaf=="natural")
                {
                    int selected=0;string item=Environment.GetEnvironmentVariable("JMR_PAPER_AIRPLANE_ITEM");Require(string.IsNullOrEmpty(item) || int.TryParse(item,out selected) && (selected==4343 || selected==4344),"known actual paper item selector");
                    foreach(int kind in new[]{4343,4344})if(selected==0 || selected==kind)Natural(context,kind);
                }
            }
            finally{naturalContext=null;natural=auditCurrent=false;current=null;aiBody=killBody=receiving=null;audit.UnpatchAll(audit.Id);Main.windPhysics=oldWind;Main.windSpeedCurrent=speed;Main.timeForVisualEffects=visual;Main.instance.SpelunkerProjectileHelper=priorSpelunker;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Births(object context)
        {
            // Birth probes borrow screen dimensions only for native reverse-gravity mapping.
            // Restore them before later physical-input samples use the real viewport.
            int screenWidth=Main.screenWidth,screenHeight=Main.screenHeight;
            try
            {
            int count=0;
            foreach(int item in new[]{4343,4344})foreach(float gravity in new[]{1f,-1f})
            {
                var p=Reset(context,item);p.position=new Vector2(650.25f,646.5f);p.gravDir=gravity;p.itemAnimationMax=17;p.itemAnimation=p.itemTime=16;Main.screenPosition=new Vector2(500.25f,400.5f);Main.screenHeight=600;Main.mouseX=300;Main.mouseY=200;Player.tileTargetX=37;Player.tileTargetY=38;
                Require(p.HeldItem.consumable && p.HeldItem.ranged && p.HeldItem.noMelee && !p.HeldItem.autoReuse && p.HeldItem.useAnimation==17,"native paper items are seventeen-action consumable throws, not a controlled/channel weapon");
                var aim=new Vector2(910.25f,660.75f);var scope=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true),"Begin",aim,true);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);var expected=(Projectile)Static(model,"Birth",p,ammo,origin,aim,Terrain());
                try{typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});}finally{Call(scope,"End");}
                var q=Main.projectile.Single(s=>s.active && s.type==p.HeldItem.shoot);long xUlps=expected==null?long.MaxValue:BirthUlps(q.velocity.X,expected.velocity.X),yUlps=expected==null?long.MaxValue:BirthUlps(q.velocity.Y,expected.velocity.Y);Console.WriteLine("PAPER birth item/gravity="+item+"/"+gravity+" actual="+q.Center+"/"+q.velocity+" ai="+string.Join(",",q.ai)+" manual="+q.manualDirectionChange+" expected="+expected?.Center+"/"+expected?.velocity+" ai="+(expected==null?"null":string.Join(",",expected.ai))+" manual="+expected?.manualDirectionChange+" velocityULP="+xUlps+","+yUlps);
                // Native x86 keeps Shoot's scalar intermediates in x87; the
                // independent Vector2 birth stores fields as Single. Only this
                // four-case birth comparison admits one ULP per component.
                // Origins/AI/flags and all existing motor/receive gates stay exact.
                Require(expected!=null && q.Center==expected.Center && xUlps<=1 && yUlps<=1 && q.ai.SequenceEqual(expected.ai) && q.manualDirectionChange,"actual normal/reverse paper Shoot agrees within one stored float ULP before first original random clock draw");Require(Main.mouseX==300 && Main.mouseY==200 && Player.tileTargetX==37 && Player.tileTargetY==38,"ordinary paper borrowed cursor restores mouse and never borrows tile targets");count++;
            }
            Console.WriteLine("PASS PAPER actual births="+count+" pairedCursor=True noTileTarget=True");
            }
            finally{Main.screenWidth=screenWidth;Main.screenHeight=screenHeight;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();}
        }
        private static void Motors(object context)
        {
            int before=currentActions;dropRequests=killDamageCalls=0;
            // The two defaults and shared clock/wind/liquid/exit boundaries.
            // Every random value below is read AFTER its one real native draw.
            for(int scene=0;scene<18;scene++)
            {
                var p=Reset(context,scene%2==0?4343:4344);Main.windSpeedCurrent=scene==4?-.7f:scene==3 || scene==5 || scene==6?.7f:scene==7?-1f:0;Main.timeForVisualEffects=scene==5?89:scene==6?215999:23;
                if(scene>=9 && scene<=12)for(int x=40;x<100;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene-9);}
                if(scene==15)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                var velocity=new Vector2(scene==2?0:scene==7?.0001f:scene==8?7:scene==16?-6:5,scene==2?5:.25f);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene==16?new Vector2(8,350):new Vector2(935,350),velocity,p.HeldItem.shoot,20,0,p.whoAmI);var q=Main.projectile[slot];
                if(scene>=2){q.ai[1]=1;q.ai[0]=-10;q.direction=1;q.rotation=q.velocity.ToRotation();}
                if(scene==2){q.ai[1]=0;q.direction=1;}if(scene==5)q.ai[0]=19;if(scene==6)q.ai[0]=69;if(scene==7)q.rotation=MathHelper.Pi;if(scene==13)q.timeLeft=1;if(scene==17)q.noDropItem=true;
                bool active=p.active;var foreign=Main.player[1];bool foreignActive=foreign.active;
                try
                {
                    if(scene==12){q.owner=1;foreign.active=true;}if(scene==14)p.active=false;
                    auditCurrent=true;for(int action=0;action<24 && q.active;action++){if(action>0)AdvanceVisual();Main.instance.SpelunkerProjectileHelper.OnPreUpdateAllProjectiles();q.Update(slot);}auditCurrent=false;
                    if(scene>=9 && scene<=11 || scene==13 || scene==14 || scene==15)Require(!q.active,"native owned wet/tile/lifetime/owner-inactive retirement occurs");
                    if(scene==12)Require(q.active && q.owner!=Main.myPlayer && q.shimmerWet,"foreign wet owner keeps native shimmer flight; no invented AI33 bounce/kill");
                    if(scene==16)Require(!q.active && q.timeLeft>0,"original outworld exit is inactive without Kill/item-drop");
                    if(scene==17){int old=dropRequests;q.Kill();Require(dropRequests==old,"noDropItem suppresses original recoverable item request");}
                }
                finally{p.active=active;foreign.active=foreignActive;auditCurrent=false;current=null;}
            }
            Require(dropRequests>=6 && killDamageCalls==0,"original owned paper kills request matching items but never run an extra damaging explosion");Console.WriteLine("PASS PAPER motor classes=18 actualActions="+(currentActions-before)+" originalDrops="+dropRequests+" noLiveItemCreation=True");
        }
        private static void Leases(object context)
        {
            Reset(context);var t=model.GetNestedType("State",Flags);var count=typeof(Main).GetField("_gameUpdateCount",Flags);uint old=Main.GameUpdateCount;double visual=Main.timeForVisualEffects;float wind=Main.windSpeedCurrent;
            try
            {
                Main.timeForVisualEffects=215999;Main.windSpeedCurrent=.4f;Main.windPhysics=false;var state=Activator.CreateInstance(t,Flags,null,new object[]{true},null);Require((bool)Get(state,"Current"),"current paper wind is consumed with global wind physics OFF");
                count.SetValue(null,unchecked(old+1));Main.timeForVisualEffects=0;Require((bool)Get(state,"Current"),"exact visual clock wrap remains current on the next real world epoch");
                Main.windSpeedCurrent=-.4f;Require(!(bool)Get(state,"Current"),"actual AI159 wind change revokes the plan even with windPhysics false");Main.windSpeedCurrent=.4f;Main.timeForVisualEffects=1;Require(!(bool)Get(state,"Current"),"visual time not matching whole-world progress revokes the lease");
            }
            finally{count.SetValue(null,old);Main.timeForVisualEffects=visual;Main.windSpeedCurrent=wind;}
            var player=Main.LocalPlayer;target=Target(6,new Vector2(990,350));int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(945,350),new Vector2(5,0),761,20,0,player.whoAmI);var q=Main.projectile[slot];q.ai[1]=1;q.ai[0]=-60;q.direction=1;
            var path=Timeline(target);var root=Obstacles(player,q,path);Require(Static(model,"Known",player,q,path,Clock(path),Terrain(),root)!=null && (bool)Get(root,"BirthInputsCurrent") && (bool)Get(root,"FamilyMembersCurrent"),"actual Known preparation owns one clock and finite member lease");
            float previousWind=Main.windSpeedCurrent;try{Main.windSpeedCurrent+=.1f;Require(!(bool)Get(root,"BirthInputsCurrent"),"root consume/CPU birth-input lease includes AI159 wind");}finally{Main.windSpeedCurrent=previousWind;}
            float rotation=q.rotation;try{q.rotation+=.01f;Require(!(bool)Get(root,"FamilyMembersCurrent"),"actual observed flight rotation is motor state, not ignored cosmetic history");}finally{q.rotation=rotation;}
            Console.WriteLine("PASS PAPER clock/wind leases=4 finiteKnownLease=3");
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<8;mode++)
            {
                var p=Reset(context,mode%2==0?4343:4344);target=Target(6,new Vector2(990,350));if(mode==2 || mode==3)target.immune[p.whoAmI]=500;if(mode==4)target.dontTakeDamage=true;if(mode==5)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);if(mode==6)for(int x=40;x<100;x++)for(int y=10;y<60;y++)Main.tile[x,y].liquid=255;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(945,350),new Vector2(5,0),p.HeldItem.shoot,40,0,p.whoAmI);var q=Main.projectile[slot];q.ai[1]=1;q.ai[0]=-60;q.direction=1;q.rotation=0;if(mode==3)q.penetrate=q.maxPenetrate=2;if(mode==7)target.reflectsProjectiles=true;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                for(int outer=1;outer<=20 && q.active && first<0;outer++){if(outer>1){NativeQuickItemChecks.BeginWorldStep();AdvanceVisual();}for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==3 || mode==4 || mode==5 || mode==6;
                Console.WriteLine("PAPER receive mode="+mode+" nativeTick="+actual+" predicted="+contact?.Tick+" confidence="+contact?.Confidence);
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"paper original receive/maximum-one owner-immune exemption/tile/wet retirement agree");if(mode==7)Require(q.friendly && !q.hostile,"AI159 is not one of the native reflectable projectile roles");
            }
            Console.WriteLine("PASS PAPER receives=8 actual positive Strike/negative controls");
        }
        private static void Natural(object context,int item)
        {
            var p=Reset(context,item);p.position=new Vector2(700,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(550,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,51,40);raw=target.Center-new Vector2(430,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;int currentStart=currentActions;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));int stack=p.HeldItem.stack;
            for(int action=0;action<3;action++){NaturalStep(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && !p.controlUseItem && p.HeldItem.stack==stack && solves==0,"withdrawn idle paper preview neither solves nor consumes a real throw");}
            natural=auditCurrent=true;naturalContext=context;preparedHands=0;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int visible=0;
            for(int action=0;action<52;action++)
            {
                bool left=action<20 || action>=22 && action<44;naturalAction=action;physicalLeft=left;NaturalStep(context,raw,left);var flight=Observe(attack,p);var shown=flight==null?null:(AttackContact)Get(flight,"Contact");Console.WriteLine("PAPER use action="+action+" physicalLeft="+left+" nativeUse="+p.controlUseItem+" anim/time="+p.itemAnimation+"/"+p.itemTime+" born="+births+" actualFlight="+shown?.Tick+" confidence="+shown?.Confidence);
                Require(GetOptional(attack,"ExpectedPreview")==null,"all52 actual paper actions keep the retired hypothetical preview absent");if(shown!=null)visible++;
                int a=solves,b=steps;var impact=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
                for(int draw=0;draw<12;draw++){Call(impact,"Capture");Call(impact,"Project",zoom,Matrix.Invert(zoom));if(flight!=null)Require((bool)Call(flight,"PresentationCurrent",0),"same-update actual paper family CPU presentation is current");}Require(a==solves && b==steps,"paper CPU Capture/Project performs zero solves/motor/RNG replay");
            }
            // Native initial ai0=-Next(30,80), followed by this same action's
            // ++, reaches the first loop motor branch (ai0=20) no earlier
            // than action 50. Preserve exact born-path timing, rather than
            // imposing an unrelated 25-action distance on this real dummy.
            long firstOrdinal=first-birthTick+1;
            Console.WriteLine("PAPER first receiving births="+births+" positive="+positive+" firstTick="+first+" birthTick="+birthTick+" ordinal="+firstOrdinal+" knownTick="+known?.Tick+" target="+target.position+"/"+target.Size+" earliestLoop=50");
            Require(births>=2 && preparedHands>=2 && visible>0 && positive>0 && known!=null && firstOrdinal==known.Tick && firstOrdinal>0 && firstOrdinal<50,"actual prepared first Strike precedes every possible initial loop-clock bifurcation and matches born path; public Flight observes the real source");
            Require(p.HeldItem.stack<stack && !p.controlUseItem,"real consumable use decreases its stack and physical release returns use permission");naturalContext=null;natural=auditCurrent=false;
            Console.WriteLine("PASS PAPER natural item="+item+" actions=52 previews="+visible+" births="+births+" assisted="+assistedBirths+" legalManual="+manualBirths+" positive="+positive+" first="+(first-birthTick+1)+" bornKnown="+known.Tick+" actualMotor="+(currentActions-currentStart)+" CPUCaptureProject=12x0 DrawUnverified=True");
        }
        private static object Observe(object attack,Player player)
        {
            var path=Timeline(target,false);var args=new object[]{player,path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return Call(attack,"ReadFlight",args);
        }
    }
}
