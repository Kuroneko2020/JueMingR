using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Gold critter sparkles consume the AI RNG according to current light.
    // Preserve that branch with an observed per-actor light class, without
    // owning a renderer/light engine or inventing a zero-light default. Future
    // light is conditional on this observed class; history checks reobserve it.
    internal static class NativeLightingSnapshot
    {
        private static readonly int[] classes=new int[Main.maxNPCs+1];
        private static readonly NPC[] owners=new NPC[classes.Length];
        private static readonly int[] types=new int[classes.Length];
        private static readonly byte[] generations=new byte[classes.Length];
        private static bool privateSide;
        internal static bool HasValues {get;private set;}
        internal static void Clear(){for(int i=0;i<classes.Length;i++){classes[i]=-1;owners[i]=null;}HasValues=false;}
        internal static void Install(Harmony patches)
        {
            privateSide=true;Clear();
            patches.Patch(typeof(NPC).GetMethod("UpdateNPC_CastLights",BindingFlags.Instance|BindingFlags.NonPublic),transpiler:new HarmonyMethod(typeof(NativeLightingSnapshot).GetMethod(nameof(Rewrite),BindingFlags.Static|BindingFlags.NonPublic)));
        }
        internal static void Release(){privateSide=false;Clear();}
        internal static bool Needs(NPC n){return n.active && n.type>=0 && n.type<NPCID.Sets.IsGoldCritter.Length && NPCID.Sets.IsGoldCritter[n.type];}
        internal static int Observe(NPC n)
        {
            if(!Needs(n))return -1;
            if(privateSide)
            {
                if(n.whoAmI<0 || n.whoAmI>=classes.Length || classes[n.whoAmI]<0 || !ReferenceEquals(owners[n.whoAmI],n) || types[n.whoAmI]!=n.type || generations[n.whoAmI]!=n.generation)throw new InvalidDataException("Unobserved gold-critter lighting premise.");
                return classes[n.whoAmI];
            }
            var center=n.Center+n.netOffset;Color light=Lighting.GetColor((int)center.X/16,(int)center.Y/16);
            int maximum=Math.Max(light.R,Math.Max(light.G,light.B));return maximum<=20?0:1+maximum/30;
        }
        internal static void Write(BinaryWriter writer,int[] slots)
        {
            int count=0;foreach(int slot in slots)if(Needs(Main.npc[slot]))count++;
            writer.Write(count);foreach(int slot in slots)if(Needs(Main.npc[slot])){writer.Write(slot);writer.Write((byte)Observe(Main.npc[slot]));}
        }
        internal static void Read(BinaryReader reader)
        {
            Clear();int count=reader.ReadInt32();if(count<0 || count>classes.Length)throw new InvalidDataException("Light premise count.");int prior=-1;
            for(int i=0;i<count;i++){int slot=reader.ReadInt32(),value=reader.ReadByte();if(slot<=prior || slot>=classes.Length || value>9)throw new InvalidDataException("Light premise identity or class.");classes[slot]=value;prior=slot;}
            HasValues=count>0;
        }
        internal static void BindRestoredActors()
        {
            for(int i=0;i<classes.Length;i++)if(classes[i]>=0)
            {
                NPC n=Main.npc[i];if(!Needs(n))throw new InvalidDataException("Light premise without its captured actor.");
                owners[i]=n;types[i]=n.type;generations[i]=n.generation;
            }
        }
        private static Color ColorFor(int x,int y,NPC npc)
        {
            int value=Observe(npc);if(value<0)throw new InvalidDataException("Unexpected light consumer.");
            byte level=(byte)(value==0?0:value==1?21:(value-1)*30);return new Color(level,level,level);
        }
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source)
        {
            var original=typeof(Lighting).GetMethod("GetColor",new[]{typeof(int),typeof(int)});int calls=0;
            foreach(var code in source)
            {
                if(Equals(code.operand,original))
                {
                    calls++;var instance=new CodeInstruction(OpCodes.Ldarg_0);instance.labels.AddRange(code.labels);instance.blocks.AddRange(code.blocks);code.labels.Clear();code.blocks.Clear();
                    yield return instance;code.opcode=OpCodes.Call;code.operand=typeof(NativeLightingSnapshot).GetMethod(nameof(ColorFor),BindingFlags.Static|BindingFlags.NonPublic);yield return code;
                }
                else yield return code;
            }
            if(calls!=1)throw new InvalidDataException("Locked native gold-critter light call shape.");
        }
    }
}
