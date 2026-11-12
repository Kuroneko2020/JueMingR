using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Terraria;
using Terraria.Map;
using Terraria.IO;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    // Actual map compressor entry and resulting isolated file, not a product counter.
    internal sealed class NativeRenameMapChecks : IDisposable
    {
        private readonly Harmony audit=new Harmony("JueMingR.Tests.RenameMap");
        private readonly bool enabled=Main.mapEnabled;
        private readonly WorldMap oldMap=Main.Map;
        private readonly int oldWorldId=Main.ActiveWorldFileData.WorldId;
        private readonly FileMetadata oldMetadata=Main.MapFileMetadata;
        private readonly string oldName=Main.worldName;
        private static int maps;
        private readonly int before;
        private NativeRenameMapChecks()
        {
            Require(Main.ActivePlayerFileData.Path.StartsWith(Terraria.Program.SavePath,StringComparison.OrdinalIgnoreCase),"isolated map path");
            MapHelper.Initialize();Main.Map=new WorldMap(Main.maxTilesX,Main.maxTilesY);Main.mapEnabled=true;Main.ActiveWorldFileData.WorldId=151819;Main.MapFileMetadata=FileMetadata.FromCurrentSettings(FileType.Map);Main.worldName="Rename map fixture";
            audit.Patch(AccessTools.Method(typeof(MapHelper),"InternalSaveMap"),prefix:new HarmonyMethod(typeof(NativeRenameMapChecks),nameof(Compress)));
            before=maps;
        }
        internal static NativeRenameMapChecks Begin(){return new NativeRenameMapChecks();}
        private static void Compress(){maps++;}
        private static void RenameThrows(){throw new InvalidOperationException("isolated native rename tail failure");}
        internal void AssertSkipped(){Require(Main.mapEnabled && maps==before,"map-enabled native rename never enters original map compressor");}
        internal void OrdinarySave()
        {
            AssertSkipped();
            string path=Path.Combine(Main.playerPathName.Substring(0,Main.playerPathName.Length-4),Main.ActiveWorldFileData.MapFileName+".map");
            Require(path.StartsWith(Terraria.Program.SavePath,StringComparison.OrdinalIgnoreCase),"exact map target remains isolated");
            if(File.Exists(path))File.Delete(path);
            Player.SavePlayer(Main.ActivePlayerFileData);
            Require(maps==before+1 && File.Exists(path) && new FileInfo(path).Length>0,"scope ended: ordinary native player save enters compressor and writes real isolated map");
        }
        internal static void Manual(object owner)
        {
            using(var map=Begin())
            {
                Require((bool)Call(owner,"Rename","Map009") && (bool)Get(owner,"PersistedVerified") && Player.LoadPlayer(Main.ActivePlayerFileData.Path,false).Player.name=="Map009","map-enabled manual native save and independent actual player reload");
                map.AssertSkipped();
                var fault=new Harmony("JueMingR.Tests.RenameMapFailure");var target=AccessTools.Method(typeof(PlayerFileData),"Rename");
                fault.Patch(target,postfix:new HarmonyMethod(typeof(NativeRenameMapChecks),nameof(RenameThrows)));
                try{Require(!(bool)Call(owner,"Rename","地图失败") && (bool)Get(owner,"NativeWriteCompleted") && !(bool)Get(owner,"PersistedVerified"),"real native rename exception preserves actual character write and unknown result");map.AssertSkipped();}
                finally{fault.Unpatch(target,HarmonyPatchType.All,fault.Id);}
                map.OrdinarySave();
            }
            Console.WriteLine("PASS map-enabled manual rename omits compressor; ordinary save really writes map.");
        }
        public void Dispose(){Main.mapEnabled=enabled;Main.Map=oldMap;Main.ActiveWorldFileData.WorldId=oldWorldId;Main.MapFileMetadata=oldMetadata;Main.worldName=oldName;foreach(var m in audit.GetPatchedMethods().ToArray())audit.Unpatch(m,HarmonyPatchType.All,audit.Id);}
    }
}
