using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Items;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Explicit leaf state which is not in the primitive actor page. Restoring
    // it must not rerun gameplay operations (Select, SitDown, Item.SetDefaults
    // or Prefix). Mount, effects and other complex objects remain separate
    // closure requirements; this is not a general object-graph serializer.
    internal static class NativeActorContext
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly NativeValueSnapshot Items=new NativeValueSnapshot(typeof(Item));
        private static readonly NativeValueSnapshot Selection=new NativeValueSnapshot(typeof(Player.SelectedItemState));
        private static readonly FieldInfo[] SelectionFields={typeof(Player.SelectedItemState).GetField("selected",Flags),typeof(Player.SelectedItemState).GetField("hotbar",Flags),typeof(Player.SelectedItemState).GetField("buffered",Flags),typeof(Player.SelectedItemState).GetField("overridden",Flags)};
        private static readonly string[] VariantNames={"StrongerVariant","WeakerVariant","RebalancedVariant","EnabledVariant","DisabledBossSummonVariant"};
        private static readonly MethodInfo SetVariant=typeof(Item).GetProperty("Variant").GetSetMethod(true);
        internal static void WritePlayer(BinaryWriter writer,Player player)
        {
            if(player.inventory==null || player.inventory.Length!=59)throw new InvalidDataException("Player inventory shape.");
            ValidateSelection(player.selectedItemState);Selection.Write(writer,player.selectedItemState);
            foreach(Item item in player.inventory)
                WriteItem(writer,item);
            // Resolve only the ten functional equipment slots at capture.
            // Native NPC logic uses GetEffectiveArmor, which may choose a
            // favorited item from another loadout. The fixed-input continuation
            // materializes these values, not unused loadout/vanity subtrees.
            for(int slot=0;slot<10;slot++)WriteItem(writer,player.GetEffectiveArmor(slot));
            var seat=player.sitting;writer.Write(seat.isSitting);writer.Write(seat.details.IsAToilet);
            writer.Write(seat.offsetForSeat.X);writer.Write(seat.offsetForSeat.Y);writer.Write(seat.sittingIndex);
            NativeTagSnapshot.Write(writer,player);
        }
        internal static void ReadPlayer(BinaryReader reader,Player player)
        {
            // Keep the owner bound by this private Player's constructor. A
            // boxed struct is copied back only after its four indices validate.
            object selected=player.selectedItemState;Selection.Read(reader,selected);ValidateSelection(selected);
            player.selectedItemState=(Player.SelectedItemState)selected;
            var inventory=new Item[59];
            for(int i=0;i<inventory.Length;i++)inventory[i]=ReadItem(reader);
            player.inventory=inventory;
            for(int slot=0;slot<10;slot++)player.armor[slot]=ReadItem(reader)??throw new InvalidDataException("Effective equipment is absent.");
            player.sitting=new PlayerSittingHelper{isSitting=reader.ReadBoolean(),details=new ExtraSeatInfo{IsAToilet=reader.ReadBoolean()},offsetForSeat=new Vector2(Finite(reader.ReadSingle(),"sitting"),Finite(reader.ReadSingle(),"sitting")),sittingIndex=reader.ReadInt32()};
            NativeTagSnapshot.Read(reader,player);
        }
        private static void WriteItem(BinaryWriter writer,Item item)
        {writer.Write(item!=null);if(item!=null){Items.Write(writer,item);writer.Write(VariantId(item.Variant));}}
        private static Item ReadItem(BinaryReader reader)
        {
            if(!reader.ReadBoolean())return null;
            var item=new Item();Items.Read(reader,item);
            if(item.type<0 || item.type>=Terraria.ID.ItemID.Count)throw new InvalidDataException("Item value identity.");
            byte variant=reader.ReadByte();if(variant>VariantNames.Length)throw new InvalidDataException("Item variant identity.");
            ItemVariant identity=variant==0?null:Variant(variant-1);
            if(identity!=null && !ItemVariants.HasVariant(item.type,identity))throw new InvalidDataException("Item variant/type mismatch.");
            SetVariant.Invoke(item,new object[]{identity});return item;
        }
        internal static void WriteProjectile(BinaryWriter writer,Projectile projectile)
        {
            var keys=projectile.hostileDamageScaling.keys;ValidateCurve(keys);writer.Write(keys.Length);
            foreach(var key in keys){writer.Write(key.input);writer.Write(key.output);}
            NativeEntityContext.WriteProjectile(writer,projectile);
        }
        internal static void ReadProjectile(BinaryReader reader,Projectile projectile)
        {
            int count=reader.ReadInt32();if(count<1 || count>16)throw new InvalidDataException("Invalid curve count.");
            var keys=new GameDifficultyData.LinearCurve.Key[count];
            for(int i=0;i<count;i++)keys[i]=new GameDifficultyData.LinearCurve.Key(reader.ReadSingle(),reader.ReadSingle());
            ValidateCurve(keys);projectile.hostileDamageScaling=new GameDifficultyData.LinearCurve(keys);
            NativeEntityContext.ReadProjectile(reader,projectile);
        }
        private static void ValidateCurve(GameDifficultyData.LinearCurve.Key[] keys)
        {
            if(keys==null || keys.Length<1 || keys.Length>16)throw new InvalidDataException("Invalid curve count.");
            float previous=float.NegativeInfinity;
            foreach(var key in keys)
            {Finite(key.input,"curve key");Finite(key.output,"curve key");if(key.input<previous)throw new InvalidDataException("Unordered curve key.");previous=key.input;}
        }
        private static void ValidateSelection(object selected)
        {
            for(int i=0;i<SelectionFields.Length;i++)
            {int value=(int)SelectionFields[i].GetValue(selected);if(value<(i<2?0:-1) || value>(i==1?9:58))throw new InvalidDataException("Invalid player selection.");}
        }
        private static byte VariantId(ItemVariant value)
        {
            if(value==null)return 0;
            for(int i=0;i<VariantNames.Length;i++)if(ReferenceEquals(Variant(i),value))return (byte)(i+1);
            throw new InvalidDataException("Unknown item variant identity.");
        }
        private static ItemVariant Variant(int index){return (ItemVariant)typeof(ItemVariants).GetField(VariantNames[index],Flags).GetValue(null);}
        private static float Finite(float value,string field){if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite "+field+".");return value;}
    }
}
