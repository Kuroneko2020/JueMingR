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
using Terraria.Testing;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMedusaScanChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int first,step;private static readonly List<Projectile> born=new List<Projectile>();
        private static uint firstStrikeGuc;private static readonly List<BirthState> bornStates=new List<BirthState>();
        private static readonly List<DamageState> damageStates=new List<DamageState>();
        private static bool traceNativeAi;private static AiState currentAi;private static readonly List<AiState> aiStates=new List<AiState>();
        private sealed class AiState
        {
            internal uint Guc;internal int Action,ManaBefore,ManaAfter;internal Vector2 Before,After;internal float ClockBefore,ClockAfter;
            internal readonly List<LosState> Lines=new List<LosState>();internal readonly List<ManaState> Payments=new List<ManaState>();
            internal AiState(Projectile q){Guc=Main.GameUpdateCount;Action=step;Before=q.Center;ClockBefore=q.ai[0];ManaBefore=Main.player[q.owner].statMana;}
            public override string ToString(){return "GUC="+Guc+" action="+Action+" parent="+Before+"->"+After+" clock="+ClockBefore+"->"+ClockAfter+" mana="+ManaBefore+"->"+ManaAfter+" LOS=["+string.Join(";",Lines)+"] payments=["+string.Join(";",Payments)+"]";}
        }
        private sealed class LosState
        {
            internal Vector2 From,To;internal int W1,H1,W2,H2;internal bool Result;
            public override string ToString(){return From+"/"+W1+","+H1+" -> "+To+"/"+W2+","+H2+" ="+Result;}
        }
        private sealed class ManaState
        {
            internal int Amount,Before,After;internal bool Pay,BlockQuick,Result;
            public override string ToString(){return "amount="+Amount+" pay="+Pay+" blockQuick="+BlockQuick+" result="+Result+" mana="+Before+"->"+After;}
        }
        private static void AiBefore(Projectile __instance,out AiState __state)
        {__state=traceNativeAi && ReferenceEquals(__instance,parent)?new AiState(__instance):null;if(__state!=null)currentAi=__state;}
        private static void AiAfter(Projectile __instance,AiState __state)
        {if(__state==null)return;__state.After=__instance.Center;__state.ClockAfter=__instance.ai[0];__state.ManaAfter=Main.player[__instance.owner].statMana;aiStates.Add(__state);currentAi=null;}
        private static void LineAfter(Vector2 __0,int __1,int __2,Vector2 __3,int __4,int __5,bool __result)
        {if(currentAi!=null)currentAi.Lines.Add(new LosState{From=__0,W1=__1,H1=__2,To=__3,W2=__4,H2=__5,Result=__result});}
        private static void ManaBefore(Player __instance,int __0,bool __1,bool __2,out ManaState __state)
        {__state=currentAi==null?null:new ManaState{Amount=__0,Pay=__1,BlockQuick=__2,Before=__instance.statMana};}
        private static void ManaAfter(Player __instance,ManaState __state,bool __result)
        {if(__state!=null){__state.After=__instance.statMana;__state.Result=__result;currentAi?.Payments.Add(__state);}}
        private sealed class DamageState
        {
            internal uint Guc;internal int Action,Slot,Damage,Owner,LifeBefore,LifeAfter,ImmuneBefore,ImmuneAfter;internal object Key;internal Vector2 Center;
            internal DamageState(Projectile q){Guc=Main.GameUpdateCount;Action=step;Slot=q.whoAmI;Damage=q.damage;Owner=q.owner;Key=q.key;Center=q.Center;LifeBefore=Main.npc[2].life;ImmuneBefore=Main.npc[2].immune[q.owner];LifeAfter=LifeBefore;ImmuneAfter=ImmuneBefore;}
            public override string ToString(){return "GUC="+Guc+" action="+Action+" slot="+Slot+" damage="+Damage+" center="+Center+" life="+LifeBefore+"->"+LifeAfter+" immune="+ImmuneBefore+"->"+ImmuneAfter;}
        }
        // The native callback copies values only; diagnostics are formatted
        // after the original Update returns, before its unchanged strict gate.
        private sealed class BirthState
        {
            internal uint Guc;internal int Action,Slot,Type,Damage,Owner,Left,Extra,Mana;internal object Key,ParentKey;internal Vector2 Center,Velocity,ParentCenter;internal float[] Ai,Local;internal float ParentClock;
            internal BirthState(Projectile q){Guc=Main.GameUpdateCount;Action=step;Slot=q.whoAmI;Type=q.type;Damage=q.damage;Owner=q.owner;Left=q.timeLeft;Extra=q.extraUpdates;Key=q.key;ParentKey=parent.key;Center=q.Center;Velocity=q.velocity;Ai=(float[])q.ai.Clone();Local=(float[])q.localAI.Clone();Mana=Main.player[q.owner].statMana;ParentCenter=parent.Center;ParentClock=parent.ai[0];}
            public override string ToString(){return "GUC="+Guc+" action="+Action+" slot="+Slot+" type="+Type+" key="+Key+" center="+Center+" V="+Velocity+" damage="+Damage+" owner="+Owner+" left="+Left+" extra="+Extra+" ai="+string.Join(",",Ai)+" local="+string.Join(",",Local)+" mana="+Mana+" parentCenter="+ParentCenter+" parentClock="+ParentClock;}
        }
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==536){born.Add(Main.projectile[__result]);bornStates.Add(new BirthState(Main.projectile[__result]));}}
        private static void Damage(Projectile __instance,out DamageState __state){receiving=__instance;__state=__instance.type==536?new DamageState(__instance):null;}
        private static void DamageDone(Projectile __instance,DamageState __state){if(__state!=null){__state.LifeAfter=Main.npc[2].life;__state.ImmuneAfter=Main.npc[2].immune[__instance.owner];damageStates.Add(__state);}receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving?.type==536 && first==0){first=step;firstStrikeGuc=Main.GameUpdateCount;}}
        private static Player Reset(object context)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3269,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.channel=true;p.statMana=p.statManaMax2=200;p.manaCost=1;p.manaPotionDelay=p.potionDelay=0;p.cursed=p.dead=p.slowMagicUse=p.manaFlower=false;Main.windPhysics=false;for(int i=1;i<58;i++)p.inventory[i].TurnToAir();foreach(var item in p.bank4.item)item.TurnToAir();foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);born.Clear();first=step=0;return p;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,parent,path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,new[]{parent},path,clock,Terrain(),root);}
        private static Projectile Shoot(Player p)
        {Main.mouseX=1300-(int)Main.screenPosition.X;Main.mouseY=667-(int)Main.screenPosition.Y;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});return Main.projectile.Single(q=>q.active && q.type==535);}
        private static void Advance(int limit)
        {
            // Known is a BeforeNpc observation of this GUC. Run that first
            // real NPC/projectile action now; only subsequent worlds advance
            // the stamp. The original action limits and native calls stay.
            for(step=1;step<=limit && first==0;step++){if(step>1)NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
        }
        private static string BodyFacts(Projectile q)
        {return "type="+q.type+" slot="+q.whoAmI+" key="+q.key+" active="+q.active+" center="+q.Center+" V="+q.velocity+" ai="+string.Join(",",q.ai)+" local="+string.Join(",",q.localAI)+" damage="+q.damage+" pen="+q.penetrate+"/"+q.maxPenetrate+" left="+q.timeLeft+" extra="+q.extraUpdates+" owner="+q.owner+" localPolicy="+q.usesLocalNPCImmunity+"/"+q.localNPCHitCooldown+" local2="+q.localNPCImmunity[2]+" staticPolicy="+q.usesIDStaticNPCImmunity;}
        private static string OwnerFacts(Player p)
        {return "position="+p.position+" V="+p.velocity+" mounted="+p.MountedCenter+" rotation="+p.fullRotation+" rotationOrigin="+p.fullRotationOrigin+" gfx="+p.gfxOffY+" direction="+p.direction+" gravity="+p.gravDir+" active/dead="+p.active+"/"+p.dead+" channel="+p.channel+" CCed="+p.CCed+" cursed="+p.cursed+" mana="+p.statMana+"/"+p.statManaMax2+" manaCost="+p.manaCost+" weaponMana="+p.HeldItem.mana+" slow="+p.slowMagicUse+" flower="+p.manaFlower+" delays="+p.manaPotionDelay+"/"+p.potionDelay+" frame="+p.bodyFrame+" animation="+p.itemAnimation+" screen="+Main.screenPosition+" mouse="+Main.mouseX+","+Main.mouseY+" ManaV2="+DebugOptions.ManaV2+" PotionDelay="+DebugOptions.ManaPotionDelay;}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMedusaScanAttack",true);var audit=new Harmony("JueMingR.Tests.MedusaScan");bool wind=Main.windPhysics,v2=DebugOptions.ManaV2,delay=DebugOptions.ManaPotionDelay;float oldRotation=Main.LocalPlayer.fullRotation;
            string replayRotation=Environment.GetEnvironmentVariable("JMR_MEDUSA_SCAN_REPLAY_ROTATION");
            if(!string.IsNullOrEmpty(replayRotation))Require(Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")=="medusa-scan" && replayRotation=="0.8000001","Medusa explicit replay is limited to the already observed legal full-prefix rotation and existing specialist leaf");
            bool trace=Environment.GetEnvironmentVariable("JMR_MEDUSA_SCAN_TRACE")=="1";
            if(trace)Require(Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")=="medusa-scan","temporary inner Medusa trace requires the existing explicit specialist leaf");
            try
            {
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(Birth)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(Strike)));
            if(trace)
            {
                audit.Patch(typeof(Projectile).GetMethod("AI_100_Medusa",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(AiBefore)),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(AiAfter)));
                audit.Patch(typeof(Collision).GetMethod("CanHitLine",Flags,null,new[]{typeof(Vector2),typeof(int),typeof(int),typeof(Vector2),typeof(int),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(LineAfter)));
                audit.Patch(typeof(Player).GetMethod("CheckMana",Flags,null,new[]{typeof(int),typeof(bool),typeof(bool)},null),prefix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(ManaBefore)),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(ManaAfter)));
            }
            Console.WriteLine("MEDUSA temporary inner-AI/LOS/Mana diagnostic trace="+trace);
            if(!string.IsNullOrEmpty(replayRotation)){Main.LocalPlayer.fullRotation=.8000001f;Console.WriteLine("MEDUSA EXPLICIT local rotation replay="+Main.LocalPlayer.fullRotation+"; original cases/windows/gates unchanged; not default full-prefix identity");}Mana(context);Anchor(context);Receive(context,trace);Boundaries(context);Console.WriteLine("PASS MEDUSA SCAN actual535 hand/channel, finite mana, nearest3/LOS/320 and synchronous536 receive.");
            }finally{traceNativeAi=false;currentAi=null;Main.windPhysics=wind;DebugOptions.ManaV2=v2;DebugOptions.ManaPotionDelay=delay;if(!string.IsNullOrEmpty(replayRotation))Main.LocalPlayer.fullRotation=oldRotation;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Mana(object context)
        {
            foreach(bool v2 in new[]{false,true})foreach(int mode in new[]{0,1,2,3,4,5})
            {
                var p=Reset(context);DebugOptions.ManaV2=v2;DebugOptions.ManaPotionDelay=false;p.statMana=mode==0?100:0;p.manaFlower=mode>=2;if(mode>=2){var item=mode==5?p.bank4.item[0]:p.inventory[1];item.SetDefaults(110);item.stack=2;}if(mode==3)p.manaPotionDelay=10;if(mode==4)p.cursed=true;if(mode==5){p.inventory[2].SetDefaults(4131);p.inventory[2].stack=1;}
                parent=Shoot(p);var state=Activator.CreateInstance(model.GetNestedType("State",Flags),Flags,null,new object[]{p,new[]{parent}},null);var budget=Call(state,"NewBudget");var shadow=(Player)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true),"PlayerCopy",p);bool expected=(bool)Call(budget,"Pay",shadow,0);bool actual=p.CheckMana(p.HeldItem.mana,true);Require(expected==actual && shadow.statMana==p.statMana && shadow.slowMagicUse==p.slowMagicUse,"native CheckMana resource payment/ManaV2 slow branch agrees mode="+mode+" v2="+v2);
                if(mode==2 || mode==5)Require(p.statMana>0 && (mode==5?p.bank4.item[0]:p.inventory[1]).stack==1,"native current inventory/voidbag auto mana is actually consumed");
            }
            Console.WriteLine("PASS MEDUSA original12 CheckMana branches including current inventory/voidbag, blocked QuickMana and ManaV2 slow state");
        }
        private static void Anchor(object context)
        {
            foreach(int facing in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})
            {var p=Reset(context);p.direction=facing;p.gravDir=gravity;parent=Shoot(p);var q=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",parent);var body=(Player)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true),"PlayerCopy",p);for(int action=0;action<8;action++){Main.mouseX=(action%2==0?600:1300)-(int)Main.screenPosition.X;Static(model,"Anchor",q,body,Main.screenPosition.X+Main.mouseX);parent.Update(parent.whoAmI);Require(parent.position==q.position && parent.velocity==q.velocity && parent.direction==q.direction && p.direction==body.direction,"native535 real physical facing/old hand anchor/gravity and post-AI movement remain exact without input override");}}
            Console.WriteLine("PASS MEDUSA native4 facing/gravity anchors x8 physical opposite mouse actions");
        }
        private static void Receive(object context,bool trace)
        {
            int failures=0,cases=0;DebugOptions.ManaV2=true;foreach(int clock in new[]{0,16,60})
            {
                var p=Reset(context);parent=Shoot(p);parent.ai[0]=clock;var n=Target(2,parent.Center+new Vector2(200,0));uint start=Main.GameUpdateCount;int manaBefore=p.statMana,sourceDamage=parent.damage;object sourceKey=parent.key;firstStrikeGuc=0;bornStates.Clear();damageStates.Clear();
                Console.WriteLine("MEDUSA RECEIVE before clock="+clock+" GUC="+start+" owner=["+OwnerFacts(p)+"] parent=["+BodyFacts(parent)+"] target="+n.Hitbox+" life="+n.life+" immune="+n.immune[p.whoAmI]+" flags="+n.active+","+n.friendly+","+n.immortal+","+n.dontTakeDamage+" netOffset="+n.netOffset);
                object root;var expected=Known(p,n,out root);
                Console.WriteLine("MEDUSA RECEIVE prepared clock="+clock+" GUC="+Main.GameUpdateCount+" sample="+expected?.Timeline.SampleTick+" tick="+expected?.Tick+" receive="+expected?.ReceiveTick+" sub="+expected?.Subupdate+" confidence="+expected?.Confidence+" aim="+expected?.AimX+","+expected?.AimY+" owner=["+OwnerFacts(p)+"] parent=["+BodyFacts(parent)+"]");
                Require(expected!=null,"observed535 finite scan has a real useful536 receive");aiStates.Clear();traceNativeAi=trace;try{Advance(90);}finally{traceNativeAi=false;currentAi=null;}
                foreach(var state in aiStates)Console.WriteLine("MEDUSA NATIVE AI "+state);
                Console.WriteLine("MEDUSA RECEIVE result clock="+clock+" GUC="+Main.GameUpdateCount+" elapsed="+((long)Main.GameUpdateCount-start)+" first="+first+" strikeGUC="+(first==0?(uint?)null:firstStrikeGuc)+" absoluteReceive="+(first==0?(long?)null:(long)firstStrikeGuc-expected.Timeline.SampleTick)+" expected="+expected.Tick+" bornCount="+born.Count+" allHarmless="+born.All(q=>q.damage==0)+" life="+n.life+" immune="+n.immune[p.whoAmI]+" target="+n.Hitbox+" netOffset="+n.netOffset+" owner=["+OwnerFacts(p)+"] parent=["+BodyFacts(parent)+"] born=["+string.Join(";",born.Select(BodyFacts))+"] birthSnapshots=["+string.Join(";",bornStates)+"]");
                // A paid scan always emits four decorations. Its second LOS
                // can reject every target even after the initial sight passes;
                // that earlier empty scan is not a fifth damaging ray. Preserve
                // exactly five for this single-target damaging scan, and exactly
                // four for each preceding paid-empty scan, including resources.
                int cost=(int)(p.HeldItem.mana*p.manaCost),mana=manaBefore,harmful=0,batches=0;
                Require(cost==15 && !p.manaFlower && bornStates.Count==born.Count && bornStates.Select(b=>b.Key).Distinct().Count()==bornStates.Count,"fixed single-target scan retains original resource and unique birth identities");
                foreach(var batch in bornStates.GroupBy(b=>b.Guc).OrderBy(b=>b.Key))
                {
                    var hits=damageStates.Where(d=>d.Guc==batch.Key && d.Damage>0).ToArray();
                    Require(hits.Length<=1 && batch.Count()==4+hits.Length && batch.All(b=>b.Type==536 && b.Owner==p.whoAmI && Equals(b.ParentKey,sourceKey) && b.Damage==0 && b.Center==b.ParentCenter && b.Mana==mana-cost),"each original paid535 scan has exactly four decorations plus its synchronous target ray, source identity and exact mana payment");
                    if(hits.Length==0)Require(batch.Count()==4 && batch.Key<firstStrikeGuc,"prior empty paid scans retain exactly four harmless decorations before first selected Damage");
                    else Require(batch.Count()==5 && batch.Key==firstStrikeGuc && hits[0].Damage==sourceDamage && hits[0].Owner==p.whoAmI && hits[0].Center==n.Center && hits[0].LifeAfter<hits[0].LifeBefore && batch.Any(b=>Equals(b.Key,hits[0].Key)),"single-target damaging scan retains original five births and its own synchronous native positive Damage");
                    mana-=cost;harmful+=hits.Length;batches++;
                }
                bool same=first>0 && first==expected.Tick && (long)firstStrikeGuc-expected.Timeline.SampleTick==expected.Tick && expected.ReceiveTick==expected.Tick && harmful==1 && batches>0 && mana==p.statMana && born.All(q=>q.damage==0);if(!same)failures++;cases++;
                if(trace || !same)Console.WriteLine("MEDUSA SYNCHRONOUS DAMAGE clock="+clock+" events=["+string.Join(";",damageStates)+"] same="+same);
                Console.WriteLine("MEDUSA cooldown="+clock+" predicted="+expected.Tick+" native="+first+" absoluteReceive="+((long)firstStrikeGuc-expected.Timeline.SampleTick)+" paidBatches="+batches+" born="+born.Count+" synchronousPositive="+harmful+" mana="+manaBefore+"->"+p.statMana);
            }
            Console.WriteLine("MEDUSA RECEIVE totals cases="+cases+" failures="+failures);Require(cases==3 && failures==0,"all three original Medusa receive clocks retain exact current-phase Strike, per-scan birth roles/resources and harmless survivors");
        }
        private static void Boundaries(object context)
        {
            foreach(int mode in new[]{0,1,2,3})
            {var p=Reset(context);parent=Shoot(p);parent.Update(parent.whoAmI);var n=Target(2,parent.Center+new Vector2(mode==0?319:mode==1?320:220,0));if(mode==2){int x=(int)(parent.Center.X+100)/16;for(int y=10;y<65;y++)NativeToolsChecks.Tile(x,y,1);}if(mode==3){Target(1,parent.Center+new Vector2(60,0));Target(3,parent.Center+new Vector2(110,0));Target(4,parent.Center+new Vector2(160,0));}object root;var expected=Known(p,n,out root);Require(mode==0?expected!=null:expected==null,"native strict320/LOS/closest3 gates determine actual scan eligibility");Advance(35);Require(mode==0?first==expected.Tick:first==0 && n.life==10000,"original535 preserves all scan negative controls instead of using HasPoint as damage");}
            var owner=Reset(context);parent=Shoot(owner);var selected=Target(2,parent.Center+new Vector2(200,0));owner.channel=false;object lease;Require(Known(owner,selected,out lease)==null,"released actual source has no scan Contact");Advance(1);Require(!parent.active && first==0,"native channel release retires before scan");
            owner=Reset(context);parent=Shoot(owner);selected=Target(2,parent.Center+new Vector2(200,0));Known(owner,selected,out lease);owner.statMana--;Require(!(bool)Get(lease,"Unchanged"),"same-GUC resource edit revokes preparation; no mana/inventory scan is borrowed by Draw");
        }
    }
}
