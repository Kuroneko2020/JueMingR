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
    internal static class NativeCombatElectrosphereChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,motor,parent;
        private static bool natural;private static int parentBirths,children,solves,steps,parentStrikes;private static long first,sourceFirst,birthTick;private static AttackContact known;private static object ammo;private static Vector2 raw,killCenter;private static int kills;
        private static readonly List<int> childSlots=new List<int>(),hits=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeKill(Projectile __instance){if(ReferenceEquals(__instance,motor)){kills++;killCenter=__instance.Center;}}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;
            if(receiving.type==442)parentStrikes++;if(receiving.type!=443)return;hits.Add(__instance.whoAmI);if(ReferenceEquals(__instance,target)){if(first<0)first=Main.GameUpdateCount;if(childSlots.Contains(receiving.whoAmI) && sourceFirst<0)sourceFirst=Main.GameUpdateCount;}
        }
        private static Player Reset(object context)
        {
            natural=false;target=null;receiving=motor=parent=null;first=sourceFirst=-1;parentBirths=children=kills=parentStrikes=0;known=null;hits.Clear();childSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),2796,0,0);p.inventory[54].SetDefaults(771);p.inventory[54].stack=999;Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static Projectile Shot(Player p,int type,Vector2 center,Vector2 v,int damage=50)
        {int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,v,type,damage,0,p.whoAmI);var q=Main.projectile[slot];if(type==442){q.ai[0]=100;q.ai[1]=50;}return q;}
        private static void Born(IEntitySource __0,int __result)
        {
            if(__result<0)return;var q=Main.projectile[__result];var source=__0 as EntitySource_Parent;
            if(q.type==443 && source?.Entity is Projectile){children++;childSlots.Add(q.whoAmI);Require(((Projectile)source.Entity).type==442,"native sphere has the actual 442 Kill source");}
            if(!natural || q.type!=442)return;parent=q;parentBirths++;birthTick=Main.GameUpdateCount;var p=Main.LocalPlayer;var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var origin=p.RotatedRelativePoint(p.MountedCenter);var velocity=(cursor-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;
            var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));
            Console.WriteLine("ELECTRO born slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" tile="+q.ai[0]+","+q.ai[1]+" actualTile="+Player.tileTargetX+","+Player.tileTargetY+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
            int tileX=Math.Max(5,Math.Min(Main.maxTilesX-5,(int)(cursor.X/16))),tileY=Math.Max(5,Math.Min(Main.maxTilesY-5,(int)(cursor.Y/16)));
            Require(!(source?.Entity is Projectile) && q.ai[0]==Player.tileTargetX && q.ai[1]==Player.tileTargetY && q.ai[0]==tileX && q.ai[1]==tileY && tileX!=(int)(raw.X/16) && q.damage==(int)Get(ammo,"Damage") && q.penetrate==1 && q.maxPenetrate==1 && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,velocity)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual hand consumes matching borrowed tile point and velocity cursor, different from physical raw target, without primary Strike");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostElectrosphereAttack",true);var audit=new Harmony("JueMingR.Tests.Electrosphere");bool wind=Main.windPhysics,foreignActive=Main.player[1].active;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatElectrosphereChecks),nameof(Strike)));
            var componentHand=new NativeCombatHandFixture(context,()=>natural,()=>target);
            try{string only=Environment.GetEnvironmentVariable("JMR_ELECTROSPHERE_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="birth")Births(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);Require(string.IsNullOrEmpty(only) || only=="motor" || only=="birth" || only=="receive" || only=="natural","native electrosphere narrow leaf");}
            finally{componentHand.Dispose();natural=false;target=null;receiving=motor=parent=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.player[1].active=foreignActive;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Compare(Projectile q,Projectile copy,int type,int scene,int outer)
        {
            bool equal=q.active==copy.active && q.width==copy.width && q.height==copy.height && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.ai.SequenceEqual(copy.ai) && q.localAI.SequenceEqual(copy.localAI) && q.friendly==copy.friendly && q.rotation==copy.rotation && q.frame==copy.frame && q.frameCounter==copy.frameCounter && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.lavaWet==copy.lavaWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f;
            Require(equal,"native sphere motor/body/alpha/friendly/Kill agrees "+type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.timeLeft+"/"+q.width+"/"+q.alpha+"/"+string.Join(",",q.ai)+"/"+string.Join(",",q.localAI)+" shadow="+copy.position+"/"+copy.velocity+"/"+copy.timeLeft+"/"+copy.width+"/"+copy.alpha+"/"+string.Join(",",copy.ai)+"/"+string.Join(",",copy.localAI));
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;foreach(int type in new[]{442,443})foreach(int scene in Enumerable.Range(0,8))
            {
                var p=Reset(context);if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                var center=scene==6?new Vector2(3,350):new Vector2(950,350);var q=Shot(p,type,center,new Vector2(scene==4?24:6,0),0);if(scene==5)q.timeLeft=1;if(scene==7){if(type==442){q.ai[0]=(int)(q.Center.X/16);q.ai[1]=(int)(q.Center.Y/16);}else {q.ai[1]=1;q.alpha=0;}}
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();motor=q;
                for(int outer=1;outer<=130 && q.active;outer++)
                {
                    var args=new object[]{copy,terrain,null,null};Require((bool)Static(model,"Step",args),"finite sphere terrain captures native dependencies");bool killed=(bool)args[2];Vector2 death=(Vector2)args[3];if(copy.active)Static(model,"Tail",copy,null,null);q.Update(q.whoAmI);actions++;Compare(q,copy,type,scene,outer);
                    if(killed)Require(kills==1 && Vector2.DistanceSquared(killCenter,death)<.0001f,"native BeforeKill center is separate from final dead position");
                }
                if(scene==6)Require(kills==0 && children==0,"world retirement skips Kill source and child birth");if(type==442 && scene==0)Require(children==1 && parentStrikes==0,"visible parent clock expires into one child without native primary Strike");if(type==443 && scene==7)Require(!q.friendly && kills==1 && children==0,"migrating sphere fades and dies without children or damage");cases++;
            }
            Console.WriteLine("PASS ELECTRO motor="+cases+" actions="+actions);
        }
        private static void Births(object context)
        {
            int cases=0;foreach(bool lower in new[]{false,true})
            {
                var p=Reset(context);Projectile filler=null;if(lower){filler=Shot(p,1,new Vector2(200,200),Vector2.Zero,0);parent=Shot(p,442,new Vector2(950.25f,350.25f),Vector2.Zero);}else parent=Shot(p,442,new Vector2(950.25f,350.25f),Vector2.Zero);
                var nearby=Shot(p,443,parent.Center+new Vector2(20,0),Vector2.Zero);nearby.alpha=0;var far=Shot(p,443,parent.Center+new Vector2(200,0),Vector2.Zero);far.alpha=0;
                var foreign=Shot(p,443,parent.Center+new Vector2(10,0),Vector2.Zero);foreign.owner=1;Main.player[1].active=true;if(filler!=null)filler.active=false;
                var copies=new[]{(Projectile)Static(model,"Copy",nearby),(Projectile)Static(model,"Copy",far),(Projectile)Static(model,"Copy",foreign)};Static(model,"Migrate",parent,parent.Center,copies);int expected=Enumerable.Range(0,1000).First(i=>!Main.projectile[i].active);motor=parent;Vector2 origin=parent.Center;parent.Kill();
                Require(children==1 && childSlots.Single()==expected && expected!=parent.whoAmI && Main.projectile[expected].Center==origin && Main.projectile[expected].velocity==Vector2.Zero && Main.projectile[expected].damage==parent.damage && Main.projectile[expected].knockBack==0,"native Kill allocates443 before freeing442 and retains exact damage/center/source");
                Require(nearby.ai.SequenceEqual(copies[0].ai) && nearby.velocity==copies[0].velocity && nearby.ai[1]==1 && far.ai[1]==0 && far.velocity==copies[1].velocity && foreign.ai[1]==0 && foreign.velocity==copies[2].velocity,"only overlapping same-owner sphere migrates toward native Kill center; foreign sphere remains untouched");
                // Ascending pool order determines whether a newborn has an AI
                // interval on this same outer update, not its parent identity.
                for(int slot=parent.whoAmI+1;slot<1000;slot++)if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);
                Require(Main.projectile[expected].ai[0]==(expected>parent.whoAmI?1:0),"native higher-slot child advances birth outer; lower-slot child waits next outer");Main.player[1].active=false;cases++;
            }
            Console.WriteLine("PASS ELECTRO actual Kill/migration/birth-slot cases="+cases);
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int mode in Enumerable.Range(0,7))
            {
                var p=Reset(context);target=Target(4,new Vector2(mode==2?1040:980,350),mode==5?300:18);if(mode==1)target.immune[p.whoAmI]=200;NPC front=mode==2?Target(1,new Vector2(968,350),20,20):null;if(mode==3)target.reflectsProjectiles=true;
                parent=Shot(p,442,new Vector2(950,350),new Vector2(6,0));if(mode==2)target.Center=new Vector2(988,350);if(mode==4)parent.Center=new Vector2(3,350);
                // Earlier local ambient sphere is not a causal source, but
                // writes owner8 before this candidate child's same-slot pass.
                if(mode==5 || mode==6){var ambient=Shot(p,443,mode==5?new Vector2(1100,350):target.Center,Vector2.Zero);ambient.alpha=0;if(mode==6){ambient.owner=1;Main.player[1].active=true;}}
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,new[]{parent},path,Clock(path),Terrain(),Obstacles(p,parent,path));long sample=path.SampleTick;
                for(int outer=1;outer<=120 && (mode==5 || first<0);outer++)
                {
                    if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(front.whoAmI);target.UpdateNPC(target.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);
                }
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("ELECTRO receive mode="+mode+" native="+actual+" known="+known?.Tick+" childSlots="+string.Join(",",childSlots)+" hits="+string.Join(",",hits)+" primaryStrikes="+parentStrikes+" owner="+target.immune[p.whoAmI]);
                // Ambient selected Strike is not this source's first event.
                // Only compare source contribution when there is no earlier
                // local ambient; its dedicated causal proof uses child slots.
                if(mode!=5)Require((actual==0)==(known==null) && (known==null || known.Tick==actual),"source child actual firstStrike equals actual-born family Known with immunity/reflection/world gates");
                if(mode==1 || mode==3 || mode==4)Require(actual==0 && known==null,"owner200, actual parent reflection, or world retirement cannot invent sphere damage");else Require(actual>0,"native sphere receives useful damage in the positive role");
                Require(parentStrikes==0,"every parent NPC receive triggers Kill before Strike");if(mode==1)Require(children==1,"parentmax1 bypasses owner immunity, but its child must respect owner200");if(mode==3 || mode==4)Require(children==0,"reflection and world retirement are not Kill birth");if(mode==5)Require(sourceFirst<0 && known==null && Main.projectile[1].ai[1]==0,"distant ambient local sphere repeatedly writes eight-frame owner history without migration or causalContact");Main.player[1].active=false;cases++;
            }
            Console.WriteLine("PASS ELECTRO receive="+cases);
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,43,40);raw=target.Center-new Vector2(390,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0 && steps==0,"withdrawn idle Electrosphere preview never solves or fires");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);var prepared=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(prepared!=null,"actual pressed Electrosphere hand has its complete finite preparation");Call(attack,"BindPresentation",prepared);ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
            int priorSolves=solves,priorSteps=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int i=0;i<20;i++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PresentationCurrent",prepared),"sphere same-update presentation retains all actual scene leases");}Require(solves==priorSolves && steps==priorSteps && solves>0,"twenty Draws add zero sphere scene/motor work");
            natural=true;first=-1;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual Electrosphere hand consumes its prepared cursor");for(int i=0;i<40 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
            Console.WriteLine("ELECTRO natural parent="+parentBirths+" children="+children+" first="+(first<0?0:first-birthTick+1)+" known="+known?.Tick+" primaryStrikes="+parentStrikes+" childSlots="+string.Join(",",childSlots));
            Require(parentBirths==1 && children==1 && parentStrikes==0 && first>=birthTick && known!=null && first-birthTick+1==known.Tick,"real one-press/release442 Kill source has useful native443 Strike on genuine immortal488 equal to actual-born Known");natural=false;
        }
    }
}
