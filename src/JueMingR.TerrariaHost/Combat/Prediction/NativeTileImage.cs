using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Runs BEFORE the worker loads Terraria. Only the authenticated private
    // byte image changes; the authenticated worker may cache verified output
    // in its own local material directory. The original game is never changed.
    // Harmony already contains this metadata engine. Its internal API is tied
    // to the locked Harmony image, not a new separately installed dependency.
    // This type's signatures must remain BCL-only so loading it cannot trigger
    // the original assembly or any of its static initializers prematurely.
    internal static class NativeTileImage
    {
        internal const string GuardName="JueMingR.PrivatePrediction.TileGuard";
        private const string GameHash="960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";
        internal static byte[] Rewrite(byte[] original,Assembly harmony)
        {
            var timing=PredictionPipeProtocol.Measure?System.Diagnostics.Stopwatch.StartNew():null;
            using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(original)).Replace("-","")!=GameHash)throw new InvalidDataException("Private image identity.");
            var api=new MetadataApi(harmony);
            using(var input=new MemoryStream(original,false))
            using(var module=(IDisposable)api.Static("ModuleDefinition","ReadModule",input))
            {
                if((Guid)api.Get(module,"Mvid")!=new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"))throw new InvalidDataException("Private image MVID.");
                var targets=new List<Tuple<object,int[]>>();int total=0,blocked=0;
                using(var resource=typeof(NativeTileImage).Assembly.GetManifestResourceStream("JueMingR.Prediction.NativeTileManifest"))
                using(var reader=new StreamReader(resource??throw new InvalidDataException("Tile manifest missing.")))
                {
                    reader.ReadLine();if(reader.ReadLine()!="identity|"+GameHash+"|"+api.Get(module,"Mvid"))throw new InvalidDataException("Tile manifest identity.");
                    string line;
                    while((line=reader.ReadLine())!=null)
                    {
                        var values=line.Split('|');
                        if(values[0]=="blocked")
                        {
                            if(values.Length!=4 || !values[3].StartsWith("Terraria.Testing.Cloning.DeepCloneCodegen.",StringComparison.Ordinal))throw new InvalidDataException("Unaudited generic array path.");
                            blocked++;continue;
                        }
                        if(values.Length!=6)throw new InvalidDataException("Tile manifest row.");
                        object method=api.Call(module,"LookupToken",int.Parse(values[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture));
                        string name=(string)api.Get(api.Get(method,"DeclaringType"),"FullName")+"."+api.Get(method,"Name");
                        // Cecil uses '/' for nested types; reflection uses '+'.
                        if(name.Replace('/','+')!=values[5])throw new InvalidDataException("Tile method identity.");
                        var counts=new[]{int.Parse(values[2],CultureInfo.InvariantCulture),int.Parse(values[3],CultureInfo.InvariantCulture),int.Parse(values[4],CultureInfo.InvariantCulture)};
                        targets.Add(Tuple.Create(method,counts));total+=counts.Sum();
                    }
                }
                if(targets.Count!=1298 || total!=13750 || blocked!=4)throw new InvalidDataException("Incomplete private-image inventory.");
                var native=new object[3];
                foreach(var target in targets)foreach(object instruction in (IEnumerable)api.Get(api.Get(target.Item1,"Body"),"Instructions"))
                {int index=Access(api,api.Get(instruction,"Operand"));if(index>=0)native[index]=api.Get(instruction,"Operand");}
                if(native[0]==null || native[1]==null)throw new InvalidDataException("Native tile array signatures.");
                // The locked image has no Tile[,] Address callsite. Retain a
                // true by-reference guard for its explicit boundary self-test;
                // a future image still requires a newly audited manifest.
                if(native[2]==null)
                {
                    native[2]=api.New("MethodReference","Address",api.New("ByReferenceType",api.Get(native[0],"ReturnType")),api.Get(native[0],"DeclaringType"));
                    api.Set(native[2],"HasThis",true);
                    foreach(object parameter in (IEnumerable)api.Get(native[0],"Parameters"))api.Call(api.Get(native[2],"Parameters"),"Add",api.New("ParameterDefinition",api.Get(parameter,"ParameterType")));
                }
                object[] guards=BuildGuard(api,module,native);
                foreach(var target in targets)
                {
                    var actual=new int[3];
                    foreach(object instruction in (IEnumerable)api.Get(api.Get(target.Item1,"Body"),"Instructions"))
                    {
                        int index=Access(api,api.Get(instruction,"Operand"));if(index<0)continue;
                        actual[index]++;api.Set(instruction,"Operand",guards[index]);api.Set(instruction,"OpCode",api.Opcode("Call"));
                    }
                    if(!actual.SequenceEqual(target.Item2))throw new InvalidDataException("Private image tile access count changed.");
                }
                Stage(timing,"tile-rewrite");NativePresentationImage.Rewrite(api,module);Stage(timing,"presentation-rewrite");
                NativeEntityImage.Rewrite(api,module);Stage(timing,"entity-rewrite");
                using(var output=new MemoryStream())
                {
                    // Cecil otherwise stamps wall time into the PE header.
                    // Stable output permits a trusted, code-owned digest.
                    object options=api.New("WriterParameters");api.Set(options,"Timestamp",(uint)0);
                    api.Call(module,"Write",output,options);byte[] result=output.ToArray();Stage(timing,"write");
                    VerifyOutput(api,result,targets);Stage(timing,"verify-written");
                    using(var sha=SHA256.Create())Console.Error.WriteLine("MATERIAL audited-sha256="+BitConverter.ToString(sha.ComputeHash(result)).Replace("-",""));
                    return result;
                }
            }
        }
        private static void Stage(System.Diagnostics.Stopwatch watch,string name)
        {if(watch!=null){Console.Error.WriteLine("MATERIAL stage="+name+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture));watch.Restart();}}
        private static void VerifyOutput(MetadataApi api,byte[] image,List<Tuple<object,int[]>> originals)
        {
            using(var input=new MemoryStream(image,false))using(var module=(IDisposable)api.Static("ModuleDefinition","ReadModule",input))
            {
                if((Guid)api.Get(module,"Mvid")!=new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"))throw new InvalidDataException("Private image MVID changed during writing.");
                foreach(var original in originals)
                {
                    int token=(int)api.Call(api.Get(original.Item1,"MetadataToken"),"ToInt32");
                    object method=api.Call(module,"LookupToken",token);
                    if((string)api.Get(method,"FullName")!=(string)api.Get(original.Item1,"FullName"))throw new InvalidDataException("Private image method token moved.");
                    int[] counts=new int[3];
                    foreach(object instruction in (IEnumerable)api.Get(api.Get(method,"Body"),"Instructions"))
                    {
                        object operand=api.Get(instruction,"Operand");
                        if(Access(api,operand)>=0)throw new InvalidDataException("Unguarded native tile access after writing.");
                        if(operand==null || !api.Type("MethodReference").IsInstanceOfType(operand) || (string)api.Get(api.Get(operand,"DeclaringType"),"FullName")!=GuardName)continue;
                        string name=(string)api.Get(operand,"Name");int index=name=="Get"?0:name=="Set"?1:name=="Address"?2:-1;
                        if(index<0)throw new InvalidDataException("Unexpected guard call.");counts[index]++;
                    }
                    if(!counts.SequenceEqual(original.Item2))throw new InvalidDataException("Guard calls changed during image writing.");
                }
                object guard=api.Call(module,"GetType",GuardName);int raw=0;
                foreach(object method in (IEnumerable)api.Get(guard,"Methods"))foreach(object instruction in (IEnumerable)api.Get(api.Get(method,"Body"),"Instructions"))if(Access(api,api.Get(instruction,"Operand"))>=0)raw++;
                if(raw!=4)throw new InvalidDataException("Guard intrinsic access shape.");
                foreach(object reference in (IEnumerable)api.Get(module,"AssemblyReferences"))if(((string)api.Get(reference,"Name")).StartsWith("JueMingR.",StringComparison.Ordinal))throw new InvalidDataException("Private image must not reference product assemblies.");
                NativeEntityImage.VerifyWritten(api,module);
                NativePresentationImage.VerifyWritten(api,module);
            }
        }
        private static int Access(MetadataApi api,object operand)
        {
            if(operand==null || !api.Type("MethodReference").IsInstanceOfType(operand))return -1;
            object owner=api.Get(operand,"DeclaringType");
            if(!(bool)api.Get(owner,"IsArray") || (int)api.Get(owner,"Rank")!=2 || (string)api.Get(api.Get(owner,"ElementType"),"FullName")!="Terraria.Tile")return -1;
            string name=(string)api.Get(operand,"Name");return name=="Get"?0:name=="Set"?1:name=="Address"?2:-1;
        }
        private static object[] BuildGuard(MetadataApi api,object module,object[] native)
        {
            object system=api.Get(module,"TypeSystem"),voidType=api.Get(system,"Void"),integer=api.Get(system,"Int32");
            object guard=api.New("TypeDefinition","JueMingR.PrivatePrediction","TileGuard",api.Enum("TypeAttributes",0x180),api.Get(system,"Object"));
            api.Call(api.Get(module,"Types"),"Add",guard);
            object enabled=Field(api,guard,"Enabled",api.Get(system,"Boolean")),missing=Field(api,guard,"Missing",api.Get(system,"Boolean"));
            object x=Field(api,guard,"MissingX",integer),y=Field(api,guard,"MissingY",integer);
            object observed=Field(api,guard,"Observed",api.Call(module,"ImportReference",typeof(Action<int,int>)));
            #if JMR_AIM_DIAGNOSTICS
            object diagnosticMissing=Field(api,guard,"DiagnosticMissing",api.Call(module,"ImportReference",typeof(Action<int,int>)));
#endif
            object array=api.Get(native[0],"DeclaringType");
            object main=api.Call(module,"GetType","Terraria.Main");
            object tiles=((IEnumerable)api.Get(main,"Fields")).Cast<object>().Single(f=>(string)api.Get(f,"Name")=="tile");
            object check=Method(api,guard,"Check",voidType,new[]{array,integer,integer});
            object instructions=api.Get(api.Get(check,"Body"),"Instructions");
            object done=api.Instruction("Ret"),failure=api.Instruction("Ldstr","Unobserved native terrain."),record=api.Instruction("Ldsfld",observed);
            Add(api,instructions,"Ldsfld",enabled);Add(api,instructions,"Brfalse",done);
            Add(api,instructions,"Ldarg_0");Add(api,instructions,"Ldsfld",tiles);Add(api,instructions,"Bne_Un",done);
            Add(api,instructions,"Ldarg_0");Add(api,instructions,"Ldarg_1");Add(api,instructions,"Ldarg_2");Add(api,instructions,"Call",native[0]);Add(api,instructions,"Brtrue",record);
            Add(api,instructions,"Ldsfld",missing);Add(api,instructions,"Brtrue",failure);
            Add(api,instructions,"Ldc_I4_1");Add(api,instructions,"Stsfld",missing);
            Add(api,instructions,"Ldarg_1");Add(api,instructions,"Stsfld",x);Add(api,instructions,"Ldarg_2");Add(api,instructions,"Stsfld",y);
#if JMR_AIM_DIAGNOSTICS
            Add(api,instructions,"Ldsfld",diagnosticMissing);Add(api,instructions,"Brfalse",failure);
            Add(api,instructions,"Ldsfld",diagnosticMissing);Add(api,instructions,"Ldarg_1");Add(api,instructions,"Ldarg_2");Add(api,instructions,"Callvirt",api.Call(module,"ImportReference",typeof(Action<int,int>).GetMethod("Invoke")));
#endif
            api.Call(instructions,"Add",failure);
            object exception=api.Call(module,"ImportReference",typeof(InvalidDataException).GetConstructor(new[]{typeof(string)}));
            Add(api,instructions,"Newobj",exception);Add(api,instructions,"Throw");
            // Only an enabled private Main.tile access reaches this BCL
            // delegate. Unrelated arrays, outside-world and unknown cells keep
            // their original behavior; the image has no product reference.
            api.Call(instructions,"Add",record);Add(api,instructions,"Ldarg_1");Add(api,instructions,"Ldarg_2");
            Add(api,instructions,"Callvirt",api.Call(module,"ImportReference",typeof(Action<int,int>).GetMethod("Invoke")));api.Call(instructions,"Add",done);
            var guards=new object[3];
            for(int i=0;i<3;i++)
            {
                var parameters=new List<object>{array,integer,integer};if(i==1)parameters.Add(api.Get(native[0],"ReturnType"));
                object method=Method(api,guard,i==0?"Get":i==1?"Set":"Address",api.Get(native[i],"ReturnType"),parameters.ToArray());guards[i]=method;
                object code=api.Get(api.Get(method,"Body"),"Instructions");
                Add(api,code,"Ldarg_0");Add(api,code,"Ldarg_1");Add(api,code,"Ldarg_2");Add(api,code,"Call",check);
                Add(api,code,"Ldarg_0");Add(api,code,"Ldarg_1");Add(api,code,"Ldarg_2");if(i==1)Add(api,code,"Ldarg_3");
                Add(api,code,"Call",native[i]);Add(api,code,"Ret");
            }
            return guards;
        }
        private static object Field(MetadataApi api,object owner,string name,object type)
        {object field=api.New("FieldDefinition",name,api.Enum("FieldAttributes",0x16),type);api.Call(api.Get(owner,"Fields"),"Add",field);return field;}
        private static object Method(MetadataApi api,object owner,string name,object result,object[] parameters)
        {
            object method=api.New("MethodDefinition",name,api.Enum("MethodAttributes",0x93),result);
            foreach(var parameter in parameters)api.Call(api.Get(method,"Parameters"),"Add",api.New("ParameterDefinition",parameter));
            api.Call(api.Get(owner,"Methods"),"Add",method);return method;
        }
        private static void Add(MetadataApi api,object instructions,string opcode,params object[] operand)
        {api.Call(instructions,"Add",api.Instruction(opcode,operand));}

        internal sealed class MetadataApi
        {
            private readonly Assembly assembly;
            private readonly Dictionary<string,Type> types=new Dictionary<string,Type>();
            private readonly Dictionary<string,object> opcodes=new Dictionary<string,object>();
            private Func<object,int> readCode;
            private readonly Dictionary<int,string> codeNames=new Dictionary<int,string>();
            private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            internal MetadataApi(Assembly assembly){this.assembly=assembly;}
            internal Type Type(string name){Type value;if(!types.TryGetValue(name,out value)){value=assembly.GetType("Mono.Cecil."+name,true);types.Add(name,value);}return value;}
            internal object Enum(string name,int value){return System.Enum.ToObject(Type(name),value);}
            internal string Code(object instruction)
            {
                if(readCode==null)
                {
                    Type owner=Type("Cil.Instruction");var opcode=Property(owner,"OpCode");var code=Property(opcode.PropertyType,"Code");
                    foreach(object value in System.Enum.GetValues(code.PropertyType))codeNames.Add(Convert.ToInt32(value,CultureInfo.InvariantCulture),value.ToString());
                    // Avoid boxing the OpCode struct and its Code enum at
                    // every visited IL instruction. This reads the same actual
                    // property after each mutation, not a stale instruction map.
                    var bridge=new DynamicMethod("PredictionMetadataCode",typeof(int),new[]{typeof(object)},typeof(NativeTileImage).Module,true);var il=bridge.GetILGenerator();var local=il.DeclareLocal(opcode.PropertyType);
                    il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,owner);il.Emit(OpCodes.Call,opcode.GetGetMethod(true));il.Emit(OpCodes.Stloc,local);il.Emit(OpCodes.Ldloca,local);
                    il.Emit(OpCodes.Call,code.GetGetMethod(true));il.Emit(OpCodes.Conv_I4);il.Emit(OpCodes.Ret);readCode=(Func<object,int>)bridge.CreateDelegate(typeof(Func<object,int>));
                }
                return codeNames[readCode(instruction)];
            }
            internal object New(string name,params object[] args){return Activator.CreateInstance(Type(name),BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,args,CultureInfo.InvariantCulture);}
            private static PropertyInfo Property(Type owner,string name)
            {
                for(Type type=owner;type!=null;type=type.BaseType)
                {var value=type.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(value!=null)return value;}
                throw new InvalidOperationException("Locked metadata property missing: "+owner.FullName+"."+name);
            }
            private readonly Dictionary<Type,Dictionary<string,Func<object,object>>> getters=new Dictionary<Type,Dictionary<string,Func<object,object>>>();
            internal object Get(object target,string name)
            {
                Type owner=target.GetType();Dictionary<string,Func<object,object>> members;
                if(!getters.TryGetValue(owner,out members)){members=new Dictionary<string,Func<object,object>>();getters.Add(owner,members);}
                Func<object,object> read;
                if(!members.TryGetValue(name,out read))
                {
                    var property=Property(owner,name);var getter=property.GetGetMethod(true);
                    var bridge=new DynamicMethod("PredictionMetadataRead",typeof(object),new[]{typeof(object)},typeof(NativeTileImage).Module,true);var il=bridge.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);il.Emit(getter.DeclaringType.IsValueType?OpCodes.Unbox:OpCodes.Castclass,getter.DeclaringType);
                    il.Emit(getter.IsVirtual?OpCodes.Callvirt:OpCodes.Call,getter);if(property.PropertyType.IsValueType)il.Emit(OpCodes.Box,property.PropertyType);il.Emit(OpCodes.Ret);
                    read=(Func<object,object>)bridge.CreateDelegate(typeof(Func<object,object>));members.Add(name,read);
                }
                return read(target);
            }
            private readonly Dictionary<Type,Dictionary<string,Action<object,object>>> setters=new Dictionary<Type,Dictionary<string,Action<object,object>>>();
            internal void Set(object target,string name,object value)
            {
                Type owner=target.GetType();Dictionary<string,Action<object,object>> members;
                if(!setters.TryGetValue(owner,out members)){members=new Dictionary<string,Action<object,object>>();setters.Add(owner,members);}
                Action<object,object> write;
                if(!members.TryGetValue(name,out write))
                {
                    var property=Property(owner,name);var setter=property.GetSetMethod(true);
                    var bridge=new DynamicMethod("PredictionMetadataWrite",typeof(void),new[]{typeof(object),typeof(object)},typeof(NativeTileImage).Module,true);var il=bridge.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);il.Emit(setter.DeclaringType.IsValueType?OpCodes.Unbox:OpCodes.Castclass,setter.DeclaringType);
                    il.Emit(OpCodes.Ldarg_1);il.Emit(property.PropertyType.IsValueType?OpCodes.Unbox_Any:OpCodes.Castclass,property.PropertyType);
                    il.Emit(setter.IsVirtual?OpCodes.Callvirt:OpCodes.Call,setter);il.Emit(OpCodes.Ret);
                    write=(Action<object,object>)bridge.CreateDelegate(typeof(Action<object,object>));members.Add(name,write);
                }
                write(target,value);
            }
            internal object Call(object target,string name,params object[] args){return Invoke(target.GetType(),target,name,args);}
            internal object Static(string type,string name,params object[] args){return Invoke(Type(type),null,name,args);}
            internal object Opcode(string name){object value;if(!opcodes.TryGetValue(name,out value)){value=Type("Cil.OpCodes").GetField(name,BindingFlags.Public|BindingFlags.Static).GetValue(null);opcodes.Add(name,value);}return value;}
            internal object Instruction(string opcode,params object[] operand)
            {var args=new object[operand.Length+1];args[0]=Opcode(opcode);Array.Copy(operand,0,args,1,operand.Length);return Static("Cil.Instruction","Create",args);}
            private static readonly Dictionary<Type,Dictionary<string,MethodInfo[]>> methodsByType=new Dictionary<Type,Dictionary<string,MethodInfo[]>>();
            private static object Invoke(Type type,object target,string name,object[] args)
            {
                Dictionary<string,MethodInfo[]> members;MethodInfo[] candidates;
                if(!methodsByType.TryGetValue(type,out members)){members=new Dictionary<string,MethodInfo[]>();methodsByType.Add(type,members);}
                string key=name+":"+(target==null)+":"+args.Length;
                if(!members.TryGetValue(key,out candidates))
                {candidates=type.GetMethods(Flags).Where(m=>m.Name==name && m.IsStatic==(target==null) && !m.ContainsGenericParameters && m.GetParameters().Length==args.Length).ToArray();members.Add(key,candidates);}
                if(candidates.Length==1)return candidates[0].Invoke(target,args);
                var methods=candidates.Where(m=>m.GetParameters().Select((p,i)=>args[i]==null?!p.ParameterType.IsValueType:p.ParameterType.IsInstanceOfType(args[i])).All(x=>x)).ToArray();
                if(methods.Length!=1)throw new InvalidOperationException("Locked metadata API mismatch: "+type.FullName+"."+name+" candidates="+methods.Length);
                return methods[0].Invoke(target,args);
            }
        }
    }
}
