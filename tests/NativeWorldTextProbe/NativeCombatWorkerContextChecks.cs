using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerContextChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(Assembly host)
        {
            Type context=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",false);
            Require(context!=null,"RED: existing player inventory/selection/sitting and projectile curves need pure context pages.");
            NativeCombatWorkerChecks.Scene(false);
            var source=new Player{active=true,whoAmI=0,position=new Vector2(750,800),itemAnimation=15};
            source.inventory[7]=new Item{type=3006,stack=1,damage=91,shootSpeed=11.25f};
            source.inventory[42]=new Item{type=97,stack=73,ammo=97};
            source.armor[3].SetDefaults(54);source.Loadouts[1].Armor[4].SetDefaults(2423);source.Loadouts[1].Armor[4].favorited=true;
            Terraria.GameContent.Items.ItemVariant variant=null;int variantType=0;
            for(int type=1;type<Terraria.ID.ItemID.Count && variant==null;type++)
            {var entry=Terraria.GameContent.Items.ItemVariants.GetVariants(type).FirstOrDefault();if(entry!=null){variant=entry.Variant;variantType=type;}}
            Require(variant!=null,"Native fixed variant fixture exists.");source.inventory[11]=new Item{type=variantType,stack=1};
            typeof(Item).GetProperty("Variant").GetSetMethod(true).Invoke(source.inventory[11],new object[]{variant});
            source.selectedItemState.Select(7); // not Main.LocalPlayer: native direct selection
            source.sitting=new Terraria.GameContent.PlayerSittingHelper{isSitting=true,details=new Terraria.GameContent.ExtraSeatInfo{IsAToilet=true},offsetForSeat=new Vector2(3,-4),sittingIndex=1};
            Main.player[0]=source;Main.npc[0].soulDrain=true;Main.npc[0].position=new Vector2(700,700);
            byte[] bytes=Write(context,"WritePlayer",source);
            string expected=SoulDrain(source);
            var copy=new Player{active=true,whoAmI=0,position=source.position,itemAnimation=15};
            Read(context,"ReadPlayer",bytes,copy);
            Require(copy.selectedItem==7 && copy.inventory[7].type==3006 && copy.inventory[7].damage==91 && copy.inventory[7].shootSpeed==11.25f && copy.inventory[42].stack==73,"Selected and unselected item value pages restored.");
            Require(source.armor[4].IsAir && source.GetEffectiveArmor(4).type==2423 && copy.GetEffectiveArmor(3).type==54 && copy.GetEffectiveArmor(4).type==2423 && !ReferenceEquals(copy.armor[4],source.GetEffectiveArmor(4)),"Only resolved functional equipment is independently materialized, including shared loadout items.");
            Require(copy.sitting.isSitting && copy.sitting.details.IsAToilet && copy.sitting.offsetForSeat==new Vector2(3,-4) && copy.sitting.sittingIndex==1,"Complete sitting leaf values restored.");
            object selection=copy.selectedItemState;
            Require(ReferenceEquals(typeof(Player.SelectedItemState).GetField("player",Flags).GetValue(selection),copy),"Selected-item owner is the private restored player.");
            Require(SoulDrain(copy)==expected,"Original SoulDrain state and RNG read the same selected inventory.");
            Require(!ReferenceEquals(source.inventory,copy.inventory) && !ReferenceEquals(source.inventory[7],copy.inventory[7]),"No live inventory alias enters private values.");
            Require(ReferenceEquals(copy.inventory[11].Variant,variant),"Variant retains the original fixed definition identity.");
            var projectile=new Projectile{hostileDamageScaling=GameDifficultyData.LightningPlayerDamageScaling};
            byte[] curve=Write(context,"WriteProjectile",projectile);var restored=new Projectile();Read(context,"ReadProjectile",curve,restored);
            foreach(float level in new[]{0f,0.5f,1f,1.75f,2f,3f})
                Require(projectile.hostileDamageScaling.Sample(level)==restored.hostileDamageScaling.Sample(level),"Original existing-projectile difficulty curve survives.");
            Require(!ReferenceEquals(projectile.hostileDamageScaling.keys,restored.hostileDamageScaling.keys),"Curve keys are independent values.");
            foreach(int count in new[]{0,17})Refuse(context,"ReadProjectile",BitConverter.GetBytes(count),new Projectile(),"curve count");
            byte[] bad=(byte[])curve.Clone();Buffer.BlockCopy(BitConverter.GetBytes(float.NaN),0,bad,4,4);Refuse(context,"ReadProjectile",bad,new Projectile(),"curve key");
            bad=(byte[])curve.Clone();Buffer.BlockCopy(BitConverter.GetBytes(-1f),0,bad,12,4);Refuse(context,"ReadProjectile",bad,new Projectile(),"curve key");
            projectile.hostileDamageScaling=new GameDifficultyData.LinearCurve(new GameDifficultyData.LinearCurve.Key(1,2),new GameDifficultyData.LinearCurve.Key(1,3));
            Read(context,"ReadProjectile",Write(context,"WriteProjectile",projectile),restored);
            Require(restored.hostileDamageScaling.keys.Length==2 && restored.hostileDamageScaling.Sample(1)==2,"Equal-input curve keys are preserved in native order.");
            int first;using(var r=new BinaryReader(new MemoryStream(bytes))){r.ReadString();first=(int)r.BaseStream.Position;}
            bad=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes(-2),0,bad,first,4);Refuse(context,"ReadPlayer",bad,new Player(),"selection");
            Console.WriteLine("PASS existing player item/selection/sitting pure values, original SoulDrain/RNG and projectile difficulty curve; malformed leaf pages rejected.");
            NativeCombatWorkerTagChecks.Run(host);
        }
        private static string SoulDrain(Player player)
        {
            Main.player[0]=player;player.soulDrain=0;Main.rand=new UnifiedRandom(777);
            for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust();
            typeof(NPC).GetMethod("UpdateNPC_SoulDrainDebuff",Flags).Invoke(Main.npc[0],null);
            Require(player.soulDrain==1,"Original selected-slot SoulDrain branch actually executed.");
            return player.soulDrain+"|"+NativeCombatWorkerChecks.RandomStamp();
        }
        private static byte[] Write(Type type,string method,object value)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){type.GetMethod(method,Flags).Invoke(null,new[]{(object)writer,value});writer.Flush();return stream.ToArray();}}
        private static void Read(Type type,string method,byte[] bytes,object value)
        {using(var reader=new BinaryReader(new MemoryStream(bytes,false)))type.GetMethod(method,Flags).Invoke(null,new[]{(object)reader,value});}
        private static void Refuse(Type type,string method,byte[] bytes,object value,string reason)
        {bool refused=false;try{Read(type,method,bytes,value);}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException && e.InnerException.Message.Contains(reason);}Require(refused,"Malformed "+reason+" rejected.");}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
