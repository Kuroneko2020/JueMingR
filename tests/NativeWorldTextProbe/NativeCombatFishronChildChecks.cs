using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFishronChildChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        internal static void Run(object context,NpcPredictionCache cache,Action step)
        {
            var io=new NativeCombatFishronChecks.Inputs(context);BubbleTriggers(io);NaturalSharks(context,cache,step,io);
        }
        private static PredictionEnvironment Environment()
        {
            var e=NativeCombatFishronChecks.Environment();var areas=new List<MotionRect>();var precise=new List<MotionRect>();var slots=new List<int>();var alive=new List<bool>();var dead=new List<bool>();
            for(int i=0;i<Main.maxPlayers;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;slots.Add(i);areas.Add(new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height));precise.Add(new MotionRect(p.position.X,p.position.Y,p.width,p.height));alive.Add(!p.dead && !p.ghost);dead.Add(p.dead);
            }
            e.Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray(),precise.ToArray(),alive.ToArray(),dead.ToArray());e.PlayerTimelineActive=true;return e;
        }
        private static void BubbleTriggers(NativeCombatFishronChecks.Inputs io)
        {
            int cases=0;
            // Original RNG remains untouched. Zero jitter is a legal model
            // representative; the one-action motor error is bounded by the
            // actual independent jitter ranges, not an invented long-window
            // exact-RNG expectation. Pop geometry is checked exactly.
            for(int scene=0;scene<9;scene++)
            {
                NativeCombatFishronChecks.World();Main.rand=new Terraria.Utilities.UnifiedRandom(7030+scene);Main.LocalPlayer.position=new Vector2(4200,2000);
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2400,1800,371,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];n.ai[3]=1;n.velocity=new Vector2(3,-2);
                var trigger=new Rectangle((int)n.position.X-40-n.width/2,(int)n.position.Y-40-n.height/2,n.width+80,n.height+80);
                Main.player[1]=new Player{whoAmI=1,active=scene!=4,dead=scene==3,ghost=scene==2};
                Main.player[1].position=new Vector2(scene==1?trigger.Right:trigger.Right-1,trigger.Y+2);
                if(scene>=5)Main.player[1].active=false;
                if(scene==5)n.ai[1]=149;
                if(scene==6)n.justHit=true;
                if(scene==7){n.ai[0]=1;n.ai[1]=1;}
                if(scene==8){n.ai[0]=1;n.ai[1]=4;}
                io.Terrain.Reset();var state=io.Capture(n);PredictionStop stop;bool valid=NpcMotion.Step(ref state,new[]{state},1,Environment(),io.Terrain,1,true,out stop);n.UpdateNPC(slot);
                Require(state.Active==n.active && state.A0==n.ai[0] && state.A1==n.ai[1] && state.Width==n.width && state.Height==n.height,"Bubble actual integer-body/dead/ghost/countdown scene="+scene+" ai="+state.A0+"/"+state.A1+" native="+string.Join("/",n.ai));
                if(n.active)
                {
                    double error=Vector2.Distance(n.position,new Vector2(state.X,state.Y));Require(valid && error<=.045 && state.CanReceive==(!n.dontTakeDamage && !n.immortal),"Bubble one-action representative/receive scene="+scene+" error="+error.ToString("R")+" stop="+stop);
                    if(scene==0 || scene==2 || scene==5)Require(n.ai[0]==1,"Actual other active non-dead player or age starts pop.");
                    if(scene==1 || scene==3 || scene==4)Require(n.ai[0]==0,"Touching edge, dead and inactive bodies do not trigger pop.");
                }
                else Require(!valid && stop==PredictionStop.Despawn,"Actual bubble countdown retires before movement.");cases++;
            }
            NativeCombatFishronChecks.World();Main.LocalPlayer.position=new Vector2(4200,2000);int fresh=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2400,1800,371,Start:16);var newborn=Main.npc[fresh];
            Require(newborn.target==255,"Actual NewNPC bubble starts target255.");var initial=io.Capture(newborn);PredictionStop initialStop;io.Terrain.Reset();
            Require(NpcMotion.PlayerPremiseTarget(initial,Environment())==Main.myPlayer && NpcMotion.Step(ref initial,new[]{initial},1,Environment(),io.Terrain,1,true,out initialStop) && initial.Target==Main.myPlayer && initial.A3==1 && !float.IsNaN(initial.Vx),"Actual born bubble accepts legal launch representative after native closest-player choice.");
            newborn.UpdateNPC(fresh);Require(newborn.target==Main.myPlayer && newborn.ai[3]>=.8f && newborn.ai[3]<=1.2f,"Original first action consumes its actual random launch and target.");
            // Other players' current bodies can be used for this completed
            // action; an unknown future body is not frozen into a long path.
            Main.player[1]=new Player{whoAmI=1,active=true};Main.player[1].position=new Vector2(5000,2000);var bounded=io.Capture(newborn);io.Terrain.Reset();
            Require(!NpcMotion.Step(ref bounded,new[]{bounded},1,Environment(),io.Terrain,2,true,out initialStop) && initialStop==PredictionStop.MissingDependency,"Unmodeled second player's future body ends the actual pop consumer.");
            Main.player[1].active=false;Console.WriteLine("PASS Fishron bubble native trigger/countdown cases="+cases+" newborn=1 foreignFutureBoundary=1 legalOneActionJitterBound=.045");
        }
        private static void NaturalSharks(object context,NpcPredictionCache cache,Action step,NativeCombatFishronChecks.Inputs io)
        {
            var oracle=new Harmony("JueMingR.Tests.FishronChildrenOriginalSlot");oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFishronChildChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatFishronChildChecks),nameof(After)));
            try
            {
                foreach(int projectile in new[]{384,386})
                {
                    NativeCombatFishronChecks.World();Main.rand=new Terraria.Utilities.UnifiedRandom(7100+projectile);var p=Main.LocalPlayer;p.position=new Vector2(4200,2400-p.height);p.isControlledByFilm=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    // The same literal parameters are issued by native 385
                    // Kill. This actual tornado action owns NPC.NewNPC and
                    // every child parameter; no child AI phase is fabricated.
                    int q=Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_DebugCommand(),new Vector2(2600,2376),new Vector2(projectile==384?-.01f:0,0),projectile,0,4,Main.myPlayer,16,projectile==384?15:24);
                    Require(Main.projectile[q].aiStyle==64,"Actual native tornado family.");int type=projectile==384?372:373;NPC actor=null;var frozen=new List<NpcTrajectory>();int actions=0,preparing=0,receiving=0,gravity=0,matches=0,longMatches=0,receiveCross=0;double maximum=0;bool valid=false;var predicted=default(NpcMotionState);
                    before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);PredictionStop stop;valid=NpcMotion.Step(ref predicted,new[]{predicted},1,Environment(),io.Terrain,1,true,out stop);Require(valid || stop==PredictionStop.Despawn,"Natural shark current stop="+stop);};
                    after=n=>{if(!ReferenceEquals(n,actor))return;actions++;if(!n.active){Require(!predicted.Active,"Native shark collision retirement.");return;}if(!valid)return;double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));Require(error<=.003 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Natural shark current type="+type+" error="+error.ToString("R"));if(n.ai[0]==0)preparing++;if(!n.dontTakeDamage)receiving++;if(!n.noGravity)gravity++;};
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    for(int frame=0;frame<450;frame++)
                    {
                        if(actor==null)foreach(var n in Main.npc)if(n.active && n.type==type){actor=n;break;}
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor==null?new Vector2(2600,2200):actor.Center);step();
                        if(actor==null)continue;if(!actor.active)break;
                        var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor) && path.Count>1 && frame%3==0)frozen.Add(path);
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var f=frozen[i];int offset=(int)((long)Main.GameUpdateCount-f.CaptureTick);if(offset>=f.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=f[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maximum=Math.Max(maximum,error);matches++;if(offset>=30)longMatches++;if(point.CanReceive!=f[0].CanReceive)receiveCross++;
                            Require(error<=.08 && point.Phase==actor.ai[0] && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Default-source shark type="+type+" frame="+frame+" offset="+offset+" error="+error.ToString("R"));
                        }
                    }
                    Require(actor!=null && actions>100 && preparing>20 && receiving>30 && gravity>0 && matches>30 && longMatches>10,"Actual tornado birth, prepare, launch, gravity and source windows type="+type+" actions="+actions+" pre="+preparing+" receive="+receiving+" gravity="+gravity+" matches="+matches+" long="+longMatches);
                    Console.WriteLine("PASS Fishron child type="+type+" actualActions="+actions+" preparing="+preparing+" receiving="+receiving+" gravity="+gravity+" matched="+matches+" long="+longMatches+" receiveCross="+receiveCross+" maxError="+maximum.ToString("R"));
                }
            }
            finally{before=after=null;oracle.UnpatchAll(oracle.Id);}
        }
    }
}
