using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Terraria;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Explicit isolated experiment: permission checks use the authenticated
    // private CLR image; timing replays fixed captured bytes with no hooks.
    internal static class NativeCombatDirectoryFastPathChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Type directory,eligibility,purpose,research;
        private static Action<object,int,int> dispatch;
        private static Dictionary<int,string> names;
        private static readonly List<string> checks=new List<string>{"case,outcome"};
        internal static void Run(Assembly host,object sandbox,string output)
        {
            directory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            eligibility=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcEligibility",true);
            purpose=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionPurpose",true);
            research=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.ConditionalNpcQuery");
            dispatch=(Action<object,int,int>)Delegate.CreateDelegate(typeof(Action<object,int,int>),typeof(Main).Assembly.GetType("JueMingR.PrivatePrediction.EntityGuard",true).GetMethod("Check",Flags));
            names=(Dictionary<int,string>)directory.GetField("FieldIdentities",Flags).GetValue(null);
            try{Permissions();}
            finally{Call(purpose,"End");Call(directory,"Reset");File.WriteAllLines(Path.Combine(output,"directory-checks.csv"),checks);}
            if(Environment.GetEnvironmentVariable("JUEMINGR_DIRECTORY_EXPERIMENT")=="guards")return;
            Require(research==null,"Ordinary fixed replay must not silently use research policy.");
            Replay(host,sandbox,output);
        }
        private static void Permissions()
        {
            // These existing checks invoke original methods and CLR byrefs,
            // including actual opaque allocation and real damage qualifiers.
            NativeCombatEligibilityChecks.Run(directory.Assembly);
            foreach(bool npc in new[]{true,false})
            {
                string prefix=npc?"NPC":"Projectile";int kind=npc?1:2,slot=npc?3:5,provider=npc?3:206;
                int who=Token("Terraria.Entity.whoAmI"),physical=Token("Terraria.Entity.position");
                foreach(bool full in new[]{false,true})
                {
                    object actor=Scene(npc,full);
                    foreach(int field in (HashSet<int>)directory.GetField(npc?"NpcReads":"ProjectileReads",Flags).GetValue(null))
                        Allow(()=>dispatch(actor,field,0),prefix+" "+full+" directory read "+names[field]);
                    Links();
                    foreach(int mode in new[]{1,2})
                    {
                        Call(purpose,"Begin",64);Call(purpose,"Enter",Main.npc[64]);
                        if(full){Allow(()=>dispatch(actor,who,mode),prefix+" captured directory mode"+mode);Links(mode==1?new[]{Tuple.Create(64,provider),Tuple.Create(provider,64)}:new[]{Tuple.Create(provider,64)});}
                        else Refuse(()=>dispatch(actor,who,mode),kind,slot,who,prefix+" opaque directory mode"+mode);
                    }
                    Call(purpose,"Begin",64);Call(purpose,"Enter",Main.npc[64]);
                    if(full){Allow(()=>dispatch(actor,physical,0),prefix+" captured physical read");Links(Tuple.Create(64,provider));}
                    else Refuse(()=>dispatch(actor,physical,0),kind,slot,physical,prefix+" opaque physical read");
                }
                object captured=Scene(npc,true);
                // Directory permission survives, but the full schema really
                // loses this scalar. A directory-only shortcut would escape.
                WithPermission(npc,false,who,()=>Refuse(()=>dispatch(captured,who,0),kind+2,slot,who,prefix+" missing full permission mode0"));
                Allow(()=>dispatch(captured,who,0),prefix+" restored current full permission");
                Links();
                captured=Scene(npc,true);
                WithPermission(npc,true,who,()=>{Allow(()=>dispatch(captured,who,0),prefix+" missing directory permission still full");Links(Tuple.Create(64,provider));});
                Call(purpose,"Begin",64);Call(purpose,"Enter",Main.npc[64]);
                Allow(()=>dispatch(captured,who,0),prefix+" restored current directory permission");Links();
                object opaque=Scene(npc,false);
                WithPermission(npc,true,who,()=>Refuse(()=>dispatch(opaque,who,0),kind,slot,who,prefix+" opaque missing directory permission"));
                // Opaque directory permission did not historically require
                // the full schema. Missing full permission must fall through.
                WithPermission(npc,false,who,()=>Allow(()=>dispatch(opaque,who,0),prefix+" opaque original directory fallback"));
                foreach(bool full in new[]{false,true})
                {
                    object actor=Scene(npc,full);var entity=(Entity)actor;
                    entity.whoAmI=11;
                    Allow(()=>dispatch(actor,who,0),prefix+" changed slot directory");Links();
                    if(full){Allow(()=>dispatch(actor,physical,0),prefix+" changed slot captured identity fallback");Links(Tuple.Create(64,npc?11:212));}
                    else Refuse(()=>dispatch(actor,physical,0),kind,slot,physical,prefix+" changed slot opaque identity fallback");
                    Scene(npc,full);
                    object born=npc?(object)new NPC{whoAmI=slot}:new Projectile{whoAmI=slot};
                    if(npc)Main.npc[slot]=(NPC)born;else Main.projectile[slot]=(Projectile)born;
                    // The original constructor itself performs guarded stores.
                    // Start this read's proof after construction, not before.
                    Call(purpose,"Begin",64);Call(purpose,"Enter",Main.npc[64]);
                    Allow(()=>dispatch(born,who,0),prefix+" replacement directory");Links();
                    Allow(()=>dispatch(born,physical,0),prefix+" original newborn full-field semantics");Links(Tuple.Create(64,provider));
                }
                Scene(npc,false);object sentinel=npc?(object)Main.npc[Main.maxNPCs]:Main.projectile[Main.maxProjectiles];
                Allow(()=>dispatch(sentinel,who,0),prefix+" directory sentinel");Links();
                Refuse(()=>dispatch(sentinel,physical,0),kind,npc?Main.maxNPCs:Main.maxProjectiles,physical,prefix+" opaque sentinel physical");
            }
            foreach(bool full in new[]{false,true})
            {
                object actor=Scene(true,full);int who=Token("Terraria.Entity.whoAmI");
                Call(eligibility,"AllocationBefore");
                try
                {
                    if(full)Allow(()=>dispatch(actor,who,0),"captured allocation retains original permission");
                    else Refuse(()=>dispatch(actor,who,0),1,3,who,"opaque stable candidate allocation requires page");
                }
                finally{Call(eligibility,"AllocationAfter");}
            }
            if(research!=null)
            {
                object actor=Scene(true,true);int who=Token("Terraria.Entity.whoAmI");
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {writer.Write(1);writer.Write(64);writer.Flush();stream.Position=0;using(var reader=new BinaryReader(stream))Call(research,"ReadRoles",reader);}
                Call(research,"Begin",new[]{3,64},64);
                long before=(long)research.GetField("QueryReads",Flags).GetValue(null);
                Allow(()=>dispatch(actor,who,0),"research directory condition precedes fast path");
                Require((long)research.GetField("QueryReads",Flags).GetValue(null)==before+1,"Research predicate must actually execute before a directory shortcut.");
                foreach(int mode in new[]{1,2})Refuse(()=>dispatch(actor,who,mode),5,3,who,"research rejects directory mode"+mode);
                var allowed=(HashSet<int>)research.GetField("readOnlyValues",Flags).GetValue(null);Require(allowed.Remove(who),"Research read premise fixture.");
                try{Refuse(()=>dispatch(actor,who,0),5,3,who,"research denied mode0 cannot escape through ordinary permission");}
                finally{allowed.Add(who);}
            }
            Console.WriteLine("PASS directory permissions cases="+(checks.Count-1)+" research="+(research!=null)+"; actual private image, no simulated ABI");
        }
        private static object Scene(bool npc,bool full)
        {
            Call(purpose,"End");Call(directory,"Reset");
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{whoAmI=i};
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{whoAmI=i};
            Main.npc[3].SetDefaults(678);Main.npc[3].whoAmI=3;Main.npc[3].active=true;
            Main.npc[64].SetDefaults(1);Main.npc[64].whoAmI=64;Main.npc[64].active=true;
            Main.projectile[5].SetDefaults(1);Main.projectile[5].whoAmI=5;Main.projectile[5].active=true;Main.projectile[5].owner=0;
            byte[] bytes;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {Call(directory,"Write",writer);Call(eligibility,"Write",writer);writer.Flush();bytes=stream.ToArray();}
            Call(directory,"Reset");using(var reader=new BinaryReader(new MemoryStream(bytes))){Call(directory,"Read",reader);Call(eligibility,"Read",reader);}
            Call(directory,"KnowNpc",64);if(full)Call(directory,npc?"KnowNpc":"KnowProjectile",npc?3:5);
            Call(directory,"Begin");Call(purpose,"Begin",64);Call(purpose,"Enter",Main.npc[64]);
            return npc?(object)Main.npc[3]:Main.projectile[5];
        }
        private static void WithPermission(bool npc,bool leaf,int field,Action check)
        {
            string name=npc?"Npc":"Projectile";
            var fields=(HashSet<int>)directory.GetField(name+(leaf?"Reads":"Fields"),Flags).GetValue(null);
            var permissions=directory.GetField(name+(leaf?"DirectoryPermissions":"Permissions"),Flags);object original=permissions.GetValue(null);
            Require(fields.Remove(field),"Permission-removal fixture needs a currently allowed field.");
            try{permissions.SetValue(null,Activator.CreateInstance(original.GetType(),Flags,null,new object[]{fields},null));check();}
            finally{fields.Add(field);permissions.SetValue(null,original);ClearMissing();}
        }
        private static void Links(params Tuple<int,int>[] expected)
        {
            var actual=(ulong[,])purpose.GetField("needs",Flags).GetValue(null);var bits=new ulong[actual.GetLength(0),actual.GetLength(1)];
            foreach(var edge in expected)bits[edge.Item1,edge.Item2/64]|=1UL<<(edge.Item2%64);
            Require(actual.Cast<ulong>().SequenceEqual(bits.Cast<ulong>()),"Permission branch changed actual Purpose edges.");
        }
        private static void Allow(Action action,string label){ClearMissing();action();Require((int)directory.GetProperty("MissingKind",Flags).GetValue(null)==0,label);checks.Add(Csv(label,"allowed"));}
        private static void Refuse(Action action,int kind,int slot,int field,string label)
        {
            ClearMissing();bool denied=false;try{action();}catch(Exception error){while(error is TargetInvocationException)error=error.InnerException;denied=error is InvalidDataException;}
            Require(denied && (int)directory.GetProperty("MissingKind",Flags).GetValue(null)==kind && (int)directory.GetProperty("MissingSlot",Flags).GetValue(null)==slot && (int)directory.GetProperty("MissingField",Flags).GetValue(null)==field,label);
            checks.Add(Csv(label,"refused kind="+kind+" slot="+slot));
        }
        private static void ClearMissing(){foreach(string n in new[]{"MissingKind","MissingSlot","MissingField"})directory.GetField("<"+n+">k__BackingField",Flags).SetValue(null,n=="MissingSlot"?-1:0);}
        private static int Token(string name)=>names.Single(p=>p.Value==name).Key;
        private static object Call(Type type,string name,params object[] args)
        {
            var matches=type.GetMethods(Flags).Where(m=>m.Name==name && m.GetParameters().Length==args.Length).ToArray();
            var method=matches.Length==1?matches[0]:matches.Single(m=>m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v));
            return method.Invoke(null,args);
        }
        private static void Replay(Assembly host,object sandbox,string output)
        {
            byte[] request=File.ReadAllBytes(Path.Combine(output,"fixed-request.bin")),expectedCore=File.ReadAllBytes(Path.Combine(output,"expected-core.bin")),expectedProof=File.ReadAllBytes(Path.Combine(output,"expected-alignment.bin"));
            var type=sandbox.GetType();var predict=type.GetMethod("Predict",Flags);var alignment=type.GetProperty("Alignment",Flags);
            var rows=new List<string>{"round,warmup,predictMs,totalMs,resetMs,restoreMs,advanceMs,frames,coreBytes,proofBytes,coreNormalizedSha256,proofNormalizedSha256,rngSha256"};
            byte[] expectedNormalized=NormalizeProof(host,expectedProof,out long ignored);string rngExpected=null;
            try
            {
                for(int round=0;round<9;round++)
                {
                    type.GetMethod("ClearWorld",Flags).Invoke(sandbox,null);
                    var watch=Stopwatch.StartNew();byte[] core=(byte[])predict.Invoke(sandbox,new object[]{request,true});watch.Stop();
                    byte[] proof=(byte[])alignment.GetValue(sandbox);
                    File.WriteAllBytes(Path.Combine(output,"replay-"+round+"-core.bin"),core);File.WriteAllBytes(Path.Combine(output,"replay-"+round+"-alignment.bin"),proof);
                    byte[] normalized=NormalizeProof(host,proof,out long advance);
                    Require(core.Take(core.Length-32).SequenceEqual(expectedCore.Take(expectedCore.Length-32)),"Fixed request core differs beyond measured clocks.");
                    Require(normalized.SequenceEqual(expectedNormalized),"Full alignment/terrain/Purpose/continuation/impact trailer differs beyond Advance clock.");
                    int end=core.Length-32;long total=BitConverter.ToInt64(core,end),frequency=BitConverter.ToInt64(core,end+8),reset=BitConverter.ToInt64(core,end+16),restore=BitConverter.ToInt64(core,end+24);
                    Require(frequency>0 && total>0,"Fixed timing requires explicitly measured ordinary build.");
                    byte[] rng;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){Call(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeRandomSnapshot",true),"Write",writer);writer.Flush();rng=stream.ToArray();}
                    string rngHash=Hash(rng);if(rngExpected==null)rngExpected=rngHash;Require(rngHash==rngExpected,"Fixed original RNG state must repeat.");
                    File.WriteAllBytes(Path.Combine(output,"replay-"+round+"-rng.bin"),rng);
                    rows.Add(Csv(round,round<2,watch.Elapsed.TotalMilliseconds,total*1000.0/frequency,reset*1000.0/frequency,restore*1000.0/frequency,advance*1000.0/frequency,BitConverter.ToInt32(core,16),core.Length,proof.Length,Hash(core.Take(core.Length-32).ToArray()),Hash(normalized),rngHash));
                }
                Console.WriteLine("PASS fixed captured request: two warmups and seven measured complete predictions; core/proof/terrain/Purpose/events exact except clocks");
            }
            finally{File.WriteAllLines(Path.Combine(output,"directory-replay.csv"),rows);}
        }
        private static byte[] NormalizeProof(Assembly host,byte[] proof,out long advance)
        {
            var read=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("Read",Flags);
            using(var stream=new MemoryStream(proof,false))using(var reader=new BinaryReader(stream))
            {
                int count=reader.ReadInt32();Require(count==181,"Fixed accepted input must retain its entire original horizon.");
                for(int i=0;i<count;i++)read.Invoke(null,new object[]{reader});int offset=checked((int)stream.Position);advance=reader.ReadInt64();
                byte[] result=(byte[])proof.Clone();Array.Clear(result,offset,8);return result;
            }
        }
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
