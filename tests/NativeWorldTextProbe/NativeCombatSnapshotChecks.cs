using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSnapshotChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(Assembly host)
        {
            ConditionalPlayerPremises(host);
            TerrainRestore(host);
            NativeCombatTerrainChecks.Trace(host);
            NativeCombatProductionPredictionChecks.Codec(host);
            BoundedShootingClock(host);
            NumericAndEventPremises(host);
            PresentationPremises(host);
            WindConvergence(host);
            BuffClocks(host);
            EffectAllocation(host);
            LightingIdentity(host);
            var permission=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory+FieldPermissions",true);
            var known=new HashSet<int>{0x04000001,0x04000003,0x040000ff};object table=Activator.CreateInstance(permission,Flags,null,new object[]{known},null);
            foreach(int token in Enumerable.Range(0x04000000,257).Concat(new[]{int.MinValue,int.MaxValue,0x05000001,1}))
                Require((bool)permission.GetMethod("Contains",Flags).Invoke(table,new object[]{token})==known.Contains(token),"Permission table preserves complete tokens, holes and out-of-range refusal.");
            Type type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeCapturedValues");
            Require(type!=null,"Sampling must seal independent values before background encoding.");
            var recorder=(BinaryWriter)Activator.CreateInstance(type,true);var source=new byte[]{2,3,4};
            float identityBits=BitConverter.ToSingle(BitConverter.GetBytes(0x7F800100U),0);
            Action<BinaryWriter> write=w=>{w.Write(true);w.Write((byte)255);w.Write((sbyte)-3);w.Write((short)-17);w.Write((ushort)65535);w.Write(-181);w.Write(uint.MaxValue);w.Write(long.MinValue);w.Write(ulong.MaxValue);w.Write(identityBits);w.Write(-0.0d);w.Write("schema 测试");w.Write(source);};
            byte[] expected;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){write(writer);writer.Flush();expected=stream.ToArray();}
            var hashType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment+ValueHashWriter",true);
            if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_HASH_COSTS")=="1")using(var measured=(BinaryWriter)Activator.CreateInstance(hashType,true))
            {
                foreach(bool zeros in new[]{false,true})for(int round=0;round<5;round++)
                {
                    hashType.GetMethod("Reset",Flags).Invoke(measured,null);var timer=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<1000000;i++)measured.Write(zeros?0:i);timer.Stop();
                    Console.WriteLine("HASH micro zeros="+zeros+" round="+round+" int-writes=1000000 elapsed-ms="+timer.Elapsed.TotalMilliseconds.ToString("F3")+" hash="+hashType.GetField("Hash",Flags).GetValue(measured));
                }
            }
            using(var hash=(BinaryWriter)Activator.CreateInstance(hashType,true))
            {
                foreach(Action<BinaryWriter> append in new Action<BinaryWriter>[]{write,w=>{write(w);w.Write(new string('文',150));w.Write("");w.Write(float.NegativeInfinity);w.Write(double.NaN);w.Write(new byte[]{1,2,3,4},1,2);},w=>{for(int i=0;i<512;i++)w.Write(0U);w.Write(1U);w.Write(0U);}})
                {
                    byte[] bytes;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){append(writer);writer.Flush();bytes=stream.ToArray();}
                    ulong expectedHash=14695981039346656037UL;foreach(byte value in bytes)unchecked{expectedHash=(expectedHash^value)*1099511628211UL;}
                    hashType.GetMethod("Reset",Flags).Invoke(hash,null);append(hash);
                    Require((ulong)hashType.GetField("Hash",Flags).GetValue(hash)==expectedHash,"Direct alignment hash preserves independent BinaryWriter bytes, NaN payloads, UTF8 lengths and reset.");
                }
                bool invalidText=false;try{hash.Write("\uD800");}catch(System.Text.EncoderFallbackException){invalidText=true;}Require(invalidText,"Alignment string encoding rejects malformed UTF8 input like BinaryWriter.");
            }
            write(recorder);type.GetMethod("Seal",Flags).Invoke(recorder,null);source[1]=99;recorder.Dispose();
            byte[] actual=Task.Run(()=>(byte[])type.GetMethod("Encode",Flags).Invoke(recorder,null)).Result;
            Require(expected.SequenceEqual(actual),"Frozen values preserve every primitive bit and cloned bytes after producer mutation/disposal.");
            var retired=type.GetMethod("ReleaseStorage",Flags).Invoke(recorder,null);
            bool expired=false;try{type.GetMethod("Encode",Flags).Invoke(recorder,null);}catch(TargetInvocationException e){expired=e.InnerException is InvalidOperationException;}Require(expired,"A transferred buffer cannot expose the previous capture after retirement.");
            using(var reused=(BinaryWriter)Activator.CreateInstance(type,Flags,null,new[]{retired},null))
            {reused.Write(1234);type.GetMethod("Seal",Flags).Invoke(reused,null);actual=(byte[])type.GetMethod("Encode",Flags).Invoke(reused,null);Require(actual.SequenceEqual(BitConverter.GetBytes(1234)),"Empty capacity reuse exposes only the next capture, never old values or byte references.");}
            bool denied=false;try{recorder.Write(1);}catch(InvalidOperationException){denied=true;}Require(denied,"Sealed capture cannot be overwritten.");
            using(var unsupported=(BinaryWriter)Activator.CreateInstance(type,true))
            {denied=false;try{unsupported.Write(1m);}catch(NotSupportedException){denied=true;}Require(denied,"An unsupported write fails instead of disappearing into Stream.Null.");}
            using(var large=(BinaryWriter)Activator.CreateInstance(type,true))
            {
                const int count=262145;for(int i=0;i<count;i++)large.Write(i);
                type.GetMethod("Seal",Flags).Invoke(large,null);actual=(byte[])type.GetMethod("Encode",Flags).Invoke(large,null);
                Require(actual.Length==count*4 && BitConverter.ToInt32(actual,actual.Length-4)==count-1,"Legal scalar captures larger than the former tape count retain exact values.");
            }
            using(var bounded=(BinaryWriter)Activator.CreateInstance(type,true))
            {
                int maximum=(int)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionPipeProtocol",true).GetField("MaximumPayload",Flags).GetRawConstantValue();
                bounded.Write(new byte[maximum-5]);bounded.Write(1234);bounded.Write("");bounded.Write(new byte[0]);
                denied=false;try{bounded.Write((byte)1);}catch(InvalidDataException){denied=true;}Require(denied,"Scalars and buffers share one pre-encoding payload limit.");
                type.GetMethod("Seal",Flags).Invoke(bounded,null);actual=(byte[])type.GetMethod("Encode",Flags).Invoke(bounded,null);Require(actual.Length==maximum,"Exact mixed-write payload boundary remains encodable.");
            }

            NativeCombatWorkerChecks.Scene(false);
            NativeCombatWorkerEntityChecks.DirectoryRoundTrip(host);
            CheckTerrain(host);CheckLinks(host);CheckGravitySource(host);CheckSessionAges(host);NativeCombatWorkerChecks.Scene(false);
            CheckSchema(host,typeof(NPC),Main.npc[0]);CheckSchema(host,typeof(Player),Main.player[0]);CheckSchema(host,typeof(Projectile),Main.projectile[0]);CheckSchema(host,typeof(Vector2),new Vector2(-0f,19));CheckSchema(host,typeof(Main),null,true);
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            object[] args={new[]{0},new[]{0,1,2},0,1000L,120};
            expected=(byte[])wire.GetMethod("CaptureScene",Flags).Invoke(null,args);
            var capture=wire.GetMethod("CaptureSceneValues",Flags);Require(capture!=null,"Production codecs expose the same independent value capture.");
            object frozen=capture.Invoke(null,args);
            Main.npc[0].velocity=new Vector2(12,-6);Main.npc[0].ai[0]=identityBits;Main.npc[0].buffTime[0]=971;
            Main.player[0].inventory[0].stack=391;Main.player[0].buffTime[0]=21;Main.projectile[0].ai[0]=117;
            Main.tile[4,4].type=2;Main.rand.Next();Projectile.perIDStaticNPCImmunity[0,0]=872;
            actual=Task.Run(()=>(byte[])type.GetMethod("Encode",Flags).Invoke(frozen,null)).Result;
            Require(expected.SequenceEqual(actual),"Whole world/entity/context/RNG/terrain snapshot reads no live values after sealing.");
            var owner=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionSession",true);var reusable=owner.GetMethod("CanReuseProof",Flags);
            var endIdentity=new NpcIdentity(1,null,0,0,2,2);var endPoints=Enumerable.Range(0,81).Select(i=>new NpcTrajectoryPoint(i,new NpcMotionState{Identity=endIdentity,Width=10,Height=10})).ToArray();
            var ending=new NpcTrajectory(endIdentity,0,1,PredictionAssumption.None,PredictionStop.Despawn,endPoints,endPoints.Length);NpcTrajectory remaining;
            Require(ending.TryWindow(61,120,2,out remaining),"Natural-end trajectory itself preserves its real short future after age 60.");
            Require((bool)reusable.Invoke(null,new object[]{60L,181}) && !(bool)reusable.Invoke(null,new object[]{61L,181}),"Native owner needs a fresh proof after age 60 even for natural ends or shorter consumers.");
            var alignmentType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            var presentation=alignmentType.GetMethod("Presentation",Flags).Invoke(null,new object[]{1000L,0});
            var state=alignmentType.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
            Require((string)alignmentType.GetMethod("Difference",Flags).Invoke(null,new[]{presentation,state})=="missing state proof","A presentation-only future can never establish current state equality.");
            var bind=typeof(NpcTrajectory).GetMethod("BindIdentity");
            Require(bind!=null,"A decoded value-only trajectory must bind its original live identity without copying or renewing time.");
            var identity=new NpcIdentity(1,null,2,3,48,48);
            var live=new NpcIdentity(1,new object(),2,3,48,48);
            var points=new[]{new NpcTrajectoryPoint(0,new NpcMotionState{Identity=identity,X=12,Width=10,Height=10})};
            var trajectory=new NpcTrajectory(identity,1000,1,PredictionAssumption.None,PredictionStop.Despawn,points,1);
            var bound=(NpcTrajectory)bind.Invoke(trajectory,new object[]{live});
            Require(bound.Identity.Equals(live) && bound.CaptureTick==1000 && bound.SampleTick==1000 && bound[0].Bounds.X==12 && trajectory.Identity.Token==null,"Binding restores only the original token and retains independent values and capture time.");
            Require(ReferenceEquals(typeof(NpcTrajectory).GetField("points",Flags).GetValue(bound),typeof(NpcTrajectory).GetField("points",Flags).GetValue(trajectory)),"Binding shares owned immutable points instead of copying the full result on the game thread.");
            foreach(var wrong in new[]{new NpcIdentity(2,live.Token,2,3,48,48),new NpcIdentity(1,live.Token,3,3,48,48),new NpcIdentity(1,live.Token,2,4,48,48),new NpcIdentity(1,live.Token,2,3,49,48),new NpcIdentity(1,live.Token,2,3,48,49)})
            {denied=false;try{bind.Invoke(trajectory,new object[]{wrong});}catch(TargetInvocationException e){denied=e.InnerException is InvalidOperationException;}Require(denied,"Mismatched value identity cannot be rebound.");}
            denied=false;try{bind.Invoke(bound,new object[]{live});}catch(TargetInvocationException e){denied=e.InnerException is InvalidOperationException;}Require(denied,"An already live-bound result cannot be retargeted.");
            var clear=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionSandbox",true).GetMethod("ClearCollisionWorld",Flags);
            Require(clear!=null,"World reset must retire collision scratch before the next prediction can hide residual values.");
            clear.Invoke(null,null);float epsilon=Collision.Epsilon;
            var scratch=new[]{"stair","stairFall","honey","shimmer","sloping","landMine","up","down"};
            foreach(string name in scratch)typeof(Collision).GetField(name,Flags).SetValue(null,true);
            var contacts=(System.Collections.IList)typeof(Collision).GetField("contacts",Flags).GetValue(null);
            var conveyors=(System.Collections.IList)typeof(Collision).GetField("_cacheForConveyorBelts",Flags).GetValue(null);
            contacts.Add(Activator.CreateInstance(contacts.GetType().GetGenericArguments()[0]));conveyors.Add(new Point(34567,45678));Collision.Epsilon=19;
            clear.Invoke(null,null);clear.Invoke(null,null);
            Require(contacts.Count==0 && conveyors.Count==0 && Collision.Epsilon==epsilon && scratch.All(name=>!(bool)typeof(Collision).GetField(name,Flags).GetValue(null)),"Clear is immediate and idempotent for collision coordinates, flags and initialization epsilon.");
            Console.WriteLine("PASS independent snapshot / all codec bytes / primitive identity bits / producer mutation / background encode / sealed and unsupported writes");
        }
        private static void LightingIdentity(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeLightingSnapshot",true);var n=Main.npc[0];n.SetDefaults(Terraria.ID.NPCID.GoldBunny);n.whoAmI=0;n.active=true;
            Action<int,int,int> read=(count,slot,light)=>{using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write(count);writer.Write(slot);writer.Write((byte)light);writer.Flush();stream.Position=0;using(var reader=new BinaryReader(stream))type.GetMethod("Read",Flags).Invoke(null,new object[]{reader});}};
            Func<int> observe=()=>(int)type.GetMethod("Observe",Flags).Invoke(null,new object[]{Main.npc[0]});
            Action denied=()=>{bool failed=false;try{observe();}catch(TargetInvocationException e){failed=e.InnerException is InvalidDataException;}Require(failed,"Unobserved lighting actor cannot inherit a sampled class.");};
            type.GetField("privateSide",Flags).SetValue(null,true);
            try
            {
                read(1,0,9);type.GetMethod("BindRestoredActors",Flags).Invoke(null,null);Require(observe()==9,"Captured lighting binds its actual restored owner.");
                var generationProperty=typeof(NPC).GetProperty("generation",Flags);byte generation=n.generation;generationProperty.SetValue(n,(byte)(generation+1),null);denied();generationProperty.SetValue(n,generation,null);
                int original=n.type;n.type=Terraria.ID.NPCID.GoldBird;denied();n.type=original;
                var replacement=new NPC();replacement.SetDefaults(original);replacement.whoAmI=0;replacement.active=true;Main.npc[0]=replacement;denied();Main.npc[0]=n;
                type.GetMethod("Clear",Flags).Invoke(null,null);denied();Require(!(bool)type.GetProperty("HasValues",Flags).GetValue(null,null),"World clear retires lighting premises.");
                foreach(var input in new[]{new[]{-1,0,0},new[]{202,0,0},new[]{1,-1,0},new[]{1,201,0},new[]{1,0,10}})
                {bool failed=false;try{read(input[0],input[1],input[2]);}catch(TargetInvocationException e){failed=e.InnerException is InvalidDataException;}Require(failed,"Malformed light count, slot and class refuse.");}
            }
            finally{type.GetMethod("Release",Flags).Invoke(null,null);NativeCombatWorkerChecks.Scene(false);}
            Console.WriteLine("PASS lighting owner / type / generation / clear / malformed premises");
        }
        private static void EffectAllocation(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
            var boundary=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true);
            Action<byte[]> read=bytes=>{using(var reader=new BinaryReader(new MemoryStream(bytes)))boundary.GetMethod("ReadAllocation",Flags).Invoke(null,new object[]{reader});};
            foreach(int i in new[]{0,7,8,Main.dust.Length-1})Main.dust[i].active=true;
            foreach(int i in new[]{0,7,8,Main.gore.Length-1})Main.gore[i].active=true;
            foreach(int i in new[]{0,7,8,Main.combatText.Length-1})Main.combatText[i].active=true;Dust.dCount=.7f;
            byte[] captured;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){boundary.GetMethod("WriteAllocation",Flags).Invoke(null,new object[]{writer});writer.Flush();captured=stream.ToArray();}
            Require(captured.Length==844,"Fixed native allocation input remains bounded.");
            foreach(var d in Main.dust)d.active=false;foreach(var g in Main.gore)g.active=false;foreach(var c in Main.combatText)c.active=false;Dust.dCount=0;
            read(captured);Require(Dust.dCount==.7f && Main.dust.Count(d=>d.active)==4 && Main.gore.Count(g=>g.active)==4 && Main.combatText.Count(c=>c.active)==4 && Main.dust[6000].active && Main.gore[600].active && Main.combatText[99].active,"Frozen allocation restores boundary slots independently of producer mutations.");
            foreach(var item in new[]{new[]{754,2},new[]{830,2},new[]{843,16}})
            {
                byte[] bad=(byte[])captured.Clone();bad[item[0]]|=(byte)item[1];bool refused=false;
                try{read(bad);}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}Require(refused,"Unused effect bitmap bits are not valid actor slots: "+item[0]);
            }
            foreach(int length in new[]{3,754,830,843}){bool refused=false;try{read(captured.Take(length).ToArray());}catch(TargetInvocationException e){refused=e.InnerException is EndOfStreamException;}Require(refused,"Truncated allocation refuses before a prediction.");}
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,-.1f,1.1f}){byte[] bad=(byte[])captured.Clone();Buffer.BlockCopy(BitConverter.GetBytes(value),0,bad,0,4);bool refused=false;try{read(bad);}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}Require(refused,"Invalid dust count premise refuses.");}
            // Reset is the same immediate value reset used by ClearWorld;
            // this unit probe does not install private hooks in the oracle.
            var sandbox=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionSandbox",true);object owner=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(sandbox);
            sandbox.GetField("restoredPlayers",Flags).SetValue(owner,new bool[Main.maxPlayers]);
            for(int i=0;i<2;i++){read(captured);sandbox.GetMethod("Reset",Flags).Invoke(owner,new object[]{120,120});Require(Dust.dCount==0 && Main.dust.All(d=>!d.active) && Main.gore.All(g=>!g.active) && Main.combatText.All(c=>!c.active),"World reset clears all allocation state before acknowledgement.");}
            NativeCombatWorkerChecks.Scene(false);
            Console.WriteLine("PASS effect allocation frozen slots / padding / truncation / invalid scalar / immediate idempotent reset");
        }
        private static void BoundedShootingClock(Assembly host)
        {
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            NativeCombatWorkerChecks.Scene(false);int[] chain=NativeCombatLongCoverageChecks.Destroyer(false);int slot=chain[chain.Length/2];NPC n=Main.npc[slot];
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            Func<object> observe=()=>type.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{slot},new int[0],slot});
            Func<object,object,string> difference=(a,b)=>(string)type.GetMethod("Difference",Flags).Invoke(null,new[]{a,b});
            n.localAI[0]=296;object first=observe();n.localAI[0]=297;object next=observe();
            Require(difference(first,next)==null,"Destroyer shooting clocks proven below the entire future trigger window may share an acceptance premise.");
            Require(n.localAI[0]==297,"Acceptance never normalizes the live object's actual clock.");
            n.localAI[0]=860;first=observe();n.localAI[0]=861;Require(difference(first,observe())!=null,"A possible threshold crossing retains exact clocks.");
            n.localAI[0]=859;first=observe();n.localAI[0]=860;Require(difference(first,observe())!=null,"The minimum laser threshold is an exclusive bound including the full 180-update horizon.");
            n.localAI[0]=20;first=observe();n.localAI[1]++;Require(difference(first,observe())!=null,"Other localAI slots are not omitted.");n.localAI[1]--;
            Main.player[1].active=true;Main.player[1].whoAmI=1;Main.player[1].position=new Vector2(700,700);first=observe();n.target=1;Require(difference(first,observe())!=null,"Actual two-player retargeting invalidates even a safe clock premise.");
            NativeCombatWorkerChecks.Scene(false);n=Main.npc[0];slot=0;first=observe();n.localAI[0]++;Require(difference(first,observe())!=null,"Ordinary Harpy localAI remains exact; no cross-type exception.");
            Console.WriteLine("PASS bounded Destroyer shooting-clock acceptance; threshold, other state, actual retarget and ordinary-type counterexamples");
        }
        private static void NumericAndEventPremises(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);var n=Main.npc[0];
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            Func<object> observe=()=>type.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
            Func<object,object,string> difference=(a,b)=>(string)type.GetMethod("Difference",Flags).Invoke(null,new[]{a,b});
            n.velocity.X=1.37426448f;object before=observe();n.velocity.X=1.3742646f;Require(difference(before,observe())==null,"Measured ghost velocity tail has bounded numerical equivalence.");
            n.velocity.X+=.003f;Require(difference(before,observe())!=null,"Numeric bound cannot become a motion smoothing allowance.");
            n.velocity=Vector2.Zero;before=observe();n.velocity.X=.0000001f;Require(difference(before,observe())!=null,"Zero/nonzero branch remains exact.");
            n.velocity.X=-.0000001f;before=observe();n.velocity.X=.0000001f;Require(difference(before,observe())!=null,"Sign branch remains exact.");
            n.oldVelocity.X=1.37426448f;before=observe();n.oldVelocity.X=1.3742646f;Require(difference(before,observe())==null,"Collision's copied prior velocity has the same explicit numeric bound.");
            before=observe();n.position.X+=.0001f;Require(difference(before,observe())==null,"Position tails allowed by the 120-step original oracle must remain usable in production.");
            n.position.X+=.003f;Require(difference(before,observe())!=null,"Absolute drift stays bounded at each observation; no reanchoring or accumulated allowance.");
            before=observe();n.oldPosition.X+=.0001f;n.oldPos[0].X+=.0001f;Require(difference(before,observe())==null,"Copied continuous history has the same small bound.");
            before=observe();n.position.X+=100;Require(difference(before,observe())!=null,"True relocation remains invalid.");
            before=observe();n.ai[0]+=.0000001f;Require(difference(before,observe())!=null,"AI phase/angle/index values remain exact.");
            foreach(string biome in new[]{"ZoneHallow","ZoneDesert","ZoneJungle","ZoneLihzhardTemple","ZoneSandstorm"})
            {var property=typeof(Player).GetProperty(biome);before=observe();bool original=(bool)property.GetValue(Main.LocalPlayer,null);property.SetValue(Main.LocalPlayer,!original,null);Require(difference(before,observe())!=null,"Observed biome invalidates: "+biome);property.SetValue(Main.LocalPlayer,original,null);}
            bool ongoing=Terraria.GameContent.Events.DD2Event.Ongoing;int active=Main.CurrentFrameFlags.ActivePlayersCount;float kills=NPC.waveKills;
            try
            {
                Terraria.GameContent.Events.DD2Event.Ongoing=true;Main.CurrentFrameFlags.ActivePlayersCount=1;before=observe();Main.CurrentFrameFlags.ActivePlayersCount=2;Require(difference(before,observe())!=null,"DD2 live non-ghost player count is an event premise.");
                before=observe();NPC.waveKills++;Require(difference(before,observe())!=null,"Real death-driven event progress retires the old premise.");
            }
            finally{Terraria.GameContent.Events.DD2Event.Ongoing=ongoing;Main.CurrentFrameFlags.ActivePlayersCount=active;NPC.waveKills=kills;}
            Console.WriteLine("PASS explicit velocity numerical bound; zero/sign/position/AI boundaries; all biome flags and DD2 event premises");
        }
        private static void PresentationPremises(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);var n=Main.npc[0];
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            Func<object> observe=()=>type.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
            Func<object,object,string> difference=(a,b)=>(string)type.GetMethod("Difference",Flags).Invoke(null,new[]{a,b});
            object before=observe();n.nameOver+=NPC.nameOverIncrement;
            Require(difference(before,observe())==null,"Name fade is a Draw-owned value and must not retire an otherwise valid trajectory.");
            var clock=typeof(NPC).GetField("targetSetFrame",Flags);before=observe();clock.SetValue(n,Convert.ChangeType(Convert.ToDouble(clock.GetValue(n))+1,clock.FieldType));
            Require(difference(before,observe())==null,"The DrawAggro timestamp is not a movement or AI premise.");
            before=observe();n.life--;Require(difference(before,observe())!=null,"Real damage is not a presentation change.");
            before=observe();n.dontTakeDamage=!n.dontTakeDamage;Require(difference(before,observe())!=null,"Damage eligibility still retires the prediction.");
            before=observe();n.localAI[0]++;Require(difference(before,observe())!=null,"AI-local state is not excluded along with name fade.");
            Console.WriteLine("PASS name fade equivalence with life, eligibility and localAI counterexamples (state perturbation, not rendering).");
        }
        private static void ConditionalPlayerPremises(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);var p=Main.LocalPlayer;var n=Main.npc[0];
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            Func<object> observe=()=>type.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
            Func<object,object,string> difference=(a,b)=>(string)type.GetMethod("Difference",Flags).Invoke(null,new[]{a,b});
            p.pulley=true;object before=observe();p.position.X+=3;p.velocity.X=2;p.jump=3;
            Require(difference(before,observe())==null,"Conditional pulley continuation does not require exact future player kinematics when current NPC state remains valid.");
            n.target=p.whoAmI;n.targetRect=p.Hitbox;before=observe();p.position.X+=3;n.targetRect=p.Hitbox;
            Require(difference(before,observe())==null,"A current player-derived target rectangle follows conditional player motion without hiding NPC state changes.");
            before=observe();n.targetRect.X++;Require(difference(before,observe())!=null,"A cached or non-player rectangle cannot inherit conditional equivalence.");n.targetRect=p.Hitbox;
            p.tankPet=1;before=observe();p.position.X+=3;n.targetRect=p.Hitbox;Require(difference(before,observe())!=null,"A possible tank-pet rectangle keeps its coordinates exact.");p.tankPet=-1;
            before=observe();n.target=300;Require(difference(before,observe())!=null,"Changing player target to NPC target remains exact.");n.target=p.whoAmI;
            before=observe();p.controlDown=!p.controlDown;Require(difference(before,observe())!=null,"A new input retires the conditional premise.");
            before=observe();p.pulley=false;p.grappling[0]=1;Require(difference(before,observe())!=null,"Changing between Complex mechanisms is not equivalent.");
            before=observe();p.wet=!p.wet;Require(difference(before,observe())!=null,"Water pursuit qualification remains causal.");
            before=observe();n.ai[0]++;Require(difference(before,observe())!=null,"Conditional player motion cannot excuse a changed NPC branch.");
            before=observe();n.position.X+=100;Require(difference(before,observe())!=null,"Conditional player motion cannot excuse target teleportation.");
            p.inventory[10].SetDefaults(1);p.inventory[10].stack=20;before=observe();p.inventory[10].stack=19;Require(difference(before,observe())==null,"An unused positive inventory count does not reject a pure NPC continuation.");
            before=observe();p.inventory[10].stack=0;Require(difference(before,observe())!=null,"Loss of item presence is not a count-only change.");
            p.grappling[0]=-1;p.pulley=false;before=observe();p.position.X+=3;Require(difference(before,observe())!=null,"Supported ordinary kinematics still have the original small error bound.");
            Console.WriteLine("PASS conditional player result vs fixed input/mechanism/target premise; unrelated pure-NPC stack count boundary.");
        }
        private static void BuffClocks(Assembly host)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);var advance=type.GetMethod("AdvanceBuffClocks",Flags);
            foreach(int[] types in new[]{new[]{1},new[]{5},new[]{12},new[]{71},new[]{73},new[]{104},new[]{343},new[]{2,3,26},new[]{26,3,2},new[]{206},new[]{207}})
            foreach(int duration in new[]{1,3,1200})foreach(int owner in new[]{Main.myPlayer,Main.myPlayer==0?1:0})
            {
                var original=new Player{whoAmI=owner};var predicted=new Player{whoAmI=owner};
                for(int i=0;i<types.Length;i++){original.buffType[i]=predicted.buffType[i]=types[i];original.buffTime[i]=predicted.buffTime[i]=duration;}
                for(int step=0;step<4;step++)
                {
                    original.moveSpeed=1;original.UpdateBuffs(owner);
                    if(owner==Main.myPlayer)for(int i=0;i<original.buffType.Length;i++)if(original.buffType[i]>0 && original.buffTime[i]<=0)original.DelBuff(i);
                    object[] args={predicted,0f};bool ordinary=(bool)advance.Invoke(null,args);
                    Require(original.buffType.SequenceEqual(predicted.buffType) && original.buffTime.SequenceEqual(predicted.buffTime),"Original buff decrement, ownership and ordered expiry compaction.");
                    if(ordinary)Require(original.moveSpeed==(float)args[1],"Last valid buff step retains original movement contribution.");
                }
            }
            foreach(int special in new[]{49,60,95,98,170,173,28,34,37,38,62,86,87,89,103,146,147,148,151,157,158,194,215,332,350,353})
                Require(!(bool)type.GetMethod("OrdinaryClock",Flags).Invoke(null,new object[]{special}),"Special refresh/transform clock cannot become an ordinary decrement: "+special);
            Console.WriteLine("PASS original common buff clocks / local and remote / last effect / mixed expiry order / special-clock exclusions");
        }
        private static void WindConvergence(Assembly host)
        {
            var advance=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeWorldSnapshot",true).GetMethod("AdvanceObservedWind",Flags);
            var original=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));
            float wind=Main.windSpeedCurrent,target=Main.windSpeedTarget,rain=Main.maxRaining;int rate=Main.dayRate,mode=Main.netMode;
            try
            {
                foreach(int count in new[]{0,1,3})foreach(float current in new[]{-.9f,0f,.4999f,.9f})foreach(float wet in new[]{0f,.6f})
                {
                    Main.dayRate=count;Main.windSpeedTarget=.5f;Main.maxRaining=wet;Main.windSpeedCurrent=current;Main.netMode=1;
                    for(int i=0;i<count;i++)original.UpdateWeather(new GameTime(),1);float expected=Main.windSpeedCurrent;
                    Main.windSpeedCurrent=current;advance.Invoke(null,null);Require(Main.windSpeedCurrent==expected,"Observed wind follows original float sequence, clamp and day-rate loops.");
                }
                NativeCombatWorkerChecks.Scene(false);var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
                Func<object> observe=()=>alignment.GetMethod("Observe",Flags).Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
                Func<object,object,string> difference=(a,b)=>(string)alignment.GetMethod("Difference",Flags).Invoke(null,new[]{a,b});
                var before=observe();Main.windSpeedTarget+=.1f;Require(difference(before,observe())=="world premise","Changed wind driver invalidates before current wind diverges.");
                before=observe();Main.maxRaining+=.1f;Require(difference(before,observe())=="world premise","Changed rain driver invalidates before current wind diverges.");
            }
            finally{Main.windSpeedCurrent=wind;Main.windSpeedTarget=target;Main.maxRaining=rain;Main.dayRate=rate;Main.netMode=mode;}
            Console.WriteLine("PASS original wind convergence / clamp / paused and accelerated clock / strict target and rain drivers");
        }
        private static void TerrainRestore(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);var original=Main.tile;
            var snapshotType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot",true);
            object snapshot=snapshotType.GetMethod("CaptureChunks",Flags).Invoke(null,new object[]{1L,new SortedSet<int>{0}});
            var storeType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainStore",true);object store=Activator.CreateInstance(storeType,true);
            Action<bool> restore=references=>{using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){snapshotType.GetMethod("Write",Flags).Invoke(snapshot,new object[]{writer,references});writer.Flush();stream.Position=0;using(var reader=new BinaryReader(stream))storeType.GetMethod("Read",Flags).Invoke(store,new object[]{reader,120,120});}};
            try
            {
                restore(false);var tile=Main.tile[0,0];var baseline=original[0,0];
                tile.type=1;tile.wall=2;tile.liquid=3;tile.sTileHeader=4;tile.bTileHeader=5;tile.bTileHeader2=6;tile.bTileHeader3=7;tile.frameX=8;tile.frameY=9;
                restore(true);
                Require(ReferenceEquals(tile,Main.tile[0,0]),"Repeated terrain restore reuses its private Tile instead of allocating every cell again.");
                Require(tile.type==baseline.type && tile.wall==baseline.wall && tile.liquid==baseline.liquid && tile.sTileHeader==baseline.sTileHeader && tile.bTileHeader==baseline.bTileHeader && tile.bTileHeader2==baseline.bTileHeader2 && tile.bTileHeader3==baseline.bTileHeader3 && tile.frameX==baseline.frameX && tile.frameY==baseline.frameY,"Every mutable Tile field is restored even for cached immutable bytes.");
                Require(Main.tile[32,0]==null,"Reuse never provides an undeclared tile.");
                Console.WriteLine("PASS terrain private Tile reuse and all nine fields restored after simulated edits.");
            }
            finally{Main.tile=original;}
        }
        private static void CheckTerrain(Assembly host)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot",true);
            var keys=new SortedSet<int>{0,3*128+3};Tile prior=Main.tile[50,50];Main.tile[50,50]=null;
            try
            {
                object value=type.GetMethod("CaptureChunks",Flags).Invoke(null,new object[]{1L,keys});var chunks=(Array)type.GetField("Chunks",Flags).GetValue(value);
                Require(chunks.Length==2,"Distant required chunks do not copy the unobserved rectangle between them.");
                foreach(object chunk in chunks)
                {
                    int cx=(int)chunk.GetType().GetField("X",Flags).GetValue(chunk),cy=(int)chunk.GetType().GetField("Y",Flags).GetValue(chunk);
                    using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                    {
                        for(int x=cx*32;x<Math.Min(Main.maxTilesX,(cx+1)*32);x++)for(int y=cy*32;y<Math.Min(Main.maxTilesY,(cy+1)*32);y++)
                        {var t=Main.tile[x,y];writer.Write(t.type);writer.Write(t.wall);writer.Write(t.liquid);writer.Write(t.sTileHeader);writer.Write(t.bTileHeader);writer.Write(t.bTileHeader2);writer.Write(t.bTileHeader3);writer.Write(t.frameX);writer.Write(t.frameY);}
                        writer.Flush();Require(stream.ToArray().SequenceEqual((byte[])chunk.GetType().GetField("Values",Flags).GetValue(chunk)),"Compact terrain capture retains all original tile bits including partial edge chunks.");
                    }
                }
                Require((bool)type.GetMethod("IsCurrent",Flags).Invoke(value,new object[]{1L}),"Unknown unrelated middle terrain cannot invalidate the sampled union.");
                var passType=type.GetNestedType("Comparison",Flags);var pass=Activator.CreateInstance(passType,true);var compare=type.GetMethod("IsCurrentObserved",Flags);
                object equivalent=type.GetMethod("CaptureChunks",Flags).Invoke(null,new object[]{1L,keys});
                Require((bool)compare.Invoke(value,new[]{(object)1L,pass}) && (bool)compare.Invoke(equivalent,new[]{(object)1L,pass}),"Two independently captured equal baselines share only this synchronous comparison proof.");
                bool measure=(bool)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionPipeProtocol",true).GetField("Measure",Flags).GetValue(null);
                long reads=(long)passType.GetField("TilesRead",Flags).GetValue(pass),hits=(long)passType.GetField("ChunkHits",Flags).GetValue(pass);
                Require(reads==(measure?1600:0) && hits==(measure?2:0),"Overlapping baseline comparison reads each live tile once; detailed counters remain OFF when disabled.");
                Console.WriteLine("TERRAIN compare distinct-equal-baselines=2 chunks-each=2 live-tiles="+reads+" reused-chunks="+hits+" old-required-live-tiles=3200 measure="+measure);
                var expandedKeys=new SortedSet<int>(keys){1};object expanded=type.GetMethod("CaptureChunksObserved",Flags).Invoke(null,new object[]{1L,expandedKeys,pass});var expandedChunks=(Array)type.GetField("Chunks",Flags).GetValue(expanded);
                Require(ReferenceEquals(chunks.GetValue(0),expandedChunks.GetValue(0)) && ReferenceEquals(chunks.GetValue(1),expandedChunks.GetValue(2)),"Region growth reuses only chunks proven against live tiles in this call.");
                Require((long)passType.GetField("TilesCaptured",Flags).GetValue(pass)==(measure?1024:0),"Expanded capture reads only its new chunk, not the two already verified chunks.");
                foreach(string field in new[]{"type","wall","liquid","sTileHeader","bTileHeader","bTileHeader2","bTileHeader3","frameX","frameY"})
                {
                    var f=typeof(Tile).GetField(field,Flags);var tile=Main.tile[0,0];object original=f.GetValue(tile);f.SetValue(tile,Convert.ChangeType(Convert.ToInt32(original)^1,f.FieldType));passType.GetMethod("Clear",Flags).Invoke(pass,null);
                    Require(!(bool)compare.Invoke(value,new[]{(object)1L,pass}),"Next update must read and reject a changed tile field: "+field);
                    object changed=type.GetMethod("CaptureChunks",Flags).Invoke(null,new object[]{1L,keys});Require((bool)compare.Invoke(changed,new[]{(object)1L,pass}),"Failed first comparison cannot poison another current baseline.");
                    f.SetValue(tile,original);
                }
                passType.GetMethod("Clear",Flags).Invoke(pass,null);Require(!(bool)compare.Invoke(value,new[]{(object)2L,pass}),"No proof crosses a world session.");
                Main.tile[0,0].wall++;Require(!(bool)type.GetMethod("IsCurrent",Flags).Invoke(value,new object[]{1L}),"A sampled tile change immediately retires the baseline.");Main.tile[0,0].wall--;
            }
            finally{Main.tile[50,50]=prior;}
        }
        private static void CheckSessionAges(Assembly host)
        {
            // Exercise the real owner/cache state machine with owned decoded
            // values. No process or simulated game update is claimed here:
            // IPC and changing-world equality have separate production tests.
            NativeCombatWorkerChecks.Scene(false);
            var owner=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionSession",true);
            var requestType=owner.GetNestedType("Request",Flags);
            var workerType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWorkerClient",true);
            var replyType=workerType.GetNestedType("DecodedReply",Flags);
            var resultType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true);
            var impactType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeImpactProof",true);
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            var frameType=alignment.GetNestedType("Frame",Flags);
            var observe=alignment.GetMethod("Observe",Flags);var presentation=alignment.GetMethod("Presentation",Flags);
            var terrainType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot",true);
            Action<object,string,object> set=(value,name,data)=>value.GetType().GetField(name,Flags).SetValue(value,data);
            var npc=Main.npc[0];var identity=new NpcIdentity(1,npc,0,1,npc.type,npc.netID);
            var valueIdentity=new NpcIdentity(1,null,0,1,npc.type,npc.netID);
            foreach(int age in new[]{60,61,80})
            {
                using(var wake=new System.Threading.AutoResetEvent(false))
                {
                bool natural=age==80;long now=1000+(natural?60:age);
                var cache=new JueMingR.Features.Combat.NpcPredictionCache();cache.Demand(0,120);
                object session=Activator.CreateInstance(owner,Flags,null,new object[]{null,cache},null);
                object worker=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(workerType);
                set(worker,"gate",new object());set(worker,"wake",wake);set(worker,"state",natural?2:3);
                set(session,"<Worker>k__BackingField",worker);set(session,"current",identity);set(session,"lastTick",now-1);set(session,"lastAttempt",now);
                ((SortedSet<int>)owner.GetField("npcs",Flags).GetValue(session)).Add(0);
                object request=Activator.CreateInstance(requestType,true);
                set(request,"Identity",identity);set(request,"Tick",1000L);set(request,"Wall",System.Diagnostics.Stopwatch.GetTimestamp());
                set(request,"Npcs",new[]{0});set(request,"Projectiles",new int[0]);
                // This static age fixture has no hit sources or events; supply
                // the same owned, nonnull proof objects as capture and decoding.
                set(request,"Impacts",Activator.CreateInstance(impactType,Flags,null,new object[]{new int[0]},null));
                set(request,"Queries",host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcEligibility",true).GetMethod("Capture",Flags).Invoke(null,new object[]{new[]{0}}));
                set(request,"Terrain",terrainType.GetMethod("Capture",Flags).Invoke(null,new object[]{1L,0,0,31,31}));
                Array frames=Array.CreateInstance(frameType,181);
                for(int i=0;i<181;i++)frames.SetValue(i<=60?observe.Invoke(null,new object[]{1000L+i,new[]{0},new int[0],0}):presentation.Invoke(null,new object[]{1000L+i,0}),i);
                var history=(System.Collections.IList)requestType.GetField("History",Flags).GetValue(request);
                for(int i=0;i<Math.Min(age,61);i++)history.Add(frames.GetValue(i));
                var points=Enumerable.Range(0,natural?80:181).Select(i=>new NpcTrajectoryPoint(i,new NpcMotionState{Identity=valueIdentity,X=npc.position.X,Y=npc.position.Y,Width=npc.width,Height=npc.height})).ToArray();
                var trajectory=new NpcTrajectory(valueIdentity,1000,1,PredictionAssumption.None,natural?PredictionStop.Despawn:PredictionStop.None,points,points.Length);
                object result=Activator.CreateInstance(resultType,true);set(result,"Frames",frames);set(result,"Trajectory",natural?typeof(NpcTrajectory).GetMethod("BindIdentity").Invoke(trajectory,new object[]{identity}):trajectory);
                set(result,"Impacts",Array.CreateInstance(impactType.GetNestedType("Hit",Flags),0));
                // This owner-age fixture supplies decoded values, not a terrain
                // simulation. The real decoder always supplies a nonnull usage
                // record; no terrain accesses are asserted for this static input.
                set(result,"TerrainUsage",Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainUsage",true),true));
                ((SortedSet<int>)resultType.GetField("Npcs",Flags).GetValue(result)).Add(0);
                if(natural){set(session,"accepted",result);set(session,"acceptedRequest",request);}
                else
                {
                    object reply=Activator.CreateInstance(replyType,true);set(reply,"Result",result);set(worker,"decodedReply",reply);set(session,"pending",request);
                }
                owner.GetMethod("Prepare",Flags).Invoke(session,new object[]{identity,now});
                Require(!(bool)owner.GetProperty("Failed",Flags).GetValue(session),"Valid decoded age fixture must not fail the owner: "+owner.GetProperty("Reason",Flags).GetValue(session));
                NpcTrajectory shown=cache.Read(0);
                if(age==60)Require(shown!=null && shown.Count==121 && shown.SampleTick==1060 && shown.CaptureTick==1000 && shown.Identity.Equals(identity),"Age-60 receipt publishes the actual remaining 120 steps with its original capture and live identity.");
                else if(age==61)Require(shown==null && (long)owner.GetField("Rejected",Flags).GetValue(session)==1,"Age-61 receipt is retired before it can enter the cache.");
                else
                {
                    Require(shown!=null && shown.Stop==PredictionStop.Despawn && shown.CaptureTick==1000,"A proven natural end keeps its real shorter future at age 60.");
                    owner.GetMethod("Prepare",Flags).Invoke(session,new object[]{identity,1061L});
                    Require(cache.Read(0)==null && owner.GetField("accepted",Flags).GetValue(session)==null,"At age 61 even an already accepted natural end retires without renewing sample time or extending its tail.");
                }
                }
            }
            Console.WriteLine("PASS native session receive age60 / reject age61 / accepted natural-end expiry / original CaptureTick");
        }
        private static void CheckLinks(Assembly host)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionSession",true);
            object session=Activator.CreateInstance(type,Flags,null,new object[]{null,new JueMingR.Features.Combat.NpcPredictionCache()},null);
            var npcs=(SortedSet<int>)type.GetField("npcs",Flags).GetValue(session);var shots=(SortedSet<int>)type.GetField("projectiles",Flags).GetValue(session);
            var gather=type.GetMethod("GatherDependencies",Flags);
            int[] linked=NativeCombatWorkerChecks.Scene(true);npcs.Add(1);gather.Invoke(session,null);
            Require(npcs.SequenceEqual(linked.OrderBy(n=>n)),"A selected Skeletron hand captures its actual head and sibling in one request.");
            NativeCombatWorkerChecks.Scene(false);npcs.Clear();shots.Clear();npcs.Add(0);Main.projectile[0].SetDefaults(1);Main.projectile[0].whoAmI=0;Main.projectile[0].active=true;
            gather.Invoke(session,null);Require(shots.SequenceEqual(new[]{1,2,3}),"Harpy batches the actual lowest inactive pages and excludes unrelated active projectiles.");
        }
        private static void CheckGravitySource(Assembly host)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            var observe=type.GetMethod("Observe",Flags);var difference=type.GetMethod("Difference",Flags);int prior=NPC.brainOfGravity;
            try
            {
                // Keep every entity page and tick identical. Only the selector
                // changes: this must retire a result even before motion differs.
                NativeCombatWorkerChecks.Scene(false);
                foreach(int source in new[]{-1,1,2})foreach(int next in new[]{-1,1,2})
                {
                    NPC.brainOfGravity=source;object before=observe.Invoke(null,new object[]{1000L,new[]{0,1,2},new int[0],0});
                    NPC.brainOfGravity=next;object after=observe.Invoke(null,new object[]{1000L,new[]{0,1,2},new int[0],0});
                    string changed=(string)difference.Invoke(null,new[]{before,after});
                    Require(source==next?changed==null:changed=="world premise","Gravity-source identity changes retire an otherwise identical observation.");
                }
            }
            finally{NPC.brainOfGravity=prior;}
        }
        private static void CheckSchema(Assembly host,Type sourceType,object source,bool statics=false)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeValueSnapshot",true);
            var schema=Activator.CreateInstance(type,Flags,null,new object[]{sourceType,statics,true,null},null);
            var scalar=type.GetMethod("WriteScalar",Flags);byte[] expected,actual;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write((string)type.GetField("Schema",Flags).GetValue(schema));
                foreach(var field in (FieldInfo[])type.GetField("fields",Flags).GetValue(schema))
                {
                    var value=field.GetValue(source);if(!field.FieldType.IsArray){scalar.Invoke(null,new[]{writer,field.FieldType,value});continue;}
                    var array=(Array)value;writer.Write(array==null?-1:array.Length);
                    if(array!=null)foreach(object item in array)scalar.Invoke(null,new[]{writer,field.FieldType.GetElementType(),item});
                }
                writer.Flush();expected=stream.ToArray();
            }
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){type.GetMethod("Write",Flags).Invoke(schema,new[]{writer,source});writer.Flush();actual=stream.ToArray();}
            Require(expected.SequenceEqual(actual),"Compiled field plan preserves the prior independent reflection format for "+sourceType.FullName);
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
