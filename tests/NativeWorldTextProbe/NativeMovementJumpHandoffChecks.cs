using System;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;
using Terraria.Initializers;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeMovementJumpChecks
    {
        // The independent answer is a legal input sequence: ground press,
        // natural wing depletion to one fuel, complete release, Sandstorm press
        // with toggle-hover, then hold through its span and release during rise.
        // No release latch, resource or future velocity is supplied by the test.
        private static void PendingHeldFlightHandoff(object jump,ref int cases)
        {
            var savedWingStats=ArmorIDs.Wing.Sets.Stats;WingStatsInitializer.Load();
            try
            {
            foreach(int wings in new[]{29,32})
            {
                Call(jump,"ClearSession");var manual=HandoffFuelReady(jump,wings,false,ref cases);var assisted=HandoffFuelReady(jump,wings,true,ref cases);
                int cloud=-1,heldEnd=-1,coast=-1,release=-1,pendingLost=0,heldLost=0,tailLost=0;
                for(int step=0;step<100;step++)
                {
                    bool pressed=step!=0;
                    if(step>1)pressed=manual.jump>0 || manual.velocity.Y*manual.gravDir>=0;
                    manual.controlJump=pressed;assisted.controlJump=step!=0;
                    if(step==1)manual.tryKeepingHoveringDown=assisted.tryKeepingHoveringDown=true;
                    int before=assisted.jump;float fuel=assisted.wingTime,velocity=assisted.velocity.Y*assisted.gravDir;
                    bool available=assisted.canJumpAgain_Cloud;
                    Main.player[Main.myPlayer]=manual;manual.Update(Main.myPlayer);
                    Main.player[Main.myPlayer]=assisted;Begin(jump,assisted,++cases);
                    try
                    {
                        assisted.Update(Main.myPlayer);
                        MovementJumpPhase phase;long version;CallState(jump,assisted,out phase,out version);
                        bool pending=(bool)Get(jump,"flightHandoff"),effective=(bool)Call(jump,"EffectiveJump",assisted,assisted.controlJump);
                        Console.WriteLine("JUMP PENDING wings="+wings+" step="+step+" physical="+assisted.controlJump+" manualInput="+pressed+" effective="+effective+
                            " beforeJump="+before+" jump="+assisted.jump+" preFuel="+fuel+" fuel="+assisted.wingTime+" hover="+assisted.TryingToHoverDown+" down="+assisted.controlDown+
                            " phase="+phase+" handoff="+pending+" preDirectedVy="+velocity+" directedVy="+(assisted.velocity.Y*assisted.gravDir)+" cloud="+assisted.canJumpAgain_Cloud+" armed="+assisted.releaseJump);
                        if(step==0)Require(assisted.jump==0 && assisted.releaseJump && assisted.wingTime==1,"One complete original release rearms Sandstorm without consuming wing fuel.");
                        if(step==1)Require(assisted.jump==45 && !assisted.canJumpAgain_Sandstorm && assisted.canJumpAgain_Cloud && assisted.wingTime==0 && assisted.TryingToHoverDown && !assisted.controlDown,
                            "A real Sandstorm press and early toggle hover consume the final wing fuel in the same native update wings="+wings);
                        if(step>=1 && assisted.jump>0 && available && assisted.canJumpAgain_Cloud)
                        {if(!pending)pendingLost++;if(phase!=MovementJumpPhase.Held)heldLost++;}
                        if(step>1 && before>0)Require(assisted.canJumpAgain_Cloud,"Cloud cannot consume while the original Sandstorm held span is positive.");
                        if(step>1 && before==1)
                        {heldEnd=step;Require(effective,"The last held update remains pressed for the downstream original readers.");if(phase!=MovementJumpPhase.Coast)tailLost++;}
                        if(!effective)release=step;
                        if(available && !assisted.canJumpAgain_Cloud)
                        {
                            cloud=step;
                            Require(before==0 && velocity>=0 && release>=0 && release<step && phase==MovementJumpPhase.Held && !pending,
                                "Cloud starts only after the actual tail with an earlier complete native release; its new Held retires the old handoff.");
                        }
                        if(step>1 && before==0 && velocity<0)
                        {if(coast<0)coast=step;if(effective || phase!=MovementJumpPhase.Coast || !assisted.canJumpAgain_Cloud)tailLost++;}
                        Require(manual.jump==assisted.jump && manual.velocity==assisted.velocity && manual.wingTime==assisted.wingTime && manual.canJumpAgain_Sandstorm==assisted.canJumpAgain_Sandstorm && manual.canJumpAgain_Cloud==assisted.canJumpAgain_Cloud,
                            "Legal manual input and assistance retain the same native movement and resources wings="+wings+" step="+step);
                        if(cloud>=0)break;
                    }
                    finally{End(jump,assisted);}
                }
                Require(heldEnd>=0 && coast==heldEnd+1 && cloud>coast && pendingLost==0 && heldLost==0 && tailLost==0,
                    "Final wing consumption remains pending through the full real held span and then hands off once wings="+wings+" lostPending="+pendingLost+" lostHeld="+heldLost+" lostTail="+tailLost);
                Console.WriteLine("PASS JUMP PENDING wings="+wings+" heldEnd="+heldEnd+" coast="+coast+" release="+release+" cloud="+cloud+" pendingLost="+pendingLost+" heldLost="+heldLost+" resourceWritesAfterStart=0 original-Update-segment=true full-Main=false");
            }
            PendingHandoffPriority(jump,ref cases);
            PendingHandoffWorkload(jump,ref cases);
            }
            finally{Call(jump,"ClearSession");ArmorIDs.Wing.Sets.Stats=savedWingStats;}
        }
        private static Player HandoffPlayer(int wings)
        {
            var p=FlightPlayer("wing");p.wings=p.wingsLogic=wings;p.wingTimeMax=p.GetWingStats(wings).FlyTime;p.wingTime=p.wingTimeMax;
            Require(p.wingTimeMax==180,"Locked original 29/32 capacity comes from native WingStatsInitializer/GetWingStats.");
            p.hasJumpOption_Sandstorm=p.hasJumpOption_Cloud=true;
            return p;
        }
        private static Player HandoffFuelReady(object jump,int wings,bool assisted,ref int cases)
        {
            var p=HandoffPlayer(wings);
            // Keep the actual equipment capacity and let vanilla consume fuel
            // naturally. The ground start also supplies both real extra jumps.
            // Never lower capacity or overwrite fuel/release after this start.
            for(int step=0;step<p.wingTimeMax+16;step++)
            {
                Main.player[Main.myPlayer]=p;
                if(assisted)Begin(jump,p,++cases);
                try{p.Update(Main.myPlayer);}finally{if(assisted)End(jump,p);}
                if(p.wingTime==1)
                {
                    Require(p.jump==0 && p.canJumpAgain_Sandstorm && p.canJumpAgain_Cloud && !p.releaseJump,"Native flight reaches one remaining fuel while preserving both naturally refreshed extra resources.");
                    Console.WriteLine("JUMP PENDING PREP wings="+wings+" assisted="+assisted+" nativeUpdates="+(step+1)+" capacity="+p.wingTimeMax+" fuel="+p.wingTime+" jump="+p.jump+" directedVy="+(p.velocity.Y*p.gravDir)+" native-resource-writes-only=true");
                    return p;
                }
            }
            throw new InvalidOperationException("Native 29/32 preparation did not reach exactly one wing fuel.");
        }
        private static Player PrimeHeldHandoff(object jump,ref int cases)
        {
            Call(jump,"ClearSession");var p=HandoffFuelReady(jump,29,true,ref cases);
            for(int step=0;step<2;step++)
            {
                p.controlJump=step!=0;if(step==1)p.tryKeepingHoveringDown=true;
                Begin(jump,p,++cases);try{p.Update(Main.myPlayer);}finally{End(jump,p);}
            }
            Require(p.jump==45 && p.wingTime==0 && (bool)Get(jump,"flightHandoff"),"Priority control starts from the actual overlapping Sandstorm/final-wing update.");
            return p;
        }
        private static void PendingHandoffPriority(object jump,ref int cases)
        {
            foreach(string reason in new[]{"disable","release","down","fall-press","fall-release"})
            {
                var p=PrimeHeldHandoff(jump,ref cases);Begin(jump,p,++cases);
                try
                {
                    bool fall=reason.StartsWith("fall",StringComparison.Ordinal);
                    if(fall)Require((bool)Call(jump,"ReserveFallInput",p,reason=="fall-release"),"Protection reserves before the pending handoff can write.");
                    if(reason=="release")p.controlJump=false;if(reason=="down")p.controlDown=true;
                    Call(jump,"BeforeJump",p,reason!="disable",true,p.controlJump,p.controlDown,1L);
                    if(fall)Call(jump,"Cancel");
                    MovementJumpPhase phase;long version;CallState(jump,p,out phase,out version);
                    Require(phase==MovementJumpPhase.None && !(bool)Get(jump,"flightHandoff"),"Cancellation/protection retires a pending fact even during positive held jump: "+reason);
                    Require((bool)Call(jump,"EffectiveJump",p,true)==(reason!="fall-release"),"Only the higher owner's requested full release masks native input: "+reason);
                    p.Update(Main.myPlayer);
                    Require(!(bool)Get(jump,"flightHandoff"),"Cancelled positive-held fact cannot be republished by exhausted wings: "+reason);
                }
                finally{End(jump,p);}
                Console.WriteLine("PASS JUMP PENDING PRIORITY reason="+reason+" jump="+p.jump+" fuel="+p.wingTime+" handoff="+Get(jump,"flightHandoff"));
            }
            var next=PrimeHeldHandoff(jump,ref cases);
            next.controlJump=false;Begin(jump,next,++cases);try{next.Update(Main.myPlayer);}finally{End(jump,next);}
            Require(next.jump==0 && next.releaseJump && !(bool)Get(jump,"flightHandoff"),"A physical release ends the old held/pending ownership through vanilla.");
            next.controlJump=true;Begin(jump,next,++cases);
            try
            {
                next.Update(Main.myPlayer);MovementJumpPhase phase;long version;CallState(jump,next,out phase,out version);
                Require(next.jump==11 && !next.canJumpAgain_Cloud && phase==MovementJumpPhase.Held && !(bool)Get(jump,"flightHandoff"),"A new legally rearmed discrete jump owns its Held and cannot inherit the retired handoff.");
            }
            finally{End(jump,next);}
            Console.WriteLine("PASS JUMP PENDING NEW-START real-release-press=true jump="+next.jump+" phase=Held handoff="+Get(jump,"flightHandoff"));
        }
        private static void PendingHandoffWorkload(object jump,ref int cases)
        {
            var p=PrimeHeldHandoff(jump,ref cases);var type=jump.GetType();
            var begin=(Action<Player,long,long,long>)Delegate.CreateDelegate(typeof(Action<Player,long,long,long>),jump,type.GetMethod("BeginUpdate",Flags));
            var before=(Action<Player,bool,bool,bool,bool,long>)Delegate.CreateDelegate(typeof(Action<Player,bool,bool,bool,bool,long>),jump,type.GetMethod("BeforeJump",Flags));
            var after=(Action<Player,bool,bool,bool>)Delegate.CreateDelegate(typeof(Action<Player,bool,bool,bool>),jump,type.GetMethod("AfterJump",Flags));
            var end=(Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),jump,type.GetMethod("EndUpdate",Flags));
            var effective=(Func<Player,bool,bool>)Delegate.CreateDelegate(typeof(Func<Player,bool,bool>),jump,type.GetMethod("EffectiveJump",Flags));
            var allocation=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public);
            Require(allocation!=null,"Pending helper measurement requires the real synchronous-thread allocation counter.");
            var allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),allocation);
            long generation=(long)Get(jump,"session"),tick=(long)Get(jump,"tick"),input=(long)Get(jump,"lastInputVersion");
            // The native scenario above owns correctness. This narrow workload
            // repeats only the helper's unchanged positive-held/pending branch;
            // it does not pretend that 20,000 calls are a native game trajectory.
            for(int i=0;i<256;i++){begin(p,generation,++tick,1);before(p,true,true,true,false,input);after(p,true,true,false);end(p);}
            var clock=System.Diagnostics.Stopwatch.StartNew();long start=allocated();int pressed=0;
            for(int i=0;i<20000;i++)
            {
                begin(p,generation,++tick,1);before(p,true,true,true,false,input);
                if(effective(p,true))pressed++;
                after(p,true,true,false);end(p);
            }
            long bytes=allocated()-start;clock.Stop();MovementJumpPhase phase;long version;CallState(jump,p,out phase,out version);
            Require(bytes==0 && pressed==20000 && phase==MovementJumpPhase.Held && (bool)Get(jump,"flightHandoff"),"Stable pending-held helper preserves one fact and full held input without synchronous allocations.");
            Console.WriteLine("PASS JUMP PENDING WORKLOAD iterations=20000 pressed="+pressed+" allocated="+bytes+" elapsedMs="+clock.Elapsed.TotalMilliseconds+" phase="+phase+" handoff="+Get(jump,"flightHandoff")+" scope=helper-only-not-FPS");
        }
    }
}
