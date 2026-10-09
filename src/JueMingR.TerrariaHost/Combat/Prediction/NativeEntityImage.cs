using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // BCL-only, executed while the authenticated game is still metadata. Keep
    // field instructions in their declaring type so private access and real
    // managed references stay intact. Readonly stores and prefixed accesses
    // stay in the original method; ordinary sites call small checked accessors
    // to avoid inflating and repeatedly JIT-compiling the giant AI dispatchers.
    internal static class NativeEntityImage
    {
        internal const string GuardName="JueMingR.PrivatePrediction.EntityGuard";
        internal static void Rewrite(NativeTileImage.MetadataApi api,object module)
        {
            object system=api.Get(module,"TypeSystem"),integer=api.Get(system,"Int32"),objectType=api.Get(system,"Object");
            object guard=api.New("TypeDefinition","JueMingR.PrivatePrediction","EntityGuard",api.Enum("TypeAttributes",0x180),objectType);
            object action=api.Call(module,"ImportReference",typeof(Action<object,int,int>));
            object handler=api.New("FieldDefinition","Handler",api.Enum("FieldAttributes",0x16),action);
            api.Call(api.Get(guard,"Fields"),"Add",handler);
            object check=api.New("MethodDefinition","Check",api.Enum("MethodAttributes",0x93),api.Get(system,"Void"));
            foreach(object parameter in new[]{objectType,integer,integer})api.Call(api.Get(check,"Parameters"),"Add",api.New("ParameterDefinition",parameter));
            api.Call(api.Get(guard,"Methods"),"Add",check);
            object code=api.Get(api.Get(check,"Body"),"Instructions"),done=api.Instruction("Ret");
            Add(api,code,"Ldsfld",handler);Add(api,code,"Brfalse",done);
            Add(api,code,"Ldsfld",handler);Add(api,code,"Ldarg_0");Add(api,code,"Ldarg_1");Add(api,code,"Ldarg_2");
            Add(api,code,"Callvirt",api.Call(module,"ImportReference",typeof(Action<object,int,int>).GetMethod("Invoke")));api.Call(code,"Add",done);
            object canClear=CreateColumnGuard(api,module,guard,system,objectType);
            // Snapshot the original method set before adding the guard. Only
            // fixed native Entity/NPC/Projectile instance field definitions are
            // relevant; value types and other game entities remain untouched.
            var methods=Types(api,api.Get(module,"Types")).SelectMany(t=>((IEnumerable)api.Get(t,"Methods")).Cast<object>()).Where(m=>(bool)api.Get(m,"HasBody")).ToArray();
            int reads=0,addresses=0,writes=0,columns=0;var fieldIdentities=new SortedDictionary<int,string>();var accessors=new Dictionary<string,object>();
            foreach(object method in methods)
            {
                object body=api.Get(method,"Body"),instructions=api.Get(body,"Instructions");
                var original=((IEnumerable)instructions).Cast<object>().ToArray();
                var redirects=new Dictionary<object,object>();var insertions=new Dictionary<object,List<object>>();bool changed=false;
                foreach(object instruction in original)
                {
                    string opcode=api.Code(instruction);
                    int mode=opcode=="Ldfld"?0:opcode=="Ldflda"?1:opcode=="Stfld"?2:-1;if(mode<0)continue;
                    object field=api.Get(instruction,"Operand");if(!IsEntityField(api,field))continue;
                    object definition=field;
                    if(!api.Type("FieldDefinition").IsInstanceOfType(definition))
                    {
                        object owner=api.Call(module,"GetType",api.Get(api.Get(field,"DeclaringType"),"FullName"));
                        definition=((IEnumerable)api.Get(owner,"Fields")).Cast<object>().Single(f=>(string)api.Get(f,"Name")== (string)api.Get(field,"Name"));
                    }
                    int token=(int)api.Call(api.Get(definition,"MetadataToken"),"ToInt32");
                    fieldIdentities[token]=(string)api.Get(api.Get(definition,"DeclaringType"),"FullName")+"."+api.Get(definition,"Name");
                    if(mode==0)reads++;else if(mode==1)addresses++;else writes++;
                    object start=instruction,previous=api.Get(start,"Previous");
                    while(previous!=null && api.Get(api.Get(previous,"OpCode"),"OpCodeType").ToString()=="Prefix")
                    {start=previous;previous=api.Get(start,"Previous");}
                    if(ReferenceEquals(start,instruction) && !(mode==2 && (bool)api.Get(definition,"IsInitOnly")) && !IsColumnReset(api,method,instruction))
                    {
                        string key=token+"_"+mode;object accessor;
                        if(!accessors.TryGetValue(key,out accessor)){accessor=Accessor(api,definition,token,mode,check,system);accessors.Add(key,accessor);}
                        api.Set(instruction,"OpCode",api.Opcode("Call"));api.Set(instruction,"Operand",accessor);changed=true;continue;
                    }
                    var before=new List<object>();object value=null;
                    if(mode==2)
                    {
                        value=api.New("Cil.VariableDefinition",api.Get(field,"FieldType"));api.Call(api.Get(body,"Variables"),"Add",value);
                        before.Add(api.Instruction("Stloc",value));
                    }
                    before.Add(api.Instruction("Dup"));before.Add(api.Instruction("Ldc_I4",token));before.Add(api.Instruction("Ldc_I4",mode));before.Add(api.Instruction("Call",check));
                    if(value!=null)before.Add(api.Instruction("Ldloc",value));
                    if(IsColumnReset(api,method,instruction))
                    {
                        // This is the sole bulk store whose opaque receiver is
                        // intentionally untouched. It is never promoted here:
                        // a later read/reuse still needs a complete new request.
                        object resume=ColumnResume(api,method,instruction);
                        before.InsertRange(0,new[]{api.Instruction("Dup"),api.Instruction("Call",canClear),api.Instruction("Brtrue",before[0]),api.Instruction("Pop"),api.Instruction("Br",resume)});
                        columns++;
                    }
                    insertions.Add(start,before);
                    redirects[start]=before[0];changed=true;
                }
                if(!changed)continue;
                // Rebuild once. Repeated Insert/IndexOf on the large original
                // AI dispatch method otherwise makes image preparation quadratic.
                if(insertions.Count!=0)
                {
                    api.Call(instructions,"Clear");
                    foreach(object instruction in original)
                    {List<object> before;if(insertions.TryGetValue(instruction,out before))foreach(object item in before)api.Call(instructions,"Add",item);api.Call(instructions,"Add",instruction);}
                }
                // Most changes replace an operand in place. Their existing
                // collection/order is already correct; rebuilding it performs
                // hundreds of thousands of reflected Add calls for no change.
                // Insertion can overflow short branches. Expand them before
                // Cecil writes offsets, and redirect every incoming edge and
                // exception boundary to include the receiver check.
                foreach(object instruction in ((IEnumerable)instructions).Cast<object>())
                {
                    object op=api.Get(instruction,"OpCode"),operand=api.Get(instruction,"Operand"),replacement;
                    if(api.Get(op,"OperandType").ToString()=="ShortInlineBrTarget")
                    {string name=api.Get(op,"Code").ToString();api.Set(instruction,"OpCode",api.Opcode(name.Substring(0,name.Length-2)));}
                    if(operand!=null && redirects.TryGetValue(operand,out replacement))api.Set(instruction,"Operand",replacement);
                    else if(operand is Array)
                    {var branches=(Array)operand;for(int i=0;i<branches.Length;i++)if(redirects.TryGetValue(branches.GetValue(i),out replacement))branches.SetValue(replacement,i);}
                }
                foreach(object handlerBody in (IEnumerable)api.Get(body,"ExceptionHandlers"))
                    foreach(string boundary in new[]{"TryStart","TryEnd","HandlerStart","HandlerEnd","FilterStart"})
                    {object old=api.Get(handlerBody,boundary),replacement;if(old!=null && redirects.TryGetValue(old,out replacement))api.Set(handlerBody,boundary,replacement);}
            }
            if(reads==0 || addresses==0 || writes==0)throw new InvalidDataException("Empty entity field inventory.");
            if(reads!=59836 || addresses!=18834 || writes!=25420)throw new InvalidDataException("Locked entity field inventory changed.");
            if(columns!=1)throw new InvalidDataException("Native immunity column store changed.");
            object entity=api.Call(module,"GetType","Terraria.Entity");
            object slotField=((IEnumerable)api.Get(entity,"Fields")).Cast<object>().Single(f=>(string)api.Get(f,"Name")=="whoAmI");
            object probe=api.New("MethodDefinition","ProbeSlotAddress",api.Enum("MethodAttributes",0x93),api.New("ByReferenceType",integer));
            api.Call(api.Get(probe,"Parameters"),"Add",api.New("ParameterDefinition",entity));
            object probeCode=api.Get(api.Get(probe,"Body"),"Instructions");
            Add(api,probeCode,"Ldarg_0");Add(api,probeCode,"Dup");Add(api,probeCode,"Ldc_I4",(int)api.Call(api.Get(slotField,"MetadataToken"),"ToInt32"));Add(api,probeCode,"Ldc_I4_1");Add(api,probeCode,"Call",check);Add(api,probeCode,"Ldflda",slotField);Add(api,probeCode,"Ret");
            api.Call(api.Get(guard,"Methods"),"Add",probe);
            // Cecil may renumber FieldDef rows while preserving method tokens.
            // Protocol field IDs therefore refer to this authenticated original
            // name map, never to a rewritten assembly's incidental row number.
            object schema=api.New("FieldDefinition","OriginalFields",api.Enum("FieldAttributes",0x8056),api.Get(system,"String"));
            api.Set(schema,"Constant",string.Join("\n",fieldIdentities.Select(p=>p.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|"+p.Value)));
            api.Call(api.Get(guard,"Fields"),"Add",schema);
            api.Call(api.Get(module,"Types"),"Add",guard);
            Console.Error.WriteLine("ENTITY fields read="+reads+" address="+addresses+" write="+writes+" compact-accessors="+accessors.Count);
        }
        private const string AccessorPrefix="__JmrField_";
        private static object Accessor(NativeTileImage.MetadataApi api,object field,int token,int mode,object check,object system)
        {
            object owner=api.Get(field,"DeclaringType"),valueType=api.Get(field,"FieldType");
            object result=mode==2?api.Get(system,"Void"):mode==1?api.New("ByReferenceType",valueType):valueType;
            object method=api.New("MethodDefinition",AccessorPrefix+token+"_"+mode,api.Enum("MethodAttributes",0x93),result);
            // Without this the CLR may expand the checks back into NPC.AI.
            api.Set(method,"ImplAttributes",api.Enum("MethodImplAttributes",8));
            api.Call(api.Get(method,"Parameters"),"Add",api.New("ParameterDefinition",owner));if(mode==2)api.Call(api.Get(method,"Parameters"),"Add",api.New("ParameterDefinition",valueType));
            object body=api.Get(method,"Body"),code=api.Get(body,"Instructions"),local=null;
            Add(api,code,"Ldarg_0");
            if(mode==2){local=api.New("Cil.VariableDefinition",valueType);api.Call(api.Get(body,"Variables"),"Add",local);Add(api,code,"Ldarg_1");Add(api,code,"Stloc",local);}
            Add(api,code,"Dup");Add(api,code,"Ldc_I4",token);Add(api,code,"Ldc_I4",mode);Add(api,code,"Call",check);
            if(mode==2)Add(api,code,"Ldloc",local);Add(api,code,mode==0?"Ldfld":mode==1?"Ldflda":"Stfld",field);Add(api,code,"Ret");
            api.Call(api.Get(owner,"Methods"),"Add",method);return method;
        }
        internal static void VerifyWritten(NativeTileImage.MetadataApi api,object module)
        {
            int accesses=0,checks=0,inline=0,proxyCalls=0;
            object guard=api.Call(module,"GetType",GuardName);
            object schema=((IEnumerable)api.Get(guard,"Fields")).Cast<object>().Single(f=>(string)api.Get(f,"Name")=="OriginalFields");
            var fieldIdentities=((string)api.Get(schema,"Constant")).Split('\n').Select(line=>line.Split('|')).ToDictionary(p=>int.Parse(p[0],System.Globalization.CultureInfo.InvariantCulture),p=>p[1]);
            foreach(object type in Types(api,api.Get(module,"Types")))
            {
                if((string)api.Get(type,"FullName")==GuardName)continue;
                foreach(object method in (IEnumerable)api.Get(type,"Methods"))
                {
                    if(!(bool)api.Get(method,"HasBody"))continue;
                    bool accessor=((string)api.Get(method,"Name")).StartsWith(AccessorPrefix,StringComparison.Ordinal);int ownAccesses=0;
                    if(accessor && !(bool)api.Get(method,"NoInlining"))throw new InvalidDataException("Field accessor must not inflate caller JIT.");
                    object body=api.Get(method,"Body");var instructions=((IEnumerable)api.Get(body,"Instructions")).Cast<object>().ToArray();var interiors=new HashSet<object>();
                    foreach(object instruction in instructions)
                    {
                        string code=api.Code(instruction);object operand=api.Get(instruction,"Operand");
                        if((code=="Ldfld" || code=="Ldflda" || code=="Stfld") && IsEntityField(api,operand))
                        {
                            if(IsColumnReset(api,method,instruction))VerifyColumnGuard(api,method,instruction);
                            accesses++;ownAccesses++;if(!accessor)inline++;object previous=api.Get(instruction,"Previous");
                            while(previous!=null && api.Get(api.Get(previous,"OpCode"),"OpCodeType").ToString()=="Prefix")previous=api.Get(previous,"Previous");
                            if(code=="Stfld")
                            {if(previous==null || api.Code(previous)!="Ldloc")throw new InvalidDataException("Entity write value reload missing.");previous=api.Get(previous,"Previous");}
                            if(!IsCheck(api,previous))throw new InvalidDataException("Entity field receiver check missing.");
                            object mode=api.Get(previous,"Previous"),token=api.Get(mode,"Previous"),duplicate=api.Get(token,"Previous");
                            if(api.Code(mode)!="Ldc_I4" || (int)api.Get(mode,"Operand")!=(code=="Ldfld"?0:code=="Ldflda"?1:2) || api.Code(token)!="Ldc_I4" || api.Code(duplicate)!="Dup")throw new InvalidDataException("Entity check mode or receiver changed.");
                            string identity;
                            if(!fieldIdentities.TryGetValue((int)api.Get(token,"Operand"),out identity) || identity!=(string)api.Get(api.Get(operand,"DeclaringType"),"FullName")+"."+api.Get(operand,"Name"))throw new InvalidDataException("Entity field check identity changed.");
                            object first=code=="Stfld"?api.Get(duplicate,"Previous"):duplicate;
                            if(code=="Stfld" && api.Code(first)!="Stloc")throw new InvalidDataException("Entity write spill missing.");
                            for(object cursor=api.Get(first,"Next");cursor!=null;cursor=api.Get(cursor,"Next")){interiors.Add(cursor);if(ReferenceEquals(cursor,instruction))break;}
                        }
                        if(code=="Call" && operand!=null && api.Type("MethodReference").IsInstanceOfType(operand) && (string)api.Get(api.Get(operand,"DeclaringType"),"FullName")==GuardName && (string)api.Get(operand,"Name")=="Check")checks++;
                        if(code=="Call" && operand!=null && api.Type("MethodReference").IsInstanceOfType(operand) && ((string)api.Get(operand,"Name")).StartsWith(AccessorPrefix,StringComparison.Ordinal))proxyCalls++;
                    }
                    if(accessor && ownAccesses!=1)throw new InvalidDataException("Field accessor must perform exactly one guarded original access.");
                    if(interiors.Count==0)continue;
                    foreach(object instruction in instructions)
                    {
                        object target=api.Get(instruction,"Operand");
                        if(target!=null && interiors.Contains(target))throw new InvalidDataException("Control flow bypasses entity receiver check.");
                        var targets=target as Array;if(targets!=null)foreach(object branch in targets)if(interiors.Contains(branch))throw new InvalidDataException("Switch bypasses entity receiver check.");
                    }
                    foreach(object handler in (IEnumerable)api.Get(body,"ExceptionHandlers"))foreach(string boundary in new[]{"TryStart","TryEnd","HandlerStart","HandlerEnd","FilterStart"})
                    {object target=api.Get(handler,boundary);if(target!=null && interiors.Contains(target))throw new InvalidDataException("Exception boundary bypasses entity receiver check.");}
                }
            }
            if(inline+proxyCalls!=104090 || checks!=accesses)throw new InvalidDataException("Entity field checks lost in private image writing.");
        }
        private static object CreateColumnGuard(NativeTileImage.MetadataApi api,object module,object guard,object system,object objectType)
        {
            object predicate=api.Call(module,"ImportReference",typeof(Func<object,bool>));
            object field=api.New("FieldDefinition","CanClearColumn",api.Enum("FieldAttributes",0x16),predicate);api.Call(api.Get(guard,"Fields"),"Add",field);
            object method=api.New("MethodDefinition","CanClearNpcImmunity",api.Enum("MethodAttributes",0x93),api.Get(system,"Boolean"));
            api.Call(api.Get(method,"Parameters"),"Add",api.New("ParameterDefinition",objectType));api.Call(api.Get(guard,"Methods"),"Add",method);
            object code=api.Get(api.Get(method,"Body"),"Instructions"),allow=api.Instruction("Ldc_I4_1");
            Add(api,code,"Ldsfld",field);Add(api,code,"Brfalse",allow);Add(api,code,"Ldsfld",field);Add(api,code,"Ldarg_0");
            Add(api,code,"Callvirt",api.Call(module,"ImportReference",typeof(Func<object,bool>).GetMethod("Invoke")));Add(api,code,"Ret");api.Call(code,"Add",allow);Add(api,code,"Ret");return method;
        }
        private static bool IsColumnReset(NativeTileImage.MetadataApi api,object method,object instruction)
        {
            if((string)api.Get(method,"FullName")!="System.Void Terraria.Projectile::ResetNPCSlotData(System.Int32)" || Opcode(api,instruction)!="Ldfld")return false;
            return (string)api.Get(api.Get(instruction,"Operand"),"FullName")=="System.Int32[] Terraria.Projectile::localNPCImmunity";
        }
        private static object ColumnResume(NativeTileImage.MetadataApi api,object method,object instruction)
        {
            if(((IEnumerable)api.Get(api.Get(method,"Body"),"ExceptionHandlers")).Cast<object>().Any())throw new InvalidDataException("Native column reset exception shape changed.");
            object cursor=api.Get(instruction,"Next");
            foreach(string expected in new[]{"Ldarg_0","Ldc_I4_0","Stelem_I4"})
            {if(Opcode(api,cursor)!=expected)throw new InvalidDataException("Native column store shape changed.");cursor=api.Get(cursor,"Next");}
            if(cursor==null)throw new InvalidDataException("Native column continuation absent.");return cursor;
        }
        private static void VerifyColumnGuard(NativeTileImage.MetadataApi api,object method,object instruction)
        {
            object originalDuplicate=api.Get(api.Get(api.Get(api.Get(instruction,"Previous"),"Previous"),"Previous"),"Previous");
            object skip=api.Get(originalDuplicate,"Previous"),pop=api.Get(skip,"Previous"),branch=api.Get(pop,"Previous"),call=api.Get(branch,"Previous"),duplicate=api.Get(call,"Previous");
            if(Opcode(api,skip)!="Br" || !ReferenceEquals(api.Get(skip,"Operand"),ColumnResume(api,method,instruction)) || Opcode(api,pop)!="Pop" || Opcode(api,branch)!="Brtrue" || !ReferenceEquals(api.Get(branch,"Operand"),originalDuplicate) || Opcode(api,call)!="Call" || (string)api.Get(api.Get(call,"Operand"),"FullName")!="System.Boolean JueMingR.PrivatePrediction.EntityGuard::CanClearNpcImmunity(System.Object)" || Opcode(api,duplicate)!="Dup")
                throw new InvalidDataException("Native column guard control flow changed.");
        }
        private static string Opcode(NativeTileImage.MetadataApi api,object instruction)
        {return instruction==null?null:api.Code(instruction);}
        private static bool IsCheck(NativeTileImage.MetadataApi api,object instruction)
        {
            if(instruction==null || api.Code(instruction)!="Call")return false;
            object method=api.Get(instruction,"Operand");return (string)api.Get(api.Get(method,"DeclaringType"),"FullName")==GuardName && (string)api.Get(method,"Name")=="Check";
        }
        private static IEnumerable<object> Types(NativeTileImage.MetadataApi api,object collection)
        {
            foreach(object type in (IEnumerable)collection){yield return type;foreach(object nested in Types(api,api.Get(type,"NestedTypes")))yield return nested;}
        }
        private static bool IsEntityField(NativeTileImage.MetadataApi api,object field)
        {
            if(field==null || !api.Type("FieldReference").IsInstanceOfType(field))return false;
            string owner=(string)api.Get(api.Get(field,"DeclaringType"),"FullName");return owner=="Terraria.Entity" || owner=="Terraria.NPC" || owner=="Terraria.Projectile";
        }
        private static void Add(NativeTileImage.MetadataApi api,object instructions,string opcode,params object[] operand)
        {api.Call(instructions,"Add",api.Instruction(opcode,operand));}
    }
}
