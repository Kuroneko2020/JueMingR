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
    internal static class NativeCombatLightsBaneChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static int depth,rng;
        private static void Random(){if(depth>0)rng++;}
        private static Player Scene(object context)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,46,50);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();p.gravDir=1;p.direction=1;return p;}
        private static NPC Target(int slot,Vector2 center,bool blocked=false)
        {var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=12;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p){depth++;try{return (Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostNoteAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{p});}finally{depth--;}}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,Terrain(),root});}finally{depth--;}}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostLightsBaneAttack",true);var audit=new Harmony("JueMingR.Tests.LightsBane");var originalRandom=Main.rand;
            foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatLightsBaneChecks),nameof(Random)));
            int motors=0,actions=0,grid=0,births=0,useful=0;
            try
            {
                foreach(float scale in new[]{1f,2f})foreach(float angle in new[]{-.7f,0f,1.3f})foreach(int nearEnd in new[]{0,1})
                {
                    var p=Scene(context);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),angle.ToRotationVector2()*.001f,974,0,0,p.whoAmI,scale);var actual=Main.projectile[slot];if(nearEnd==1){actual.frame=11;actual.frameCounter=2;}var shadow=Copy(actual);var terrain=Terrain();
                    for(int tick=1;tick<=40 && actual.active;tick++)
                    {
                        depth++;try{Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{shadow,terrain}),"LightsBane finite motor reads known environment");}finally{depth--;}
                        if(shadow.active && --shadow.timeLeft<=0){shadow.active=false;shadow.timeLeft=0;}NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);
                        Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.0001f && actual.scale==shadow.scale && actual.rotation==shadow.rotation && actual.frame==shadow.frame && actual.frameCounter==shadow.frameCounter && actual.localAI[0]==shadow.localAI[0] && actual.timeLeft==shadow.timeLeft),"original AI188 whole life/frame retirement agrees scale="+scale+" angle="+angle+" tick="+tick);actions++;
                        if(actual.active && tick==1)for(int x=-110;x<=110;x+=5)for(int y=-110;y<=110;y+=5){var box=new Rectangle((int)actual.Center.X+x,(int)actual.Center.Y+y,3,5);bool expected=actual.Colliding(actual.Hitbox,box),value=(bool)model.GetMethod("Collides",Flags).Invoke(null,new object[]{shadow,new MotionRect(box.X,box.Y,box.Width,box.Height)});Require(expected==value,"native974 exact scaled cross-section and integer bound grid");grid++;}
                    }motors++;
                }
                foreach(int frontCount in new[]{0,1,2})foreach(bool blocked in new[]{false,true})
                {
                    var p=Scene(context);var n=Target(3,new Vector2(820,450),blocked);for(int i=0;i<frontCount;i++)Target(i+1,new Vector2(780+i*10,450));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),Vector2.UnitX*.001f,974,40,0,p.whoAmI,1);var q=Main.projectile[slot];object root;var plan=Known(p,q,n,out root);Require((bool)Get(root,"Unchanged"),"native974 lease initially current");q.frameCounter++;Require(!(bool)Get(root,"Unchanged"),"same-GUC damage lifetime frame retires lease");q.frameCounter--;int first=0;
                    for(int tick=1;tick<=35 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(slot);if(n.life<100000 && first==0)first=tick;}
                    bool reachable=!blocked && frontCount<2;Console.WriteLine("LIGHTS receive front="+frontCount+" blocked="+blocked+" plan="+plan?.Tick+" native="+first+" damage="+q.damage+" pen="+q.penetrate+" lives="+string.Join(",",Main.npc.Take(4).Select(x=>x.life)));Require(reachable?plan!=null && first==plan.Tick && first>0:plan==null && first==0,"native974 penetration2/permanent per-body local gate and invulnerable negative");if(reachable)useful++;
                }
                foreach(int facing in new[]{-1,1})foreach(int gravity in new[]{-1,1})foreach(bool nearby in new[]{false,true})foreach(int seed in new[]{1,17})
                {
                    var p=Scene(context);p.gravDir=gravity;p.direction=-facing;Vector2 basePoint=p.MountedCenter+new Vector2(70*facing,-40*gravity);NPC n=nearby?Target(2,basePoint):null;NativeToolExecutionChecks.Sample(context,Get(context,"Input"),p.Center+new Vector2(300*facing,0),true);Call(Get(context,"Combat"),"Sample");int mouseX=Main.mouseX,mouseY=Main.mouseY;Main.rand=new Terraria.Utilities.UnifiedRandom(seed);
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(x=>x.active && x.type==974);Require(p.direction==facing && (q.ai[0]==1 || q.ai[0]==2) && Math.Abs(q.velocity.Length()-.001f)<.000001f && q.damage==(int)(p.GetWeaponDamage(p.HeldItem)*q.ai[0]*.5),"native974 actual facing/gravity/crit birth keeps sealed near-stationary slash");Require(nearby?n.Hitbox.Contains(q.Center.ToPoint()):Vector2.Distance(q.Center,basePoint)<31,"native50px nearby choice/random spawn support remains local");Require(Main.mouseX==mouseX && Main.mouseY==mouseY,"autonomous birth does not leave borrowed cursor");
                    if(nearby){object root;var plan=Known(p,q,n,out root);int first=0;for(int tick=1;tick<=35 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(q.whoAmI);if(n.life<100000 && first==0)first=tick;}Require(plan!=null && first==plan.Tick,"actual randomly born974 has exact sealed first native Strike");useful++;}births++;
                }
                var owner=Scene(context);Require((bool)host.GetType("JueMingR.TerrariaHost.Combat.HostSwingAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{owner.HeldItem}),"LightsBane retains ordinary native melee body beside sealed slash");Require(!(bool)host.GetType("JueMingR.TerrariaHost.Combat.HostSwingAttack").GetMethod("Mixed",Flags).Invoke(null,new object[]{owner.HeldItem}),"autonomous near-target slash does not hide the actual hand behind a nonexistent speed-controlled shot");Require(useful>0 && rng==0,"private974 motor/receive/copy never consumes live RNG");
            }
            finally{depth=0;Main.rand=originalRandom;audit.UnpatchAll(audit.Id);}
            Console.WriteLine("LIGHTS-BANE motors="+motors+" actions="+actions+" grid="+grid+" trueBirths="+births+" useful="+useful+" receiveCases=6 rng=0");
        }
    }
}
