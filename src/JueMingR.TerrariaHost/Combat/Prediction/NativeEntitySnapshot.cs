using System;
using System.IO;
using System.Reflection;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Only explicitly required projectile dependencies belong to this scene.
    // A key is not a slot or a numeric float: preserve its packed identity and
    // rebuild the native key index before any NPC can dereference an ai slot.
    internal static class NativeEntitySnapshot
    {
        private static readonly NativeValueSnapshot Projectiles=new NativeValueSnapshot(typeof(Projectile));
        private static readonly FieldInfo Generations=typeof(Projectile).GetField("slotGenerations",BindingFlags.Static|BindingFlags.NonPublic);
        internal static void Write(BinaryWriter writer,int[] slots)
        {
            var generations=(int[])Generations.GetValue(null);
            if(generations.Length!=1001 || slots==null || slots.Length>Main.maxProjectiles+1)throw new InvalidDataException("Projectile dependency shape.");
            foreach(int generation in generations)writer.Write(generation);
            writer.Write(slots.Length);int prior=-1;
            foreach(int slot in slots)
            {
                if(slot<=prior || slot>Main.maxProjectiles || Main.projectile[slot]==null || Main.projectile[slot].active && Main.projectile[slot].whoAmI!=slot)throw new InvalidDataException("Projectile dependency identity.");
                prior=slot;writer.Write(slot);Projectiles.Write(writer,Main.projectile[slot]);NativeActorContext.WriteProjectile(writer,Main.projectile[slot]);
                AimLightTrace.Guardian(Main.projectile[slot],(long)Main.GameUpdateCount);
            }
        }
        internal static int[] Read(BinaryReader reader)
        {
            var generations=(int[])Generations.GetValue(null);for(int i=0;i<generations.Length;i++)generations[i]=reader.ReadInt32();
            // Directory keys must remain visible: an omitted dependency must
            // produce a missing-page response, not TryLookup(false).
            int count=reader.ReadInt32();if(count<0 || count>Main.maxProjectiles+1)throw new InvalidDataException("Projectile dependency count.");
            var slots=new int[count];int prior=-1;
            for(int i=0;i<count;i++)
            {
                int slot=reader.ReadInt32();if(slot<=prior || slot>Main.maxProjectiles)throw new InvalidDataException("Projectile dependency order.");
                prior=slot;slots[i]=slot;var projectile=Main.projectile[slot];uint directoryKey=(uint)projectile.key;int directoryType=projectile.type;bool active=projectile.active;Projectiles.Read(reader,projectile);
                if((uint)projectile.key!=directoryKey || projectile.type!=directoryType || projectile.active!=active)throw new InvalidDataException("Projectile page conflicts with directory identity.");
                if(projectile.active && (projectile.whoAmI!=slot || projectile.type==0) || projectile.type<0 || projectile.type>=Terraria.ID.ProjectileID.Count || projectile.key.Index>1000)throw new InvalidDataException("Projectile key identity.");
                if(projectile.ai==null || projectile.ai.Length!=3 || projectile.localAI==null || projectile.localAI.Length!=3)throw new InvalidDataException("Projectile AI shape.");
                NativeActorContext.ReadProjectile(reader,projectile);
                // A full page is not permission to reindex a stale/noncanonical
                // key. The directory preserves the original lookup relation.
                NativeEntityDirectory.KnowProjectile(slot);
            }
            return slots;
        }
    }
}
