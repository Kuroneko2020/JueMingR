using System;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Mount is owned by the captured player, not by its primitive actor page.
    // Restore values and fixed native definitions without SetMount: that API
    // changes buffs, dimensions and velocity and can emit network traffic.
    internal static class NativeMountSnapshot
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly NativeValueSnapshot Values=new NativeValueSnapshot(typeof(Mount),fieldNames:new[]{"_type","_flipDraw","_frame","_frameCounter","_frameExtra","_frameExtraCounter","_frameState","_flyTime","_idleTime","_idleTimeNext","_fatigue","_fatigueMax","_abilityCharging","_abilityCharge","_abilityCooldown","_abilityDuration","_abilityActive","_aiming","_shouldSuperCart","_walkingGraceTimeLeft","_active"});
        private static readonly FieldInfo Definitions=typeof(Mount).GetField("mounts",Flags),Data=typeof(Mount).GetField("_data",Flags),Specific=typeof(Mount).GetField("_mountSpecificData",Flags);
        private static readonly Type Flying=typeof(Mount).GetNestedType("SelectiveFlyingMountData",Flags),Extra=typeof(Mount).GetNestedType("ExtraFrameMountData",Flags),Drill=typeof(Mount).GetNestedType("DrillMountData",Flags),Beam=typeof(Mount).GetNestedType("DrillBeam",Flags);
        private static readonly NativeValueSnapshot FlyingValues=new NativeValueSnapshot(Flying),ExtraValues=new NativeValueSnapshot(Extra),DrillValues=new NativeValueSnapshot(Drill),BeamValues=new NativeValueSnapshot(Beam);
        private static readonly FieldInfo Beams=Drill.GetField("beams",Flags),Target=Beam.GetField("curTileTarget",Flags);

        internal static void InitializePrivateDefinitions()
        {
            // Only the private sandbox calls this once. The original server
            // branch builds movement/size definitions without graphics assets.
            int network=Main.netMode;
            try{Main.netMode=2;Mount.Initialize();}finally{Main.netMode=network;}
        }
        internal static void Write(BinaryWriter writer,Mount mount)
        {
            if(mount==null)throw new InvalidDataException("Missing player mount.");
            var definitions=(Mount.MountData[])Definitions.GetValue(null);object data=Data.GetValue(mount);
            int id=data==null?-1:definitions==null?-2:Array.IndexOf(definitions,data);
            if(id< -1 || data!=null && id<0 || mount.Active && (mount.Type<0 || mount.Type>=Terraria.ID.MountID.Count || id!=mount.Type))throw new InvalidDataException("Unknown mount definition identity.");
            Values.Write(writer,mount);writer.Write(id);
            // A small fixed union, never arbitrary object types or references.
            // These leaves can be read by already observed native projectiles;
            // preserving them does not introduce a Mount.Update simulation.
            object specific=Specific.GetValue(mount);
            byte kind=specific==null?(byte)0:specific is bool?(byte)1:specific.GetType()==Flying?(byte)2:specific.GetType()==Extra?(byte)3:specific.GetType()==Drill?(byte)4:throw new InvalidDataException("Unknown mount leaf.");
            ValidateKind(mount.Type,kind);writer.Write(kind);
            if(kind==1)writer.Write((bool)specific);
            else if(kind==2)FlyingValues.Write(writer,specific);
            else if(kind==3)ExtraValues.Write(writer,specific);
            else if(kind==4)
            {
                DrillValues.Write(writer,specific);var beams=(Array)Beams.GetValue(specific);
                if(beams==null || beams.Length!=8)throw new InvalidDataException("Mount beam count.");
                foreach(object beam in beams){if(beam==null)throw new InvalidDataException("Missing mount beam.");BeamValues.Write(writer,beam);var target=(Point16)Target.GetValue(beam);writer.Write(target.X);writer.Write(target.Y);}
            }
        }
        internal static Mount Read(BinaryReader reader)
        {
            var mount=new Mount();Values.Read(reader,mount);int id=reader.ReadInt32();
            var definitions=(Mount.MountData[])Definitions.GetValue(null);
            if(mount.Type< -1 || mount.Type>=Terraria.ID.MountID.Count || id< -1 || id>=Terraria.ID.MountID.Count || id>=0 && (definitions==null || id>=definitions.Length || definitions[id]==null) || mount.Active && (mount.Type<0 || id!=mount.Type))throw new InvalidDataException("Invalid mount definition identity.");
            // Native Reset leaves its former _data behind even when type=-1.
            // Preserve that identity instead of silently clearing the leaf.
            Data.SetValue(mount,id<0?null:definitions[id]);
            byte kind=reader.ReadByte();ValidateKind(mount.Type,kind);object specific=null;
            if(kind==1)specific=reader.ReadBoolean();
            else if(kind>=2)
            {
                specific=Activator.CreateInstance(kind==2?Flying:kind==3?Extra:Drill,true);
                (kind==2?FlyingValues:kind==3?ExtraValues:DrillValues).Read(reader,specific);
                if(kind==4)foreach(object beam in (Array)Beams.GetValue(specific)){BeamValues.Read(reader,beam);Target.SetValue(beam,new Point16(reader.ReadInt16(),reader.ReadInt16()));}
            }
            Specific.SetValue(mount,specific);return mount;
        }
        private static void ValidateKind(int type,byte kind)
        {if(kind>4 || kind==2 && type!=54 || kind==3 && type!=35 || kind==4 && type!=8)throw new InvalidDataException("Invalid mount leaf identity.");}
    }
}
