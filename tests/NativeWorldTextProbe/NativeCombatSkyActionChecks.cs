using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Finite, test-only observation of the real Source -> ItemCheck -> Shoot
    // chain. It never supplies a trajectory, changes the clock or grants work.
    internal static class NativeCombatSkyActionChecks
    {
        const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static object attack,host;static Player player;static NPC target;
        static int solves,launches,replays,steps,expands,required,births,enhanced;
        static int solveCount,solveAge;static string solveStop;static double enterUsed,exitUsed;
        static AttackContact preparedContact;static int preparedDirection;static bool movingPlayer,displace;static int facingChanges,displacements;
        static void MovementTokens(IList<string> __0){if(movingPlayer)__0.Add("D");}
        static void Displace(Player __0){if(displace && ReferenceEquals(__0,player) && preparedContact!=null){player.position.X+=16;displacements++;}}
        static readonly List<string> shots=new List<string>();
        static void Solve(object[] __args)
        {solves++;var path=(NpcTrajectory)__args[3];solveCount=path.Count;solveAge=(int)__args[5];solveStop=path.Stop.ToString();preparedDirection=player.direction;preparedContact=null;enterUsed=((CombatPredictionWork)Get(host,"PredictionWork")).UsedMilliseconds;}
        static void Solved(AttackContact __result){preparedContact=__result;exitUsed=((CombatPredictionWork)Get(host,"PredictionWork")).UsedMilliseconds;}
        static void Launch(){launches++;}
        static void Replay(){replays++;}
        static void Advance(){steps++;}
        static void Expand(){expands++;}
        static void Preferred(int __result){required=__result;}
        static void Born(int __result)
        {
            if(player==null || __result<0 || __result>=Main.maxProjectiles)return;
            var q=Main.projectile[__result];if(q.owner!=player.whoAmI || q.type!=645)return;
            births++;var input=Get(attack,"consumedInput");string kind=Get(input,"Kind").ToString();if(kind=="Enhanced")enhanced++;
            var point=(Vector2)Get(input,"Point");var path=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);
            var contact=preparedContact;var work=(CombatPredictionWork)Get(host,"PredictionWork");
            if(kind=="Enhanced" && preparedDirection!=player.direction)facingChanges++;
            if(kind=="Enhanced" && Environment.GetEnvironmentVariable("JUEMINGR_SKY_CANDIDATE")==null)
            {
                Require(contact!=null && preparedDirection==player.direction,"actual lunar Shoot consumes a Contact from the final facing");
                var nativeAim=new Vector2(Main.mouseX+Main.screenPosition.X,Main.mouseY+Main.screenPosition.Y);
                Require(nativeAim==point,"enhanced sky Contact and actual integer raw cursor are identical");
                var delta=nativeAim-q.Center;delta.Y=Math.Max(20,Math.Abs(delta.Y));var expected=delta.SafeNormalize(Vector2.UnitY)*5;
                Require(Vector2.Distance(q.velocity,expected)<.002f && q.Center.Y==player.MountedCenter.Y-600-(births-1)%3*100,"actual simultaneous lunar birth/velocity uses the final cursor without player-velocity duplication");
            }
            shots.Add("SKY-ACTION tick="+Main.GameUpdateCount+" phase="+Get(input,"Phase")+" shot="+births+" weapon="+player.HeldItem.type+" speed="+q.velocity.Length()+" target="+target.Center+" targetVelocity="+target.velocity+" input="+kind+" aim="+point+" contact="+(contact==null?"none":contact.Tick+"/"+contact.Subupdate)+" sample="+(path==null?"none":path.SampleTick+"/"+path.Count+"/"+path.Stop)+" firstDemand="+required+" expands="+expands+" solve="+solves+" solveCount="+solveCount+" age="+solveAge+" stop="+solveStop+" launch="+launches+" replay="+replays+" steps="+steps+" used="+work.UsedMilliseconds+" solveUsed="+enterUsed+"->"+exitUsed+" exceeded="+work.Exceeded+" direction="+player.direction+" mounted="+player.MountedCenter+" birth="+q.Center+" velocity="+q.velocity);
        }
        internal static void Run(object context)
        {
            attack=Get(Get(context,"Combat"),"Attack");host=Get(context,"CombatObservation");var a=attack.GetType().Assembly;var audit=new Harmony("JueMingR.Tests.SkyAction20261024");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            void Observe(Type t,string name,string prefix,string postfix=null){audit.Patch(t.GetMethod(name,F),prefix:prefix==null?null:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),prefix),postfix:postfix==null?null:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),postfix));}
            Observe(a.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true),"Solve",nameof(Solve),nameof(Solved));
            Observe(a.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true),"Launch",nameof(Launch));
            Observe(typeof(AttackIntercept),"Replay",nameof(Replay));Observe(typeof(AttackMotion),"Advance",nameof(Advance));
            var lunar=a.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true).GetMethod("LunarReplay",F);if(lunar!=null)audit.Patch(lunar,prefix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Replay)));
            Observe(host.GetType(),"ExpandActionPrediction",nameof(Expand));
            audit.Patch(attack.GetType().GetMethod("ActionPreferred",F,null,new[]{typeof(NpcIdentity)},null),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Preferred)));
            audit.Patch(typeof(Projectile).GetMethods(F).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Born)));
            audit.Patch(Get(context,"Input").GetType().GetMethod("AfterNativeMouse",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(MovementTokens)));
            audit.Patch(a.GetType("JueMingR.TerrariaHost.Combat.CombatFacing",true).GetMethod("Apply",F),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Displace)));
            try
            {
                if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
                foreach(int direction in new[]{1,-1,0,2,3,4})Scene(context,direction);
                OffCosts(context);
            }
            finally{player=null;movingPlayer=displace=false;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        static void Scene(object context,int direction)
        {
            var combat=Get(context,"Combat");NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3570,0,0);foreach(var armor in player.armor)armor.TurnToAir();player.ResetEffects();player.position=new Vector2(700,646);player.releaseUseItem=true;player.statMana=player.statManaMax=player.statManaMax2=10000;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=51;x<115;x++)Main.tile[x,43].ClearEverything();
            target=Main.npc[2];target.SetDefaults(NPCID.Zombie);target.whoAmI=2;target.active=true;target.width=target.height=34;target.Center=player.Center+new Vector2(350,direction==2?-40:30);target.velocity=new Vector2(direction==0?0:direction<0?-2:2,direction==2?.4f:0);target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.target=0;target.life=target.lifeMax=100000;target.knockBackResist=0;
            Main.screenPosition=new Vector2(480.25f,400.75f);Main.GameViewMatrix.Zoom=Vector2.One;Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
            movingPlayer=direction==0;displace=direction==4;displacements=0;facingChanges=0;if(direction==3){player.direction=-1;NativeCombatCadenceChecks.Save(combat,new CombatOptions(32));}
            births=enhanced=0;shots.Clear();float playerStart=player.position.X;
            for(int i=0;i<75;i++)
            {
                solves=launches=replays=steps=expands=required=0;solveCount=solveAge=0;enterUsed=exitUsed=0;preparedContact=null;
                NativeCombatBalancedAimChecks.StepAt(context,player.Center+new Vector2(350,30),i>=20);
            }
            foreach(var shot in shots)Console.WriteLine(shot);
            Console.WriteLine("SKY-ACTION SUMMARY direction="+direction+" births="+births+" enhanced="+enhanced+" damage="+(100000-target.life)+" playerDelta="+(player.position.X-playerStart)+" staleFacing="+facingChanges);
            Require(births>0,"normal Source path actually enters original lunar Shoot");
            if(movingPlayer)Require(player.position.X>playerStart,"physical mapped movement actually advances the player");
            if(Environment.GetEnvironmentVariable("JUEMINGR_SKY_CANDIDATE")==null)
            {
                if(displace)Require(displacements>0 && enhanced==0,"real post-prepare displacement cannot reuse a sky Contact");
                else Require(enhanced>0 && facingChanges==0,"steady native Source produces useful enhanced sky input with final birth dependencies");
            }
            movingPlayer=displace=false;
        }
        static void OffCosts(object context)
        {
            foreach(bool noTarget in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3570,0,0);
                foreach(var npc in Main.npc)npc.active=false;
                if(noTarget)NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
                solves=launches=replays=steps=0;
                for(int i=0;i<4;i++)NativeCombatBalancedAimChecks.StepAt(context,player.Center+new Vector2(300,0),true);
                Require(solves==0 && launches==0 && replays==0,"AimOff/no-target never enters sky proposal or replay work");
                Console.WriteLine("SKY-OFF noTarget="+noTarget+" solve="+solves+" launch="+launches+" replay="+replays);
            }
        }
    }
}
