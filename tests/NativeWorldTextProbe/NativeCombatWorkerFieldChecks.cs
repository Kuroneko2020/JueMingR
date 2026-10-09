using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerFieldChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(Assembly host)
        {
            NativeCombatWorkerChecks.Scene(false);
            Type codec=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityContext",true);
            foreach(int type in new[]{1,112,552,668})
            {
                var original=new NPC();original.SetDefaults(type);original.GivenName="source\0猫";
                string random=SoundRandoms();byte[] bytes=Write(codec,"WriteNpc",original);var copy=new NPC();Read(codec,"ReadNpc",bytes,copy);
                Require(copy.GivenName==original.GivenName && Sound(copy.HitSound)==Sound(original.HitSound) && Sound(copy.DeathSound)==Sound(original.DeathSound),"Original sound leaf values and names roundtrip: "+type);
                Require(SoundRandoms()==random,"Capture and restore do not sample sound variant or pitch RNG.");
                Require(original.HitSound==null || !ReferenceEquals(original.HitSound,copy.HitSound),"Copied sound has no mutable parent object alias.");
            }
            var dynamicSound=new NPC{HitSound=Terraria.ID.SoundID.NPCHit17.WithVolume(0.37f).WithPitchVariance(0.2f)};
            var restored=new NPC();Read(codec,"ReadNpc",Write(codec,"WriteNpc",dynamicSound),restored);
            Require(Sound(restored.HitSound)==Sound(dynamicSound.HitSound),"Historical sound overrides are not replaced with type defaults.");
            byte[] bad=Write(codec,"WriteNpc",new NPC{HitSound=Terraria.ID.SoundID.NPCHit1});
            // Empty name is Int32(0), then nullable sound and seven leaf values.
            foreach(var item in new[]{Tuple.Create(13,0),Tuple.Create(17,999),Tuple.Create(9,int.MaxValue)})
            {byte[] invalid=(byte[])bad.Clone();Buffer.BlockCopy(BitConverter.GetBytes(item.Item2),0,invalid,item.Item1,4);Refuse(()=>Read(codec,"ReadNpc",invalid,new NPC()),"sound values");}
            byte[] nonfinite=(byte[])bad.Clone();Buffer.BlockCopy(BitConverter.GetBytes(float.NaN),0,nonfinite,21,4);Refuse(()=>Read(codec,"ReadNpc",nonfinite,new NPC()),"sound values");
            Refuse(()=>Read(codec,"ReadNpc",BitConverter.GetBytes(4097),new NPC()),"text limit");
            var readonlyField=typeof(NPC).GetField("netSpamPacketLimit");readonlyField.SetValue(dynamicSound,9);
            Refuse(()=>Write(codec,"WriteNpc",dynamicSound),"readonly NPC constant");
            Dialogue(codec);
            ProjectileValues(codec);
            var schemaType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeValueSnapshot",true);
            object schema=Activator.CreateInstance(schemaType,Flags,null,new object[]{typeof(NPC),false,true,null},null);
            var omissions=(string[])schemaType.GetField("Unsupported",Flags).GetValue(schema);
            Require(omissions.Any(s=>s.StartsWith("Terraria.NPC.netSpamPacketLimit:")),"Readonly omissions remain visible in schema inventory.");
            Console.WriteLine("PASS captured NPC sounds/name/dialogue/readonly and projectile hitbox/whip/lightning/text leaf state, aliases, native readers, source RNG purity and malformed values.");
        }
        private static void Dialogue(Type codec)
        {
            var registryField=typeof(ConditionalDialogue).GetField("_registry",Flags);object old=registryField.GetValue(null);bool oldCake=NPC.freeCake;
            try
            {
                registryField.SetValue(null,new List<ConditionalDialogue>[Terraria.ID.NPCID.Count]);typeof(ConditionalDialogue).GetMethod("Init",Flags).Invoke(null,null);
                var original=new NPC();original.SetDefaults(208);original.whoAmI=0;NPC.freeCake=true;
                typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);
                var check=typeof(NPC).GetMethod("CheckDialogue",Flags);check.Invoke(original,null);
                Require(original.nextDialogue!=null && original.nextDialogue.ConditionsMet(original),"Original ten-tick dialogue selection actually occurs.");
                original.nextDialogue.HideIndicator();byte[] shared=Write(codec,"WriteShared"),page=Write(codec,"WriteNpc",original);
                Read(codec,"ReadShared",shared);var a=new NPC{type=208,whoAmI=0};var b=new NPC{type=195,whoAmI=0};Read(codec,"ReadNpc",page,a);Read(codec,"ReadNpc",page,b);
                Require(ReferenceEquals(a.nextDialogue,b.nextDialogue) && !ReferenceEquals(a.nextDialogue,original.nextDialogue) && !a.nextDialogue.ShowIndicator,"Restored actors share the private original directory object and its mutable indicator.");
                typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1001U);check.Invoke(a,null);
                Require(a.nextDialogue!=null,"Original non-ten-tick check retains the sampled pending dialogue.");
                NPC.freeCake=false;check.Invoke(a,null);Require(a.nextDialogue==null,"Original predicate clears stale pending dialogue without codec normalization.");
                // The second request must restore mutable directory state even
                // though its native predicate and object identity are reused.
                Read(codec,"ReadShared",new byte[]{1,1});Read(codec,"ReadNpc",page,a);Require(a.nextDialogue.ShowIndicator,"Next request restores shared indicator.");
                Read(codec,"ReadShared",new byte[]{0});Refuse(()=>Read(codec,"ReadNpc",page,new NPC()),"Missing NPC dialogue");
                var unknown=new NPC{nextDialogue=new UnknownDialogue()};Refuse(()=>Write(codec,"WriteNpc",unknown),"Unregistered NPC dialogue");
                var table=new List<ConditionalDialogue>[Terraria.ID.NPCID.Count];table[0]=new List<ConditionalDialogue>{unknown.nextDialogue};registryField.SetValue(null,table);
                Refuse(()=>Write(codec,"WriteShared"),"Unknown dialogue");
            }
            finally{registryField.SetValue(null,old);NPC.freeCake=oldCake;}
        }
        private static void ProjectileValues(Type codec)
        {
            var source=new Projectile{miscText="原版\0sign",WhipPointsForCollision=new List<Vector2>{new Vector2(1,2),new Vector2(5,7)}};
            var lightning=typeof(Projectile).GetField("_lightningLastHitChainPos",Flags);lightning.SetValue(source,(Vector2?)new Vector2(13,-9));
            var hitbox=new MultiPointHitbox(new Point(12,18),new[]{new Vector2(100,200),new Vector2(105,210),new Vector2(150,205)});source.customHitbox=hitbox;
            byte[] bytes=Write(codec,"WriteProjectile",source);var copy=new Projectile();Read(codec,"ReadProjectile",bytes,copy);
            Require(copy.miscText==source.miscText && (Vector2?)lightning.GetValue(copy)==(Vector2?)lightning.GetValue(source),"Nullable lightning chain and text restore exact historical values.");
            Require(copy.WhipPointsForCollision.SequenceEqual(source.WhipPointsForCollision) && !ReferenceEquals(copy.WhipPointsForCollision,source.WhipPointsForCollision),"Whip points preserve values without aliases.");
            var result=(MultiPointHitbox)copy.customHitbox;
            Require(result.PointSize==hitbox.PointSize && result.BoundingRect==hitbox.BoundingRect && result.Points.SequenceEqual(hitbox.Points) && !ReferenceEquals(result.Points,hitbox.Points),"MultiPoint geometry and cached bounds restore separately owned values.");
            foreach(var rect in new[]{new Rectangle(100,200,2,2),new Rectangle(140,205,2,2),new Rectangle(110,203,2,2),new Rectangle(1000,200,2,2)})Require(hitbox.Intersects(rect)==result.Intersects(rect),"Independent native Intersects agrees including gaps.");
            source.WhipPointsForCollision[0]=Vector2.Zero;hitbox.Points[0]=new Vector2(-10000,-10000);
            Require(copy.WhipPointsForCollision[0]==new Vector2(1,2) && result.Points[0]==new Vector2(100,200),"Later source mutation cannot change restored shape.");
            Refuse(()=>Write(codec,"WriteProjectile",source),"Inconsistent multipoint");source.customHitbox=new object();Refuse(()=>Write(codec,"WriteProjectile",source),"Unknown projectile hitbox");
            source.customHitbox=null;source.WhipPointsForCollision=new List<Vector2>{new Vector2(float.NaN,0)};Refuse(()=>Write(codec,"WriteProjectile",source),"Nonfinite");
            byte[] bad=(byte[])bytes.Clone();bad[bad.Length-1]^=1;Refuse(()=>Read(codec,"ReadProjectile",bad,new Projectile()),"Inconsistent multipoint");
            var empty=new Projectile{WhipPointsForCollision=null,miscText=null};Read(codec,"ReadProjectile",Write(codec,"WriteProjectile",empty),copy);
            Require(copy.customHitbox==null && copy.WhipPointsForCollision==null && copy.miscText==null && lightning.GetValue(copy)==null,"Nullable fields do not become invented empty values.");
        }
        private sealed class UnknownDialogue:ConditionalDialogue
        {public override string GetChatAndClearCondition(NPC npc){throw new InvalidOperationException("Must not execute dialogue actions.");}}
        private static string Sound(LegacySoundStyle sound)
        {return sound==null?"null":string.Join("|",sound.SoundId,typeof(LegacySoundStyle).GetField("_style",Flags).GetValue(sound),sound.Variations,sound.MaxTrackedInstances,sound.Type,sound.Volume.ToString("R"),sound.PitchVariance.ToString("R"));}
        private static string SoundRandoms()
        {return Random((UnifiedRandom)typeof(LegacySoundStyle).GetField("Random",Flags).GetValue(null))+"/"+Random((UnifiedRandom)typeof(SoundStyle).GetField("_random",Flags).GetValue(null));}
        private static string Random(UnifiedRandom random)
        {return string.Join(";",typeof(UnifiedRandom).GetFields(Flags).Where(f=>!f.IsStatic).Select(f=>{object v=f.GetValue(random);return v is int[]?string.Join(",",(int[])v):Convert.ToString(v);}));}
        private static byte[] Write(Type type,string method,params object[] values)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){type.GetMethod(method,Flags).Invoke(null,new object[]{writer}.Concat(values).ToArray());writer.Flush();return stream.ToArray();}}
        private static void Read(Type type,string method,byte[] bytes,params object[] values)
        {using(var reader=new BinaryReader(new MemoryStream(bytes,false))){type.GetMethod(method,Flags).Invoke(null,new object[]{reader}.Concat(values).ToArray());Require(reader.BaseStream.Position==bytes.Length,"Complete leaf page consumed.");}}
        private static void Refuse(Action operation,string reason)
        {bool refused=false;try{operation();}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException && e.InnerException.Message.Contains(reason);}Require(refused,"Exact malformed context rejected: "+reason);}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
