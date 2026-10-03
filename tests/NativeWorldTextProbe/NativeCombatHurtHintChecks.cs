using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // A same-input before/after experiment. The original world and real Session
    // continue throughout; no page is preseeded and no reply is manufactured.
    internal static class NativeCombatHurtHintChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            int count=int.Parse(Environment.GetEnvironmentVariable("JUEMINGR_HURT_BACKGROUNDS")??"8");
            string change=Environment.GetEnvironmentVariable("JUEMINGR_HURT_CHANGE")??"useful";
            Require(count==0 || count==8 || count==32,"Only the registered background sizes are allowed.");
            Require(change=="useful" || change=="wall","Only the registered state changes are allowed.");
            var host=Get(context,"CombatObservation");var worker=Get(native,"Worker");
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
            Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
            player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
            player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;
            player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            player.statLife=player.statLifeMax=player.statLifeMax2=100000;player.immune=true;player.immuneTime=100000;
            Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
            Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
            for(int i=0;i<10;i++)player.armor[i].TurnToAir();
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
            int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,110,Start:16,Target:Main.myPlayer);
            var selected=Main.npc[target];selected.life=selected.lifeMax=100000;
            for(int i=0;i<count;i++)
            {
                int slot=i<target?i:i+1;var n=new NPC();n.SetDefaults(678);n.whoAmI=slot;n.active=true;
                n.position=new Vector2(3000+i*30,2400-n.height);Main.npc[slot]=n;
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            var rows=new List<string>{"phase,frame,tick,published,capture,count,pending,accepted,failed,reason,playerLife,immuneTime,playerX,playerY,npcX,npcY,npcPages,projectilePages,activeProjectiles,canHit,npcUpdates,projectileUpdates"};
            var summaries=new List<string>{"phase,startTick,endTick,updates,published,longestBlank,firstPublished,requests,received,rejected,refused,carryIn,carryOut,npcUpdates,projectileUpdates"};
            var hurtRows=new List<string>{"frame,tick,result,beforePages,afterPages,oldPendingRetired,oldAcceptedStillOwned,oldPathStillPublished,velocityX,velocityY"};
            bool done=false;
            using(var trace=new NativeCombatAttackTrace(native,output))
            try
            {
                trace.Selected=target;
                for(int phase=0;phase<3;phase++)
                {
                    string name=phase==0?"learn":phase==1?"settle":"after-hurt";trace.Phase=change+"-"+count+"-"+name;
                    // The barrier is present for 180 real updates before Hurt.
                    // Existing arrows expire naturally; target and towns keep
                    // their original objects, AI and active state.
                    if(phase==1 && change=="wall")
                        for(int x=77;x<=79;x++)for(int y=80;y<150;y++){var tile=Main.tile[x,y];tile.active(true);tile.type=1;}
                    int duration=phase==0?300:phase==1?180:480;
                    ulong start=Main.GameUpdateCount;object carry=Tick(Get(native,"pending"));
                    long req=(long)Get(native,"Requests"),received=trace.Received,rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused"),nu=trace.NpcUpdates,pu=trace.ProjectileUpdates;
                    int published=0,blank=0,longest=0,first=-1;
                    for(int frame=0;frame<duration;frame++)
                    {
                        trace.Frame=frame;object oldPending=null,oldAccepted=null;NpcTrajectory oldPath=null;double hurt=0;int beforePages=0;
                        if(phase==2 && frame%160==0)
                        {
                            oldPending=Get(native,"pending");oldAccepted=Get(native,"acceptedRequest");oldPath=cache.Read(0);
                            beforePages=(int)Get(Get(native,"npcs"),"Count");
                            Require(beforePages>=count+1 || frame>0,"Real page learning completed before the first Hurt.");
                            player.immune=false;player.immuneTime=0;Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
                            hurt=player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated Hurt page-selection comparison"),10,1,quiet:true,dodgeable:false);
                            Require(hurt>0 && !player.dead,"The original Hurt succeeds without death.");
                            player.immuneTime=100000;
                        }
                        long ns=trace.NpcUpdates,ps=trace.ProjectileUpdates;
                        NativeCombatModeledImpactChecks.SampleMouse(context,selected.Center);step();
                        Require(ReferenceEquals(worker,Get(native,"Worker")),"One real worker owns all phases.");
                        if(trace.Fault!=null)throw new InvalidOperationException("Original Prepare failed.",trace.Fault);
                        Require(!(bool)Get(native,"Failed") && !(bool)Get(host,"pathFailed") && !player.dead && selected.active,"Healthy continuous input.");
                        var path=cache.Read(0);
                        if(hurt>0)
                        {
                            bool retired=oldPending==null || (bool)Get(oldPending,"Retired");
                            bool owns=oldAccepted!=null && ReferenceEquals(oldAccepted,Get(native,"acceptedRequest"));
                            bool oldShown=oldPath!=null && path!=null && path.CaptureTick==oldPath.CaptureTick;
                            hurtRows.Add(Csv(frame,Main.GameUpdateCount,hurt,beforePages,Get(Get(native,"npcs"),"Count"),retired,owns,oldShown,player.velocity.X,player.velocity.Y));
                            Require(retired && !owns && !oldShown,"Successful Hurt revokes old pending/publication before consumption.");
                        }
                        if(path!=null)
                        {
                            Require(ReferenceEquals(path.Identity.Token,selected) && path.Identity.Slot==target && path.SampleTick==Main.GameUpdateCount && path.Count==121,"Only actual current+120 publications count.");
                            published++;blank=0;if(first<0)first=frame;
                        }
                        else longest=Math.Max(longest,++blank);
                        rows.Add(Csv(trace.Phase,frame,Main.GameUpdateCount,path!=null,path?.CaptureTick,path?.Count,Tick(Get(native,"pending")),Tick(Get(native,"acceptedRequest")),Get(native,"Failed"),Get(native,"Reason"),player.statLife,player.immuneTime,player.position.X,player.position.Y,selected.position.X,selected.position.Y,Get(Get(native,"npcs"),"Count"),Get(Get(native,"projectiles"),"Count"),Main.projectile.Count(p=>p.active),Collision.CanHit(selected.position,selected.width,selected.height,player.position,player.width,player.height),trace.NpcUpdates-ns,trace.ProjectileUpdates-ps));
                    }
                    summaries.Add(Csv(trace.Phase,start+1,Main.GameUpdateCount,duration,published,longest,first,(long)Get(native,"Requests")-req,trace.Received-received,(long)Get(native,"Rejected")-rejected,(long)Get(native,"Refused")-refused,carry,Tick(Get(native,"pending")),trace.NpcUpdates-nu,trace.ProjectileUpdates-pu));
                    Console.WriteLine("HURT-HINT "+trace.Phase+" published="+published+"/"+duration+" longest="+longest+" first="+first);
                }
                done=true;
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"hurt-updates.csv"),rows);
                File.WriteAllLines(Path.Combine(output,"hurt-summary.csv"),summaries);
                File.WriteAllLines(Path.Combine(output,"hurt-events.csv"),hurtRows);
                File.WriteAllText(Path.Combine(output,"hurt-status.txt"),done?"bounded-windows-completed; compare real publications and workload":"interrupted");
            }
            // A behavior RED: the old implementation must relearn all useful
            // NPC pages after each hit. Projectile discovery remains separate.
            if(change=="useful" && count>0)
            {
                int misses=File.ReadLines(Path.Combine(output,"attack-replies.csv")).Count(s=>s.Contains("after-hurt") && s.Contains("Unobserved entity field kind=1"));
                Require(misses==0,"Already learned useful NPC pages must not be serially rediscovered after Hurt; actual missing-page replies="+misses);
            }
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
