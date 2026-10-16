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
    internal static class NativeCombatOrnamentChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,killBody;
        private static bool observe,auditCurrent,natural,firstBornMatched;private static Projectile firstBorn;private static ProjectileKey firstBornKey;private static int solves,steps,kills,killDamage,handBirths,childBirths,currentActions,positive,firstType,naturalAction;
        private static long first,birthTick;private static AttackContact known;private static Vector2 raw;private static bool physicalLeft;
        private static readonly HashSet<ProjectileKey> sources=new HashSet<ProjectileKey>();
        private static readonly List<Projectile> newborns=new List<Projectile>();
        private sealed class Input {internal Projectile Shot;internal object Terrain;internal int BornAt;}
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static bool Family(Projectile p){return p!=null && (p.type==907 || p.type==335);}
        private static Projectile Copy(Projectile p){return (Projectile)Static(model,"Copy",p);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static bool SkipNetwork(int msgType,int number)
        {
            // Only this isolated family's original spawn/sync outlet is
            // swallowed. Its real NewProjectile/AI/Update/Kill still runs.
            if(msgType!=27 || number<0 || number>=Main.projectile.Length)return true;
            return !Family(Main.projectile[number]);
        }
        private static void BeforeKill(Projectile __instance,out Projectile __state){__state=killBody;if(Family(__instance)){killBody=__instance;kills++;}}
        private static void AfterKill(Projectile __state){killBody=__state;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;if(Family(killBody))killDamage++;}
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(!Family(receiving) || __result<=0 || !ReferenceEquals(__instance,target) || !sources.Contains(receiving.key))return;
            positive++;if(first>=0)return;first=Main.GameUpdateCount;firstType=receiving.type;firstBornMatched=ReferenceEquals(receiving,firstBorn) && receiving.key.Equals(firstBornKey);
            Console.WriteLine("ORNAMENT first Strike tick="+first+" role="+receiving.type+"/"+receiving.whoAmI+"/"+receiving.key+" damage="+__result);
        }
        private static void Born(IEntitySource __0,int __result)
        {
            if(!observe || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(!Family(q))return;
            bool child=__0 is EntitySource_Parent parent && parent.Entity is Projectile;
            if(child)
            {
                childBirths++;var p=(Projectile)((EntitySource_Parent)__0).Entity;
                if(sources.Contains(p.key))sources.Add(q.key);
                Require(p.type==907 && q.type==335 && p.ai[0]>0 && p.ai[0]<50 && p.ai[0]%10==0 && q.Center==p.Center && q.ai[0]==0 && q.ai[1]>=0 && q.ai[1]<4,"real 907 emits 335 from its pre-movement center at native clock10/20/30/40; color remains a legal cosmetic draw");
            }
            else
            {
                handBirths++;sources.Add(q.key);
                if(natural)
                {
                    var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
                    var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var expected=(Projectile)Static(model,"Birth",p,ammo,origin,cursor,q.whoAmI,Terrain());
                    var scope=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true).GetField("owner",Flags).GetValue(null);bool borrowed=scope!=null && !(bool)Get(scope,"ended");
                    Console.WriteLine("ORNAMENT hand action="+naturalAction+" physicalLeft="+physicalLeft+" borrowed="+borrowed+" tick="+Main.GameUpdateCount+" slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" cursor="+cursor+" anim/time="+p.itemAnimation+"/"+p.itemTime);
                    Require(q.type==907 && expected!=null && q.Center==expected.Center && Vector2.DistanceSquared(q.velocity,expected.velocity)<.0001f && q.damage==expected.damage && q.ai[0]==0 && q.penetrate==2,"actual hand starts the native star with its real consumed cursor/origin/damage");
                    if(physicalLeft)Require(borrowed && (int)Get(scope,"ownX")==Main.mouseX && (int)Get(scope,"ownY")==Main.mouseY && Vector2.Dot(q.velocity,raw-origin)<0,"authorized star birth consumes its own assisted cursor, opposite physical mouse");
                    if(handBirths==1){firstBorn=q;firstBornKey=q.key;birthTick=Main.GameUpdateCount;var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));}
                }
            }
            newborns.Add(Copy(q));
        }
        private static bool SameChild(Projectile child,Projectile actual)
        {
            // NewProjectile assigns color twice through actual RNG. Neither
            // frame nor ai1 is a motor/receive input; do not copy that draw.
            return child.type==actual.type && child.position==actual.position && Vector2.DistanceSquared(child.velocity,actual.velocity)<.00001f && child.owner==actual.owner && child.damage==actual.damage && child.knockBack==actual.knockBack && child.ai[0]==actual.ai[0] && child.armorPenetration==actual.armorPenetration && child.bonusTagDamage==actual.bonusTagDamage && child.bonusCritChance==actual.bonusCritChance && child.tagEffectType==actual.tagEffectType;
        }
        private static bool SameMotion(Projectile expected,Projectile actual)
        {
            return expected.type==actual.type && Vector2.DistanceSquared(expected.position,actual.position)<.002f && Vector2.DistanceSquared(expected.velocity,actual.velocity)<.00001f && expected.ai.SequenceEqual(actual.ai) && expected.localAI[0]==actual.localAI[0] && Math.Abs(expected.rotation-actual.rotation)<.00001f && expected.direction==actual.direction && expected.wet==actual.wet && expected.honeyWet==actual.honeyWet && expected.shimmerWet==actual.shimmerWet;
        }
        private static void BeforeUpdate(Projectile __instance,out Input __state)
        {
            __state=null;if(!auditCurrent || !Family(__instance) || !__instance.active)return;
            Require(__instance.extraUpdates==0,"native defaults use one full subaction for this current oracle");
            __state=new Input{Shot=Copy(__instance),Terrain=Terrain(),BornAt=newborns.Count};
        }
        private static void AfterUpdate(Projectile __instance,Input __state)
        {
            if(__state==null)return;bool match=false;int born=newborns.Count-__state.BornAt;
            // Exactly twenty legal angle/speed pairs, only at a real emission.
            // Actual Update remains unique; no live RNG or second AI executes.
            bool emits=__state.Shot.type==907 && __state.Shot.owner==Main.myPlayer && __state.Shot.ai[0]+1<50 && (__state.Shot.ai[0]+1)%10==0;
            for(int angle=emits?-5:0;angle<(emits?5:1) && !match;angle++)for(int speed=0;speed<(emits?2:1) && !match;speed++)
            {
                var expected=Copy(__state.Shot);var args=new object[]{expected,__state.Terrain,angle,speed,null};Require((bool)Static(model,"Step",args),"copied ornament current action retains terrain");var child=(Projectile)args[4];Static(model,"Finish",expected);
                bool childMatch=child==null?born==0:born==1 && SameChild(child,newborns[__state.BornAt]);
                if(SameMotion(expected,__instance) && childMatch && (natural || expected.active==__instance.active && expected.timeLeft==__instance.timeLeft))match=true;
            }
            Require(match,"native current ornament motor/child/retirement agrees with one legal private branch type="+__state.Shot.type+" slot="+__state.Shot.whoAmI+" clock="+__state.Shot.ai[0]+" prior="+__state.Shot.position+"/"+__state.Shot.velocity+" native="+__instance.position+"/"+__instance.velocity+" ai="+string.Join(",",__instance.ai)+" children="+born);currentActions++;
        }
        private static Player Reset(object context)
        {
            observe=auditCurrent=natural=firstBornMatched=false;target=null;firstBorn=null;receiving=killBody=null;sources.Clear();newborns.Clear();handBirths=childBirths=kills=killDamage=positive=0;first=-1;known=null;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1928,0,0);p.fullRotation=p.gfxOffY=0;Main.netMode=0;Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n,bool beforeNpc=true)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-(beforeNpc?1:0),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostOrnamentAttack",true);var audit=new Harmony("JueMingR.Tests.Ornament");bool wind=Main.windPhysics;int mode=Main.netMode;var priorSpelunker=Main.instance.SpelunkerProjectileHelper;Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            var outlet=new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(SkipNetwork));outlet.priority=Priority.First+100;audit.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:outlet);
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(BeforeUpdate)),postfix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(AfterUpdate)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(BeforeKill)),postfix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(AfterKill)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(AfterDamage)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatOrnamentChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_ORNAMENT_LEAF");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="receive" || leaf=="natural","known Ornament leaf");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor"){Births(context);Motors(context);FamilyOrder(context);}
                if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);
                if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{observe=auditCurrent=natural=false;receiving=killBody=null;target=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.netMode=mode;Main.instance.SpelunkerProjectileHelper=priorSpelunker;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Births(object context)
        {
            // Birth probes borrow screen dimensions only for native reverse-gravity mapping.
            // Restore them before later physical-input samples use the real viewport.
            int screenWidth=Main.screenWidth,screenHeight=Main.screenHeight;
            try
            {
            foreach(float gravity in new[]{1f,-1f})
            {
                var p=Reset(context);p.position=new Vector2(650.25f,600.5f);p.gravDir=gravity;p.fullRotation=.2f;Main.screenPosition=new Vector2(500.25f,400.5f);Main.screenHeight=600;Main.mouseX=430;Main.mouseY=250;Player.tileTargetX=37;Player.tileTargetY=38;
                var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX+Main.screenPosition.X,gravity==-1?Main.screenPosition.Y+Main.screenHeight-Main.mouseY:Main.mouseY+Main.screenPosition.Y);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var expected=(Projectile)Static(model,"Birth",p,ammo,origin,cursor,0,Terrain());
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active);
                Require(p.HeldItem.useStyle==1 && !p.HeldItem.noMelee && p.HeldItem.autoReuse && p.HeldItem.useTime==23 && q.type==907 && q.aiStyle==30 && q.width==22 && q.height==22 && q.penetrate==2 && expected!=null && q.Center==expected.Center && Vector2.DistanceSquared(q.velocity,expected.velocity)<.0001f && q.damage==expected.damage,"actual 1928 style1 hand keeps its real melee role and one 907 birth, normal/reverse gravity");
                Require(Player.tileTargetX==37 && Player.tileTargetY==38,"ornament uses ordinary integer mouse mapping, without borrowing tile targets");
            }
            Console.WriteLine("PASS ORNAMENT actual hand births=2 normal/reverse; handMelee=True star907/child335");
            }
            finally{Main.screenWidth=screenWidth;Main.screenHeight=screenHeight;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();}
        }
        private static void Motors(object context)
        {
            int actions=currentActions,cases=0,children=0;
            for(int scene=0;scene<18;scene++)
            {
                var p=Reset(context);var foreign=Main.player[1];bool active=p.active,foreignActive=foreign.active;int oldMode=Main.netMode;
                try
                {
                    int type=scene<6?907:335;int count=scene==0?42:scene==6?95:scene>=10 && scene<=13?4:scene==14?8:3;
                    var center=scene==5?new Vector2(8,350):new Vector2(945,350);var velocity=scene==6?new Vector2(1,.25f):scene==7 || scene==9?new Vector2(.2f,0):scene==8?Vector2.Zero:new Vector2(5,0);
                    if(scene>=10 && scene<=13)for(int x=40;x<100;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene-10);}
                    if(scene==14)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                    foreign.active=true;int owner=scene==4?1:p.whoAmI;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,20,2,owner);var q=Main.projectile[slot];q.armorPenetration+=3;
                    if(scene==1)q.ai[0]=49;if(scene==2)q.ai[0]=99;if(scene==3){q.ai[0]=9;q.timeLeft=1;}if(scene==8)q.ai[0]=1;if(scene==9)Main.netMode=1;if(scene==15)p.active=false;if(scene==16)q.timeLeft=1;if(scene==17)q.penetrate=0;
                    observe=auditCurrent=true;int before=childBirths;for(int step=0;step<count && q.active;step++){Main.instance.SpelunkerProjectileHelper.OnPreUpdateAllProjectiles();q.Update(slot);}
                    children+=childBirths-before;
                    if(scene==0)Require(childBirths==4,"native star emits exactly four ornaments at clock10/20/30/40");
                    if(scene==2 || scene==3 || scene>=14)Require(!q.active,"actual clock/tile/owner/time/penetration retirement is preserved");
                    if(scene==3)Require(childBirths==1,"clock10 birth precedes the parent's lifetime Kill, which emits no additional child");
                    if(scene==4)Require(childBirths==0,"foreign owner does not execute the local-only child spawn");
                    if(scene==5)Require(!q.active && kills==0 && q.timeLeft==3600,"world-edge exit is inactive without Kill or lifetime decrement");
                    if(scene==6)Require(q.ai[0]<0 && q.velocity.Y>0,"observed ornament decelerates, really hovers, then falls on negative clock");
                    if(scene==7)Require(q.ai[0]==47 && q.velocity==Vector2.Zero,"single/server stop sets50 once then counts down");
                    if(scene==8)Require(q.ai[0]==-1 && q.velocity.Y==.25f,"already-zero ornament never invents another50 reset");
                    if(scene==9)Require(q.ai[0]==-1 && q.velocity.Y==.5f,"client stop does not write50; negative clock gravity follows");
                    if(scene==13)Require(q.active && q.shimmerWet,"AI30 has no copied AI33 shimmer bounce or artificial wet kill");
                    Require(killDamage==0,"original parent/child Kill has no additional damaging explosion");cases++;
                }
                finally{p.active=active;foreign.active=foreignActive;Main.netMode=oldMode;observe=auditCurrent=false;}
            }
            Console.WriteLine("PASS ORNAMENT motor classes="+cases+" actualActions="+(currentActions-actions)+" originalChildren="+children+" killExtraDamage=0");
        }
        private static void FamilyOrder(object context)
        {
            int cases=0;
            foreach(bool early in new[]{false,true})
            {
                var p=Reset(context);for(int i=0;i<10;i++){int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,500,0,0,335,0,0,p.whoAmI);Require(slot==i,"real ornament occupants hold ten low native slots");}
                int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),945,350,5,0,907,30,0,p.whoAmI);Require(parentSlot==10,"original star uses first free slot10");if(early)Main.projectile[0].Kill();var q=Main.projectile[parentSlot];q.ai[0]=9;
                target=Target(2,new Vector2(945,333),60,10);sources.Add(q.key);observe=true;NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                for(int outer=1;outer<=2;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active && Family(Main.projectile[slot]))Main.projectile[slot].Update(slot);}
                Console.WriteLine("ORNAMENT slot-order early="+early+" childSlot="+newborns.Single(s=>s.type==335).whoAmI+" actual="+(first-sample)+" plan="+known?.Tick+" role="+firstType);
                Require(childBirths==1 && firstType==335 && first-sample==(early?2:1) && known!=null && known.Tick==first-sample,"actual later-slot child moves/receives in its birth action; earlier-slot child waits for the next native loop");cases++;
            }
            foreach(int parity in new[]{0,1})
            {
                var p=Reset(context);if(parity==1)Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,500,0,0,335,0,0,p.whoAmI);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),945,350,5,0,907,20,0,p.whoAmI);var q=Main.projectile[slot];q.ai[0]=9;observe=auditCurrent=true;q.Update(slot);
                Require(childBirths==1 && Math.Sign(newborns[0].velocity.Y)==(parity==0?-1:1),"native parent slot parity flips the ornament's side for every legal angle representative");cases++;
            }
            Console.WriteLine("PASS ORNAMENT native slot/parity cases="+cases);
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<8;mode++)
            {
                var p=Reset(context);int type=mode==0 || mode==1?907:335;target=Target(2,new Vector2(990,350));if(mode==1 || mode==2)target.immune[p.whoAmI]=500;if(mode==3)target.dontTakeDamage=true;if(mode==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);if(mode==5)target.reflectsProjectiles=true;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),945,350,5,0,type,40,0,p.whoAmI);var q=Main.projectile[slot];if(type==907)q.ai[0]=50;if(mode==6)q.timeLeft=1;if(mode==7)q.damage=0;sources.Add(q.key);observe=true;NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var root=Obstacles(p,q,path);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),root);long sample=path.SampleTick;
                if(mode==0)
                {
                    Require(known!=null && (bool)Get(root,"FamilyMembersCurrent"),"finite known family is captured with its actual generation");q.ai[0]++;Require(!(bool)Get(root,"Unchanged"),"real emission clock change revokes the family lease");q.ai[0]--;Require((bool)Get(root,"Unchanged"),"restoring exact family input restores the existing lease");
                    foreach(string field in new[]{"aiStyle","ownerHitCheck","usesOwnerMeleeHitCD","manualDirectionChange"})
                    {
                        var f=typeof(Projectile).GetField(field,Flags);object old=f.GetValue(q);f.SetValue(q,f.FieldType==typeof(bool)?(object)!(bool)old:(int)old+1);
                        Require(!(bool)Get(root,"FamilyMembersCurrent"),"changed native ornament motor/receive qualification revokes finite family lease: "+field);f.SetValue(q,old);Require((bool)Get(root,"FamilyMembersCurrent"),"exact ornament field restore restores its lease: "+field);
                    }
                    Require(Static(model,"KnownFamily",p,new[]{q},path,Clock(path,"Projectiles"),Terrain(),Obstacles(p,q,path))==null,"partial projectile array cannot replay an earlier already-stepped family member");
                }
                for(int outer=1;outer<=100 && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active && Family(Main.projectile[i]))Main.projectile[i].Update(i);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==1 || mode==3 || mode==4 || mode==6 || mode==7;Console.WriteLine("ORNAMENT receive mode="+mode+" type="+type+" native="+actual+" predicted="+known?.Tick+" confidence="+known?.Confidence);
                Require((actual==0)==negative && (known==null)==negative && (known==null || known.Tick==actual),"actual 907 maxPen2 and335 maxPen1 owner-CD exemption, receiving/tile/lifetime qualification agree");if(mode==5)Require(q.friendly && !q.hostile,"AI30 is outside the native reflectable damage whitelist");
            }
            Console.WriteLine("PASS ORNAMENT receives=8 actual positive Strike and negatives");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(700,646);p.ResetFallDamage();p.releaseUseItem=true;Main.screenPosition=new Vector2(550,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,51,40);raw=target.Center-new Vector2(430,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;int start=currentActions;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null && p.itemAnimation==0 && !Main.projectile.Any(q=>q.active) && solves==0,"withdrawn idle mixed sword preview neither solves nor swings nor emits a star");}
            observe=auditCurrent=natural=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int shownCount=0;
            for(int action=0;action<130;action++)
            {
                naturalAction=action;physicalLeft=action<29 || action>=32 && action<59;
                if(physicalLeft)
                {
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
                    if(action==0)Require(GetOptional(attack,"ExpectedImpact")!=null,"actual pressed mixed sword/star hand prepares under1000ms mechanism allowance");
                }
                NativeCombatSelectionPreviewChecks.Step(context,raw,physicalLeft);var flight=Observe(attack,p);var shown=flight==null?null:(AttackContact)Get(flight,"Contact");Console.WriteLine("ORNAMENT use action="+action+" left="+physicalLeft+" nativeUse="+p.controlUseItem+" anim/time="+p.itemAnimation+"/"+p.itemTime+" actualFlight="+shown?.Tick+" confidence="+shown?.Confidence+" hand/child="+handBirths+"/"+childBirths);
                Require(GetOptional(attack,"ExpectedPreview")==null,"all130 original sword/star actions keep the retired hypothetical preview absent");if(shown!=null)shownCount++;int a=solves,b=steps;var impact=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int draw=0;draw<12;draw++){Call(impact,"Capture");Call(impact,"Project",zoom,Matrix.Invert(zoom));if(flight!=null)Require((bool)Call(flight,"PresentationCurrent",0),"same-update actual sword/star family presentation lease remains current");}Require(a==solves && b==steps,"CPU Capture/Project never repeats ornament motor/candidate search");
            }
            Console.WriteLine("ORNAMENT natural receiving first="+(first-birthTick+1)+" known="+known?.Tick+" role="+firstType+" firstBornMatched="+firstBornMatched+" positive="+positive+" hand/child="+handBirths+"/"+childBirths);
            Require(handBirths>=2 && childBirths>=4 && positive>0 && shownCount>0 && firstType==907 && firstBornMatched && known!=null && first-birthTick+1==known.Tick,"distant real first Damage is the deterministic first born star, matched by token/key before random child can precede it; public Flight observes the real family");Require(!p.controlUseItem && killDamage==0,"release ends use permission and real family retirement has no Kill explosion");
            Console.WriteLine("PASS ORNAMENT natural item1928 actions=130 previews="+shownCount+" handBirths="+handBirths+" children="+childBirths+" pureCurrentActions="+(currentActions-start)+" positive="+positive+" first="+(first-birthTick+1)+" known="+known.Tick+" CPUCaptureProject=12x0 DrawUnverified=True");
        }
        private static object Observe(object attack,Player player)
        {
            var path=Timeline(target,false);var args=new object[]{player,path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return Call(attack,"ReadFlight",args);
        }
    }
}
