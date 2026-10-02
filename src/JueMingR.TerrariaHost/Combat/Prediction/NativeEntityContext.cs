using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Historical reference fields on captured entities. These codecs are
    // deliberately explicit: an outer reference is safe only with its complete
    // supported leaf state. Player/network/audio facilities have other owners.
    internal static class NativeEntityContext
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private const int MaximumText=4096,MaximumPoints=4096;
        private static readonly FieldInfo GivenName=typeof(NPC).GetField("_givenName",Flags);
        private static readonly FieldInfo Style=typeof(LegacySoundStyle).GetField("_style",Flags);
        private static readonly FieldInfo Lightning=typeof(Projectile).GetField("_lightningLastHitChainPos",Flags);
        private static readonly FieldInfo Registry=typeof(ConditionalDialogue).GetField("_registry",Flags);
        private static readonly MethodInfo Indicator=typeof(ConditionalDialogue).GetProperty("ShowIndicator").GetSetMethod(true);
        private static readonly ConstructorInfo SoundConstructor=typeof(LegacySoundStyle).GetConstructor(Flags,null,new[]{typeof(int),typeof(int),typeof(int),typeof(SoundType),typeof(float),typeof(float),typeof(int)},null);
        private static ConditionalDialogue privateCake;
        private static List<ConditionalDialogue>[] privateRegistry;
        internal static readonly string[] NpcFields={"Terraria.NPC._givenName","Terraria.NPC.HitSound","Terraria.NPC.DeathSound","Terraria.NPC.nextDialogue","Terraria.NPC.netSpamPacketLimit","Terraria.NPC.netSpamTicksPerPacket","Terraria.NPC.netSpamTicksPerPacketForBosses"};
        internal static readonly string[] ProjectileFields={"Terraria.Projectile.customHitbox","Terraria.Projectile.miscText","Terraria.Projectile._lightningLastHitChainPos","Terraria.Projectile.WhipPointsForCollision"};

        internal static void WriteShared(BinaryWriter writer)
        {
            ConditionalDialogue cake=RegisteredCake();writer.Write(cake!=null);
            if(cake!=null)writer.Write(cake.ShowIndicator);
        }
        internal static void ReadShared(BinaryReader reader)
        {
            if(privateRegistry==null)
            {
                // This exact native initializer only registers the fixed cake
                // predicate. Never run Main initialization or dialogue actions.
                privateRegistry=new List<ConditionalDialogue>[Terraria.ID.NPCID.Count];Registry.SetValue(null,privateRegistry);
                typeof(ConditionalDialogue).GetMethod("Init",Flags).Invoke(null,null);
                privateCake=RegisteredCake();
            }
            bool present=reader.ReadBoolean();
            privateRegistry[208]=present?new List<ConditionalDialogue>{privateCake}:null;
            Registry.SetValue(null,privateRegistry);
            Indicator.Invoke(privateCake,new object[]{present?reader.ReadBoolean():true});
        }
        private static ConditionalDialogue RegisteredCake()
        {
            var registry=(List<ConditionalDialogue>[])Registry.GetValue(null);
            if(registry==null || registry.Length!=Terraria.ID.NPCID.Count)throw new InvalidDataException("Dialogue registry shape.");
            ConditionalDialogue result=null;
            for(int i=0;i<registry.Length;i++)
            {
                var list=registry[i];if(list==null || list.Count==0)continue;
                if(i!=208 || list.Count!=1 || list[0]==null || list[0].GetType().FullName!="Terraria.GameContent.ConditionalDialogue+FreeCakeDialogue")throw new InvalidDataException("Unknown dialogue directory identity.");
                result=list[0];
            }
            return result;
        }
        internal static void WriteNpc(BinaryWriter writer,NPC npc)
        {
            ValidateConstants(npc);WriteText(writer,(string)GivenName.GetValue(npc));
            WriteSound(writer,npc.HitSound);WriteSound(writer,npc.DeathSound);
            if(npc.nextDialogue!=null && !ReferenceEquals(npc.nextDialogue,RegisteredCake()))throw new InvalidDataException("Unregistered NPC dialogue.");
            writer.Write(npc.nextDialogue!=null);
        }
        internal static void ReadNpc(BinaryReader reader,NPC npc)
        {
            ValidateConstants(npc);GivenName.SetValue(npc,ReadText(reader));
            npc.HitSound=ReadSound(reader);npc.DeathSound=ReadSound(reader);
            bool required=reader.ReadBoolean();npc.nextDialogue=required?RegisteredCake():null;
            // A non-null identity cannot be invented from an absent catalogue.
            if(npc.nextDialogue==null && required)throw new InvalidDataException("Missing NPC dialogue directory.");
        }
        private static void ValidateConstants(NPC npc)
        {
            if(npc.netSpamPacketLimit!=3 || npc.netSpamTicksPerPacket!=30 || npc.netSpamTicksPerPacketForBosses!=5)throw new InvalidDataException("Native readonly NPC constant changed.");
        }
        private static void WriteSound(BinaryWriter writer,LegacySoundStyle value)
        {
            writer.Write(value!=null);if(value==null)return;
            if(value.GetType()!=typeof(LegacySoundStyle))throw new InvalidDataException("Unknown NPC sound type.");
            int style=(int)Style.GetValue(value);
            ValidateSound(style,value.Variations,value.Type,value.Volume,value.PitchVariance);
            // Style is a RANDOM getter when Variations != 1. Sampling its
            // backing value must leave both live sound RNGs untouched.
            writer.Write(value.SoundId);writer.Write(style);writer.Write(value.Variations);writer.Write((int)value.Type);
            writer.Write(value.Volume);writer.Write(value.PitchVariance);writer.Write(value.MaxTrackedInstances);
        }
        private static LegacySoundStyle ReadSound(BinaryReader reader)
        {
            if(!reader.ReadBoolean())return null;
            int id=reader.ReadInt32(),style=reader.ReadInt32(),variations=reader.ReadInt32();var type=(SoundType)reader.ReadInt32();
            float volume=reader.ReadSingle(),pitch=reader.ReadSingle();int maximum=reader.ReadInt32();
            ValidateSound(style,variations,type,volume,pitch);
            return (LegacySoundStyle)SoundConstructor.Invoke(new object[]{id,style,variations,type,volume,pitch,maximum});
        }
        private static void ValidateSound(int style,int variations,SoundType type,float volume,float pitch)
        {
            if(variations<1 || (long)style+variations>int.MaxValue || !Enum.IsDefined(typeof(SoundType),type) || !Finite(volume) || !Finite(pitch))throw new InvalidDataException("Invalid NPC sound values.");
        }
        internal static void WriteProjectile(BinaryWriter writer,Projectile projectile)
        {
            WriteText(writer,projectile.miscText);
            var lightning=(Vector2?)Lightning.GetValue(projectile);writer.Write(lightning.HasValue);if(lightning.HasValue)WriteVector(writer,lightning.Value);
            var points=projectile.WhipPointsForCollision;writer.Write(points==null?-1:points.Count);
            if(points!=null){CheckCount(points.Count,0);foreach(var point in points)WriteVector(writer,point);}
            writer.Write(projectile.customHitbox!=null);if(projectile.customHitbox==null)return;
            if(projectile.customHitbox.GetType()!=typeof(MultiPointHitbox))throw new InvalidDataException("Unknown projectile hitbox type.");
            var hitbox=(MultiPointHitbox)projectile.customHitbox;
            if(hitbox.Points==null)throw new InvalidDataException("Null multipoint hitbox points.");CheckCount(hitbox.Points.Length,1);
            // Points are mutable but the cached rectangle is readonly. Refuse
            // an inconsistent source rather than silently changing its shape.
            if(new MultiPointHitbox(hitbox.PointSize,hitbox.Points).BoundingRect!=hitbox.BoundingRect)throw new InvalidDataException("Inconsistent multipoint bounds.");
            writer.Write(hitbox.PointSize.X);writer.Write(hitbox.PointSize.Y);writer.Write(hitbox.Points.Length);foreach(var point in hitbox.Points)WriteVector(writer,point);
            writer.Write(hitbox.BoundingRect.X);writer.Write(hitbox.BoundingRect.Y);writer.Write(hitbox.BoundingRect.Width);writer.Write(hitbox.BoundingRect.Height);
        }
        internal static void ReadProjectile(BinaryReader reader,Projectile projectile)
        {
            projectile.miscText=ReadText(reader);Lightning.SetValue(projectile,reader.ReadBoolean()?(Vector2?)ReadVector(reader):null);
            int count=reader.ReadInt32();if(count< -1)throw new InvalidDataException("Invalid whip point count.");
            if(count<0)projectile.WhipPointsForCollision=null;
            else{CheckCount(count,0);var points=new List<Vector2>(count);for(int i=0;i<count;i++)points.Add(ReadVector(reader));projectile.WhipPointsForCollision=points;}
            projectile.customHitbox=null;if(!reader.ReadBoolean())return;
            var size=new Point(reader.ReadInt32(),reader.ReadInt32());count=reader.ReadInt32();CheckCount(count,1);
            var vertices=new Vector2[count];for(int i=0;i<count;i++)vertices[i]=ReadVector(reader);
            var hitbox=new MultiPointHitbox(size,vertices);var bounds=new Rectangle(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
            if(hitbox.BoundingRect!=bounds)throw new InvalidDataException("Inconsistent multipoint bounds.");projectile.customHitbox=hitbox;
        }
        private static void CheckCount(int count,int minimum){if(count<minimum || count>MaximumPoints)throw new InvalidDataException("Entity context point limit.");}
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
        private static void WriteVector(BinaryWriter writer,Vector2 value)
        {if(!Finite(value.X)||!Finite(value.Y))throw new InvalidDataException("Nonfinite entity context point.");writer.Write(value.X);writer.Write(value.Y);}
        private static Vector2 ReadVector(BinaryReader reader)
        {var value=new Vector2(reader.ReadSingle(),reader.ReadSingle());if(!Finite(value.X)||!Finite(value.Y))throw new InvalidDataException("Nonfinite entity context point.");return value;}
        private static void WriteText(BinaryWriter writer,string text)
        {if(text!=null && text.Length>MaximumText)throw new InvalidDataException("Entity context text limit.");writer.Write(text==null?-1:text.Length);if(text!=null)foreach(char value in text)writer.Write((ushort)value);}
        private static string ReadText(BinaryReader reader)
        {int count=reader.ReadInt32();if(count< -1 || count>MaximumText)throw new InvalidDataException("Entity context text limit.");if(count<0)return null;var chars=new char[count];for(int i=0;i<count;i++)chars[i]=(char)reader.ReadUInt16();return new string(chars);}
    }
}
