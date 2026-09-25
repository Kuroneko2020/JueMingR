using System;
using System.Security.Cryptography;
using System.Text;
using Terraria;
namespace JueMingR.TerrariaHost.Onboarding
{
    internal static class CharacterIdentity
    {
        internal static string Path(out bool cloud)
        {
            cloud = false; var file = Main.ActivePlayerFileData;
            if (file == null || !ReferenceEquals(file.Player, Main.LocalPlayer) || file.ServerSideCharacter || Main.ServerSideCharacter ||
                String.IsNullOrEmpty(file.Path) || file.Path.Length > 32768 || Main.netMode == 1 && (Netplay.Connection == null || Netplay.Connection.State != 10)) return null;
            cloud = file.IsCloudSave; return file.Path;
        }
        internal static string Key(string path, bool cloud)
        {
            // This is the accepted file identity, not a permanent Terraria UUID.
            // No world identity, display name, slot or server data enters this key.
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(new UTF8Encoding(false, true).GetBytes("onboarding-character-v1\0" + (cloud ? "cloud:" : "local:") + path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
