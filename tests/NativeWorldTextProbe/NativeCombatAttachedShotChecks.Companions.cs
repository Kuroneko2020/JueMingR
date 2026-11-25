using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAttachedShotChecks
    {
        private sealed class HandReceipt
        {
            internal Player Player;
            internal object Actor;
            internal int Births,Representative,Hits,Attempts,Effective,FirstLead=-1;
            internal readonly HashSet<int> BornWithLead=new HashSet<int>();
            internal int LeadHits;
        }

        private static HandReceipt[] team;
        private static object localAttack;
        private static NPC movingReceiver;
        private static int teamFrame,wallHandStarts;
        private static bool wallScene;

        private static void TeamHandStarted(Player __instance)
        {
            if(!wallScene || !team.Skip(1).Any(r=>ReferenceEquals(r.Player,__instance)))return;
            wallHandStarts++;
            Console.WriteLine("ATTACHED team wall new hand role="+__instance.whoAmI+" step="+teamFrame+" use="+__instance.controlUseItem);
        }

        private static void TeamBorn(IEntitySource __0,int __result)
        {
            if(__result<0 || __result>=Main.maxProjectiles || !(__0 is EntitySource_ItemUse))return;
            var shot=Main.projectile[__result];if(shot.type!=636)return;
            var row=team.FirstOrDefault(r=>r.Player.whoAmI==shot.owner);if(row==null)return;
            Require(shot.owner==Main.myPlayer && shot.damage>0 && shot.MaxUpdates==2,"Daybreak birth belongs to the actual native hand and original subupdate count");
            row.Births++;
            var point=row.Actor==null?(Vector2)Get(Get(localAttack,"consumedInput"),"Point"):(Vector2)Get(row.Actor,"Aim");
            bool lead=row.Actor==null
                ? (bool)Get(localAttack,"hasConsumedInput") && (uint)Get(localAttack,"consumedInputStep")==Main.GameUpdateCount && Get(Get(localAttack,"consumedInput"),"Kind").ToString()=="Enhanced"
                : (bool)Get(row.Actor,"HasAim") && Vector2.DistanceSquared(point,movingReceiver.Center)>500*500;
            if(wallScene)Console.WriteLine("ATTACHED team wall birth role="+shot.owner+" step="+teamFrame+" center="+shot.Center+" velocity="+shot.velocity+" aim="+point+" target="+movingReceiver.Center+
                " use="+row.Player.controlUseItem+" animation="+row.Player.itemAnimation+"/"+row.Player.itemAnimationMax);
            if(lead)
            {
                if(row.FirstLead<0)row.FirstLead=teamFrame;
                row.Representative++;row.BornWithLead.Add((int)shot.key);
                Console.WriteLine("ATTACHED team lead role="+shot.owner+" step="+teamFrame+" point="+point+" center="+shot.Center+" velocity="+shot.velocity);
            }
        }

        private static void TeamStrike(NPC __instance,int __result)
        {
            if(!ReferenceEquals(__instance,movingReceiver) || __result<=0 || receiving?.type!=636)return;
            var row=team.FirstOrDefault(r=>r.Player.whoAmI==receiving.owner);if(row==null)return;
            row.Hits++;if(row.BornWithLead.Contains((int)receiving.key))row.LeadHits++;
        }

        internal static void CompanionOrdinary(object context,object host,Player first,Guid other)
        {
            Require((bool)Call(host,"Join",other),"join the second original companion body for the shared aim scope");
            var second=(Player)Call(host,"GetBody",other);var human=Main.LocalPlayer;
            team=new[]{new HandReceipt{Player=human},new HandReceipt{Player=first,Actor=Call(host,"Owned",first)},new HandReceipt{Player=second,Actor=Call(host,"Owned",second)}};
            var firstProfile=(CompanionProfile)Get(team[1].Actor,"Profile");
            var original=team.Select(r=>r.Player.inventory[0]).ToArray();var positions=team.Select(r=>r.Player.position).ToArray();
            var combat=Get(context,"Combat");var observation=Get(context,"CombatObservation");var input=Get(context,"Input");
            localAttack=Get(combat,"Attack");var humanWork=(CombatPredictionWork)Get(observation,"PredictionWork");var actorWork=(CombatPredictionWork)Get(host,"work");
            Require(!ReferenceEquals(humanWork,actorWork) && (long)Get(humanWork,"allowance")==Stopwatch.Frequency*6/1000 &&
                (long)Get(actorWork,"allowance")==Stopwatch.Frequency*2/1000,"actual 6ms player owner and one actual 2ms team owner");
            bool wind=Main.windPhysics;int xWall=59;var savedWall=new Tile[57];
            for(int y=3;y<60;y++)savedWall[y-3]=Main.tile[xWall,y];
            var audit=new Harmony("JueMingR.Tests.CompanionAttachedOrdinary");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(TeamHandStarted)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(TeamBorn)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttachedShotChecks),nameof(TeamStrike)));
            try
            {
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,false,false,false,false,50,true,true));
                Require((int)Get(Get(observation,"Experiments"),"EffectiveMask")==0,"hard lock, fast fire and through-wall are disabled");
                Main.windPhysics=false;Main.screenPosition=new Vector2(160,540);Main.GameViewMatrix.Zoom=Vector2.One;
                foreach(var n in Main.npc)n.active=false;
                foreach(var q in Main.projectile)q.active=false;
                for(int i=0;i<team.Length;i++)
                {
                    var row=team[i];var p=row.Player;p.inventory[0]=new Item(ItemID.DayBreak);p.selectedItemState.Select(0);p.selectedItemState.Update();
                    p.position=new Vector2(i==1?490:i==2?545:650,960-p.height);p.velocity=Vector2.Zero;p.ResetFallDamage();p.releaseUseItem=true;
                    if(row.Actor!=null)Call(host,"SetBehavior",((CompanionProfile)Get(row.Actor,"Profile")).Id,new CompanionBehavior{Follow=true,AttackEnabled=true,MainWeapons=new[]{new CompanionWeaponPreference(ItemID.DayBreak,0,0)}});
                }
                movingReceiver=Target(4,new Vector2(1160,875),34,34);movingReceiver.velocity=new Vector2(-1,-.15f);
                var playerCost=new List<double>();var teamCost=new List<double>();int playerBudget=0,teamBudget=0;
                float oldX=human.position.X,oldActorX=first.position.X;int movingAttackFrames=0;
                for(teamFrame=0;teamFrame<156;teamFrame++)
                {
                    float before=human.position.X;
                    Step(teamFrame>=4,teamFrame<49?1:teamFrame<86?-1:0);
                    if(human.position.X!=before && (first.controlUseItem || second.controlUseItem))movingAttackFrames++;
                    playerCost.Add(humanWork.UsedMilliseconds);teamCost.Add(actorWork.UsedMilliseconds);
                    if(humanWork.Exceeded)playerBudget++;if(actorWork.Exceeded)teamBudget++;
                }
                playerCost.Sort();teamCost.Sort();
                foreach(var row in team)Console.WriteLine("ATTACHED native team role="+row.Player.whoAmI+" attempts="+row.Attempts+" effectiveTicks="+row.Effective+
                    " births="+row.Births+" leadBirths="+row.Representative+" hits="+row.Hits+" leadHits="+row.LeadHits+" firstLead="+row.FirstLead);
                Console.WriteLine("ATTACHED team budgets player6ms p95="+playerCost[148]+" exceeded="+playerBudget+"/156 actorShared2ms p95="+teamCost[148]+" exceeded="+teamBudget+
                    "/156 ownerDx="+(human.position.X-oldX)+" firstActorDx="+(first.position.X-oldActorX)+" movingAttackFrames="+movingAttackFrames);
                Require(team.Skip(1).All(r=>r.Attempts>0 && r.Effective>0) && movingAttackFrames>0 && Math.Abs(first.position.X-oldActorX)>16,"both roles receive real shared-budget attempts while following and attacking");
                Require(team.All(r=>r.Births>0 && r.Representative>0 && r.LeadHits>0),"human and both real companion hands produce and hit with Daybreak representative input");

                // Let all three hands release on native input and finish any
                // already admitted animation/tail before placing the wall.
                var daybreak=new[]{new CompanionWeaponPreference(ItemID.DayBreak,0,0)};
                int beforeRelease=team.Sum(r=>r.Births);
                foreach(var row in team.Skip(1))
                    Require((bool)Call(host,"SetBehavior",((CompanionProfile)Get(row.Actor,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=false,MainWeapons=daybreak}),
                        "both native actor attack settings actually disable before draining the hands");
                int settle=0;
                for(;settle<36;settle++)
                {
                    Step(false,0);
                    if(team.All(r=>r.Player.itemAnimation==0 && r.Player.itemTime==0 && !r.Player.controlUseItem))break;
                }
                Require(settle<36,"all three real Daybreak hands finish animation and delayed births on release");
                Console.WriteLine("ATTACHED team released before wall updates="+(settle+1)+" nativeTailBirths="+(team.Sum(r=>r.Births)-beforeRelease));
                foreach(var row in team.Skip(1))
                {
                    Require((bool)Call(host,"SetBehavior",((CompanionProfile)Get(row.Actor,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true,MainWeapons=daybreak}),
                        "both native actor attack settings actually reenable before the wall test");
                    row.Player.position=new Vector2(650,960-row.Player.height);row.Player.velocity=Vector2.Zero;
                }
                movingReceiver.Center=new Vector2(1130,880);movingReceiver.velocity=Vector2.Zero;
                for(int y=3;y<60;y++){Main.tile[xWall,y]=new Tile();NativeToolsChecks.Tile(xWall,y,TileID.Stone);}
                foreach(var row in team.Skip(1))
                {
                    var origin=row.Player.Center;
                    Require(!Collision.CanHitLine(origin,0,0,movingReceiver.Center,0,0) && !Collision.CanHit(origin,0,0,movingReceiver.Center,0,0),
                        "independent native surface checks both reject the wall-separated scene");
                }
                int beforeWall=team.Skip(1).Sum(r=>r.Births);
                wallHandStarts=0;wallScene=true;for(teamFrame=0;teamFrame<36;teamFrame++)Step(false,0);
                Console.WriteLine("ATTACHED team wall newStarts="+wallHandStarts+" births="+(team.Skip(1).Sum(r=>r.Births)-beforeWall));
                Require(wallHandStarts==0 && team.Skip(1).Sum(r=>r.Births)==beforeWall,"both actual actor hands withhold new Daybreak starts and births behind continuous native blocking terrain");
                Console.WriteLine("ATTACHED team wall newBirths=0 over 36 actual actor updates");
            }
            finally
            {
                audit.UnpatchAll(audit.Id);receiving=null;wallScene=false;if(movingReceiver!=null)movingReceiver.active=false;movingReceiver=null;localAttack=null;
                foreach(var q in Main.projectile)if(team.Any(r=>q.owner==r.Player.whoAmI))q.active=false;
                for(int y=3;y<60;y++)Main.tile[xWall,y]=savedWall[y-3];
                for(int i=0;i<team.Length;i++){team[i].Player.inventory[0]=original[i];team[i].Player.position=positions[i];}
                NativeCombatObservationChecks.Save(observation,new ObservationOptions());Main.windPhysics=wind;
                Call(host,"SetBehavior",firstProfile.Id,firstProfile.Behavior);Call(host,"Recall",other);
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(other);});
                team=null;
            }

            void Step(bool fire,int movement)
            {
                NativeToolExecutionChecks.Sample(context,input,human.Center-new Vector2(180,100),fire,movement);Call(combat,"Sample");Call(observation,"SampleMouse");
                int x=Main.mouseX,y=Main.mouseY;
                NativeQuickItemChecks.BeginWorldStep();human.Update(human.whoAmI);int attempts=0;
                foreach(var row in team.Skip(1).OrderBy(r=>r.Player.whoAmI))
                {
                    long before=(long)Get(row.Actor,"LastAimAttempt");row.Player.Update(row.Player.whoAmI);
                    if((long)Get(row.Actor,"LastAimAttempt")!=before){row.Attempts++;attempts++;}
                    if((bool)Get(row.Actor,"HasAim"))row.Effective++;
                    Require(!(bool)Get(row.Actor,"Faulted"),"native companion remains healthy in ordinary aim: "+Get(row.Actor,"Status"));
                }
                Require(attempts<=1,"both companions share one actual capture turn in a world update");
                movingReceiver.UpdateNPC(movingReceiver.whoAmI);Call(host,"AdvanceNpcObservation");
                if(Main.instance.SpelunkerProjectileHelper==null)Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
                if(Main.instance.ChumBucketProjectileHelper==null)Main.instance.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
                typeof(Main).GetMethod("UpdateWorld_Projectiles",Flags).Invoke(Main.instance,null);
                Call(context,"UpdateRuntime");Call(context,"UpdateShell");
                Require(Main.mouseX==x && Main.mouseY==y && Main.myPlayer==human.whoAmI,"actual human and companion input scopes restore physical cursor and owner");
            }
        }
    }
}
