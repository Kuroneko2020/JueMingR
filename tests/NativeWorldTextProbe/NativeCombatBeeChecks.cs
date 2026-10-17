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
    internal static class NativeCombatBeeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool watchKill;private static int parentKills;private static Vector2 killPosition;
        private static void BeforeKill(Projectile __instance)
        {if(watchKill && __instance.active && (__instance.type==469 || __instance.type==183)){parentKills++;killPosition=__instance.position;}}
        private static Player Reset(object context)
        {var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),2888,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;}
        private static NPC Target(int slot,Vector2 position,bool wet=false)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=32;n.height=40;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.wet=wet;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(Assembly a,NPC n)
        {var key=(NpcIdentity)a.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true,Wet=n.wet};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(Assembly a)
        {var t=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        internal static void Run(object context)
        {
            var a=Get(context,"Combat").GetType().Assembly;var model=a.GetType("JueMingR.TerrariaHost.Combat.HostBeeAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var targetsType=a.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);bool wind=Main.windPhysics;var random=Main.rand;var audit=new Harmony("JueMingR.Tests.Bee");
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBeeChecks).GetMethod("BeforeKill",Flags)));
            try
            {
                if(Environment.GetEnvironmentVariable("JMR_BEE_ORACLE_ONLY")=="birth"){Births(context,a,model);return;}
                ForeignOwnerGate(context);
                if(Environment.GetEnvironmentVariable("JMR_BEE_ORACLE_ONLY")=="owner")return;
                int cases=0,actions=0,bounces=0;
                foreach(int type in new[]{181,566})foreach(int scene in Enumerable.Range(0,12))
                {
                    var p=Reset(context);var target=Target(2,new Vector2(1110,335),scene==5);target.dontTakeDamage=scene!=5;var timeline=Timeline(a,target);
                    if(scene>=1 && scene<=4)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}
                    if(scene>=6 && scene<=9){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-5));}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene>=6 && scene<=9?new Vector2(936,390):new Vector2(950,350),scene>=6 && scene<=9?new Vector2(24,8):scene==11?Vector2.Zero:new Vector2(6,0),type,0,0,p.whoAmI,scene==5?30:0);var q=Main.projectile[slot];if(scene==10)q.timeLeft=2;
                    var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var targets=targetsType.GetMethod("CaptureWetChase",Flags).Invoke(null,new object[]{q,1L});var terrain=Terrain(a);
                    for(int outer=1;outer<=80 && q.active;outer++)
                    {
                        int updates=(int)Get(state,"Extra")+1;
                        for(int sub=0;sub<updates;sub++)
                        {int pen=(int)Get(state,"Penetration");var args=new object[]{q,targets,timeline,outer,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"bee retains finite water/target/slope dependencies");state=args[5];if((int)Get(state,"Penetration")<pen)bounces++;var tail=new object[]{state};model.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[0];actions++;if((bool)Get(state,"Stopped"))break;}
                        q.Update(slot);
                        Require(q.active==(bool)Get(state,"Alive") && q.timeLeft==(int)Get(state,"Left") && q.extraUpdates==(int)Get(state,"Extra") && q.alpha==(int)Get(state,"Alpha") && q.ai[0]==(float)Get(state,"Counter") && q.penetrate==(int)Get(state,"Penetration") && Vector2.DistanceSquared(q.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f,"native bee full motor/penetration/tail agrees: "+type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.ai[0]+"/"+q.timeLeft+"/"+q.penetrate+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Counter")+"/"+Get(state,"Left")+"/"+Get(state,"Penetration"));
                        if(scene==5)Require((int)Get(state,"TargetSlot")==-1,"wet non370 chase candidate is filtered before native steering");
                    }
                    if(scene==1 || scene==3)Require(!q.active,"water/lava native AI036 kills bees while honey/shimmer differs");
                    if(scene==11)Require(q.velocity==Vector2.Zero,"unacquired native zero velocity is not an invented UnitX ray");
                    if(scene==5){Require((bool)Get(targets,"Current"),"unchanged wet chase lease remains current");target.wet=false;Require(!(bool)Get(targets,"Current"),"wet qualification changes invalidate preparation");}cases++;
                }
                Require(bounces>0,"real native tile reflection spends bee penetration");Console.WriteLine("PASS BEE motor="+cases+" actions="+actions+" bounces="+bounces);
                Births(context,a,model);
                Workload(context,a,model);
            }
            finally{watchKill=false;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.rand=random;}
        }
        private static void ForeignOwnerGate(object context)
        {
            var foreignPlayer=Main.player[254];bool oldActive=foreignPlayer.active;
            try
            {
                foreach(int type in new[]{181,566})foreach(bool localFirst in new[]{false,true})
                {
                    var p=Reset(context);foreignPlayer.active=true;
                    var target=Target(2,new Vector2(940,320));target.width=target.height=100;
                    Array.Clear(target.buffType,0,target.buffType.Length);Array.Clear(target.buffTime,0,target.buffTime.Length);
                    target.immune[254]=0;Projectile.perIDStaticNPCImmunity[181,2]=0;
                    Func<int,Projectile> born=owner=>
                    {
                        int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,type,40,0,owner);
                        var shot=Main.projectile[slot];shot.tileCollide=false;return shot;
                    };
                    var foreign=born(254);var local=born(p.whoAmI);int initialPen=local.penetrate,initialAlpha=foreign.alpha,life=target.life;
                    if(localFirst){local.Update(local.whoAmI);Require(target.life<life && local.penetrate==initialPen-1,"overlapping local Bee has real native Update/Damage");}
                    int stableLife=target.life,localImmune=target.immune[p.whoAmI],foreignImmune=target.immune[254];uint stableStatic=Projectile.perIDStaticNPCImmunity[181,2];
                    // Both actors have exactly the same qualifying body. The
                    // active foreign owner rules out Update's inactive-owner
                    // Kill; no owner immunity or miss can explain this gate.
                    foreign.Damage();
                    Require(target.life==stableLife && target.immune[p.whoAmI]==localImmune && target.immune[254]==foreignImmune && Projectile.perIDStaticNPCImmunity[181,2]==stableStatic,"foreign Damage cannot write PVE/static181 despite an overlapping unblocked body");
                    int left=foreign.timeLeft;foreign.Update(foreign.whoAmI);
                    Require(foreign.active && foreign.alpha<initialAlpha && foreign.timeLeft<left,"foreign Bee's original AI/subcycle actually advances with its owner active");
                    Require(target.life==stableLife && target.immune[p.whoAmI]==localImmune && target.immune[254]==foreignImmune && Projectile.perIDStaticNPCImmunity[181,2]==stableStatic,"foreign Update preserves shared181 and both owner histories");
                    if(!localFirst){life=target.life;local.Update(local.whoAmI);Require(target.life<life && local.penetrate==initialPen-1 && target.immune[254]==foreignImmune,"local Bee after foreign Update still deals actual damage through its own owner policy");}
                    Require(Projectile.perIDStaticNPCImmunity[181,2]==Main.GameUpdateCount+(uint)local.idStaticNPCHitCooldown,"local Damage alone writes the native shared181 expiry");
                    Console.WriteLine("BEE ownerGate type="+type+" localFirst="+localFirst+" life="+target.life+" expiry="+Projectile.perIDStaticNPCImmunity[181,2]+" foreignActive="+foreign.active);
                }
            }
            finally{foreignPlayer.active=oldActive;}
        }
        private static void Births(object context,Assembly a,Type model)
        {
            int cases=0,hits=0;var obstacleType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);var clockType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);var phaseType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
            foreach(int parentType in new[]{469,183})foreach(bool strong in new[]{false,true})foreach(int seed in Enumerable.Range(0,4))
            {
                var p=Reset(context);p.strongBees=strong;Main.rand=new Terraria.Utilities.UnifiedRandom(7100+seed);var target=Target(2,new Vector2(1040,330));
                for(int i=0;i<Main.maxNPCs;i++)Projectile.perIDStaticNPCImmunity[181,i]=0;
                int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,12,0,parentType,90,0,p.whoAmI);var parent=Main.projectile[parentSlot];parent.timeLeft=2;parentKills=0;watchKill=true;
                try{for(int outer=0;outer<2;outer++){NativeQuickItemChecks.BeginWorldStep();parent.Update(parentSlot);}}
                finally{watchKill=false;}
                Vector2 position=killPosition;var children=Main.projectile.Where(member=>member.active && member.owner==p.whoAmI && (member.type==181 || member.type==566)).ToArray();
                Require(!parent.active && parentKills==1 && target.life==10000,"native parent full updates reach one real tail Kill before selected primary Damage");
                Require(children.Length>=(parentType==469?3:15) && children.Length<=(parentType==469?6:24),"native parent death retains the exact random birth count interval");
                Require(children.All(member=>member.immunityIdentity==181 && member.usesIDStaticNPCImmunity && member.penetrate==(parentType==469?2:member.type==181?3:4)),"real normal/giant children share181 but retain parent-specific penetration");
                if(parentType==183)Require(children.All(member=>Vector2.DistanceSquared(member.Center,position)<.0001f),"HoneyGrenade child center is the actual Kill-entry parent top-left");
                var timeline=Timeline(a,target);var terrain=Terrain(a);var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,children[0],timeline,0,true,false},null);var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"BeforeNpc"),0},null);
                var occupied=Main.projectile.Select(member=>member.active).ToArray();var liveKeys=Main.projectile.Select(member=>member.key).ToArray();
                // Reserve the observed dead parent's slot as it was during
                // Kill. The factory must return copied births, not mutate the
                // native pool or advance any member's motor/history.
                occupied[parentSlot]=true;Func<int> allocate=()=>{for(int index=0;index<occupied.Length;index++)if(!occupied[index]){occupied[index]=true;return index;}return -1;};
                var privateBirths=(Projectile[])model.GetMethod("BirthMembersRepresentative",Flags).Invoke(null,new object[]{parent,position,parent.oldVelocity,strong,allocate,terrain,obstacle});
                Require(privateBirths.Length==(parentType==469?3:15) && privateBirths.All(member=>member.whoAmI!=parentSlot && member.owner==p.whoAmI),"private birth factory reserves the still-occupied parent with the legal minimum count");
                Require(privateBirths.All(member=>children.Any(actual=>Vector2.DistanceSquared(actual.Center,member.Center)<.0001f)),"every representative even-index birth origin is independently present in this real native Kill family");
                Require(Main.projectile.Where((member,index)=>!member.key.Equals(liveKeys[index])).Count()==0 && children.All(member=>member.active),"private representative factory neither rewrites the native pool nor runs child AI");
                var plan=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,children,timeline,clock,terrain,obstacle});int first=0;
                Require((bool)Get(obstacle,"Unchanged"),"captured actual family and native receiver premises remain current immediately after Prepare");
                var changed=children[children.Length-1];int alpha=changed.alpha;changed.alpha++;Require(!(bool)Get(obstacle,"Unchanged"),"a sibling's real AI state invalidates the family consume lease");changed.alpha=alpha;Require((bool)Get(obstacle,"Unchanged"),"restoring the exact sibling state restores this still-current preparation");
                for(int outer=1;outer<=120;outer++)
                {NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int slot=0;slot<1000;slot++){var shot=Main.projectile[slot];if(shot.active && (shot.type==181 || shot.type==566))shot.Update(slot);}if(target.life<10000){first=outer;break;}if(!children.Any(member=>member.active))break;}
                Require(first==(plan?.Tick??0),"actual captured bee family primary useful Damage agrees with shared181 immunity/slot order: parent="+parentType+" strong="+strong+" seed="+seed+" native="+first+" model="+plan?.Tick);if(first>0)hits++;cases++;
            }
            Require(hits>0,"native random family denominator includes real useful Damage");Console.WriteLine("PASS BEE actual parent birth/family cases="+cases+" usefulDamage="+hits);
        }
        private static void Workload(object context,Assembly a,Type model)
        {
            var p=Reset(context);var target=Target(2,new Vector2(1040,330));target.dontTakeDamage=true;var timeline=Timeline(a,target);var terrain=Terrain(a);
            int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,12,0,469,90,0,p.whoAmI);var parent=Main.projectile[slot];
            var obstacle=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,parent,timeline,0,true,false},null);
            var reset=model.GetMethod("ResetCounters",Flags);reset?.Invoke(null,null);var targets=model.GetMethod("CaptureTargets",Flags).Invoke(null,new object[]{parent,obstacle});var births=Call(obstacle,"CaptureBirthSlots");
            for(int candidate=0;candidate<3;candidate++)
            {
                Call(obstacle,"BeginCandidate");var slots=Call(births,"Copy");
                var plan=(AttackContact)model.GetMethod("BirthRepresentative",Flags).Invoke(null,new object[]{parent,parent.position,new Vector2(12,0),false,slots,timeline,1,1,terrain,obstacle,targets});
                Require(plan==null,"a real unreceivable selected target cannot become a bee contact");
            }
            if(reset!=null)
            {
                int captures=(int)model.GetField("TargetCaptures",Flags).GetValue(null),families=(int)model.GetField("Families",Flags).GetValue(null),steps=(int)model.GetField("MotorSteps",Flags).GetValue(null),queries=(int)model.GetField("TargetQueries",Flags).GetValue(null);
                Require(captures==1 && families==3 && steps>0 && steps<=3*3*120*4 && queries==0,"hard-miss family candidates reuse one actual chase capture, with no per-angle scene scan or fake target query");
                Console.WriteLine("BEE WORKLOAD captures="+captures+" families="+families+" motor="+steps+" targetQueries="+queries);
            }
            else Console.WriteLine("NOT_EXECUTED_REQUIRES_SAME_SOURCE_DEBUG bee workload counters; three actual hard-miss families and lease negatives still execute");
            Require((bool)Get(obstacle,"Unchanged"),"shared target/owner/pool premises remain attached to the actual consume lease");bool strong=p.strongBees;p.strongBees=!strong;try{Require(!(bool)Get(obstacle,"Unchanged"),"strong-bee equipment changes invalidate representative preparation");}finally{p.strongBees=strong;}
            Console.WriteLine("PASS BEE actual hard-miss families=3 and strong-bee lease negative");
        }
    }
}
