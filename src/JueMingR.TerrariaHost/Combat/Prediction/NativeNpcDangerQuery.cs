using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Provenance for one native read, never permission to answer it. The
    // complete authenticated 1.4.5.8 body binds instruction 1934 to the first
    // danger-loop stinky read (before distance/self/wall). The later read at
    // 1998 and every other method retain their ordinary missing-page behavior.
    internal static class NativeNpcDangerQuery
    {
        internal enum Origin : byte { None, TownDangerStinky }
        internal struct Source
        {
            internal Origin Kind;
            internal int CallerSlot,Generation,Type,NetId;
        }
        internal const int StinkyToken=67109777;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Func<NPC,bool> read;
        internal static Source Missing {get;private set;}
        internal static void Reset(){Missing=default(Source);}
        internal static void Install(Harmony patches)
        {
            patches.Patch(typeof(NPC).GetMethod("AI_007_TownEntities",Flags),transpiler:new HarmonyMethod(typeof(NativeNpcDangerQuery).GetMethod(nameof(Bind),Flags)));
        }
        private static IEnumerable<CodeInstruction> Bind(IEnumerable<CodeInstruction> input,MethodBase __originalMethod)
        {
            var code=input.ToList();var body=__originalMethod.GetMethodBody();
            string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(body.GetILAsByteArray())).Replace("-","");
            var accessor=typeof(NPC).GetMethod("__JmrField_"+StinkyToken+"_0",Flags);
            // A changed layout simply has no ticket authority. Do not search
            // for a same-named field elsewhere and silently broaden the site.
            if(digest!="ABBF433D59208DAA150624936192DC9EFB0249437FC2C6DD964B462557C30CD1" || body.ExceptionHandlingClauses.Count!=0 || code.Count<1999 || accessor==null ||
                !code[1934].Calls(accessor) || !code[1998].Calls(accessor) || code[1933].opcode!=OpCodes.Ldelem_Ref || code[1935].opcode!=OpCodes.Brfalse || code[1918].opcode!=OpCodes.Ldc_I4 || (int)code[1918].operand!=690)return code;
            read=(Func<NPC,bool>)Delegate.CreateDelegate(typeof(Func<NPC,bool>),accessor);
            var caller=new CodeInstruction(OpCodes.Ldarg_0);caller.labels.AddRange(code[1934].labels);code[1934].labels.Clear();
            code[1934]=new CodeInstruction(OpCodes.Call,typeof(NativeNpcDangerQuery).GetMethod(nameof(Read),Flags));code.Insert(1934,caller);
            return code;
        }
        private static bool Read(NPC candidate,NPC caller)
        {
            bool first=NativeEntityDirectory.MissingKind==0;
            try{return read(candidate);}
            catch(InvalidDataException)
            {
                // Execute the very same guarded accessor first: Research,
                // allocation, full-page permissions and Purpose stay owners.
                if(first && NativeEntityDirectory.MissingKind==1 && NativeEntityDirectory.MissingField==StinkyToken && NativeEntityDirectory.IsOpaque(candidate) && NativeEntityDirectory.IsCapturedNpc(caller))
                    Missing=new Source{Kind=Origin.TownDangerStinky,CallerSlot=caller.whoAmI,Generation=caller.generation,Type=caller.type,NetId=caller.netID};
                throw;
            }
        }
        internal static void Write(BinaryWriter writer,bool allowed)
        {
            var source=allowed?Missing:default(Source);writer.Write((byte)source.Kind);
            if(source.Kind==Origin.None)return;
            writer.Write(source.CallerSlot);writer.Write(source.Generation);writer.Write(source.Type);writer.Write(source.NetId);
        }
        internal static Source ReadSource(BinaryReader reader,int missingKind,int missingField)
        {
            var source=new Source{Kind=(Origin)reader.ReadByte()};if(source.Kind==Origin.None)return source;
            if(source.Kind!=Origin.TownDangerStinky || missingKind!=1 || missingField!=StinkyToken)throw new InvalidDataException("Native danger query source.");
            source.CallerSlot=reader.ReadInt32();source.Generation=reader.ReadInt32();source.Type=reader.ReadInt32();source.NetId=reader.ReadInt32();
            if(source.CallerSlot<0 || source.CallerSlot>=Main.maxNPCs || source.Generation<0 || source.Generation>255 || source.Type<=0 || source.Type>=Terraria.ID.NPCID.Count)throw new InvalidDataException("Native danger caller identity.");
            return source;
        }
    }
}
