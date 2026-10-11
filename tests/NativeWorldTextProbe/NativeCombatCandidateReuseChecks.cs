using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCandidateReuseChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            object host=Get(context,"CombatObservation"),production=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var type=production.GetType();object source=Activator.CreateInstance(type,Flags,null,new object[]{null,false},null);
            var candidate=type.GetMethod("TryCandidateReuse",Flags);var read=type.GetMethod("Read",Flags);var clear=type.GetMethod("Clear",Flags);
            Func<int> reuses=()=>(int)type.GetProperty("CandidateMissReuses",Flags).GetValue(source,null);
            Func<NPC,long,bool,int,bool,NpcTrajectory> sample=(n,tick,before,required,reuse)=>(NpcTrajectory)candidate.Invoke(source,new object[]{((NpcMotionState)read.Invoke(null,new object[]{n,session})).Identity,tick,before,required,reuse});
            int expected=0;
            try
            {
                foreach(int member in new[]{224,472})
                {
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.getGoodWorld=false;
                    NPC.brainOfGravity=Main.wofNPCIndex=-1;
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                    var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.ResetFallDamage();player.statLife=player.statLifeMax=player.statLifeMax2=5000;
                    var actors=new NPC[6];long tick=(long)Main.GameUpdateCount;
                    for(int i=0;i<actors.Length;i++)
                    {
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700+i*40,2100,member,Start:16,Target:Main.myPlayer);
                        actors[i]=Main.npc[slot];actors[i].velocity=new Vector2(-2,1);
                    }
                    for(int i=0;i<actors.Length;i++)
                    {
                        var path=sample(actors[i],tick,false,30,false);
                        Require(path!=null && path.Count==31 && path.Stop==PredictionStop.None,"Complete production candidate input proof member="+member+" actor="+i);
                    }
                    // This is cache equivalence, not a claim that native AI
                    // clocks remain frozen through Player/NPC.Update. Genuine
                    // motion correctness belongs to existing natural tests.
                    for(int pass=1;pass<=3;pass++)foreach(var actor in actors)
                    {
                        int prior=reuses();Require(sample(actor,tick+pass,false,30,true)==null && reuses()==prior+1,"Six unchanged candidates retain independent complete proofs, including dense same-type closure.");expected++;
                    }
                    var n=actors[0];
                    Require(sample(n,tick+4,false,30,false)!=null,"Attack owner can require a real path despite an equivalent input proof.");
                    AssertMissInvalidated(()=>n.position.X+=.25f,n,tick+5,sample,reuses,"actor movement");
                    AssertMissInvalidated(()=>n.ai[1]+=1,n,tick+6,sample,reuses,"phase clock");
                    AssertMissInvalidated(()=>player.controlRight=!player.controlRight,n,tick+7,sample,reuses,"player control");
                    AssertMissInvalidated(()=>Main.dayTime=!Main.dayTime,n,tick+8,sample,reuses,"environment day");
                    Require(sample(n,tick+9,true,30,true)!=null,"BeforeNpc and CompletedWorld never share a proof.");
                    Require(sample(n,tick+10,false,31,true)!=null,"A changed horizon acquires its own complete proof.");
                    int reused=reuses();type.GetMethod("ObserveNpcQueryUpdate",Flags).Invoke(source,new object[]{n.whoAmI,false});
                    Require(sample(n,tick+11,false,30,true)!=null && reuses()==reused,"Same-instance sync revokes all candidate proofs, not only the last sampled actor.");
                    clear.Invoke(source,null);
                }
                // FreezeCells keeps only captured cells, independently of
                // Reset, and treats a formerly available null cell as changed.
                object terrain=Get(source,"Terrain");var typed=(IPredictionTerrain)terrain;typed.Reset();PredictionTile tile;PredictionStop stop;
                Require(typed.Tile(80,150,out tile,out stop),"Real tile supplies an independent terrain lease.");
                object lease=terrain.GetType().GetMethod("FreezeCells",Flags).Invoke(terrain,null);
                var current=lease.GetType().GetProperty("Current",Flags);typed.Reset();Require((bool)current.GetValue(lease,null),"Candidate switching/reset preserves lease values.");
                var native=Main.tile[80,150];bool old=native.active();
                try
                {
                    native.active(!old);Require(!(bool)current.GetValue(lease,null),"Changed physical cell revokes the lease.");native.active(old);
                    Require((bool)current.GetValue(lease,null),"Restored exact cell is equal without inventing a future.");
                    Main.tile[80,150]=null;Require(!(bool)current.GetValue(lease,null),"Unknown in-world cell cannot validate an old lease.");
                }
                finally{Main.tile[80,150]=native;native.active(old);}
                Console.WriteLine("PASS candidate complete-input miss proofs sameObservedFacts="+expected+" sixCandidatesAndDenseClosure; movement/clock/player/environment/phase/horizon/sync/terrain invalidation");
                NativePlayerClocks(source,session,candidate,read,clear,reuses);
            }
            finally{clear.Invoke(source,null);Main.netMode=0;}
        }
        private static void AssertMissInvalidated(Action change,NPC n,long tick,Func<NPC,long,bool,int,bool,NpcTrajectory> sample,Func<int> reuses,string reason)
        {
            int prior=reuses();change();Require(sample(n,tick,false,30,true)!=null && reuses()==prior,"Complete input change requires new work: "+reason);
        }
        private static void NativePlayerClocks(object source,long session,MethodInfo candidate,MethodInfo read,MethodInfo clear,Func<int> reuses)
        {
            var randoms=typeof(Main).GetField("_rngs",Flags);var savedRandoms=randoms.GetValue(null);
            try
            {
                randoms.SetValue(null,new System.Collections.Generic.Dictionary<string,Terraria.Utilities.UnifiedRandom>());
                for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.getGoodWorld=false;Main.windSpeedCurrent=Main.windSpeedTarget=0;
                NPC.brainOfGravity=Main.wofNPCIndex=-1;
                Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;
                p.position=new Vector2(1100,2400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.ResetFallDamage();
                for(int warm=0;warm<2;warm++)using(Main.SwapRandom("UpdatePlayers"))p.Update(Main.myPlayer);
                var actors=new NPC[6];long tick=(long)Main.GameUpdateCount;
                for(int i=0;i<actors.Length;i++)actors[i]=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700+i*40,2100,224,Start:16,Target:Main.myPlayer)];
                // This wall is real captured terrain. The NPC observations are
                // held as this test's fixed inputs; ONLY the original player
                // runs here, isolating the newly proven irrelevant clocks.
                for(int y=100;y<150;y++){Main.tile[90,y].active(true);Main.tile[90,y].type=1;}
                Func<NPC,long,bool,NpcTrajectory> sample=(actor,time,reuse)=>(NpcTrajectory)candidate.Invoke(source,new object[]{((NpcMotionState)read.Invoke(null,new object[]{actor,session})).Identity,time,false,30,reuse});
                foreach(var actor in actors)Require(sample(actor,tick,false)!=null,"Real blocked scene establishes a complete candidate future.");
                var playerRead=source.GetType().GetMethod("ReadPlayer",Flags);var initial=(PredictionPlayerMotion)playerRead.Invoke(null,new object[]{p});int matches=0,clockChanges=0;
                for(int frame=1;frame<=12;frame++)
                {
                    using(Main.SwapRandom("UpdatePlayers"))p.Update(Main.myPlayer);
                    var current=(PredictionPlayerMotion)playerRead.Invoke(null,new object[]{p});
                    if(current.WallRegenTime!=initial.WallRegenTime || current.WallRegenCount!=initial.WallRegenCount)clockChanges++;
                    Require(PlayerMotionContinuation.SameFixedInput(initial,current,false),"Original stationary Player.Update changes only absent-Wall regen clocks frame="+frame);
                    foreach(var actor in actors){int prior=reuses();Require(sample(actor,tick+frame,true)==null && reuses()==prior+1,"Native player updates preserve equivalent blocked candidate proofs frame="+frame);matches++;}
                }
                Require(clockChanges==12 && matches==72,"Native regen clocks actually changed on every update; six proofs survived.");
                var changed=initial;changed.WallRegenTime+=1;Require(!initial.SameSample(changed) && !PlayerMotionContinuation.SameFixedInput(initial,changed,true),"General SameSample and necessary Wall source remain exact.");
                changed.WallTonguedTicks=10;Require(!PlayerMotionContinuation.SameFixedInput(initial,changed,false),"Pending tongue prevents irrelevant-clock projection.");
                changed=initial;changed.WallLife--;Require(!PlayerMotionContinuation.SameFixedInput(initial,changed,false),"Real HP changes still invalidate candidate inputs.");
                Console.WriteLine("PASS original Player.Update absent-Wall regen clock actions=12 sixBlockedCandidateProofReuses="+matches+"; NPC clocks remain fixed observed inputs, not normalized");
            }
            finally{clear.Invoke(source,null);randoms.SetValue(null,savedRandoms);}
        }
    }
}
