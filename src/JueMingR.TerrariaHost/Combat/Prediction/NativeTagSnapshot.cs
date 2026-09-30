using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Existing effects are state, not future attacks. Keep stack priority and
    // inactive historical entries; Activate/Apply would alter buffs, ordering,
    // duration and proc ownership. Only locked native definition IDs cross IPC.
    internal static class NativeTagSnapshot
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly FieldInfo States=typeof(TagEffectStack).GetField("_effectStates",Flags),Count=typeof(TagEffectStack).GetField("_activeEffectCount",Flags);
        private static readonly FieldInfo StackOwner=typeof(TagEffectStack).GetField("_owner",Flags),StateOwner=typeof(TagEffectState).GetField("_owner",Flags);
        private static readonly FieldInfo Effect=typeof(TagEffectState).GetField("_effect",Flags),Time=typeof(TagEffectState).GetField("TimeLeftOnNPC",Flags),Proc=typeof(TagEffectState).GetField("ProcTimeLeftOnNPC",Flags);
        private static readonly MethodInfo SetType=typeof(TagEffectState).GetProperty("Type").GetSetMethod(true);
        internal static void Write(BinaryWriter writer,Player player)
        {
            var stack=player.TagEffectStack;
            if(stack==null || !ReferenceEquals(StackOwner.GetValue(stack),player))throw new InvalidDataException("Tag stack owner.");
            var states=(TagEffectState[])States.GetValue(stack);int count=(int)Count.GetValue(stack);ValidateCount(count);
            if(states==null || states.Length!=5 || TagEffectStack.MaxEffects!=5)throw new InvalidDataException("Tag stack shape.");
            writer.Write(count);var active=new HashSet<int>();
            for(int i=0;i<states.Length;i++)
            {
                TagEffectState state=states[i];writer.Write(state!=null);
                if(state==null){if(i<count)throw new InvalidDataException("Missing active tag.");continue;}
                UniqueTagEffect effect=Definition(state.Type,i<count);
                if(!ReferenceEquals(StateOwner.GetValue(state),player) || !ReferenceEquals(Effect.GetValue(state),effect))throw new InvalidDataException("Tag state definition or owner.");
                if(i<count && !active.Add(state.Type))throw new InvalidDataException("Duplicate active tag.");
                writer.Write(state.Type);WriteTimes(writer,(int[])Time.GetValue(state));WriteTimes(writer,(int[])Proc.GetValue(state));
            }
        }
        internal static void Read(BinaryReader reader,Player player)
        {
            int count=reader.ReadInt32();ValidateCount(count);var stack=new TagEffectStack(player);
            var states=(TagEffectState[])States.GetValue(stack);var active=new HashSet<int>();
            for(int i=0;i<states.Length;i++)
            {
                if(!reader.ReadBoolean()){if(i<count)throw new InvalidDataException("Missing active tag.");continue;}
                int type=reader.ReadInt32();UniqueTagEffect effect=Definition(type,i<count);
                if(i<count && !active.Add(type))throw new InvalidDataException("Duplicate active tag.");
                var state=new TagEffectState(player);SetType.Invoke(state,new object[]{type});Effect.SetValue(state,effect);
                // Do not assign readonly fields. Constructors bind the private
                // owner and allocate independent arrays; copy signed values,
                // including nonpositive inactive values, without normalization.
                ReadTimes(reader,(int[])Time.GetValue(state));ReadTimes(reader,(int[])Proc.GetValue(state));states[i]=state;
            }
            Count.SetValue(stack,count);player.TagEffectStack=stack;
        }
        private static UniqueTagEffect Definition(int type,bool active)
        {
            if(type<0 || type>=ItemID.Sets.UniqueTagEffects.Length)throw new InvalidDataException("Tag definition identity.");
            var effect=ItemID.Sets.UniqueTagEffects[type];
            if(effect==null && (active || type!=0))throw new InvalidDataException("Unknown tag definition.");return effect;
        }
        private static void ValidateCount(int count){if(count<0 || count>5)throw new InvalidDataException("Tag stack count.");}
        private static void WriteTimes(BinaryWriter writer,int[] values)
        {if(values==null || values.Length!=Main.maxNPCs)throw new InvalidDataException("Tag counter shape.");foreach(int value in values)writer.Write(value);}
        private static void ReadTimes(BinaryReader reader,int[] values)
        {for(int i=0;i<values.Length;i++)values[i]=reader.ReadInt32();}
    }
}
