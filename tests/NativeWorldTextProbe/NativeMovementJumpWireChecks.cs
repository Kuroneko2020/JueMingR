using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual primitive/leaf/motion pages and complete frame-zero alignment.
    // This is the receiver closure, separate from the scalar 180-step check.
    internal static class NativeMovementJumpWireChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var host=context.GetType().Assembly;
            var motion=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);
            var actor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",true);
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            var jumpType=host.GetType("JueMingR.TerrariaHost.Movement.MovementJumpHost",true);
            object values=wire.GetField("Players",F).GetValue(null),jump=Activator.CreateInstance(jumpType,true);
            Player original=Main.LocalPlayer;NPC oldNpc=Main.npc[0];float gravity=Player.defaultGravity;
            var active=new bool[Main.maxPlayers];for(int i=0;i<active.Length;i++)if(Main.player[i]!=null){active[i]=Main.player[i].active;Main.player[i].active=false;}
            int cases=0;
            try
            {
                foreach(bool startingDash in new[]{false,true})foreach(bool assisted in new[]{false,true})foreach(bool complex in new[]{false,true})
                {
                    var source=new Player{active=true,whoAmI=Main.myPlayer,controlJump=true,releaseJump=false,position=new Vector2(700,900)};
                    source.grappling[0]=-1;source.hasJumpOption_Cloud=complex;source.dashDelay=startingDash?-1:0;Main.player[Main.myPlayer]=source;
                    var npc=new NPC();npc.SetDefaults(NPCID.BlueSlime);npc.active=true;npc.whoAmI=0;npc.target=Main.myPlayer;npc.targetRect=source.Hitbox;Main.npc[0]=npc;
                    Call(jump,"ClearSession");Call(jump,"BeginUpdate",source,1L,++cases,1L);
                    Call(jump,"BeforeJump",source,assisted,true,true,false,1L);Call(jump,"EndUpdate",source);
                    var expected=alignment.GetMethod("Observe",F).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
                    byte[] profile=Write(motion.GetMethod("Write",F),null,source);
                    Require((profile[0]!=0)==(startingDash || assisted && complex) && (profile[profile.Length-1]!=0)==assisted,"Live writer records true/false intent and its exact complex support source.");
                    byte[] captured;
                    using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
                    {
                        values.GetType().GetMethod("Write",F).Invoke(values,new object[]{writer,source});
                        actor.GetMethod("WritePlayer",F).Invoke(null,new object[]{writer,source});
                        motion.GetMethod("Write",F).Invoke(null,new object[]{writer,source});writer.Flush();captured=bytes.ToArray();
                    }
                    // Same evolved facts on both sides of the codec. This
                    // tests changing mechanism classification, not a claim
                    // that the conditional motor simulates a dash lifecycle.
                    int nextDash=startingDash?0:-1;source.dashDelay=nextDash;
                    byte[] evolvedProfile=Write(motion.GetMethod("Write",F),null,source);
                    var evolvedExpected=alignment.GetMethod("Observe",F).Invoke(null,new object[]{1001L,new[]{0},new int[0],0});
                    source.dashDelay=startingDash?-1:0;
                    // A real worker has no published live owner. Removing it
                    // after capture must not change a sealed receiver premise.
                    Call(jump,"ClearSession");var copy=new Player();Main.player[Main.myPlayer]=copy;
                    object receiver=Activator.CreateInstance(motion,true);
                    using((IDisposable)Call(receiver,"UseRestoredProfiles"))
                    {
                        using(var bytes=new MemoryStream(captured,false))using(var reader=new BinaryReader(bytes))
                        {
                            values.GetType().GetMethod("Read",F).Invoke(values,new object[]{reader,copy});
                            actor.GetMethod("ReadPlayer",F).Invoke(null,new object[]{reader,copy});
                            Call(receiver,"Read",reader,Main.myPlayer);Require(bytes.Position==bytes.Length,"Receiver consumes the complete native player pages.");
                        }
                        object[] live={copy,0L};Require(!(bool)jumpType.GetMethod("ReadIntent",F).Invoke(null,live),"Restored receiver has no real input owner.");
                        byte[] reencoded=Write(motion.GetMethod("Write",F),null,copy);
                        Require(profile.SequenceEqual(reencoded),"Receiver re-encoding retains captured motion bytes with no live owner.");
                        var actual=alignment.GetMethod("Observe",F).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
                        Require(alignment.GetMethod("Difference",F).Invoke(null,new[]{expected,actual})==null,"Actual writer -> primitive/leaf/motion reader -> full alignment hash closes for assisted="+assisted+" complex="+complex);
                        Require((bool)motion.GetMethod("Conditional",F).Invoke(null,new object[]{copy})==(startingDash || assisted && complex),"Receiver retains conditional versus ordinary support quality.");
                        copy.dashDelay=nextDash;
                        var evolved=alignment.GetMethod("Observe",F).Invoke(null,new object[]{1001L,new[]{0},new int[0],0});
                        Require(evolvedProfile.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)) && alignment.GetMethod("Difference",F).Invoke(null,new[]{evolvedExpected,evolved})==null,
                            "Later frame derives Complex from current mechanism plus captured assistance, including dash negative <-> zero.");
                        copy.dashDelay=startingDash?-1:0;
                        var clone=new Player{active=true,whoAmI=copy.whoAmI,controlJump=true};
                        Refuses(()=>Write(motion.GetMethod("Write",F),null,clone),"A same-slot clone cannot borrow the restored profile.");
                        bool reentered=false;
                        try{using((IDisposable)Call(receiver,"UseRestoredProfiles")){}}
                        catch(TargetInvocationException e){reentered=e.InnerException is InvalidOperationException;}
                        Require(reentered && profile.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)),"Same receiver reentry refuses before altering the outer captured page.");
                        object again=Activator.CreateInstance(motion,true);
                        using((IDisposable)Call(again,"UseRestoredProfiles"))
                        {
                            ReadProfile(again,reencoded,copy.whoAmI);
                            var repeated=alignment.GetMethod("Observe",F).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
                            Require(profile.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)) && alignment.GetMethod("Difference",F).Invoke(null,new[]{expected,repeated})==null,"Worker rewrite -> next reader preserves the full alignment premise.");
                        }
                        // Existing worker code may encode another leaf while a
                        // nested receiver is active. Each scope owns its page.
                        byte[] neutral=(byte[])profile.Clone();neutral[0]=(byte)(startingDash?1:0);neutral[neutral.Length-1]=0;
                        object nested=Activator.CreateInstance(motion,true);
                        using((IDisposable)Call(nested,"UseRestoredProfiles"))
                        {
                            ReadProfile(nested,neutral,copy.whoAmI);
                            Require(neutral.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)),"Nested receiver reads its own captured profile.");
                            bool indirectReentry=false;
                            try{using((IDisposable)Call(receiver,"UseRestoredProfiles")){}}
                            catch(TargetInvocationException e){indirectReentry=e.InnerException is InvalidOperationException;}
                            Require(indirectReentry && neutral.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)),"A -> B -> A refuses the occupied outer receiver before changing B's current page.");
                            if(assisted)
                            {
                                var changed=alignment.GetMethod("Observe",F).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
                                Require(alignment.GetMethod("Difference",F).Invoke(null,new[]{expected,changed})!=null,"A genuinely changed captured intention still fails strict alignment.");
                            }
                        }
                        Require(profile.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)),"Normal B exit restores A's unchanged page after indirect reentry refusal.");
                        bool propagated=false;
                        try
                        {
                            using((IDisposable)Call(nested,"UseRestoredProfiles"))
                            {ReadProfile(nested,neutral,copy.whoAmI);throw new InvalidOperationException("owned-receiver-failure");}
                        }
                        catch(InvalidOperationException e){propagated=e.Message=="owned-receiver-failure";}
                        Require(propagated && profile.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)),"Exceptional nested exit preserves the exception and prior profile.");
                        Player.defaultGravity=gravity+.1f;
                        Require(profile.SequenceEqual(Write(motion.GetMethod("Write",F),null,copy)),"Receiver also retains the captured default-gravity source.");
                        Player.defaultGravity=gravity;
                    }
                    Require(Write(motion.GetMethod("Write",F),null,copy).Last()==0,"Normal receiver exit returns to live false intent.");
                    using((IDisposable)Call(receiver,"UseRestoredProfiles"))
                        Refuses(()=>Write(motion.GetMethod("Write",F),null,copy),"A later request cannot reuse an unread prior player page.");
                    bool escaped=false;
                    try
                    {
                        using((IDisposable)Call(receiver,"UseRestoredProfiles"))
                        {ReadProfile(receiver,profile,copy.whoAmI);throw new InvalidOperationException("owned-root-receiver-failure");}
                    }
                    catch(InvalidOperationException e){escaped=e.Message=="owned-root-receiver-failure";}
                    Require(escaped && Write(motion.GetMethod("Write",F),null,copy).Last()==0,"Exceptional root exit clears the receiver and restores live sampling.");
                }
                Console.WriteLine("PASS MOVEMENT JUMP wire native player pages/full alignment cases="+cases+"; true/false, ordinary/complex, evolving mechanism, no live owner, nested/reentry/exception/clone/request cleanup");
            }
            finally
            {
                Call(jump,"ClearSession");Player.defaultGravity=gravity;Main.player[Main.myPlayer]=original;Main.npc[0]=oldNpc;
                for(int i=0;i<active.Length;i++)if(Main.player[i]!=null)Main.player[i].active=active[i];
            }
        }
        private static byte[] Write(MethodInfo method,object owner,Player player)
        {using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){method.Invoke(owner,new object[]{writer,player});writer.Flush();return bytes.ToArray();}}
        private static void ReadProfile(object receiver,byte[] bytes,int slot)
        {using(var reader=new BinaryReader(new MemoryStream(bytes,false)))Call(receiver,"Read",reader,slot);}
        private static void Refuses(Action action,string reason)
        {bool refused=false;try{action();}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}Require(refused,reason);}
    }
}
