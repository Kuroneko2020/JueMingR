using System;
using System.Security.Cryptography;
using System.Text;
using Terraria;

namespace JueMingR.TerrariaHost.Map
{
    internal static class LocalWorldIdentity
    {
        internal static string Observe()
        {
            var player = Main.LocalPlayer; var file = Main.ActivePlayerFileData; var world = Main.ActiveWorldFileData;
            if (file == null || !ReferenceEquals(file.Player, player) || file.ServerSideCharacter || Main.ServerSideCharacter || String.IsNullOrEmpty(file.Path) || file.Path.Length > 32768 || world == null || world.UniqueId == Guid.Empty ||
                Main.netMode == 1 && (Netplay.Connection == null || Netplay.Connection.State != 10)) return null;
            // Preserve the accepted death/time key byte-for-byte. Domains share
            // admitted identity, never files, archive semantics or live state.
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes("world-records-v1\0" + (file.IsCloudSave ? "cloud:" : "local:") + file.Path + "\0" + world.UniqueId.ToString("N")))).Replace("-", "").ToLowerInvariant();
        }
    }
}
