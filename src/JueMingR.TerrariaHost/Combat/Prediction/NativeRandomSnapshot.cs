using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Main.rand is not the NPC stream at CompletedWorldUpdate. Native world
    // phases own distinct, persistent generators. Capture them without calling
    // SwapRandom, inserting keys in the live dictionary, or consuming a sample.
    internal static class NativeRandomSnapshot
    {
        private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;
        private static readonly FieldInfo Streams=typeof(Main).GetField("_rngs",Flags);
        private static readonly FieldInfo Index=typeof(UnifiedRandom).GetField("inext",Flags);
        private static readonly FieldInfo Seeds=typeof(UnifiedRandom).GetField("SeedArray",Flags);
        private static readonly string[] Keys={"UpdateParticleSystems_World","UpdatePlayers","UpdateNPCs","UpdateGores","UpdateProjectiles","UpdateDust","UpdateFloatingText","UpdateTime"};
        private static bool split;
        internal static void Write(BinaryWriter writer)
        {
            var streams=(Dictionary<string,UnifiedRandom>)Streams.GetValue(null);
            writer.Write(streams!=null);WriteState(writer,Main.rand);
            if(streams==null)return; // standalone original-method fixtures
            foreach(string key in Keys)
            {
                UnifiedRandom random;
                if(!streams.TryGetValue(key,out random))
                {
                    if(Main.ActiveWorldFileData==null)throw new InvalidDataException("Missing native world random seed.");
                    // Match the original first-use rule, but construct only an
                    // independent value object; never create a live stream.
                    random=new UnifiedRandom(Main.ActiveWorldFileData.Seed);
                }
                WriteState(writer,random);
            }
        }
        internal static void Read(BinaryReader reader)
        {
            split=reader.ReadBoolean();Main.rand=ReadState(reader);
            Dictionary<string,UnifiedRandom> streams=null;
            if(split){streams=new Dictionary<string,UnifiedRandom>(StringComparer.Ordinal);foreach(string key in Keys)streams.Add(key,ReadState(reader));}
            Streams.SetValue(null,streams);
        }
        internal static IDisposable Use(string key){return split?Main.SwapRandom(key):null;}
        private static void WriteState(BinaryWriter writer,UnifiedRandom random)
        {
            if(random==null)throw new InvalidDataException("Missing native random state.");
            uint index=(uint)Index.GetValue(random);var seeds=(int[])Seeds.GetValue(random);
            Validate(index,seeds);writer.Write(index);foreach(int value in seeds)writer.Write(value);
        }
        private static UnifiedRandom ReadState(BinaryReader reader)
        {
            uint index=reader.ReadUInt32();var seeds=new int[56];for(int i=0;i<seeds.Length;i++)seeds[i]=reader.ReadInt32();Validate(index,seeds);
            var random=new UnifiedRandom(0);Index.SetValue(random,index);Seeds.SetValue(random,seeds);return random;
        }
        private static void Validate(uint index,int[] seeds)
        {
            if(index>55 || seeds==null || seeds.Length!=56)throw new InvalidDataException("Invalid native random state shape.");
            foreach(int seed in seeds)if(seed<0)throw new InvalidDataException("Invalid native random seed value.");
        }
    }
}
