using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.CoinDeposit;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    // Faults happen after the actual fixed vanilla entry, never instead of it.
    internal static class NativeCoinSeedAdmissionChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static Action afterNative, afterRelease;
        private static bool nonzero;
        internal static void Run(object context, object host)
        {
            Player p = Main.LocalPlayer; object originalWorld = Main.ActiveWorldFileData;
            var audit = new Harmony("JueMingR.Tests.CoinSeedAdmission");
            MethodInfo native = typeof(ChestUI).GetMethod("MoveCoins", Flags, null, new[] { typeof(Item[]), typeof(Item[]), typeof(int) }, null);
            MethodInfo release = typeof(ItemOperationOwnership).GetMethod("EndCoins");
            audit.Patch(native, postfix: new HarmonyMethod(typeof(NativeCoinSeedAdmissionChecks).GetMethod(nameof(AfterNative), Flags)));
            audit.Patch(release, postfix: new HarmonyMethod(typeof(NativeCoinSeedAdmissionChecks).GetMethod(nameof(AfterRelease), Flags)));
            try
            {
                // A fresh permit must not silently rebind a request to an equal
                // replacement between normalized receipt and lease release.
                Reject(context, host, "post-receipt equal replacement", () => p.inventory[50] = p.inventory[50].Clone(), true);
                Reject(context, host, "wallet equal replacement", () => p.inventory[50] = p.inventory[50].Clone());
                Reject(context, host, "target noncoin equal replacement", () => p.bank.item[0] = p.bank.item[0].Clone());
                Reject(context, host, "other account equal replacement", () => p.bank2.item[0] = p.bank2.item[0].Clone());
                Reject(context, host, "equal total different wallet slots", () => { Item a=p.inventory[50]; p.inventory[50]=p.inventory[51]; p.inventory[51]=a; });
                Reject(context, host, "equal total different target slots", () => { Item a=p.bank.item[0]; p.bank.item[0]=p.bank.item[1]; p.bank.item[1]=a; });
                Reject(context, host, "mouse identity", () => Main.mouseItem = new Item());
                Reject(context, host, "slot58 identity", () => p.inventory[58] = new Item());
                Reject(context, host, "noncanonical Air", () => p.bank.item[1].favorited = true);
                Reject(context, host, "IsAir alone", () => p.bank.item[1].stack = 1);
                Reject(context, host, "unreadable member", () => p.bank.item[39] = null);
                Reject(context, host, "target array", () => p.bank.item = (Item[])p.bank.item.Clone());
                Reject(context, host, "other account array", () => p.bank2.item = (Item[])p.bank2.item.Clone());
                Reject(context, host, "wallet array", () => p.inventory = (Item[])p.inventory.Clone());
                Chest bank=p.bank;
                try { Reject(context, host, "account identity", () => typeof(Player).GetField("bank").SetValue(p,Chest.CreateBank(-2))); }
                finally { typeof(Player).GetField("bank").SetValue(p,bank); }
                Reject(context, host, "withdrawal protection", () => { var intent=(CoinIntent)Get(host,"Intent"); intent.Withdrawal((long)Get(host,"Session"),intent.Generation,100,100); });
                Reject(context, host, "manual operation", () => p.chest=-2);
                Reject(context, host, "vanilla lock", () => p.inventoryChestStack[50]=true);
                Reject(context, host, "shared occupancy", () => { var own=(ItemOperationOwnership)Get(Get(host,"Items"),"Ownership"); own.HoldInterruptedSource(own.Session,1UL<<50); });
                Reject(context, host, "post-release protection", () => { var intent=(CoinIntent)Get(host,"Intent"); intent.Withdrawal((long)Get(host,"Session"),intent.Generation,100,100); },true);
                Reject(context, host, "exception", () => { throw new InvalidOperationException("after-actual-native-zero"); });
                Reject(context, host, "abnormal result", () => nonzero=true);
                Reject(context, host, "world identity", () => Main.ActiveWorldFileData=null);
                Fresh(context,host); p.bank.item[1]=Coin(71,1);
                object transfer=Get(host,"Transfer"); long seeds=(long)Get(transfer,"SeedCalls");
                Require(NativeCoinSeedChecks.Execute(host)=="Completed" && (long)Get(transfer,"SeedCalls")==seeds && Total(p.inventory,58)==0,
                    "actual vanilla transfer completes normally and never enters first-deposit exception");
            }
            finally
            {
                afterNative=afterRelease=null; nonzero=false;
                audit.Unpatch(native,HarmonyPatchType.All,audit.Id); audit.Unpatch(release,HarmonyPatchType.All,audit.Id);
                Main.gamePaused=true; Main.ActiveWorldFileData=(Terraria.IO.WorldFileData)originalWorld; Call(context,"UpdateRuntime");
                Main.gamePaused=false; NativeCoinMatrix.Reset(p,host); Array.Clear(p.inventoryChestStack,0,p.inventoryChestStack.Length);
            }
            Console.WriteLine("PASS: actual native zero-path seed rejects replacement, slot/value mutation, identities, locks, protection, occupancy, exception and unreadable results; no old request rebinding.");
        }
        internal static void Fresh(object context, object host)
        {
            Main.gamePaused=true;
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(System.IO.Path.Combine(Terraria.Program.SavePath,"seed-"+Guid.NewGuid().ToString("N")+".wld"),false);
            Call(context,"UpdateRuntime"); Main.gamePaused=false;
            Player p=Main.LocalPlayer; NativeCoinMatrix.Reset(p,host); Array.Clear(p.inventoryChestStack,0,p.inventoryChestStack.Length);
            Main.tile[40,40].type=29; p.inventory[50]=Coin(73,2); p.inventory[51]=Coin(72,3);
            p.bank.item[0].SetDefaults(8);p.bank.item[0].stack=9;
            p.bank2.item[0].SetDefaults(8);p.bank2.item[0].stack=3;
        }
        private static void Reject(object context,object host,string name,Action mutation,bool onRelease=false)
        {
            Fresh(context,host); object transfer=Get(host,"Transfer"); long seeds=(long)Get(transfer,"SeedCalls"),calls=(long)Get(transfer,"NativeCalls");
            if(onRelease) afterRelease=mutation; else afterNative=mutation;
            string result=NativeCoinSeedChecks.Execute(host);
            Require((long)Get(transfer,"NativeCalls")==calls+1,"actual vanilla entry executed: "+name);
            Require((long)Get(transfer,"SeedCalls")==seeds && result!="Completed" && result!="Partial","seed refuses "+name+"; outcome="+result);
        }
        private static void AfterNative(ref long __result)
        { Action action=afterNative;afterNative=null;action?.Invoke();if(nonzero){nonzero=false;__result=1;} }
        private static void AfterRelease()
        { Action action=afterRelease;afterRelease=null;action?.Invoke(); }
    }
}
