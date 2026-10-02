using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Version-bound value schema for the initial integration experiment. Complex
    // references are reported, never serialized as live objects or arbitrary
    // runtime types. Their dependency audit is a prerequisite to broad rollout.
    internal sealed class NativeValueSnapshot
    {
        private readonly FieldInfo[] fields;
        private readonly Action<BinaryWriter,object> writeFields;
        internal readonly string Schema;
        internal readonly string[] Unsupported;
        internal NativeValueSnapshot(Type type,bool staticFields=false,bool arrays=true,string[] fieldNames=null)
        {
            var all=new List<FieldInfo>();
            for(var current=type;current!=null && current!=typeof(object);current=current.BaseType)
                all.AddRange(current.GetFields((staticFields?BindingFlags.Static:BindingFlags.Instance)|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(f=>!f.IsLiteral));
            fields=all.Where(f=>!f.IsInitOnly && Supported(f.FieldType) && (arrays || !f.FieldType.IsArray) && (fieldNames==null || fieldNames.Contains(f.Name))).OrderBy(f=>f.DeclaringType.FullName+"."+f.Name,StringComparer.Ordinal).ToArray();
            if(fieldNames!=null && fields.Length!=fieldNames.Length)throw new InvalidDataException("Incomplete named native value schema.");
            Unsupported=all.Where(f=>!fields.Contains(f)).Select(f=>f.DeclaringType.FullName+"."+f.Name+":"+f.FieldType.FullName).ToArray();
            string names=string.Join("\n",fields.Select(f=>f.DeclaringType.FullName+"."+f.Name+":"+f.FieldType.FullName));
            using(var sha=SHA256.Create())Schema=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(names))).Replace("-","");
            writeFields=CompileWriter();
            RuntimeHelpers.PrepareDelegate(writeFields);
        }
        internal IEnumerable<string> FieldIdentities {get{return fields.Select(f=>f.DeclaringType.FullName+"."+f.Name);}}
        private static bool Supported(Type type)
        {return Scalar(type) || type.IsArray && type.GetArrayRank()==1 && Scalar(type.GetElementType());}
        private static bool Scalar(Type type)
        {return type.IsPrimitive && type!=typeof(IntPtr) && type!=typeof(UIntPtr) || type.IsEnum || type==typeof(Vector2) || type==typeof(Rectangle) || type==typeof(Color) || type==typeof(Terraria.BitsByte) || type==typeof(Terraria.DataStructures.ProjectileKey);}
        internal void Write(BinaryWriter writer,object value)
        {
            writer.Write(Schema);writeFields(writer,value);
        }
        internal void WriteHeader(BinaryWriter writer){writer.Write(Schema);}
        internal void WriteFields(BinaryWriter writer,object value){writeFields(writer,value);}
        internal void ReadHeader(BinaryReader reader){if(reader.ReadString()!=Schema)throw new InvalidDataException("Native snapshot schema mismatch.");}
        private Action<BinaryWriter,object> CompileWriter()
        {
            // Compile this exact authenticated field layout once. No live
            // entity is captured by the delegate. Direct typed field loads
            // remove per-frame reflection and scalar boxing without changing
            // schema selection, guard permissions or virtual writer dispatch.
            var method=new DynamicMethod("WriteNativeValues",typeof(void),new[]{typeof(BinaryWriter),typeof(object)},typeof(NativeValueSnapshot),true);
            var il=method.GetILGenerator();
            foreach(var field in fields)
            {
                il.Emit(OpCodes.Ldarg_0);
                if(field.IsStatic)il.Emit(OpCodes.Ldsfld,field);
                else{il.Emit(OpCodes.Ldarg_1);il.Emit(field.DeclaringType.IsValueType?OpCodes.Unbox:OpCodes.Castclass,field.DeclaringType);il.Emit(OpCodes.Ldfld,field);}
                Type type=field.FieldType;
                if(type.IsArray)
                {var values=typeof(NativeValueSnapshot).GetMethod(nameof(WriteValues),BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(type.GetElementType());RuntimeHelpers.PrepareMethod(values.MethodHandle);il.Emit(OpCodes.Call,values);continue;}
                Type wire=type.IsEnum?Enum.GetUnderlyingType(type):type==typeof(char)?typeof(ushort):type;
                var write=typeof(BinaryWriter).GetMethod("Write",new[]{wire});
                if(write!=null)il.Emit(OpCodes.Callvirt,write);
                else il.Emit(OpCodes.Call,typeof(NativeValueSnapshot).GetMethod(nameof(WriteValue),BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(BinaryWriter),type},null)??throw new InvalidDataException("Missing typed native field encoder."));
            }
            il.Emit(OpCodes.Ret);return (Action<BinaryWriter,object>)method.CreateDelegate(typeof(Action<BinaryWriter,object>));
        }
        private static void WriteValues<T>(BinaryWriter writer,T[] array)
        {int count=array==null?-1:array.Length;if(count>4096)throw new InvalidDataException("Native value array limit.");writer.Write(count);if(array!=null)WriteArray(writer,array,typeof(T));}
        private static void WriteValue(BinaryWriter w,Vector2 p){w.Write(p.X);w.Write(p.Y);}
        private static void WriteValue(BinaryWriter w,Rectangle p){w.Write(p.X);w.Write(p.Y);w.Write(p.Width);w.Write(p.Height);}
        private static void WriteValue(BinaryWriter w,Color p){w.Write(p.PackedValue);}
        private static void WriteValue(BinaryWriter w,Terraria.BitsByte p){w.Write((byte)p);}
        private static void WriteValue(BinaryWriter w,Terraria.DataStructures.ProjectileKey p){w.Write((uint)p);}
        private static void WriteArray(BinaryWriter writer,Array array,Type element)
        {
            // The measured production cost includes repeated alignment hashes.
            // Typed loops preserve the existing wire bytes while avoiding one
            // reflection/boxing operation per primitive array element.
            if(array is int[] ints){foreach(int n in ints)writer.Write(n);return;}
            if(array is float[] singles){foreach(float n in singles)writer.Write(n);return;}
            if(array is bool[] flags){foreach(bool n in flags)writer.Write(n);return;}
            if(array is byte[] bytes){writer.Write(bytes);return;}
            if(array is ushort[] words){foreach(ushort n in words)writer.Write(n);return;}
            if(array is short[] shorts){foreach(short n in shorts)writer.Write(n);return;}
            if(array is uint[] unsigned){foreach(uint n in unsigned)writer.Write(n);return;}
            if(array is Vector2[] vectors){foreach(var n in vectors){writer.Write(n.X);writer.Write(n.Y);}return;}
            for(int i=0;i<array.Length;i++)WriteScalar(writer,element,array.GetValue(i));
        }
        internal void Read(BinaryReader reader,object value)
        {
            ReadHeader(reader);ReadFields(reader,value);
        }
        internal void ReadFields(BinaryReader reader,object value)
        {
            foreach(var field in fields)
            {
                Type type=field.FieldType;
                if(!type.IsArray){field.SetValue(value,ReadScalar(reader,type));continue;}
                int count=reader.ReadInt32();if(count< -1 || count>4096)throw new InvalidDataException("Native array length.");
                if(count<0){field.SetValue(value,null);continue;}
                Type element=type.GetElementType();var array=Array.CreateInstance(element,count);
                for(int i=0;i<count;i++)array.SetValue(ReadScalar(reader,element),i);field.SetValue(value,array);
            }
        }
        private static void WriteScalar(BinaryWriter w,Type t,object v)
        {
            if(t.IsEnum){WriteScalar(w,Enum.GetUnderlyingType(t),Convert.ChangeType(v,Enum.GetUnderlyingType(t)));return;}
            if(t==typeof(Vector2)){var p=(Vector2)v;w.Write(p.X);w.Write(p.Y);return;}
            if(t==typeof(Rectangle)){var p=(Rectangle)v;w.Write(p.X);w.Write(p.Y);w.Write(p.Width);w.Write(p.Height);return;}
            if(t==typeof(Color)){w.Write(((Color)v).PackedValue);return;}
            if(t==typeof(Terraria.BitsByte)){w.Write((byte)(Terraria.BitsByte)v);return;}
            if(t==typeof(Terraria.DataStructures.ProjectileKey)){w.Write((uint)(Terraria.DataStructures.ProjectileKey)v);return;}
            switch(Type.GetTypeCode(t))
            {
                case TypeCode.Boolean:w.Write((bool)v);break;case TypeCode.Byte:w.Write((byte)v);break;
                case TypeCode.SByte:w.Write((sbyte)v);break;case TypeCode.Int16:w.Write((short)v);break;
                case TypeCode.UInt16:w.Write((ushort)v);break;case TypeCode.Int32:w.Write((int)v);break;
                case TypeCode.UInt32:w.Write((uint)v);break;case TypeCode.Int64:w.Write((long)v);break;
                case TypeCode.UInt64:w.Write((ulong)v);break;case TypeCode.Single:w.Write((float)v);break;
                case TypeCode.Double:w.Write((double)v);break;case TypeCode.Char:w.Write((ushort)(char)v);break;
                default:throw new InvalidDataException("Unsupported scalar.");
            }
        }
        private static object ReadScalar(BinaryReader r,Type t)
        {
            if(t.IsEnum)return Enum.ToObject(t,ReadScalar(r,Enum.GetUnderlyingType(t)));
            if(t==typeof(Vector2))return new Vector2(Finite(r.ReadSingle()),Finite(r.ReadSingle()));
            if(t==typeof(Rectangle))return new Rectangle(r.ReadInt32(),r.ReadInt32(),r.ReadInt32(),r.ReadInt32());
            if(t==typeof(Color))return new Color{PackedValue=r.ReadUInt32()};
            if(t==typeof(Terraria.BitsByte))return (Terraria.BitsByte)r.ReadByte();
            if(t==typeof(Terraria.DataStructures.ProjectileKey))return (Terraria.DataStructures.ProjectileKey)r.ReadUInt32();
            switch(Type.GetTypeCode(t))
            {
                case TypeCode.Boolean:return r.ReadBoolean();case TypeCode.Byte:return r.ReadByte();case TypeCode.SByte:return r.ReadSByte();
                case TypeCode.Int16:return r.ReadInt16();case TypeCode.UInt16:return r.ReadUInt16();case TypeCode.Int32:return r.ReadInt32();
                case TypeCode.UInt32:return r.ReadUInt32();case TypeCode.Int64:return r.ReadInt64();case TypeCode.UInt64:return r.ReadUInt64();
                // Native ai slots can hold ProjectileKey's raw uint/float
                // union, including NaN bit patterns. Validate physical values
                // at their use boundary, never reinterpret an identity as math.
                case TypeCode.Single:return r.ReadSingle();case TypeCode.Double:return r.ReadDouble();
                case TypeCode.Char:return (char)r.ReadUInt16();default:throw new InvalidDataException("Unsupported scalar.");
            }
        }
        private static float Finite(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite value.");return value;}
    }
}
