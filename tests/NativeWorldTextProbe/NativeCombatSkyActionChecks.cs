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
        static AttackContact preparedContact;static bool preparedPoint;static int preparedDirection;static bool movingPlayer,displace;static int facingChanges,displacements;
        static bool dummy,observingChannels;static int geometricBirthLines,mainDamageEvents;static readonly List<float> crossings=new List<float>();
        sealed class Channel
        {
            internal ProjectileKey Key;internal int Number,AiCalls,NearSamples;internal uint BornTick,FirstAiTick,LastTick;
            internal Vector2 Birth,Last,NearFirst,NearLast;internal bool NearSeen,NearComplete,BodyOverlap,Retired;
            internal string Kind,Retirement;
        }
        static readonly Dictionary<ProjectileKey,Channel> primary=new Dictionary<ProjectileKey,Channel>();
        static readonly List<Channel> channels=new List<Channel>();static Projectile damageSource;static int nativeStrikeDamage;
        static bool InsideWorld(Projectile q)
        {return q.position.X>Main.leftWorld && q.position.X+q.width<Main.rightWorld && q.position.Y>Main.topWorld && q.position.Y+q.height<Main.bottomWorld;}
        static void Ai(Projectile __instance)
        {
            Channel channel;if(!observingChannels || player==null || !primary.TryGetValue(__instance.key,out channel))return;
            if(channel.AiCalls++==0)channel.FirstAiTick=Main.GameUpdateCount;
        }
        // Observe real living positions at native Damage/Update outlets. A
        // birth ray alone is never counted as an actual approach opportunity.
        static void ObserveChannel(Projectile q)
        {
            Channel channel;if(!observingChannels || player==null || !primary.TryGetValue(q.key,out channel))return;
            channel.Last=q.Center;channel.LastTick=Main.GameUpdateCount;
            if(q.active && dummy && q.width==10 && q.height==10)
            {
                var body=q.Hitbox;var receiver=target.Hitbox;
                if(body.Bottom>receiver.Top && body.Top<receiver.Bottom)
                {
                    if(!channel.NearSeen)channel.NearFirst=q.Center;
                    channel.NearSeen=true;channel.NearSamples++;channel.NearLast=q.Center;
                    channel.BodyOverlap|=body.Intersects(receiver);
                }
                if(channel.NearSeen && body.Top>=receiver.Bottom)channel.NearComplete=true;
            }
            // Native direct OnHit freezes/expands the NEXT AI. That channel
            // already had its actual small-body opportunity; no fictitious
            // continuation through the receiver is needed to finish it.
            if(channel.NearSeen && q.width==140)channel.NearComplete=true;
            if(!q.active && !channel.Retired)
            {
                channel.Retired=true;
                channel.Retirement=!InsideWorld(q)?"native-world-boundary":q.ai[1]==-1 && q.ai[0]>=Main.projFrames[645]*q.MaxUpdates*3?"lunar-ai-threshold":q.timeLeft<=0?"native-timeLeft":q.penetrate==0?"native-penetration":"native-other";
            }
        }
        static void Updated(Projectile __instance){ObserveChannel(__instance);}
        static void MovementTokens(IList<string> __0){if(movingPlayer)__0.Add("D");}
        static void Displace(Player __0){if(displace && ReferenceEquals(__0,player) && preparedPoint){player.position.X+=16;displacements++;}}
        static readonly List<string> shots=new List<string>();
        static void Solve(object[] __args)
        {solves++;var path=(NpcTrajectory)__args[3];solveCount=path.Count;solveAge=(int)__args[5];solveStop=path.Stop.ToString();preparedDirection=player.direction;preparedContact=null;preparedPoint=false;enterUsed=((CombatPredictionWork)Get(host,"PredictionWork")).UsedMilliseconds;}
        static void Solved(object __result){preparedContact=__result as AttackContact;if(__result!=null && !(__result is AttackContact)){preparedContact=(AttackContact)GetOptional(__result,"Contact");preparedPoint=(bool)Get(__result,"HasPoint");}else preparedPoint=preparedContact!=null;exitUsed=((CombatPredictionWork)Get(host,"PredictionWork")).UsedMilliseconds;}
        static void Damaging(Projectile __instance,out Projectile __state){__state=damageSource;damageSource=__instance;ObserveChannel(__instance);}
        static void Damaged(Projectile __state){damageSource=__state;}
        static void Hit(NPC __instance,int __result)
        {
            if(!ReferenceEquals(__instance,target) || __result<=0 || damageSource==null || !primary.ContainsKey(damageSource.key))return;
            mainDamageEvents++;nativeStrikeDamage+=__result;
            shots.Add("SKY-DAMAGE tick="+Main.GameUpdateCount+" victim="+target.whoAmI+"/"+target.type+" projectile="+damageSource.type+"/"+damageSource.key+" nativeStrikeDamage="+__result+" ai="+damageSource.ai[0]+"/"+damageSource.ai[1]+" body="+damageSource.Hitbox);
        }
        static void Launch(){launches++;}
        static void Replay(){replays++;}
        static void Advance(){steps++;}
        static void Expand(){expands++;}
        static void Preferred(int __result){required=__result;}
        static void Born(int __result)
        {
            if(!observingChannels || player==null || __result<0 || __result>=Main.maxProjectiles)return;
            var q=Main.projectile[__result];if(q.owner!=player.whoAmI || q.type!=645)return;
            births++;var input=Get(attack,"consumedInput");string kind=Get(input,"Kind").ToString();if(kind=="Enhanced")enhanced++;
            var channel=new Channel{Key=q.key,Number=births,Kind=kind,BornTick=Main.GameUpdateCount,Birth=q.Center,Last=q.Center,LastTick=Main.GameUpdateCount};primary.Add(q.key,channel);channels.Add(channel);
            Require(InsideWorld(q),"each evaluated original sky birth starts inside the unchanged native world boundary");
            var point=(Vector2)Get(input,"Point");var path=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);
            var contact=preparedContact;var work=(CombatPredictionWork)Get(host,"PredictionWork");
            if(kind=="Enhanced" && preparedDirection!=player.direction)facingChanges++;
            if(kind=="Enhanced" && Environment.GetEnvironmentVariable("JUEMINGR_SKY_CANDIDATE")==null)
            {
                Require(preparedPoint && preparedDirection==player.direction,"actual lunar Shoot consumes a prepared point from the final facing; Contact is optional");
                Require(Get(input,"PointSemantics").ToString()=="WorldPoint","sky semantics reach the actual raw-input consumer");
                var nativeAim=new Vector2(Main.mouseX+Main.screenPosition.X,Main.mouseY+Main.screenPosition.Y);
                Require(nativeAim==point,"enhanced sky Contact and actual integer raw cursor are identical");
                var delta=nativeAim-q.Center;delta.Y=Math.Max(20,Math.Abs(delta.Y));var expected=delta.SafeNormalize(Vector2.UnitY)*5;
                Require(Vector2.Distance(q.velocity,expected)<.002f && q.Center.Y==player.MountedCenter.Y-600-(births-1)%3*100,"actual simultaneous lunar birth/velocity uses the final cursor without player-velocity duplication");
                Require(path!=null && Enumerable.Range(0,path.Count).Any(i=>{var b=path[i].ProjectileReceiveBounds;return Math.Abs(point.X-b.CenterX)<1.01f && Math.Abs(point.Y-b.CenterY)<1.01f;}),"no-gravity lunar command is a quantized same-time published target centre, never a far ray point");
            }
            if(dummy && q.velocity.Y>0)
            {
                float cross=q.Center.X+(target.Center.Y-q.Center.Y)/q.velocity.Y*q.velocity.X;
                crossings.Add(cross);if(Math.Abs(cross-target.Center.X)<=target.width*.5f+q.width*.5f+1)geometricBirthLines++;
            }
            shots.Add("SKY-ACTION tick="+Main.GameUpdateCount+" phase="+Get(input,"Phase")+" shot="+births+" weapon="+player.HeldItem.type+" speed="+q.velocity.Length()+" target="+target.Center+" targetVelocity="+target.velocity+" input="+kind+" aim="+point+" contact="+(contact==null?"none":contact.Tick+"/"+contact.Subupdate)+" sample="+(path==null?"none":path.SampleTick+"/"+path.Count+"/"+path.Stop)+" firstDemand="+required+" expands="+expands+" solve="+solves+" solveCount="+solveCount+" age="+solveAge+" stop="+solveStop+" launch="+launches+" replay="+replays+" steps="+steps+" used="+work.UsedMilliseconds+" solveUsed="+enterUsed+"->"+exitUsed+" exceeded="+work.Exceeded+" direction="+player.direction+" mounted="+player.MountedCenter+" birth="+q.Center+" velocity="+q.velocity);
        }
        internal static void Run(object context)
        {
            AppDomain.MonitoringIsEnabled=true;
            attack=Get(Get(context,"Combat"),"Attack");host=Get(context,"CombatObservation");var a=attack.GetType().Assembly;var audit=new Harmony("JueMingR.Tests.SkyAction20261024");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            void Observe(Type t,string name,string prefix,string postfix=null){audit.Patch(t.GetMethod(name,F),prefix:prefix==null?null:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),prefix),postfix:postfix==null?null:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),postfix));}
            var sky=a.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true);
            Observe(sky,sky.GetMethod("Prepare",F)==null?"Solve":"Prepare",nameof(Solve),nameof(Solved));
            Observe(a.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true),"Launch",nameof(Launch));
            Observe(typeof(AttackIntercept),"Replay",nameof(Replay));Observe(typeof(AttackMotion),"Advance",nameof(Advance));
            var lunar=a.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true).GetMethod("LunarReplay",F);if(lunar!=null)audit.Patch(lunar,prefix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Replay)));
            Observe(host.GetType(),"ExpandActionPrediction",nameof(Expand));
            audit.Patch(attack.GetType().GetMethod("ActionPreferred",F,null,new[]{typeof(NpcIdentity)},null),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Preferred)));
            audit.Patch(typeof(Projectile).GetMethods(F).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Damaging)),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Damaged)));
            Observe(typeof(Projectile),"AI",nameof(Ai));Observe(typeof(Projectile),"Update",null,nameof(Updated));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",F,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Hit)));
            audit.Patch(Get(context,"Input").GetType().GetMethod("AfterNativeMouse",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(MovementTokens)));
            audit.Patch(a.GetType("JueMingR.TerrariaHost.Combat.CombatFacing",true).GetMethod("Apply",F),postfix:new HarmonyMethod(typeof(NativeCombatSkyActionChecks),nameof(Displace)));
            try
            {
                if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
                foreach(int direction in new[]{5,1,-1,0,2,3,4})Scene(context,direction);
                OffCosts(context);
            }
            finally{player=null;movingPlayer=displace=observingChannels=false;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        static void Scene(object context,int direction)
        {
            var combat=Get(context,"Combat");NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3570,0,0);foreach(var armor in player.armor)armor.TurnToAir();player.ResetEffects();
            // Translate the existing relative geometry by exactly 16 tiles.
            // Native topWorld is unchanged: all three original birth heights
            // now have legal flight, with the same 7193 seed/75-frame window.
            const int shift=256;player.position=new Vector2(700,646+shift);player.releaseUseItem=true;player.statMana=player.statManaMax=player.statManaMax2=10000;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=5;x<115;x++){Main.tile[x,43].ClearEverything();if(x<51)NativeToolsChecks.Tile(x,59,1);}Main.worldSurface+=16;
            dummy=direction==5;
            if(dummy){target=NativeCombatControlPreviewChecks.Dummy(2,64,55);NativeToolsChecks.Tile(64,58,1);NativeToolsChecks.Tile(65,58,1);}
            else{target=Main.npc[2];target.SetDefaults(NPCID.Zombie);target.whoAmI=2;target.active=true;target.width=target.height=34;target.Center=player.Center+new Vector2(350,direction==2?-40:30);target.velocity=new Vector2(direction==0?0:direction<0?-2:2,direction==2?.4f:0);target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.target=0;target.life=target.lifeMax=100000;target.knockBackResist=0;}
            Main.screenPosition=new Vector2(480.25f,400.75f+shift);Main.GameViewMatrix.Zoom=Vector2.One;Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,dummy,50,true,true));
            movingPlayer=direction==0;displace=direction==4;displacements=0;facingChanges=0;if(direction==3){player.direction=-1;NativeCombatCadenceChecks.Save(combat,new CombatOptions(32));}
            births=enhanced=geometricBirthLines=mainDamageEvents=nativeStrikeDamage=0;shots.Clear();crossings.Clear();primary.Clear();channels.Clear();damageSource=null;observingChannels=true;float playerStart=player.position.X;int initialLife=target.life;
            var wall=System.Diagnostics.Stopwatch.StartNew();var process=System.Diagnostics.Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocation=AppDomain.MonitoringIsEnabled?AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize:0;
            for(int i=0;i<75;i++)
            {
                solves=launches=replays=steps=expands=required=0;solveCount=solveAge=0;enterUsed=exitUsed=0;preparedContact=null;preparedPoint=false;
                NativeCombatBalancedAimChecks.StepAt(context,player.Center+new Vector2(350,30),i>=20);
            }
            foreach(var shot in shots)Console.WriteLine(shot);
            foreach(var channel in channels)Console.WriteLine("SKY-CHANNEL direction="+direction+" shot="+channel.Number+" kind="+channel.Kind+" key="+channel.Key+" bornTick="+channel.BornTick+" birth="+channel.Birth+" firstAi="+channel.FirstAiTick+" aiCalls="+channel.AiCalls+" nearSeen="+channel.NearSeen+" nearComplete="+channel.NearComplete+" bodyOverlap="+channel.BodyOverlap+" nearSamples="+channel.NearSamples+" nearFirst="+channel.NearFirst+" nearLast="+channel.NearLast+" lastTick="+channel.LastTick+" last="+channel.Last+" end="+(channel.Retired?channel.Retirement:"finite-window-live"));
            int completed=channels.Count(c=>c.NearComplete),overlaps=channels.Count(c=>c.NearComplete && c.BodyOverlap);
            Console.WriteLine("SKY-ACTION SUMMARY direction="+direction+" npcType="+target.type+" births="+births+" enhanced="+enhanced+" lifeLoss="+(initialLife-target.life)+" nativeStrikeDamage="+nativeStrikeDamage+" mainDamageEvents="+mainDamageEvents+" geometricBirthLines="+geometricBirthLines+" crossingX="+string.Join(";",crossings)+" actualNearTracked="+dummy+" actualNearCompleted="+completed+" actualBodyOverlap="+overlaps+" pendingBeforeNear="+(dummy?channels.Count(c=>!c.NearSeen && !c.Retired):-1)+" wallMs="+wall.Elapsed.TotalMilliseconds+" cpuMs="+(process.TotalProcessorTime-cpu).TotalMilliseconds+" allocation="+(AppDomain.MonitoringIsEnabled?AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocation:-1)+" playerDelta="+(player.position.X-playerStart)+" staleFacing="+facingChanges+" world="+Main.topWorld+"/"+Main.bottomWorld);
            Require(births>0,"normal Source path actually enters original lunar Shoot");
            Require(channels.All(c=>c.AiCalls>0) && channels.All(c=>c.Retirement!="native-world-boundary"),"all three native birth heights receive real AI flight opportunity inside the unchanged world");
            if(dummy)
            {
                Require(target.type==488 && !target.CanBeChasedBy() && target.active,"original placed target dummy retains its actual AI and distinct lunar trigger qualification");
                // These are completed actual small-body paths in the target's
                // vertical band, not extrapolated births or a damage quota.
                // Late births still in flight at frame75 remain censored.
                Require(completed>0 && overlaps==completed,"actual living primary channels completing the dummy band converge through its body, rather than only extrapolated birth rays");
                Require(mainDamageEvents>0,"the actual original primary lunar attacks damage the placed dummy");
            }
            if(movingPlayer)Require(player.position.X>playerStart,"physical mapped movement actually advances the player");
            if(Environment.GetEnvironmentVariable("JUEMINGR_SKY_CANDIDATE")==null)
            {
                if(displace)Require(displacements>0 && enhanced==0,"real post-prepare displacement cannot reuse a sky Contact");
                else Require(enhanced>0 && facingChanges==0,"steady native Source produces useful enhanced sky input with final birth dependencies");
            }
            movingPlayer=displace=observingChannels=false;
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
