using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Startup discovery only: preserve the complete method scan, but do not
    // construct Harmony labels, locals and branch graphs for irrelevant IL.
    // Selected methods still go through the existing verified transpilers.
    internal sealed class NativeInstructionInventory
    {
        private static readonly OpCode[] one=new OpCode[256],two=new OpCode[256];
        private readonly Dictionary<Module,Dictionary<int,MemberInfo>> members=new Dictionary<Module,Dictionary<int,MemberInfo>>();
        static NativeInstructionInventory()
        {
            foreach(var field in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static))
            {var value=(OpCode)field.GetValue(null);int code=unchecked((ushort)value.Value);if(value.Size==1)one[code]=value;else two[code&255]=value;}
        }
        internal List<CodeInstruction> Read(MethodBase method)
        {
            var result=new List<CodeInstruction>();var body=method.GetMethodBody();if(body==null)return result;
            byte[] bytes=body.GetILAsByteArray();int offset=0;Dictionary<int,MemberInfo> module;
            if(!members.TryGetValue(method.Module,out module)){module=new Dictionary<int,MemberInfo>();members.Add(method.Module,module);}
            while(offset<bytes.Length)
            {
                int first=bytes[offset++];if(first==254 && offset==bytes.Length)throw new InvalidDataException("Truncated IL opcode.");
                OpCode code=first==254?two[bytes[offset++]]:one[first];if(code.Size==0)throw new InvalidDataException("Unknown IL opcode.");
                int size;
                switch(code.OperandType)
                {
                    case OperandType.InlineNone:size=0;break;
                    case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
                    case OperandType.InlineVar:size=2;break;
                    case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
                    case OperandType.InlineSwitch:
                        if(offset>bytes.Length-4)throw new InvalidDataException("Truncated IL switch.");
                        int count=BitConverter.ToInt32(bytes,offset);if(count<0 || count>(bytes.Length-offset-4)/4)throw new InvalidDataException("Invalid IL switch.");size=4+count*4;break;
                    default:size=4;break;
                }
                if(size>bytes.Length-offset)throw new InvalidDataException("Truncated IL operand.");
                if(code.OperandType==OperandType.InlineMethod || code.OperandType==OperandType.InlineField)
                {
                    int token=BitConverter.ToInt32(bytes,offset);MemberInfo member;
                    // Generic contexts are uncommon here; don't share their
                    // resolutions across different constructed methods/types.
                    bool generic=method.ContainsGenericParameters || method.DeclaringType.IsGenericType;
                    if(generic || !module.TryGetValue(token,out member))
                    {member=method.Module.ResolveMember(token,method.DeclaringType.GetGenericArguments(),method.GetGenericArguments());if(!generic)module.Add(token,member);}
                    result.Add(new CodeInstruction(code,member));
                }
                offset+=size;
            }
            return result;
        }
    }
}
