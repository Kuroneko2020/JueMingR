using System;
using System.Reflection;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinWorkloadChecks
    {
        internal static void Run(object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            var settings = (CoinSettings)Get(host, "Settings"); var intent = (CoinIntent)Get(host, "Intent");
            object range = Get(host, "Range"), transfer = Get(host, "Transfer");
            Func<string, long> r = name => (long)Get(range, name);
            Func<string, long> t = name => (long)Get(transfer, name);
            Type hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.CoinDeposit.CoinWithdrawalHooks");
            Func<long> scopes = () => (long)hooks.GetField("Scopes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            settings.Set(false); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !settings.Busy; });
            long tiles = r("TileReads"), projectiles = r("ProjectileReads"), bankReads = (long)Get(host,"CapacityReads"), snapshots = t("Snapshots"), sourceScopes = scopes(), wallet = (long)Get(host,"WalletReads");
            p.bank.item[0] = Coin(72, 99); p.chest = -2;
            for (int i = 0; i < 100; i++) { Main.mouseItem = new Item(); ItemSlot.PickupItemIntoMouse(p.bank.item, 4, 0, p); }
            NativeCoinMatrix.Tick(host, 0, 10000);
            Require(r("TileReads") == tiles && r("ProjectileReads") == projectiles && (long)Get(host,"CapacityReads") == bankReads && t("Snapshots") == snapshots && scopes() == sourceScopes && (long)Get(host,"WalletReads") == wallet,
                "disabled hot/native hooks and 10000 updates have no coin observation/plan/storage work");
            settings.Set(true); NativeQuickItemChecks.Until(() => { Call(host,"Poll"); return !settings.Busy; });
            NativeCoinMatrix.Reset(p,host); NativeCoinMatrix.Tick(host,0,10000);
            Require(r("TileReads")==tiles && r("ProjectileReads")==projectiles && (long)Get(host,"CapacityReads")==bankReads && t("Snapshots")==snapshots,
                "enabled empty-wallet updates never search banks");
            long finiteWallet = (long)Get(host,"WalletReads") - wallet;
            Require(finiteWallet <= 58L * 1667 && finiteWallet > 0,"finite coalesced empty-wallet observations are counted honestly");

            NativeCoinMatrix.Reset(p,host); Main.tile[40,40].type=29;
            intent.Withdrawal((long)Get(host,"Session"),intent.Generation,100,100);
            NativeCoinMatrix.Tick(host,0,10); tiles=r("TileReads"); projectiles=r("ProjectileReads"); wallet=(long)Get(host,"WalletReads");
            NativeCoinMatrix.Tick(host,10,10010);
            Require(intent.Protected && r("TileReads")==tiles && r("ProjectileReads")==projectiles && (long)Get(host,"WalletReads")==wallet && (long)Get(host,"CapacityReads")==bankReads && t("Snapshots")==snapshots,
                "stable nonvoid protection uses only positive entrance witness, no bank balances or periodic full scan");

            // A still-valid nonvoid witness is sufficient even when the cache
            // also contains a void entrance. No bag scan is then necessary.
            NativeCoinMatrix.Reset(p, host); Main.tile[40,40].type=29; Main.tile[41,40].type=491;
            object[] query = { p, false, false }; range.GetType().GetMethod("TryComplete", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(range, query);
            Require((int)Get(range,"Count") == 2, "mixed bank witness fixture");
            intent.Withdrawal((long)Get(host,"Session"),intent.Generation,100,100);
            wallet=(long)Get(host,"WalletReads"); tiles=r("TileReads"); projectiles=r("ProjectileReads");
            NativeCoinMatrix.Tick(host,0,10000);
            Require(intent.Protected && (long)Get(host,"WalletReads")==wallet && r("TileReads")==tiles && r("ProjectileReads")==projectiles,
                "mixed nonvoid and void protection keeps wallet and region work at zero");

            NativeCoinMatrix.Reset(p,host); Main.tile[40,40].type=29;p.bank.item[0]=Coin(71,1);p.inventory[50]=Coin(73,1);
            NativeCoinMatrix.Tick(host,0,160);tiles=r("TileReads");projectiles=r("ProjectileReads");long calls=t("NativeCalls");
            NativeCoinMatrix.Tick(host,160,1000);
            Require(Total(p.inventory,58)==0 && r("TileReads")==tiles && r("ProjectileReads")==projectiles && t("NativeCalls")==calls,"completed empty wallet stops discovery and transactions");
            // High activity is coalesced by actual work, not per pickup. Each
            // write still gets an atomic order and fresh source authorization.
            long full=r("CompleteQueries");calls=t("NativeCalls");
            for(ulong tick=1000;tick<1600;tick++){if(p.inventory[50].IsAir)p.inventory[50]=Coin(71,1);else if(p.inventory[50].stack<90)p.inventory[50].stack++;Call(host,"Update",tick);}
            Require(t("NativeCalls")-calls<=10 && r("CompleteQueries")-full<=10 && t("NativeCalls")>calls,"continuous pickups coalesce actual native calls and complete order checks");
            Console.WriteLine("PASS: G06 workload: off 10000 updates zero coin work; no-coin wallet reads="+finiteWallet+"; stable protection no balance/region work; high-activity native calls="+(t("NativeCalls")-calls)+" / 600 updates.");
        }
    }
}
