using System;
using System.Linq;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatChecks
    {
        internal static void Run(object context)
        {
            object combat=GetOptional(context,"Combat");
            Require(combat!=null,"full candidate composition must include the independent combat owner");
            Require((bool)Get(combat,"Available"),"combat native seams installed: "+GetOptional(combat,"SetupError"));
            var catalog=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.WeaponCatalog");
            Func<string,Item,bool> eligible=(name,item)=>(bool)catalog.GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{item});
            var native=new Item();
            int[] release={198,199,200,201,202,203,4258,5535,5670,3764,3765,3766,3767,3768,3769,4259,5536,5671,671,3772,3352,5462,6153};
            foreach(int type in release){native.SetDefaults(type);Require(eligible("Switchable",native),"verified release weapon "+type);}
            foreach(int type in new[]{113,218,495,2882,3541,3269,2269,3262,162,3507})
            {native.SetDefaults(type);Require(!eligible("Switchable",native),"sustained, guided, dedicated and ordinary weapon excluded "+type);}
            foreach(int type in new[]{3262,3278,3292,3315,3317,3389,5294}){native.SetDefaults(type);Require(eligible("Yoyo",native),"native yoyo "+type);}
            foreach(int type in new[]{162,163,220,389,801,1259,4272,5011,5012,5526}){native.SetDefaults(type);Require(eligible("Flail",native),"native flail "+type);}
            native.SetDefaults(ItemID.DirtBlock);Require(eligible("AutoClick",native),"no damage requirement on vanilla placeable");
            native.SetDefaults(ItemID.CopperShortsword);Require(eligible("AutoClick",native),"ordinary shortsword remains eligible");
            foreach(int type in new[]{2269,198,5462,6153,3262,162,ItemID.CopperPickaxe,ItemID.WoodFishingPole}){native.SetDefaults(type);Require(!eligible("AutoClick",native),"dedicated release/channel/tool gate "+type);}
            Console.WriteLine("PASS G11A native item defaults: 23 release weapons, flails, yoyos, general eligibility and exclusions.");
            NativeCombatCadenceChecks.Run(context);
        }
    }
}
