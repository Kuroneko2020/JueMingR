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
    internal static class NativeCombatLaserPaintChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private struct Growth{internal int Damage,Penetration;}
        private static readonly List<Growth> growths=new List<Growth>();
        private static Assembly assembly;private static Type model;private static Projectile motor,receiving;private static NPC target;
        private static int beforeDamage,beforePen,solves,steps,drawSolves,drawSteps,births;
        private static bool natural;private static long first,birthTick;private static AttackContact known,prepared;private static object ammo;private static Vector2 raw;
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeAI(Projectile __instance){if(ReferenceEquals(__instance,motor)){beforeDamage=__instance.damage;beforePen=__instance.penetrate;}}
        private static void AfterAI(Projectile __instance){if(ReferenceEquals(__instance,motor))growths.Add(new Growth{Damage=__instance.damage-beforeDamage,Penetration=__instance.penetrate-beforePen});}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(__instance,target) && receiving!=null && receiving.owner==Main.myPlayer && (receiving.type==88 || receiving.type==587) && receiving.damage>0 && __result>0 && first<0)first=Main.GameUpdateCount;}
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static Player Reset(object context,int item=514)
        {
            natural=false;motor=receiving=null;target=null;first=-1;births=0;growths.Clear();known=prepared=null;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=32,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!natural || __result<0)return;var q=Main.projectile[__result];if(q.type!=88 && q.type!=587)return;
            var p=Main.LocalPlayer;var source=__0 as EntitySource_Parent;Require(!(source?.Entity is Projectile),"Laser/Paint native birth is a real hand source, never an invented child");births++;birthTick=Main.GameUpdateCount;
            var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            Vector2 expected=(cursor-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed"),birth=origin;
            if(q.type==587)
            {
                Vector2 jitter=q.velocity-expected;
                Require(new[]{-.5f,0,.5f}.Any(v=>Math.Abs(jitter.X-v)<.0001f) && new[]{-.5f,0,.5f}.Any(v=>Math.Abs(jitter.Y-v)<.0001f),"captured Paintball launch lies in each native three-value random component interval");
                bool clear=Collision.CanHitLine(p.Center,0,0,origin+q.velocity*2,0,0);if(clear)birth+=q.velocity;birth.Y-=p.gravDir*4;
                Require(q.ai[1]>=0 && q.ai[1]<=11f/6 && Math.Abs(q.ai[1]*6-(int)Math.Round(q.ai[1]*6))<.00001f,"native random hue is a visual-only legal twelfth");
            }
            else expected=(Vector2)Static(model,"LimitLaunch",expected);
            var path=Timeline(target);known=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));
            Console.WriteLine("LASER PAINT actual birth item="+p.HeldItem.type+" type="+q.type+" slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" dmg="+q.damage+" pen/max="+q.penetrate+"/"+q.maxPenetrate+" extra="+q.extraUpdates+" scale="+q.scale+" local="+q.localAI[0]+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
            Require(q.owner==p.whoAmI && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(q.Center,birth)<.0001f && (q.type==587 || Vector2.DistanceSquared(q.velocity,expected)<.0001f) && Vector2.Dot(q.velocity,raw-origin)<0,"native hand consumes the assisted cursor, exact launch body/damage and legal velocity without changing RNG");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostLaserPaintAttack",true);var audit=new Harmony("JueMingR.Tests.LaserPaint");bool wind=Main.windPhysics;var random=Main.rand;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethod("AI_001",Flags),prefix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(BeforeAI)),postfix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(AfterAI)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatLaserPaintChecks),nameof(Strike)));
            try
            {
                string only=Environment.GetEnvironmentVariable("JMR_LASER_PAINT_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="birth")PaintBirths(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);
                Require(string.IsNullOrEmpty(only) || only=="motor" || only=="birth" || only=="receive" || only=="natural","known Laser/Paint narrow leaf");
            }
            finally{natural=false;motor=receiving=null;target=null;growths.Clear();Main.windPhysics=wind;Main.rand=random;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Compare(Projectile q,object s,string label)
        {
            Require(q.active==(bool)Get(s,"Alive") && q.timeLeft==(int)Get(s,"Left") && q.penetrate==(int)Get(s,"Penetration") && q.maxPenetrate==(int)Get(s,"MaximumPenetration") && q.damage==(int)Get(s,"Damage") && q.extraUpdates==(int)Get(s,"Extra") && q.alpha==(int)Get(s,"Alpha") && q.ai[0]==(float)Get(s,"Counter") && q.localAI[0]==(float)Get(s,"Visual") && q.scale==(float)Get(s,"Scale") && q.knockBack==(float)Get(s,"KnockBack") && q.wet==(bool)Get(s,"Wet") && q.honeyWet==(bool)Get(s,"Honey") && q.lavaWet==(bool)Get(s,"Lava") && q.shimmerWet==(bool)Get(s,"Shimmer") && Vector2.DistanceSquared(q.position,(Vector2)Get(s,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(s,"Velocity"))<.0001f,"native Laser/Paint motor/clock/growth agrees: "+label+" native="+q.position+"/"+q.velocity+" left="+q.timeLeft+" scale="+q.scale+" extra="+q.extraUpdates+" damage/pen="+q.damage+"/"+q.penetrate+" model="+Get(s,"Position")+"/"+Get(s,"Velocity")+" left="+Get(s,"Left")+" scale="+Get(s,"Scale")+" extra="+Get(s,"Extra")+" damage/pen="+Get(s,"Damage")+"/"+Get(s,"Penetration"));
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0,events=0;var foreign=Main.player[254];bool active=foreign.active;
            try
            {
                foreach(int type in new[]{88,587})foreach(int scene in Enumerable.Range(0,12))
                {
                    if(type==587 && scene>=10)continue;var p=Reset(context,type==88?514:3350);foreign.active=true;
                    if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                    if(scene>=8 && scene<=9)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                    if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);if(scene==5){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope(1);}
                    Vector2 origin=scene==5?new Vector2(936,390):scene==6?new Vector2(10,350):new Vector2(950,350),v=scene==4?new Vector2(24,0):scene==5?new Vector2(24,8):scene==6?new Vector2(-7,0):scene>=8 && scene<=9?new Vector2(6,scene==8?2:-2):new Vector2(6,-.3f);
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,v,type,50,2,scene==10?254:p.whoAmI);var q=Main.projectile[slot];if(scene==7)q.timeLeft=1;if(scene==11)q.ai[0]=2;
                    var s=Activator.CreateInstance(model.GetNestedType("State",BindingFlags.NonPublic),Flags,null,new object[]{q},null);var terrain=Terrain();motor=q;
                    for(int outer=1;outer<=40 && q.active;outer++)
                    {
                        int updates=q.extraUpdates+1;growths.Clear();q.Update(slot);
                        for(int sub=0;sub<updates;sub++)
                        {
                            var g=sub<growths.Count?growths[sub]:default(Growth);if(g.Damage!=0 || g.Penetration!=0){Require(type==88 && q.owner==Main.myPlayer && g.Damage>=1 && g.Damage<=3 && g.Penetration>=0 && g.Penetration<=1,"observed local native growth uses the complete legal damage/penetration intervals");events++;}
                            Require((bool)Static(model,"Step",q,s,terrain,g.Damage==0?1:g.Damage,g.Penetration),"finite Laser/Paint scene dependencies remain available");actions++;if((bool)Get(s,"Stopped"))break;Static(model,"Tail",s);
                        }
                        Compare(q,s,type+"/"+scene+"/"+outer);
                    }
                    if(scene==3)Require(!q.active && q.timeLeft==0,"full shimmer column calls actual Kill");if(scene==6)Require(!q.active && q.timeLeft>0,"world edge retires without a Kill");if(scene==10)Require(q.damage==50 && q.penetrate==3 && q.knockBack==2 && q.localAI[0]==0,"foreign laser drawing growth cannot invent local damage or budget growth");
                    // SetDefaults scales the nominal 6px body once by .65 (integer 3px). AI001 later grows drawing scale only.
                    if(type==88){var box=q.Damage_GetHitbox();Require(q.width==3 && q.height==3 && box.Width==3 && box.Height==3,"native laser keeps the actual 3px post-default damage body while drawing scale grows");}cases++;
                }
            }
            finally{motor=null;foreign.active=active;}
            Require(events>0,"full local growth branch actually executes");Console.WriteLine("PASS LASER PAINT motor="+cases+" actions="+actions+" observedGrowth="+events);
        }
        private static void PaintBirths(object context)
        {
            int cases=0;foreach(bool blocked in new[]{false,true})foreach(float dx in new[]{-.5f,0,.5f})foreach(float dy in new[]{-.5f,0,.5f})
            {
                var p=Reset(context,3350);Vector2 origin=new Vector2(950,350),center=new Vector2(900,350),v=new Vector2(10+dx,dy);if(blocked)for(int y=10;y<60;y++)NativeToolsChecks.Tile(59,y,1);
                bool clear=Collision.CanHitLine(center,0,0,origin+v*2,0,0);Require(clear!=blocked,"Paint birth source-line fixture truly selects both native clear/blocked branches");
                Vector2 expected=origin;if(clear)expected+=v;expected.Y-=4;var args=new object[]{Terrain(),center,origin,v,1f,null};Require((bool)Static(model,"PaintBirth",args) && (Vector2)args[5]==expected,"all nine legal Paint jitter members retain exact native conditional origin advance and -4 gravity offset");cases++;
            }
            Console.WriteLine("PASS PAINT birth-origin roles="+cases+"; legal members do not call RNG");
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int type in new[]{88,587})foreach(int mode in new[]{0,1,2})
            {
                var p=Reset(context,type==88?514:3350);bool blocked=mode==1;target=Target(2,new Vector2(960,350),50,60);target.immune[p.whoAmI]=blocked?100:0;var front=mode==2?Target(1,new Vector2(950,350),50,60):null;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,8,0,type,50,0,p.whoAmI);var q=Main.projectile[slot];NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                for(int outer=1;outer<=12 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("LASER PAINT receive type="+type+" mode="+mode+" ownerBlocked="+blocked+" native="+actual+" known="+contact?.Tick+" remaining="+target.immune[p.whoAmI]);
                Require((actual==0)==(contact==null) && (contact==null || actual==contact.Tick) && (type==88 && blocked || type==587 && mode==2?actual==0:actual==1) && (front==null || front.life<10000),"native owner gate and actual lower-slot receiver budget agree; one-hit Paint bypasses owner immunity but is exhausted by an earlier body");cases++;
            }
            Console.WriteLine("PASS LASER PAINT receive="+cases);
        }
        private static void Natural(object context)
        {
            int cases=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            foreach(int item in new[]{514,3350})
            {
                var p=Reset(context,item);p.position=new Vector2(650,646);p.releaseUseItem=true;p.statMana=p.statManaMax2=200;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,43,40);raw=target.Center-new Vector2(390,0);solves=steps=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);prepared=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(prepared!=null && !Main.projectile.Any(q=>q.active),"idle native Laser/Paint hand stage is previewed without firing");ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
                drawSolves=solves;drawSteps=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int draw=0;draw<20;draw++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",prepared),"same-update Laser/Paint presentation retains its preparation lease");}Require(drawSolves==solves && drawSteps==steps && solves>0,"twenty Draws do no Laser/Paint solves or native motor work");
                natural=true;first=-1;births=0;long start=Main.GameUpdateCount;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);for(int i=0;i<8 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                Console.WriteLine("LASER PAINT natural item="+item+" births="+births+" first="+(first<0?0:first-start)+" actualBornKnown="+known?.Tick+" representative="+prepared.Tick+" solves="+solves+" steps="+steps);
                Require(births==1 && first>=birthTick && known!=null && first-birthTick+1==known.Tick,"actual one-press/release hand birth has useful positive Strike on genuine immortal dummy, with each native born contact equal to Known");natural=false;cases++;
            }
            Console.WriteLine("PASS LASER PAINT natural="+cases);
        }
    }
}
