using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatScytheChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC target;private static int currentType,expectedType;private static long firstHit;
        private static void BeforeDamage(Projectile __instance){currentType=__instance.type;}
        private static void AfterDamage(){currentType=0;}
        private static void Strike(NPC __instance,int __result)
        {if(currentType==expectedType && ReferenceEquals(target,__instance) && __result>0 && firstHit<0)firstHit=Main.GameUpdateCount;}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");bool wind=Main.windPhysics;
            var audit=new Harmony("JueMingR.Tests.Scythes");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatScytheChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatScytheChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatScytheChecks),nameof(Strike)));
            try{Motor(context,combat.GetType().Assembly);FiniteEdges(context,combat.GetType().Assembly);Natural(context,host,combat);}
            finally{target=null;currentType=0;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void Motor(object context,Assembly assembly)
        {
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostFiniteShotAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            int cases=0,actions=0,retired=0,signed=0;
            foreach(int type in new[]{45,263,274})foreach(int clock in type==45?new[]{0,29,99,200}:new[]{0})foreach(int scene in Enumerable.Range(-1,10))
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),type==45?272:type==263?1306:1327,0,0);Main.windPhysics=false;foreach(var n in Main.npc)n.active=false;
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(scene>=0 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene);}
                if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(65,y,1);
                if(scene>=5){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope((byte)(scene-4));}
                var center=scene>=5?new Vector2(936,390):new Vector2(950,350);var velocity=scene>=5?new Vector2(24,8):type==45?new Vector2(.25f,.025f):new Vector2(12,.2f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,0,0,p.whoAmI,clock);var q=Main.projectile[slot];Vector2 anchor;int width,height;q.GetCollisionParams(out anchor,out width,out height);
                if(type==263){Require(width== -2 && height== -2,"Original IceSickle movement really uses signed -2 dimensions");signed++;}
                var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int outer=1;outer<=(type==45?110:185) && q.active;outer++)
                {
                    for(int sub=0;sub<=q.extraUpdates && (bool)Get(state,"Alive");sub++)
                    {var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"scythe motor keeps known terrain: "+type+"/"+clock+"/"+scene+"/"+outer);state=args[2];var finish=new object[]{state};model.GetMethod("Finish",Flags).Invoke(null,finish);state=finish[0];actions++;}
                    q.Update(slot);bool alive=(bool)Get(state,"Alive");
                    Require(q.active==alive && q.ai[0]==(float)Get(state,"Counter") && q.timeLeft==(int)Get(state,"Left") && (!alive || Vector2.DistanceSquared(q.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.wet==(bool)Get(state,"Wet") && q.shimmerWet==(bool)Get(state,"Shimmer")),"original scythe movement/clock/lifetime/liquids: "+type+"/"+clock+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.ai[0]+"/"+q.timeLeft+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Counter")+"/"+Get(state,"Left"));
                }
                if(!q.active)retired++;cases++;
            }
            Require(cases==60 && signed==10 && retired>0,"all three hand roles, four acceleration clocks, actual negative shape and retirements were exercised");
            Console.WriteLine("PASS original AI018 motor cases="+cases+" actions="+actions+" retired="+retired+" signed="+signed);
        }
        private static void FiniteEdges(object context,Assembly assembly)
        {
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostFiniteShotAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            float left=Main.leftWorld,top=Main.topWorld,right=Main.rightWorld,bottom=Main.bottomWorld;int cases=0,retired=0,actions=0;
            try
            {
                foreach(int type in new[]{45,263,274,118,119,359})foreach(int side in Enumerable.Range(0,4))foreach(int liquid in new[]{-1,0,2})
                {
                    var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),272,0,0);Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;
                    Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
                    for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)
                    {Main.tile[x,y].ClearEverything();if(liquid>=0 && (x<4 || y<4 || x>=Main.maxTilesX-4 || y>=Main.maxTilesY-4)){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}}
                    int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(500,500),Vector2.Zero,type,0,0,player.whoAmI);var shot=Main.projectile[slot];
                    shot.position=new Vector2(side==0?1:side==1?Main.rightWorld-shot.width-1:500,side==2?1:side==3?Main.bottomWorld-shot.height-1:500);
                    shot.velocity=side<2?new Vector2(side==0?-8:8,.25f):new Vector2(.25f,side==2?-8:8);
                    var state=Activator.CreateInstance(stateType,Flags,null,new object[]{shot,shot.Center,shot.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    for(int outer=0;outer<12 && shot.active;outer++)
                    {
                        int updates=shot.extraUpdates+1;
                        for(int sub=0;sub<updates && (bool)Get(state,"Alive");sub++)
                        {var args=new object[]{shot,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"finite original clipped border is known: "+type+"/"+side+"/"+liquid);state=args[2];var tail=new object[]{state};model.GetMethod("Finish",Flags).Invoke(null,tail);state=tail[0];actions++;}
                        shot.Update(slot);bool alive=(bool)Get(state,"Alive");
                        Require(shot.active==alive && shot.timeLeft==(int)Get(state,"Left") && shot.ai[0]==(float)Get(state,"Counter") && (!alive || Vector2.DistanceSquared(shot.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(shot.velocity,(Vector2)Get(state,"Velocity"))<.0001f),"finite native border retirement without Kill: type="+type+" side="+side+" liquid="+liquid+" outer="+outer+" native="+shot.position+"/"+shot.velocity+"/"+shot.timeLeft+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Left"));
                    }
                    if(!shot.active)retired++;cases++;
                }
                Require(cases==72 && retired>0,"all six finite hand shots exercise clipped scans and actual world retirement");
                Console.WriteLine("PASS six-role original border cases="+cases+" actions="+actions+" retired="+retired);
            }
            finally{Main.leftWorld=left;Main.topWorld=top;Main.rightWorld=right;Main.bottomWorld=bottom;}
        }
        private static void Natural(object context,object host,object combat)
        {
            var input=Get(context,"Input");var attack=Get(combat,"Attack");var failures=new List<string>();int cases=0,hits=0,negative=0;
            foreach(var row in new[]{new[]{272,45},new[]{1306,263},new[]{1327,274}})foreach(int scene in Enumerable.Range(0,3))
            {
                try
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(650,646);p.ResetEffects();p.ResetFallDamage();p.statMana=p.statManaMax=p.statManaMax2=200;p.releaseUseItem=true;Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);
                    target=NativeCombatControlPreviewChecks.Dummy(2,50,40);firstHit=-1;expectedType=row[1];
                    if(scene==1)for(int y=2;y<43;y++)NativeToolsChecks.Tile(46,y,1);
                    if(scene==2){if(row[1]==45)target.immune[p.whoAmI]=200;else Projectile.perIDStaticNPCImmunity[Terraria.ID.ContentSamples.ProjectilesByType[row[1]].immunityIdentity,2]=Main.GameUpdateCount+200;}
                    bool blocked=scene==2 || scene==1 && row[1]!=274;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));AttackContact preview=null;
                    for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(!Main.projectile.Any(q=>q.active),"scythe idle path observation never launches");}
                    Require(blocked?preview==null:preview!=null,"scythe held preview respects actual wall/receiver rules: "+row[0]+"/"+scene);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));long start=Main.GameUpdateCount;bool born=false;
                    for(int frame=1;frame<=120 && firstHit<0;frame++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,frame==1);born|=Main.projectile.Any(q=>q.active && q.type==row[1] && q.owner==p.whoAmI);}
                    Require(born,"scythe normal manual birth is preserved even without an aim solution");
                    Require(blocked?firstHit<0:firstHit-start==preview.Tick,"scythe original first projectile Damage agrees with idle preview: "+row[0]+"/"+scene+" native="+(firstHit<0?-1:firstHit-start)+" model="+preview?.Tick);
                    if(blocked)negative++;else hits++;cases++;Console.WriteLine("SCYTHE NATURAL item="+row[0]+" scene="+scene+" native="+(firstHit<0?-1:firstHit-start)+" model="+preview?.Tick);
                }
                catch(Exception error){failures.Add(row[0]+"/"+scene+": "+error.Message);Console.WriteLine("SCYTHE NATURAL FAIL "+error);}
            }
            Require(failures.Count==0,"natural scythe failures="+string.Join(" | ",failures));Require(cases==9 && hits==4 && negative==5,"scythe full original first-use and wall/immune negative controls");
            Console.WriteLine("PASS original scythe first-use cases="+cases+" hits="+hits+" negative="+negative);
        }
    }
}
