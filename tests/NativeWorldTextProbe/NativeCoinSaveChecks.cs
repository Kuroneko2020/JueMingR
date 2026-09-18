using System;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.IO;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinSaveChecks
    {
        internal static void Run(object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            p.name = "G06 Coin Probe"; p.inventory[50] = Coin(74, 9000); p.inventory[51] = Coin(73, 99); p.inventory[51].favorited = true;
            Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(71, 1);
            NativeCoinMatrix.Tick(host, 0, 150);
            for (int b = 1; b < 4; b++) Bank(p, b).item[0] = Coin(74, b + 1);
            p.bank4.item[0].favorited = true; p.inventory[10].SetDefaults(8); p.inventory[10].stack = 7;
            long before = Total(p.inventory, 58);
            for (int b = 0; b < 4; b++) before += Total(Bank(p, b).item, 40);
            Require(Main.mouseItem != null && Main.CreativeMenu.GetItemByIndex(0) != null && Main.guideItem != null && Main.reforgeItem != null, "real save temporary slots are initialized in isolated fixture");
            string path = Path.Combine(Terraria.Program.SavePath, "coin-roundtrip.plr");
            Require(!File.Exists(path), "native player output must be new and isolated");
            var file = new PlayerFileData(path, false) { Metadata = FileMetadata.FromCurrentSettings(FileType.Player), Player = p };
            Require(!file.ServerSideCharacter, "normal player save authority");
            typeof(Player).GetMethod("InternalSavePlayerFile", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { file });
            Require(File.Exists(path) && new FileInfo(path).Length > 100, "real encrypted player file written");
            var loaded = Player.LoadPlayer(path, false);
            Require(loaded.Player != null && loaded.Player.loadStatus == StatusID.Ok, "real player read succeeded, not swallowed UnknownError");
            Player copy = loaded.Player; long after = Total(copy.inventory, 58);
            for (int b = 0; b < 4; b++) after += Total(Bank(copy, b).item, 40);
            Require(before == after && copy.name == p.name && copy.inventory[51].favorited && copy.inventory[10].type == 8 && copy.inventory[10].stack == 7 && copy.bank4.item[0].favorited,
                "native encrypted save/reload conserves wallet+four banks and actual persisted attributes");
            for (int b = 0; b < 4; b++) Require(Total(Bank(p,b).item,40) == Total(Bank(copy,b).item,40), "each bank roundtrips independently " + b);
            Console.WriteLine("PASS: real InternalSavePlayerFile encryption/disk + LoadPlayer roundtrip; all four bank and wallet totals preserved. Outer achievement/map SavePlayer flow not exercised.");
        }
    }
}
