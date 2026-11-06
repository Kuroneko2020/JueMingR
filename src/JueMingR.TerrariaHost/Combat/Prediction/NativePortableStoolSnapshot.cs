using System.IO;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // This leaf is deliberately separate from the primitive Player schema.
    // It is current observed geometry, not an instruction to equip, stand,
    // resize or replay Player.Update. The protocol and authenticated Host code
    // own its layout; future versions and damaged combinations are refused.
    internal static class NativePortableStoolSnapshot
    {
        private const int Version=1;
        internal static void Write(BinaryWriter writer,Player player)
        {
            var value=player.portableStoolInfo;Validate(value);
            writer.Write(Version);writer.Write(value.HasAStool);writer.Write(value.IsInUse);
            writer.Write(value.HeightBoost);writer.Write(value.VisualYOffset);writer.Write(value.MapYOffset);
        }
        internal static void Read(BinaryReader reader,Player player)
        {
            if(reader.ReadInt32()!=Version)throw new InvalidDataException("Portable stool page version.");
            var value=new PortableStoolUsage{HasAStool=Boolean(reader),IsInUse=Boolean(reader),
                HeightBoost=reader.ReadInt32(),VisualYOffset=reader.ReadInt32(),MapYOffset=reader.ReadInt32()};
            Validate(value);player.portableStoolInfo=value;
        }
        private static bool Boolean(BinaryReader reader)
        {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException("Portable stool boolean.");return value!=0;}
        private static void Validate(PortableStoolUsage value)
        {
            // Locked .8 Reset clears all five values; both original equipment
            // producers set 26/26/26. JumpMovement may clear only IsInUse, so
            // equipment does not imply use, and primitive height must NOT be
            // tied to this bit across the original resize/jump stages.
            bool valid=value.HasAStool
                ? value.HeightBoost==26 && value.VisualYOffset==26 && value.MapYOffset==26
                : !value.IsInUse && value.HeightBoost==0 && value.VisualYOffset==0 && value.MapYOffset==0;
            if(!valid)throw new InvalidDataException("Portable stool value combination.");
        }
    }
}
