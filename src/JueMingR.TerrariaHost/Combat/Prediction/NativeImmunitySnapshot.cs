using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Static hit immunity is indexed by NPC slot, not by the target's type.
    // Capture only complete NPC pages' columns. A native new generation clears
    // its entire column; an omitted old column remains unknown, never zero.
    internal static class NativeImmunitySnapshot
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private const int Types=1136,Slots=200;
        private static readonly bool[] Known=new bool[Slots];
        private static uint[,] owned;
        internal static string Failure {get;private set;}
        internal static void Write(BinaryWriter writer,int[] npcs)
        {
            ValidateShape(Projectile.perIDStaticNPCImmunity);writer.Write(npcs.Count(n=>n<Slots));
            foreach(int slot in npcs)
            {
                if(slot>=Slots)continue;writer.Write(slot);writer.Write(Main.npc[slot].generation);
                for(int type=0;type<Types;type++)writer.Write(Projectile.perIDStaticNPCImmunity[type,slot]);
            }
        }
        internal static void Read(BinaryReader reader)
        {
            CheckMatrix(Projectile.perIDStaticNPCImmunity);int count=reader.ReadInt32(),previous=-1;
            if(count<0 || count>Slots)throw new InvalidDataException("Immunity column count.");
            for(int i=0;i<count;i++)
            {
                int slot=reader.ReadInt32();byte generation=reader.ReadByte();
                if(slot<=previous || slot>=Slots || generation!=Main.npc[slot].generation || !NativeEntityDirectory.CanAdvance(Main.npc[slot]))throw new InvalidDataException("Immunity column/NPC page identity.");
                previous=slot;for(int type=0;type<Types;type++)owned[type,slot]=reader.ReadUInt32();Known[slot]=true;
            }
        }
        internal static void Reset()
        {
            // Only the private helper calls this. Keep the owned baseline
            // reference; overwritten columns are unknown until this request's
            // page is read, so no full matrix copy/clear is needed per request.
            Failure=null;Array.Clear(Known,0,Known.Length);
            if(owned==null){ValidateShape(Projectile.perIDStaticNPCImmunity);owned=Projectile.perIDStaticNPCImmunity;}
            else Projectile.perIDStaticNPCImmunity=owned;
        }
        internal static void ClearWorld(){Reset();Array.Clear(owned,0,owned.Length);}
        internal static void Install(Harmony harmony)
        {
            Reset();int methods=0,gets=0,sets=0;
            var inventory=new NativeInstructionInventory();
            foreach(var method in typeof(Projectile).GetMethods(Flags))
            {
                if(method.GetMethodBody()==null)continue;
                var code=inventory.Read(method);int get=code.Count(i=>Access(i.operand)=="Get"),set=code.Count(i=>Access(i.operand)=="Set");
                if(code.Any(i=>Access(i.operand)=="Address"))throw new InvalidDataException("Unaudited immunity matrix address.");
                if(get+set==0)continue;methods++;gets+=get;sets+=set;
                switch(method.Name)
                {
                    case "IsNPCIndexImmuneToProjectileType":
                        Require(get==1 && set==0,"Immunity predicate access changed.");harmony.Patch(method,transpiler:Patch("Rewrite"));break;
                    case "ResetNPCSlotData":
                        Require(get==0 && set==1,"Immunity initializer access changed.");harmony.Patch(method,postfix:Patch("Cleared"));break;
                    case "ResetStaticImmunityMatching":
                        Require(get==1 && set==1,"Immunity matching access changed.");harmony.Patch(method,prefix:Patch("ClearMatching"));break;
                    case "Damage_PVE_Inner":
                        Require(get==0 && set==2,"Immunity hit writes changed.");harmony.Patch(method,transpiler:Patch("Rewrite"));break;
                    default:throw new InvalidDataException("Unaudited immunity matrix method: "+method.Name);
                }
            }
            Require(methods==4 && gets==2 && sets==4,"Immunity matrix IL inventory changed.");
            Console.Error.WriteLine("IMMUNITY original IL methods=4 get=2 set=4; bounded NPC columns");
        }
        internal static void VerifyIntrinsics()
        {
            // Execute the patched original entry points. Manual scalar values
            // isolate the long-comparison and unknown-column contracts; actual
            // cross-process NPC damage is tested by the independent oracle.
            var saved0=new uint[Types];var saved1=new uint[Types];
            for(int type=0;type<Types;type++){saved0[type]=owned[type,0];saved1[type]=owned[type,1];}
            try
            {
                Reset();owned[119,0]=7;owned[119,1]=7;Known[0]=true;
                var projectile=new Projectile{immunityIdentity=119};
                projectile.ResetStaticImmunityMatching(-1);projectile.ResetStaticImmunityMatching((long)uint.MaxValue+8);
                Require(owned[119,0]==7,"Immunity expectedTime must remain signed long.");
                projectile.ResetStaticImmunityMatching(7);
                Require(owned[119,0]==0 && owned[119,1]==7 && !Known[1],"Matching reset must preserve unknown-column status.");
                bool refused=false;try{Projectile.IsNPCIndexImmuneToProjectileType(119,1);}catch(InvalidDataException){refused=true;}
                Require(refused && NativeEntityDirectory.MissingKind==1 && NativeEntityDirectory.MissingSlot==1,"Native unknown matrix read guard.");NativeEntityDirectory.Reset();
                refused=false;try{Set(owned,119,1,0);}catch(InvalidDataException){refused=true;}
                Require(refused && !Known[1],"Ordinary zero write cannot initialize unknown column.");NativeEntityDirectory.Reset();
                Projectile.ResetNPCSlotData(1);
                Require(Known[1] && Projectile.IsNPCIndexImmuneToProjectileType(119,1),"Original NPC initialization establishes zero column.");
                Projectile.perIDStaticNPCImmunity=new uint[Types,Slots];refused=false;
                try{Projectile.IsNPCIndexImmuneToProjectileType(119,0);}catch(InvalidDataException){refused=true;}
                Require(refused && Failure!=null,"Matrix replacement must not bypass knowledge guard.");
            }
            finally
            {
                Reset();NativeEntityDirectory.Reset();
                for(int type=0;type<Types;type++){owned[type,0]=saved0[type];owned[type,1]=saved1[type];}
            }
            Console.Error.WriteLine("IMMUNITY native long comparison / unknown read and zero write / new column / replaced matrix verified");
        }
        private static HarmonyMethod Patch(string name){return new HarmonyMethod(typeof(NativeImmunitySnapshot).GetMethod(name,Flags));}
        private static string Access(object operand)
        {var method=operand as MethodInfo;return method!=null && method.DeclaringType==typeof(uint[,])?method.Name:null;}
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source)
        {
            foreach(var instruction in source)
            {
                string access=Access(instruction.operand);
                if(access=="Get" || access=="Set"){instruction.opcode=OpCodes.Call;instruction.operand=typeof(NativeImmunitySnapshot).GetMethod(access,Flags);}
                yield return instruction;
            }
        }
        private static uint Get(uint[,] matrix,int type,int slot){CheckMatrix(matrix);CheckColumn(slot);return matrix[type,slot];}
        private static void Set(uint[,] matrix,int type,int slot,uint value){CheckMatrix(matrix);CheckColumn(slot);matrix[type,slot]=value;}
        private static void Cleared(int __0)
        {
            // Only successful completion of the exact original full zero-store
            // establishes knowledge. Ordinary hit writes of zero may be uint
            // wraparound and never grant this initialization privilege.
            CheckMatrix(Projectile.perIDStaticNPCImmunity);Known[__0]=true;
        }
        private static bool ClearMatching(Projectile __instance,long __0)
        {
            // Host code is outside the rewritten image: explicitly preserve the
            // receiver guard before reading immunityIdentity. Skipped unknown
            // columns stay opaque; later need forces an entire request replay.
            NativeEntityDirectory.RequireKnown(__instance);CheckMatrix(Projectile.perIDStaticNPCImmunity);
            int type=__instance.immunityIdentity;
            if(type<0 || type>=Types)throw new IndexOutOfRangeException();
            for(int slot=0;slot<Slots;slot++)if(Known[slot] && (long)owned[type,slot]==__0)owned[type,slot]=0;
            return false;
        }
        private static void CheckColumn(int slot)
        {if(slot<0 || slot>=Slots)throw new IndexOutOfRangeException();if(!Known[slot])NativeEntityDirectory.MissingNpcColumn(slot);}
        private static void CheckMatrix(uint[,] matrix)
        {
            if(ReferenceEquals(matrix,owned) && ReferenceEquals(Projectile.perIDStaticNPCImmunity,owned))return;
            Failure="Immunity matrix reference changed during request.";throw new InvalidDataException(Failure);
        }
        private static void ValidateShape(uint[,] matrix)
        {Require(matrix!=null && matrix.GetLength(0)==Types && matrix.GetLength(1)==Slots,"Native immunity matrix shape.");}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    }
}
