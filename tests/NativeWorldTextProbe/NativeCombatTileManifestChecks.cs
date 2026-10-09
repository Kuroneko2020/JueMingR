using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using Terraria;

namespace NativeWorldTextProbe
{
    // Independent raw-IL inventory. Generation is a development action; the
    // normal check compares every discovered callsite against the shipped list.
    internal static class NativeCombatTileManifestChecks
    {
        private const string Identity="960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";
        private static readonly Dictionary<ushort,OpCode> Opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).ToDictionary(f=>unchecked((ushort)((OpCode)f.GetValue(null)).Value),f=>(OpCode)f.GetValue(null));
        internal static void Run(string output,bool generate)
        {
            byte[] image=File.ReadAllBytes(Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"));
            if(Hash(image)!=Identity)throw new InvalidOperationException("Tile inventory game identity.");
            var lines=new List<string>{"# Tile callsites; generated from locked original IL, not original source.","identity|"+Identity+"|"+typeof(Main).Module.ModuleVersionId};
            int methods=0,accesses=0,blocked=0;var entityFields=new int[3];
            foreach(Type type in Types().OrderBy(t=>t.MetadataToken))
            foreach(MethodBase method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static)).OrderBy(m=>m.MetadataToken))
            {
                // Source-audited shader compiler helper is never a simulation
                // root. Its generic locals require the absent XNA build SDK.
                // This exact exclusion is bound to the whole original hash.
                if(type.FullName=="Terraria.Testing.FxReader")continue;
                if(method.IsAbstract || method.GetMethodBody()==null)continue;
                byte[] il=method.GetMethodBody().GetILAsByteArray();int[] count=new int[3];bool genericArray=false;
                for(int offset=0;offset<il.Length;)
                {
                    ushort code=il[offset++];if(code==254)code=(ushort)(65024+il[offset++]);OpCode opcode=Opcodes[code];
                    if(opcode.OperandType==OperandType.InlineMethod)
                    {
                        var called=method.Module.ResolveMethod(BitConverter.ToInt32(il,offset),type.IsGenericType?type.GetGenericArguments():null,method.IsGenericMethod?method.GetGenericArguments():null);
                        Type owner=called.DeclaringType;
                        if(owner!=null && owner.IsArray && owner.GetArrayRank()==2)
                        {
                            int index=called.Name=="Get"?0:called.Name=="Set"?1:called.Name=="Address"?2:-1;
                            if(index>=0 && owner==typeof(Tile[,]))count[index]++;
                            if(index>=0 && owner.ContainsGenericParameters)genericArray=true;
                        }
                    }
                    if(opcode==OpCodes.Ldfld || opcode==OpCodes.Ldflda || opcode==OpCodes.Stfld)
                    {
                        var field=method.Module.ResolveField(BitConverter.ToInt32(il,offset),type.IsGenericType?type.GetGenericArguments():null,method.IsGenericMethod?method.GetGenericArguments():null);
                        if(field.DeclaringType==typeof(Entity) || field.DeclaringType==typeof(NPC) || field.DeclaringType==typeof(Projectile))entityFields[opcode==OpCodes.Ldfld?0:opcode==OpCodes.Ldflda?1:2]++;
                    }
                    offset+=Size(opcode.OperandType,il,offset);
                }
                if(genericArray)
                {
                    if(type.FullName!="Terraria.Testing.Cloning.DeepCloneCodegen")throw new InvalidOperationException("Unaudited generic array access: "+type.FullName+"."+method.Name);
                    lines.Add("blocked|"+method.MetadataToken.ToString("X8")+"|"+Hash(il)+"|"+type.FullName+"."+method.Name);blocked++;
                }
                if(count.Sum()==0)continue;
                if(method.ContainsGenericParameters)throw new InvalidOperationException("Generic Tile callsite requires a boundary.");
                lines.Add(method.MetadataToken.ToString("X8")+"|"+Hash(il)+"|"+string.Join("|",count)+"|"+type.FullName+"."+method.Name);methods++;accesses+=count.Sum();
            }
            string text=string.Join("\n",lines)+"\n";
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"NativeTileManifest.txt"),text,new UTF8Encoding(false));
            if(!generate && File.ReadAllText(Path.Combine(Program.Repository,"src/JueMingR.TerrariaHost/Combat/Prediction/NativeTileManifest.txt")).Replace("\r\n","\n")!=text)
                throw new InvalidOperationException("Tile manifest differs from complete locked IL inventory.");
            Console.WriteLine((generate?"GENERATED":"PASS")+" Tile manifest methods="+methods+" accesses="+accesses+" explicitly-blocked-generic="+blocked);
            if(!entityFields.SequenceEqual(new[]{59836,18834,25420}))throw new InvalidOperationException("Locked raw IL entity field inventory differs: "+string.Join(",",entityFields));
            Console.WriteLine("PASS independent entity field IL inventory read/address/write="+string.Join("/",entityFields));
        }
        private static Type[] Types()
        {
            try{return typeof(Main).Assembly.GetTypes();}
            catch(ReflectionTypeLoadException e)
            {
                if(e.Types.Count(t=>t==null)!=2 || e.LoaderExceptions.Length!=2 || e.LoaderExceptions.Any(x=>!(x is FileNotFoundException) || new AssemblyName(((FileNotFoundException)x).FileName).Name!="Microsoft.Xna.Framework.Content.Pipeline"))throw;
                foreach(string name in new[]{"Terraria.Testing.FxReader+DummyPipelineContext","Terraria.Testing.FxReader+PipelineLogger"})
                {
                    try{typeof(Main).Assembly.GetType(name,true);throw new InvalidOperationException("Unexpected FxReader metadata shape.");}
                    catch(FileNotFoundException missing){if(new AssemblyName(missing.FileName).Name!="Microsoft.Xna.Framework.Content.Pipeline")throw;}
                }
                return e.Types.Where(t=>t!=null).ToArray();
            }
        }
        private static int Size(OperandType type,byte[] bytes,int offset)
        {
            switch(type)
            {
                case OperandType.InlineNone:return 0;
                case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:return 1;
                case OperandType.InlineVar:return 2;
                case OperandType.InlineI8:case OperandType.InlineR:return 8;
                case OperandType.InlineSwitch:return 4+4*BitConverter.ToInt32(bytes,offset);
                default:return 4;
            }
        }
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    }
}
