using System;
using System.Reflection;
using JueMingR.Features.CoinDeposit;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinSeedChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context, object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            Main.tile[40,40].type=29;
            p.inventory[5]=Coin(73,2); p.inventory[7]=Coin(73,3); p.inventory[50]=Coin(72,17);
            p.inventory[52]=Coin(74,2);p.inventory[52].favorited=true;
            p.bank.item[0].SetDefaults(8);p.bank.item[0].stack=9;
            p.bank.item[1].SetDefaults(0); // Legal original Air, not plain-new fields.
            Item moved=p.inventory[5],noncoin=p.bank.item[0],oldAir=p.bank.item[1],favorite=p.inventory[52];
            long before=Total(p.inventory,58)+Total(p.bank.item,40);
            object transfer=Get(host,"Transfer"); long calls=(long)Get(transfer,"NativeCalls");
            var result=Execute(host);
            Require(ReferenceEquals(p.bank.item[1],moved) && p.inventory[5].IsAir,
                "real zero-return normalization permits a fresh highest-denomination whole-stack first deposit");
            Require(result=="Partial" && (long)Get(transfer,"NativeCalls")==calls+1 && (long)Get(transfer,"Amount")==20000,
                "first deposit is one distinct bounded operation after one actual native call");
            Require(ReferenceEquals(p.bank.item[0],noncoin) && noncoin.stack==9 && ReferenceEquals(p.inventory[52],favorite) && favorite.favorited &&
                p.inventory[7].stack==3 && p.inventory[50].stack==17 && !ReferenceEquals(p.inventory[5],oldAir) &&
                Total(p.inventory,58)+Total(p.bank.item,40)==before,"seed preserves favorites, noncoins, other stacks and both-end total without restoring old Air");
            NativeCoinMatrix.Reset(p,host);
            foreach(ushort tile in new ushort[]{97,463,491})
            {
                NativeCoinMatrix.Reset(p,host);Main.tile[40,40].type=tile;p.inventory[50]=Coin(73,2);
                long seedCalls=(long)Get(transfer,"SeedCalls");NativeCoinMatrix.Tick(host,0,300);
                Require((long)Get(transfer,"SeedCalls")==seedCalls && p.inventory[50].stack==2,"other three empty accounts never inherit piggy exception: "+tile);
            }
            NativeCoinMatrix.Reset(p,host);Main.tile[40,40].type=29;p.inventory[50]=Coin(74,7);
            for(int i=0;i<40;i++){p.bank.item[i].SetDefaults(8);p.bank.item[i].stack=1;}
            calls=(long)Get(transfer,"NativeCalls");long fullSeeds=(long)Get(transfer,"SeedCalls");
            NativeCoinMatrix.Tick(host,0,2000);
            Require((long)Get(transfer,"NativeCalls")==calls+1 && (long)Get(transfer,"SeedCalls")==fullSeeds && p.inventory[50].stack==7,
                "coinless full piggy makes one bounded attempt without seed/retry loop");
            p.bank.item[39]=new Item();NativeCoinMatrix.Tick(host,2000,2100);
            Require(p.inventory[50].IsAir && p.bank.item[39].type==74 && p.bank.item[39].stack==7,"real capacity change wakes full coinless piggy for one whole stack");
            NativeCoinMatrix.Reset(p,host);
            Console.WriteLine("PASS: true native empty-bank normalization and fresh whole-stack piggy first deposit.");
        }
        internal static string Execute(object host)
        {
            object range=Get(host,"Range");object[] args={Main.LocalPlayer,false,false};
            Require((bool)range.GetType().GetMethod("TryComplete",Flags).Invoke(range,args) && (bool)args[2],"seed fixture real current bank query");
            object entrance=((Array)Get(range,"Banks")).GetValue(0);
            return Call(Get(host,"Transfer"),"Execute",entrance,((CoinIntent)Get(host,"Intent")).Generation).ToString();
        }
    }
}
