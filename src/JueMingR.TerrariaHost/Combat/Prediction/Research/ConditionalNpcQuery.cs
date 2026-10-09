#if JMR_CONDITIONAL_RESEARCH
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using HarmonyLib;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Explicit research build only. A query page contains real observed bytes;
    // its future geometry uses held observed velocity, NOT background AI.
    // Mutation/address/reference-array access asks the Session for an exact
    // actor and a new real capture. Unknown fields still use the normal guard.
    internal static class ConditionalNpcQuery
    {
        internal static int[] CaptureRoles;
        private static int[] roles;
        private static readonly NPC[] queries=new NPC[Main.maxNPCs+1];
        private static readonly Vector2[] origins=new Vector2[Main.maxNPCs+1],velocities=new Vector2[Main.maxNPCs+1];
        private static readonly HashSet<int> readOnlyValues=new HashSet<int>();
        private static NPC geometryWriter;
        private static int positionField;
        internal static long QueryReads,GeometryStores;
        internal static bool Active {get;private set;}
        internal static void Prepare(IDictionary<string,int> names)
        {
            if(readOnlyValues.Count!=0)return;
            for(Type type=typeof(NPC);type!=null;type=type.BaseType)
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    int id;Type value=field.FieldType;
                    if(names.TryGetValue(type.FullName+"."+field.Name,out id) && (value.IsPrimitive || value.IsEnum || value==typeof(Vector2) || value==typeof(Rectangle)))readOnlyValues.Add(id);
                }
            positionField=names["Terraria.Entity.position"];
        }
        internal static bool ReadOnly(NPC value,int field,int mode)
        {
            if(mode==0 && readOnlyValues.Contains(field)){QueryReads++;return true;}
            if(mode==2 && field==positionField && ReferenceEquals(value,geometryWriter)){GeometryStores++;return true;}
            return false;
        }
        internal static void Install(Harmony patches)
        {
            const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic;
            // Locked .8 leaf methods only add/subtract netOffset around the
            // hitbox query. Their pure temporary stores are not a real hit.
            // No damage/AI call is inside this permission scope, and finalizer
            // cleanup preserves exceptions. All other stores still promote.
            foreach(string name in new[]{"Damage_StartIteratingNPC","Damage_StopIteratingNPC"})
                patches.Patch(typeof(Projectile).GetMethod(name,flags),new HarmonyMethod(typeof(ConditionalNpcQuery).GetMethod(nameof(GeometryBegin),flags)),finalizer:new HarmonyMethod(typeof(ConditionalNpcQuery).GetMethod(nameof(GeometryEnd),flags)));
        }
        private static void GeometryBegin(NPC targetNPC,ref NPC __state){__state=geometryWriter;geometryWriter=targetNPC;}
        private static Exception GeometryEnd(NPC __state,Exception __exception){geometryWriter=__state;return __exception;}
        internal static bool IsQuery(NPC npc)
        {return Active && (uint)npc.whoAmI<(uint)queries.Length && ReferenceEquals(queries[npc.whoAmI],npc);}
        internal static void WriteRoles(BinaryWriter writer,int[] slots,int selected)
        {
            int[] exact=CaptureRoles??new[]{selected};
            if(exact.Length==0 || Array.IndexOf(exact,selected)<0 || exact.Any(slot=>Array.IndexOf(slots,slot)<0))throw new InvalidDataException("Research exact roles need real pages.");
            writer.Write(exact.Length);foreach(int slot in exact)writer.Write(slot);
        }
        internal static void ReadRoles(BinaryReader reader)
        {
            int count=reader.ReadInt32();if(count<1 || count>Main.maxNPCs+1)throw new InvalidDataException("Research role count.");
            roles=new int[count];int previous=-1;
            for(int i=0;i<count;i++){int slot=reader.ReadInt32();if(slot<=previous || slot>Main.maxNPCs)throw new InvalidDataException("Research role identity.");roles[i]=slot;previous=slot;}
        }
        internal static int[] Begin(int[] pages,int selected)
        {
            if(roles==null || Array.IndexOf(roles,selected)<0 || roles.Any(slot=>Array.IndexOf(pages,slot)<0))throw new InvalidDataException("Research exact role lacks observed page.");
            foreach(int slot in pages)if(Array.IndexOf(roles,slot)<0)
            {NPC npc=Main.npc[slot];queries[slot]=npc;origins[slot]=npc.position;velocities[slot]=npc.velocity;}
            Active=true;return roles;
        }
        internal static void Advance(int step)
        {
            // One common future instant for AI and projectile geometry queries.
            // This deliberately assumes no background acceleration/AI/phase
            // transition until the next real observation; it is not a claim
            // that a stationary observed actor will remain stationary in game.
            for(int slot=0;slot<queries.Length;slot++)if(queries[slot]!=null)
            {
                NPC npc=queries[slot];npc.oldPosition=npc.position;Vector2 next=origins[slot]+velocities[slot]*step;
                if(float.IsNaN(next.X) || float.IsInfinity(next.X) || float.IsNaN(next.Y) || float.IsInfinity(next.Y))throw new InvalidDataException("Research query geometry nonfinite.");
                npc.position=next;
            }
        }
        internal static void Reset(){Active=false;roles=null;geometryWriter=null;QueryReads=GeometryStores=0;Array.Clear(queries,0,queries.Length);}
    }
}
#endif
