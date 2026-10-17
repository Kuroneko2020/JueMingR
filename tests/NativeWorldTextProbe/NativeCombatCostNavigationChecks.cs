using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCostNavigationChecks
    {
        private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            string leaf=Environment.GetEnvironmentVariable("JMR_COST_NAV_LEAF");
            if(leaf==null || leaf=="release")Release(context);
            if(leaf==null || leaf=="contact"){var audit=new HarmonyLib.Harmony("JueMingR.Tests.ContactNavigation");audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));try{Contact(context);}finally{audit.UnpatchAll(audit.Id);}}
            if(leaf==null || leaf=="group")Group(context);
        }
        private static NpcTrajectory Future(object host,NPC n,int horizon,float vx=0)
        {
            var key=new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=true,Life=n.life};
            var points=Enumerable.Range(0,horizon+1).Select(t=>{var s=state;s.X+=vx*t;return new NpcTrajectoryPoint(t,s);}).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static void Release(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3262,0,0);p.position=new Vector2(1000,646);p.ResetEffects();p.magicString=true;
            var assembly=combat.GetType().Assembly;var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostYoyoReleaseAttack",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var clockType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.life=10000;
            int oldFound=0,newFound=0,oldSteps=0,newSteps=0,better=0,worse=0,equal=0,preciseTimeDiff=0,dominated=0;float worstQualityLoss=0;
            foreach(int horizon in new[]{16,32,64,120})foreach(float x in new[]{0f,4f,40f,100f,300f,500f,-300f,-500f})foreach(float y in new[]{-200f,-100f,0f,200f})foreach(float vx in new[]{0f,1f})
            {
                n.position=p.RotatedRelativePoint(p.MountedCenter)+new Vector2(x,y);n.width=18;n.height=40;
                var path=Future(host,n,horizon,vx);var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{path,phase,0},null);int first=(int)Get(clock,"FirstTick");
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");Vector2 original;int work;bool before=Legacy(p,p.HeldItem,p.HeldItem.shootSpeed,path,first,terrain,out original,out work);oldSteps+=work;
                var counter=type.GetField("ResponseSteps",Flags);if(counter!=null)counter.SetValue(null,0);
                Call(terrain,"Reset");object[] args={p,p.HeldItem,p.HeldItem.shootSpeed,path,clock,terrain,Vector2.Zero};bool after;
                after=(bool)type.GetMethod("TryPoint",Flags).Invoke(null,args);
                int used=counter==null?-1:(int)counter.GetValue(null);if(used>=0)newSteps+=used;
                if(before)oldFound++;if(after)newFound++;
                Console.WriteLine("RELEASE MODEL H="+horizon+" target="+x+","+y+" vx="+vx+" old="+before+" new="+after+" steps="+work+"/"+used+" point="+args[6]);
                Require(!before || after,"bounded response preserves an old valid direction: H="+horizon+" target="+x+","+y+" vx="+vx);
                if(before && after)
                {
                    var start=p.RotatedRelativePoint(p.MountedCenter);float speed=ProjectileID.Sets.YoyosTopSpeed[p.HeldItem.shoot],inertia=Math.Max(1,14-speed/2),held=p.HeldItem.shootSpeed>speed?p.HeldItem.shootSpeed*.98f:p.HeldItem.shootSpeed,inherited=(held*(inertia-1)+speed)/inertia;
                    int oldTime,newTime;float oldError=Quality(path,first,start,original,inherited,speed,out oldTime),newError=Quality(path,first,start,(Vector2)args[6],inherited,speed,out newTime);
                    int oldReceive=FirstReceive(path,first,start,original,inherited,speed,16,16),newReceive=FirstReceive(path,first,start,(Vector2)args[6],inherited,speed,16,16);
                    if(oldReceive>0 && (newReceive<0 || newReceive>oldReceive) && newError>oldError+1){dominated++;Console.WriteLine("RELEASE LOW ARC LOST H="+horizon+" target="+x+","+y+" vx="+vx+" overlap="+oldReceive+"/"+newReceive+" centreError="+oldError+"/"+newError);}
                    if(newError>oldError+1){worse++;worstQualityLoss=Math.Max(worstQualityLoss,newError-oldError);Console.WriteLine("RELEASE QUALITY LOSS H="+horizon+" target="+x+","+y+" vx="+vx+" oldError="+oldError+" newError="+newError+" time="+oldTime+"/"+newTime);}else if(oldError>newError+1)better++;else equal++;
                    if(oldError<16 && newError<16 && oldTime!=newTime){preciseTimeDiff++;Console.WriteLine("RELEASE PRECISE TIME H="+horizon+" target="+x+","+y+" vx="+vx+" time="+oldTime+"/"+newTime);Require(newTime<=oldTime,"ranked seeds do not replace an early precise receive with a late arc");}
                }
            }
            LowArcNeighborhood(p,n,host,type,terrainType,clockType,phase);
            Require(dominated==0,"a later, less accurate direction cannot replace an already valid lower arc");
            // A successful proposal may be reused across the same complete
            // receive prefix, but its phase/player inputs and new terrain
            // passage still belong to the current caller.
            var scopeType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackAim+SolvePreparation",false);
            if(scopeType!=null)
            {
                n.position=p.RotatedRelativePoint(p.MountedCenter)+new Vector2(300,-100);
                var narrow=Future(host,n,64);var wide=Future(host,n,120);
                using((IDisposable)Activator.CreateInstance(scopeType,true))
                {
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{narrow,phase,0},null);
                    object[] args={p,p.HeldItem,p.HeldItem.shootSpeed,narrow,clock,terrain,Vector2.Zero};
                    Require((bool)type.GetMethod("TryPoint",Flags).Invoke(null,args),"first legal direction is solved before continuation");var beforePoint=(Vector2)args[6];
                    var counter=type.GetField("ResponseSteps",Flags);int stepsBefore=counter==null?0:(int)counter.GetValue(null);
                    args[3]=wide;args[4]=Activator.CreateInstance(clockType,Flags,null,new object[]{wide,phase,0},null);
                    Require((bool)type.GetMethod("TryPoint",Flags).Invoke(null,args) && (Vector2)args[6]==beforePoint,"same complete prefix retains a pure direction, not an old input lease");
                    if(counter!=null)Require((int)counter.GetValue(null)==stepsBefore,"same-prefix expansion does not replay a successful response");
                    p.velocity.X=1;type.GetMethod("TryPoint",Flags).Invoke(null,args);p.velocity.X=0;
                    if(counter!=null)Require((int)counter.GetValue(null)>stepsBefore,"real player motion rejects prepared response reuse");
                }
            }
            // Reduced receiver tail from the original Main's first changed
            // dynamic hand. Earlier times are unreachable in X; t53 grazes,
            // while t54 has the old substantially nearer centre solution.
            // This checks launch quality, not just the existence of overlap.
            var savedTiles=Main.tile;int savedWidth=Main.maxTilesX,savedHeight=Main.maxTilesY,savedDirection=p.direction;var savedPosition=p.position;var savedVelocity=p.velocity;
            try
            {
            // Only the launch's local dry cells are queried; retain the
            // original world-coordinate float precision without a world run.
            Main.maxTilesX=2200;Main.maxTilesY=500;Main.tile=new Tile[2200,500];
            for(int x=2080;x<2120;x++)for(int y=380;y<420;y++)Main.tile[x,y]=new Tile();
            ExactReleaseWindow(host,p,n,type,terrainType,clockType);
            foreach(int scenario in new[]{0,1,2})foreach(int mirror in new[]{1,-1})
            {
                bool earlier=scenario!=0,actualPrefix=scenario==2;
                // Keep the original subpixel world position: native receive
                // bounds quantize in world space, so translating to (1000,646)
                // erases the earlier-time regression's distinguishing margin.
                float startX=actualPrefix?BitConverter.ToSingle(BitConverter.GetBytes(1191383205),0):earlier?33552.64f:33566.52f;
                p.position+=new Vector2(startX,6379)-p.RotatedRelativePoint(p.MountedCenter);
                p.direction=mirror;
                p.velocity=actualPrefix?new Vector2(-.8000011f*mirror,0):Vector2.Zero;
                n.SetDefaults(1);n.active=true;n.life=10000;n.width=32;n.height=18;
                var tailPhase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),actualPrefix?"BeforeNpc":"Projectiles");
                var seed=Future(host,n,63);var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{seed,tailPhase,0},null);int first=(int)Get(clock,"FirstTick");
                var start=p.RotatedRelativePoint(p.MountedCenter);float[] tail=actualPrefix?new float[]{4,-4,-11,-17,-24,-30,-36,-42,-47,-52,-57,-61,-65,-69,-72,-76,-79,-81,-83,-85,-87,-89}:earlier?new float[]{-52,-57,-61,-65,-69,-72,-76,-79,-82,-85,-88,-90,-93}:new float[]{-4,-11,-17,-24,-30,-36,-42,-47,-52,-57,-62,-66,-71};
                int tailStart=actualPrefix?41:50;
                var key=seed.Identity;var points=Enumerable.Range(0,first+(earlier?63:62)).Select(i=>{int t=i-first;float dy=t<tailStart?12:tail[Math.Min(t-tailStart,tail.Length-1)];float dx=actualPrefix && t<=40?(t<2?34295:34296)-33552.64f:(earlier?863.36f:873.48f)-3*t;var state=new NpcMotionState{Identity=key,X=start.X+mirror*dx-16,Y=start.Y+dy-9,Width=32,Height=18,Active=true,CanReceive=true,Life=10000};return new NpcTrajectoryPoint(i,state);}).ToArray();
                var path=new NpcTrajectory(key,seed.SampleTick,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);clock=Activator.CreateInstance(clockType,Flags,null,new object[]{path,tailPhase,0},null);
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");Vector2 oldPoint;int work;Require(Legacy(p,p.HeldItem,p.HeldItem.shootSpeed,path,first,terrain,out oldPoint,out work),"dynamic late-tail has a legal old direction");
                Call(terrain,"Reset");object[] args={p,p.HeldItem,p.HeldItem.shootSpeed,path,clock,terrain,Vector2.Zero};
                Require((bool)type.GetMethod("TryPoint",Flags).Invoke(null,args),"dynamic late-tail keeps its release proposal");
                float speed=ProjectileID.Sets.YoyosTopSpeed[p.HeldItem.shoot],inertia=Math.Max(1,14-speed/2),held=p.HeldItem.shootSpeed>speed?p.HeldItem.shootSpeed*.98f:p.HeldItem.shootSpeed,inherited=(held*(inertia-1)+speed)/inertia;
                int oldTime=0,newTime=0;float oldError=float.MaxValue,newError=float.MaxValue;var oldUnit=(oldPoint-start).SafeNormalize(Vector2.UnitX);var newUnit=((Vector2)args[6]-start).SafeNormalize(Vector2.UnitX);
                for(int t=2;first+t<path.Count;t++){var box=path[first+t].ProjectileReceiveBounds;var center=new Vector2(box.CenterX,box.CenterY);float before=Vector2.DistanceSquared(End(start,oldUnit,inherited,speed,t,ref work),center),after=Vector2.DistanceSquared(End(start,newUnit,inherited,speed,t,ref work),center);if(before<oldError){oldError=before;oldTime=t;}if(after<newError){newError=after;newTime=t;}}
                Console.WriteLine("RELEASE LATE-TAIL earlier="+earlier+" actualPrefix="+actualPrefix+" mirror="+mirror+" oldError="+oldError+" newError="+newError+" time="+oldTime+"/"+newTime+" start="+start+" inherited="+inherited+" first="+first+" count="+path.Count);Require(newError<=oldError+1,"both adjacent times preserve centre accuracy instead of accepting a grazing overlap");
            }
            }
            finally{Main.tile=savedTiles;Main.maxTilesX=savedWidth;Main.maxTilesY=savedHeight;p.position=savedPosition;p.velocity=savedVelocity;p.direction=savedDirection;}
            Console.WriteLine("RELEASE MODEL TOTAL oldFound="+oldFound+" newFound="+newFound+" steps="+oldSteps+"/"+newSteps);
            Console.WriteLine("RELEASE QUALITY better="+better+" equal="+equal+" worse="+worse+" worstSquaredLoss="+worstQualityLoss+" bothPreciseDifferentTime="+preciseTimeDiff);
            Require(oldFound>15 && newFound>=oldFound,"matrix includes real supported launch parameters and nontrivial successful proposals");
            if(newSteps>0)Require(newSteps<oldSteps/2,"method removes repeated prefix advancement, not window or firing opportunities");
        }
        // Raw IEEE-754 values captured from one actual BeforeNpc call. Every
        // body/owner/receive field is reconstructed and compared before use;
        // a rounded centre and a wider receiver concealed this regression.
        private static void ExactReleaseWindow(object host,Player p,NPC n,Type type,Type terrainType,Type clockType)
        {
            var data=File.ReadAllLines(Path.Combine(Program.Repository,"tests/NativeWorldTextProbe/Fixtures/MagicReleaseBeforeNpc.bits")).Where(x=>x.Length>0).ToDictionary(x=>x.Substring(0,x.IndexOf('=')),x=>x.Substring(x.IndexOf('=')+1));
            foreach(var pair in data)if(pair.Value.StartsWith("f:")){int bits=int.Parse(pair.Value.Substring(2));Require(BitConverter.ToInt32(BitConverter.GetBytes(BitConverter.ToSingle(BitConverter.GetBytes(bits),0)),0)==bits,"captured float readback: "+pair.Key);}
            Func<string,float> f=name=>BitConverter.ToSingle(BitConverter.GetBytes(int.Parse(data[name].Substring(2))),0);
            Func<string,bool> b=name=>bool.Parse(data[name].Substring(data[name].IndexOf(':')+1));
            Func<string,int> number=name=>int.Parse(data[name].Substring(data[name].IndexOf(':')+1));
            p.position=new Vector2(f("position.X"),f("position.Y"));p.velocity=new Vector2(f("velocity.X"),f("velocity.Y"));p.fullRotation=f("fullRotation");p.fullRotationOrigin=new Vector2(f("rotationOrigin.X"),f("rotationOrigin.Y"));p.meleeSpeed=f("melee");p.yoyoString=b("string");p.direction=number("direction");
            p.HeldItem.shootSpeed=f("shootSpeed");
            Require(p.HeldItem.type==number("itemType") && p.HeldItem.prefix==number("itemPrefix") && p.HeldItem.shoot==number("shoot") && ProjectileID.Sets.YoyosTopSpeed[p.HeldItem.shoot]==f("topSpeed") && ProjectileID.Sets.YoyosMaximumRange[p.HeldItem.shoot]==f("range"),"actual release item input is unchanged");
            var start=p.RotatedRelativePoint(p.MountedCenter);Require(start.X==f("start.X") && start.Y==f("start.Y"),"actual world-space launch readback is exact");
            var ctor=typeof(NpcTrajectoryPoint).GetConstructors(Flags).Single(c=>c.GetParameters().Length==15);
            var key=new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);
            var points=new NpcTrajectoryPoint[number("future.Count")];
            for(int i=0;i<points.Length;i++)
            {
                string prefix="point."+i+".";
                var rect=new MotionRect(f(prefix+"Bounds.X"),f(prefix+"Bounds.Y"),f(prefix+"Bounds.Width"),f(prefix+"Bounds.Height"));
                points[i]=(NpcTrajectoryPoint)ctor.Invoke(new object[]{i,rect,f(prefix+"ownerX"),f(prefix+"ownerY"),b(prefix+"HasProjectileExtension"),f(prefix+"Vx"),f(prefix+"Vy"),f(prefix+"Phase"),b(prefix+"CanReceive"),b(prefix+"CanHarm"),b(prefix+"NewSegment"),b(prefix+"NoTileCollide"),b(prefix+"Wet"),b(prefix+"ReflectsProjectiles"),b(prefix+"ProjectileReflectionUnknown")});
                ExactFields(data,prefix,points[i]);
            }
            // Identity is owned by this isolated host and the clock epoch is
            // translated to its GUC. All consumed floats/point flags and Age/
            // FirstTick retain the captured values; no old lease is imported.
            ulong tick=Main.GameUpdateCount;
            foreach(string phaseName in new[]{"BeforeNpc","Projectiles","CompletedWorld"})foreach(int count in new[]{63,64,65,121})
                {
                    var phase=Enum.Parse(clockType.Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phaseName);
                    var path=new NpcTrajectory(key,(long)tick-(phaseName=="BeforeNpc"?1:0),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,count,PredictionStrategy.RollingConditional);
                    var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{path,phase,0},null);int first=(int)Get(clock,"FirstTick");
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");Vector2 oldPoint;int work;bool before=Legacy(p,p.HeldItem,f("launch"),path,first,terrain,out oldPoint,out work);
                    Call(terrain,"Reset");object[] args={p,p.HeldItem,f("launch"),path,clock,terrain,Vector2.Zero};bool after=(bool)type.GetMethod("TryPoint",Flags).Invoke(null,args);
                    Console.WriteLine("RELEASE EXACT phase="+phaseName+" first="+first+" count="+count+" shape="+points[0].ProjectileReceiveBounds.Width+"x"+points[0].ProjectileReceiveBounds.Height+" old="+before+" new="+after);
                    Require(!before || after,"exact native short-window proposal and adjacent phase/window boundaries survive");
                    if(before && after){int ot,nt;float oe=Quality(path,first,start,oldPoint,f("probeInherited"),f("topSpeed"),out ot),ne=Quality(path,first,start,(Vector2)args[6],f("probeInherited"),f("topSpeed"),out nt);Console.WriteLine("RELEASE EXACT QUALITY old="+oe+" new="+ne+" time="+ot+"/"+nt);Require(ne<=oe+1,"exact native short-window centre quality survives");}
                }
        }
        private static void ExactFields(Dictionary<string,string> data,string prefix,object value)
        {
            foreach(var field in value.GetType().GetFields(Flags))
            {
                if(field.IsStatic)continue;object item=field.GetValue(value);string name=prefix+field.Name;
                if(field.FieldType==typeof(float))Require(data[name]=="f:"+BitConverter.ToInt32(BitConverter.GetBytes((float)item),0),"exact native float field: "+name);
                else if(field.FieldType==typeof(bool))Require(data[name]=="Boolean:"+item,"exact native flag: "+name);
                else if(field.FieldType==typeof(int))Require(data[name]=="Int32:"+item,"exact native tick: "+name);
                else ExactFields(data,name+".",item);
            }
        }
        private static int FirstReceive(NpcTrajectory path,int first,Vector2 start,Vector2 point,float inherited,float speed,int width,int height)
        {
            var unit=(point-start).SafeNormalize(Vector2.UnitX);int ignored=0;
            for(int t=2;first+t<path.Count;t++){var actual=End(start,unit,inherited,speed,t,ref ignored);var box=path[first+t].ProjectileReceiveBounds;if(new Rectangle((int)(actual.X-width*.5f),(int)(actual.Y-height*.5f),width,height).Intersects(new Rectangle((int)box.X,(int)box.Y,(int)box.Width,(int)box.Height)))return t;}
            return -1;
        }
        private static void LowArcNeighborhood(Player p,NPC n,object host,Type type,Type terrainType,Type clockType,object phase)
        {
            var start=p.RotatedRelativePoint(p.MountedCenter);float speed=ProjectileID.Sets.YoyosTopSpeed[p.HeldItem.shoot],inertia=Math.Max(1,14-speed/2),held=p.HeldItem.shootSpeed>speed?p.HeldItem.shootSpeed*.98f:p.HeldItem.shootSpeed,inherited=(held*(inertia-1)+speed)/inertia;
            foreach(int horizon in new[]{64,120})foreach(int mirror in new[]{1,-1})foreach(var offset in new[]{new Vector2(500,-100),new Vector2(484,-100),new Vector2(516,-100),new Vector2(500,-84),new Vector2(500,-116)})
            {
                n.position=start+new Vector2(mirror*(offset.X+9)-9,offset.Y);n.width=18;n.height=40;
                var path=Future(host,n,horizon);var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{path,phase,0},null);int first=(int)Get(clock,"FirstTick");
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");Vector2 oldPoint;int work;bool before=Legacy(p,p.HeldItem,p.HeldItem.shootSpeed,path,first,terrain,out oldPoint,out work);
                Call(terrain,"Reset");object[] args={p,p.HeldItem,p.HeldItem.shootSpeed,path,clock,terrain,Vector2.Zero};bool after=(bool)type.GetMethod("TryPoint",Flags).Invoke(null,args);
                Require(!before || after,"nearby low-arc geometry retains an old valid proposal");if(!before)continue;
                int ot,nt;float oe=Quality(path,first,start,oldPoint,inherited,speed,out ot),ne=Quality(path,first,start,(Vector2)args[6],inherited,speed,out nt);
                int oldReceive=FirstReceive(path,first,start,oldPoint,inherited,speed,16,16),newReceive=FirstReceive(path,first,start,(Vector2)args[6],inherited,speed,16,16);
                Console.WriteLine("RELEASE LOW ARC H="+horizon+" mirror="+mirror+" offset="+offset+" receive="+oldReceive+"/"+newReceive+" nearest="+ot+"/"+nt+" error="+oe+"/"+ne);
                if(horizon==120 && offset==new Vector2(500,-100))
                {
                    int oldHit=NativeReleaseReceive(p,n,start,oldPoint,inherited),newHit=NativeReleaseReceive(p,n,start,(Vector2)args[6],inherited);
                    Console.WriteLine("RELEASE LOW ARC NATIVE mirror="+mirror+" firstDamage="+oldHit+"/"+newHit+" nearest="+ot+"/"+nt);
                    Require(oldHit>0 && newHit>0 && newHit<=oldHit,"original detached AI99 and Damage retain the effective low-arc receive");
                }
                Require(oldReceive<0 || newReceive>0 && (newReceive<=oldReceive || ne<=oe+1),"nearby valid low arc is not replaced by a later and less accurate branch");
            }
        }
        // Initialize only the model's detached birth state, then let original
        // AI99, collision, NPC immunity and Damage own every subsequent step.
        // This validates receive timing for these directions; the separate
        // natural-use checks own actual parent -> split birth and cadence.
        private static int NativeReleaseReceive(Player p,NPC n,Vector2 start,Vector2 point,float inherited)
        {
            foreach(var q in Main.projectile)q.active=false;
            n.active=true;n.life=n.lifeMax=100000;n.aiStyle=-1;n.noGravity=true;n.velocity=Vector2.Zero;n.netOffset=Vector2.Zero;n.knockBackResist=0;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);
            var unit=(point-start).SafeNormalize(Vector2.UnitX);int slot=Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_DebugCommand(),start+unit*inherited,unit*inherited,p.HeldItem.shoot,100,0,p.whoAmI,-2);var shot=Main.projectile[slot];
            Require(shot.width==16 && shot.height==16 && shot.aiStyle==99 && shot.ai[0]==-2,"actual detached body matches the response model's initial state");
            for(int step=1;step<=120 && shot.active;step++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);int life=n.life;shot.Update(slot);if(n.life<life){shot.active=false;return step;}}
            shot.active=false;return -1;
        }
        private static float Quality(NpcTrajectory path,int first,Vector2 start,Vector2 point,float inherited,float speed,out int time)
        {
            float best=float.MaxValue;time=0;int unused=0,bestTime=0;var unit=(point-start).SafeNormalize(Vector2.UnitX);
            for(int t=2;first+t<path.Count;t++){var box=path[first+t].ProjectileReceiveBounds;float error=Vector2.DistanceSquared(End(start,unit,inherited,speed,t,ref unused),new Vector2(box.CenterX,box.CenterY));if(error<16 && time==0)time=t;if(error<best){best=error;bestTime=t;}}
            if(time==0)time=bestTime;
            return best;
        }
        private static bool Legacy(Player player,Item item,float launchSpeed,NpcTrajectory future,int first,object terrain,out Vector2 point,out int steps)
        {
            steps=0;point=Vector2.Zero;var start=player.RotatedRelativePoint(player.MountedCenter);float multiplier=(1+player.meleeSpeed*3)/4;
            float speed=ProjectileID.Sets.YoyosTopSpeed[item.shoot]/multiplier,range=ProjectileID.Sets.YoyosMaximumRange[item.shoot];if(player.yoyoString)range=range*1.25f+30;range/=multiplier;
            float inertia=Math.Max(1,14-speed/2),held=launchSpeed>speed?launchSpeed*.98f:launchSpeed,inherited=(held*(inertia-1)+Math.Min((range-1)/2,speed))/inertia;
            var sample=ContentSamples.ProjectilesByType[item.shoot];float best=float.MaxValue,bound=float.MaxValue;Vector2 chosen=Vector2.Zero;
            for(int time=2;first+time<future.Count;time++)
            {
                var target=future[first+time].ProjectileReceiveBounds;var goal=new Vector2(target.CenterX,target.CenterY);var unit=(goal-start-new Vector2(0,.15f*time*(time+1))).SafeNormalize(Vector2.UnitX*player.direction);
                for(int refine=0;refine<4;refine++){var end=End(start,unit,inherited,speed,time,ref steps);unit=(unit+(goal-end)/Math.Max(1,inherited*(time+1))).SafeNormalize(unit);}
                var actual=End(start,unit,inherited,speed,time,ref steps);float error=Vector2.DistanceSquared(actual,goal);if(error>=best)continue;best=error;chosen=unit;
                float dx=Math.Max(0,Math.Abs(actual.X-goal.X)-(target.Width+sample.width)/2),dy=Math.Max(0,Math.Abs(actual.Y-goal.Y)-(target.Height+sample.height)/2);bound=dx*dx+dy*dy;if(error<16)break;
            }
            point=start+chosen*(range-1);return bound<=4 && (bool)Call(terrain,"ProjectilePassage",start.X,start.Y,start.X+chosen.X*inherited,start.Y+chosen.Y*inherited,sample.width,sample.height);
        }
        private static Vector2 End(Vector2 start,Vector2 unit,float inherited,float speed,int time,ref int steps)
        {
            var velocity=unit*inherited;var position=start+velocity;
            for(int i=0;i<time;i++) {steps++;velocity.Y+=.3f;if(velocity.LengthSquared()>256)velocity=velocity.SafeNormalize(Vector2.Zero)*16;else if(velocity.Y<=3){float top=Math.Min(16,speed);if(Math.Abs(velocity.X)<.1f)velocity.X+=unit.X<0?-.1f:.1f;else if(velocity.X>0 && velocity.X<top)velocity.X+=.1f;else if(velocity.X<0 && velocity.X>-top)velocity.X-=.1f;}position+=velocity;}return position;
        }
        private static void PrepareNavigation(object host,object attack,NPC n)
        {
            typeof(NativeCombatAimChecks).GetMethod("PublishFixture",Flags).Invoke(null,new object[]{host,n,0f});
            var phase=Enum.Parse(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase"),"Projectiles");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(attack,"PrepareCore",phase);
        }
        private static void Contact(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            foreach(int weapon in Environment.GetEnvironmentVariable("JMR_CONTACT_TERRARIAN_ONLY")=="1"?new[]{3389}:new[]{3278,3389})foreach(int aperture in weapon==3389?new[]{1,2}:new[]{1})foreach(bool sealedGap in new[]{false,true})foreach(float offset in new[]{0f,.25f,1f})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.ResetEffects();p.releaseUseItem=true;p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;
                p.Center=new Vector2(1024+222+offset,640+91);Main.screenPosition=new Vector2(950,400);
                for(int x=62;x<84;x++)for(int y=38;y<52;y++)Main.tile[x,y].ClearEverything();
                for(int x=2;x<=15;x++){if((x!=8 && (aperture==1 || x!=9)) || sealedGap)NativeToolsChecks.Tile(64+x,43,1);NativeToolsChecks.Tile(64+x,47,1);}
                for(int y=3;y<=7;y++){NativeToolsChecks.Tile(66,40+y,1);NativeToolsChecks.Tile(79,40+y,1);}
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.aiStyle=-1;n.noGravity=true;n.velocity=n.netOffset=Vector2.Zero;n.position=new Vector2(1120,656);n.width=16;n.height=32;n.life=n.lifeMax=10000;n.knockBackResist=0;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));PrepareNavigation(host,attack,n);
                var control=Get(attack,"Control");bool opening=(bool)Get(control,"openingUsable");if(!opening){var navType=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostYoyoNavigation",true);var nav=Activator.CreateInstance(navType,true);Call(Get(nav,"terrain"),"Reset");var find=navType.GetMethod("FindContact",Flags);Console.WriteLine("CONTACT DIAGNOSTIC direct="+(find==null?"old":find.Invoke(nav,new object[]{p.RotatedRelativePoint(p.MountedCenter),new MotionRect(n.position.X,n.position.Y,n.width,n.height),p.Center,130f,16,16}).ToString())+" openingGate="+attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackWindow").GetMethod("Opening",Flags).Invoke(null,new object[]{combat,p,p.HeldItem})+" used="+Get(Get(host,"PredictionWork"),"UsedMilliseconds"));}
                Console.WriteLine("CONTACT OPEN weapon="+weapon+" aperture="+aperture+" sealed="+sealedGap+" offset="+offset+" opening="+opening+" player="+p.Center+" range="+ProjectileID.Sets.YoyosMaximumRange[p.HeldItem.shoot]+" sample="+ContentSamples.ProjectilesByType[p.HeldItem.shoot].width+"x"+ContentSamples.ProjectilesByType[p.HeldItem.shoot].height);
                bool fits=true;Require(opening==!sealedGap,"contact region finds top gap and rejects sealed gap using actual native yoyo dimensions/range");if(weapon==3389 && aperture==1 && !sealedGap && offset==0)ManualTerrarian(context,combat,host,input,p,n);if(sealedGap || !fits)continue;foreach(var existing in Main.projectile)existing.active=false;Array.Clear(n.immune,0,n.immune.Length);n.life=10000;p.channel=p.controlUseItem=p.releaseUseItem=true;p.itemTime=p.itemAnimation=0;NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));PrepareNavigation(host,attack,n);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==p.HeldItem.shoot);bool crossed=false;int hit=-1;
                for(int t=0;t<110 && shot.active && hit<0;t++)
                {
                    NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");n.UpdateNPC(2);PrepareNavigation(host,attack,n);shot.Update(shot.whoAmI);crossed|=shot.position.Y+shot.height<=688;if(n.life<10000)hit=t;
                    if(t<22 || t%15==0)Console.WriteLine("CONTACT MOTOR entryPoint="+Get(((System.Collections.IDictionary)Get(control,"shots"))[(int)shot.key],"Point")+" path="+string.Join(";",((System.Collections.IEnumerable)Get(Get(((System.Collections.IDictionary)Get(control,"shots"))[(int)shot.key],"Navigation"),"path")).Cast<object>())+" t="+t+" center="+shot.Center+" velocity="+shot.velocity+" damage="+(10000-n.life));
                }
                Console.WriteLine("CONTACT RESULT weapon="+weapon+" aperture="+aperture+" offset="+offset+" crossed="+crossed+" hit="+hit+" center="+shot.Center);Require(crossed && hit>=0,"actual original AI99/TileCollision/Damage crosses top hole and contacts standing target");
            }
        }
        private static Projectile damageSource;private static int parentHits,childHits,manualStep;private static object manualTerrain;
        private static void DamageBefore(Projectile __instance){damageSource=__instance;}
        private static void DamageAfter(){damageSource=null;}
        private static void ManualStrike(NPC __instance,int __result){if(__instance.whoAmI!=2 || __result<=0 || damageSource==null)return;if(damageSource.type==603)parentHits++;if(damageSource.type==604)childHits++;}
        private static bool Comparing {get{return manualTerrain!=null && manualStep>=5 && manualStep<=12 && Main.ProjectileUpdateLoopIndex>=0 && Main.projectile[Main.ProjectileUpdateLoopIndex].type==603;}}
        private static void TileCompared(Vector2 __0,Vector2 __1,int __2,int __3,Vector2 __result)
        {
            if(!Comparing)return;Call(manualTerrain,"Reset");object[] args={__0.X+__2*.5f,__0.Y+__3*.5f,__1.X,__1.Y,__2,__3,0f,0f,true};bool valid=(bool)Call(manualTerrain,"ProjectileCollision",args);Require(valid && Math.Abs((float)args[6]-__result.X)<.001f && Math.Abs((float)args[7]-__result.Y)<.001f,"full-size native and pure Terrarian clipped collision agree at the aperture");
            Console.WriteLine("TERRARIAN TILE t="+manualStep+" position="+__0+" v="+__1+" size="+__2+"x"+__3+" native="+__result+" pure="+args[6]+","+args[7]+" valid="+valid);
        }
        private static void SlopeCompared(Vector2 __0,Vector2 __1,int __2,int __3,Vector4 __result)
        {
            if(!Comparing)return;Call(manualTerrain,"Reset");object[] args={__0.X+__2*.5f,__0.Y+__3*.5f,__1.X,__1.Y,__2,__3};bool valid=(bool)Call(manualTerrain,"ProjectileSlope",args);Require(valid && Math.Abs((float)args[0]-(__result.X+__2*.5f))<.001f && Math.Abs((float)args[1]-(__result.Y+__3*.5f))<.001f && Math.Abs((float)args[2]-__result.Z)<.001f && Math.Abs((float)args[3]-__result.W)<.001f,"native and pure Terrarian slope continuation agree at the aperture");
            Console.WriteLine("TERRARIAN SLOPE t="+manualStep+" position="+__0+" v="+__1+" native="+__result+" pureCenter="+args[0]+","+args[1]+" pureV="+args[2]+","+args[3]+" valid="+valid);
        }
        private static void ManualTerrarian(object context,object combat,object host,object input,Player p,NPC n)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var physical=new Vector2(1160,656);NativeToolExecutionChecks.Sample(context,input,physical,true);Call(combat,"Sample");
            var random=typeof(Main).GetField("_rngs",Flags);object priorRandom=random.GetValue(null);random.SetValue(null,new System.Collections.Generic.Dictionary<string,Terraria.Utilities.UnifiedRandom>{{"UpdateProjectiles",new Terraria.Utilities.UnifiedRandom(702)}});
            manualTerrain=Activator.CreateInstance(combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);
            var audit=new HarmonyLib.Harmony("JueMingR.Tests.TerrarianManualParts");audit.Patch(typeof(Collision).GetMethod("TileCollision",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCostNavigationChecks),nameof(TileCompared)));audit.Patch(typeof(Collision).GetMethod("SlopeCollision",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCostNavigationChecks),nameof(SlopeCompared)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCostNavigationChecks),nameof(DamageBefore)),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCostNavigationChecks),nameof(DamageAfter)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCostNavigationChecks),nameof(ManualStrike)));
            try
            {
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var parent=Main.projectile.Single(q=>q.active && q.type==603);Vector2 anchor;int width,height;parent.GetCollisionParams(out anchor,out width,out height);float minY=parent.Center.Y;bool crossed=false,childCrossed=false;parentHits=childHits=0;int born=0;
                for(int t=0;t<110 && parent.active;t++)
                {
                    manualStep=t;NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,physical,true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatPhaseChecks.ProjectilePhase();minY=Math.Min(minY,parent.Center.Y);if(t<35 || !crossed && parent.position.Y+parent.height<=688)Console.WriteLine("TERRARIAN MANUAL STEP t="+t+" center="+parent.Center+" v="+parent.velocity+" ai="+parent.ai[0]+" collide="+parent.tileCollide+" active="+parent.active+" clock="+parent.localAI[0]+" floorTile="+Main.tile[71,43].active()+"/"+Main.tileSolid[1]);crossed|=parent.position.Y+parent.height<=688;
                    foreach(var child in Main.projectile.Where(q=>q.active && q.type==604)){born++;childCrossed|=child.position.Y+child.height<=688;}
                }
                Console.WriteLine("TERRARIAN MANUAL source603="+parent.width+"x"+parent.height+" collision="+width+"x"+height+" anchor="+anchor+" range="+ProjectileID.Sets.YoyosMaximumRange[603]+" topHole=16 parentCrossed="+crossed+" minCenterY="+minY+" parentHits="+parentHits+" child604Crossed="+childCrossed+" childHits="+childHits+" childObservations="+born);
                Require(crossed && width==parent.width && height==parent.height && width>16,"actual native Terrarian retains its full collision dimensions; one-tile traversal is observed separately");
            }
            finally{random.SetValue(null,priorRandom);manualTerrain=null;damageSource=null;audit.UnpatchAll(audit.Id);}
        }
        internal static void RunRelease(object context){Release(context);}
        internal static void RunNavigation(object context)
        {
            var audit=new HarmonyLib.Harmony("JueMingR.Tests.ContactNavigationDefault");audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));try{Contact(context);Group(context);}finally{audit.UnpatchAll(audit.Id);}
        }
        private static void Group(object context)
        {
            var combat=Get(context,"Combat");var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3278,0,0);p.ResetEffects();
            var type=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostYoyoNavigation",true);var factsType=type.GetNestedType("GroupFacts",Flags);Require(factsType!=null,"one definite preparation captures group facts");
            for(int i=0;i<4;i++){var shot=Main.projectile[i+10];shot.SetDefaults(i==3?545:541);shot.active=true;shot.owner=0;shot.whoAmI=i+10;shot.localAI[0]=i*35;shot.ai[0]=i==2?-2:0;}
            var scans=type.GetField("GroupScans",Flags);if(scans!=null)scans.SetValue(null,0);var group=Activator.CreateInstance(factsType,Flags,null,new object[]{p},null);
            foreach(var shot in Main.projectile.Where(q=>q.active && q.owner==0 && q.ai[0]>=-1))
            {
                bool secondary=(bool)type.GetMethod("Secondary",Flags).Invoke(null,new object[]{shot});int old=(int)type.GetMethod("Remaining",Flags).Invoke(null,new object[]{p,shot,secondary});
                Require((bool)Call(group,"Secondary",shot)==secondary && (int)Call(group,"Remaining",p,shot,secondary)==old,"same captured primary/paired group preserves native safety lifetime");
            }
            if(scans!=null)Require((int)scans.GetValue(null)==1,"all captured ball queries share one whole-pool scan");
            Main.projectile[5].SetDefaults(541);Main.projectile[5].active=true;Main.projectile[5].owner=0;Main.projectile[5].whoAmI=5;Main.projectile[5].localAI[0]=200;Main.projectile[5].ai[0]=0;
            var fresh=Activator.CreateInstance(factsType,Flags,null,new object[]{p},null);if(scans!=null)Require((int)scans.GetValue(null)==2,"a real pool change captures one fresh group");Require((bool)Call(fresh,"Secondary",Main.projectile[10]),"same-update new earlier-slot ball invalidates old primary facts on next definite capture");
            Console.WriteLine("GROUP PASS exact Remaining/Secondary equivalence and same-update new-slot recapture");
        }
    }
}
