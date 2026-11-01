using System;
using System.Collections.Generic;

namespace JueMingR.TerrariaHost.WorldGenPreview
{
    // These are display values for the locked native preview. They never replace
    // GenPass.Name, manifest keys, commands, paths, seeds or player input.
    internal static class WorldGenPreviewText
    {
        private static readonly Dictionary<string, string> Steps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Terrain", "地形" },
            { "Skyblock", "空岛世界" },
            { "Dunes", "沙丘与金字塔位置" },
            { "Ocean Sand", "海洋沙层" },
            { "Sand Patches", "沙地" },
            { "Tunnels", "隧道" },
            { "Mount Caves", "山体洞穴" },
            { "Dirt Wall Backgrounds", "泥土背景墙" },
            { "Rocks In Dirt", "泥土中的岩石" },
            { "Dirt In Rocks", "岩石中的泥土" },
            { "Clay", "黏土" },
            { "Small Holes", "小洞穴" },
            { "Dirt Layer Caves", "泥土层洞穴" },
            { "Rock Layer Caves", "岩石层洞穴" },
            { "Surface Caves", "地表洞穴" },
            { "Wavy Caves", "曲折洞穴" },
            { "Generate Ice Biome", "冰雪生物群系" },
            { "Grass", "草地" },
            { "Jungle", "丛林" },
            { "Mud Caves To Grass", "淤泥洞穴丛林化" },
            { "Full Desert", "沙漠生物群系" },
            { "Mushroom Patches", "发光蘑菇区域" },
            { "Marble", "大理石" },
            { "Granite", "花岗岩" },
            { "Floating Islands", "浮空岛" },
            { "Dirt To Mud", "泥土转为淤泥" },
            { "Silt", "泥沙" },
            { "Shinies", "矿石" },
            { "Webs", "蛛网" },
            { "Underworld", "地狱" },
            { "Corruption", "腐化与猩红" },
            { "Lakes", "湖泊" },
            { "Slush", "冰块与雪泥" },
            { "Dual Dungeons Dither Snake", "双地牢交错通道" },
            { "Dungeon", "地牢" },
            { "Mountain Caves", "山洞入口" },
            { "Beaches", "海滩" },
            { "Gems", "宝石" },
            { "Gravitating Sand", "落沙整理" },
            { "Create Ocean Caves", "海洋洞穴" },
            { "Shimmer", "微光" },
            { "Clean Up Dirt", "泥土背景墙整理" },
            { "Pyramids", "金字塔" },
            { "Dirt Rock Wall Runner", "泥土与岩石洞壁" },
            { "Living Trees", "生命树" },
            { "Wood Tree Walls", "生命树背景墙" },
            { "Altars", "恶魔与猩红祭坛" },
            { "Wet Jungle", "丛林地表水" },
            { "Jungle Temple", "丛林神庙" },
            { "Hives", "蜂巢" },
            { "Jungle Chests", "丛林神龛" },
            { "Settle Liquids", "液体沉降" },
            { "Remove Water From Sand", "清除沙地表层水" },
            { "Oasis", "绿洲" },
            { "Shell Piles", "贝壳堆与洞穴陷坑" },
            { "Smooth World", "地形平滑" },
            { "Waterfalls", "瀑布" },
            { "Ice", "薄冰" },
            { "Wall Variety", "洞壁变化" },
            { "Life Crystals", "生命水晶" },
            { "Statues", "雕像" },
            { "Buried Chests", "地下小屋与埋藏宝箱" },
            { "Surface Chests", "地表宝箱" },
            { "Jungle Chests Placement", "丛林神龛宝箱" },
            { "Water Chests", "水下宝箱" },
            { "Spider Caves", "蜘蛛洞" },
            { "Gem Caves", "宝石洞" },
            { "Moss", "苔藓与苔藓洞" },
            { "Temple", "神庙整理" },
            { "Cave Walls", "封闭洞穴背景墙" },
            { "Jungle Trees", "地下丛林树" },
            { "Floating Island Houses", "浮空岛房屋" },
            { "Quick Cleanup", "初步整理" },
            { "Pots", "罐子与巨石堆" },
            { "Hellforge", "地狱熔炉" },
            { "Spreading Grass", "地表植被与熔岩整理" },
            { "Surface Ore and Stone", "地表矿石与岩石" },
            { "Place Fallen Log", "倒木与水景" },
            { "Traps", "机关" },
            { "Piles", "碎堆" },
            { "Spawn Point", "出生点" },
            { "Grass Wall", "地表草墙" },
            { "Guide", "初始城镇居民" },
            { "Sunflowers", "向日葵" },
            { "Planting Trees", "种植树木" },
            { "Herbs", "草药" },
            { "Dye Plants", "染料植物" },
            { "Webs And Honey", "蛛网与蜂蜜" },
            { "Weeds", "地表植物" },
            { "Glowing Mushrooms and Jungle Plants", "发光蘑菇与丛林植物" },
            { "Jungle Plants", "丛林植被" },
            { "Vines", "藤蔓" },
            { "Flowers", "花卉" },
            { "Mushrooms", "蘑菇" },
            { "Gems In Ice Biome", "冰雪区外露宝石" },
            { "Random Gems", "地下外露宝石" },
            { "Moss Grass", "长苔藓" },
            { "Muds Walls In Jungle", "丛林淤泥墙与边界" },
            { "Larva", "蜂巢幼虫" },
            { "Settle Liquids Again", "再次沉降液体" },
            { "Cactus, Palm Trees, & Coral", "仙人掌、棕榈树与珊瑚" },
            { "Tile Cleanup", "图格整理" },
            { "Lihzahrd Altars", "丛林蜥蜴祭坛" },
            { "Micro Biomes", "微型生物群系" },
            { "Water Plants", "水生植物" },
            { "Stalac", "洞穴石笋与宝石树" },
            { "Remove Broken Traps", "清除无效机关" },
            { "Final Cleanup", "最终整理" }
        };

        // Literal fragments are changed in the verified native producers at
        // installation, before their existing conditional formatting executes.
        // This is not a dictionary applied over arbitrary rendered text.
        private static readonly Dictionary<string, string> Literals = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Skipped: ", "已跳过：" },
            { "Disabled: ", "已禁用：" },
            { "Run", "运行" },
            { "Rerun", "重跑" },
            { " to ", "至：" },
            { "Hold shift to ignore snapshots\n", "按住 Shift 忽略生成快照\n" },
            { "[c/FFA500:Must be paused to rerun or load snapshots]\n", "[c/FFA500:须暂停后才能重跑或加载生成快照]\n" },
            { "Right click to disable\n", "右键禁用\n" },
            { "Left click to enable\n", "左键启用\n" },
            { "Shift to edit ranges\n", "按住 Shift 批量调整连续阶段\n" },
            { "Alt click to toggle highlight\n", "按住 Alt 点击切换高亮\n" },
            { "Snapshot is outdated and will only be used for comparison when the pass is run again", "生成快照已过期，仅用于与该阶段重跑后的结果比较" },
            { "Reset to snapshot", "恢复到生成快照" },
            { "Take snapshot", "创建生成快照" },
            { "Left click to load snapshot\n", "左键加载生成快照\n" },
            { "Right click to delete snapshot\n", "右键删除生成快照\n" },
            { "[c/FFA500:Must be paused to load a snapshot]", "[c/FFA500:须暂停后才能加载生成快照]" },
            { "Left click to take snapshot\n", "左键创建生成快照\n" },
            { "Delete all snapshots", "删除全部生成快照" },
            { "Click to clear all snapshots\nEstimated Disk Usage: ", "点击删除全部生成快照\n预计占用磁盘：" },
            { "\n[c/FFA500:Must be paused to manipulate snapshots]", "\n[c/FFA500:须暂停后才能操作生成快照]" },
            { "Reset", "重置" },
            { "[c/FFA500:Must be paused to reset]", "[c/FFA500:须暂停后才能重置]" },
            { "Step Back", "后退一步" },
            { "Hotkey: Up/Left", "快捷键：↑ / ←" },
            { "Pause", "暂停" },
            { "Play", "继续" },
            { "Hotkey: Space", "快捷键：空格" },
            { "Step Forward", "前进一步" },
            { "Hotkey: Down/Right", "快捷键：↓ / →" },
            { "Toggle Map", "显示/隐藏地图" },
            { "Left click to toggle the map display", "左键切换地图显示" },
            { "Toggle Chat", "显示/隐藏聊天" },
            { "Left click to toggle the chat log", "左键切换聊天记录显示" },
            { "Create snaphots: Manually", "生成快照：手动创建" },
            { "Create snaphots: Automatically", "生成快照：自动创建" },
            { "Create snaphots: After every pass", "生成快照：每个阶段完成后创建" },
            { "Pause on gen pass change: ", "生成结果出现差异时暂停：" },
            { "On", "开启" },
            { "Off", "关闭" },
            { "Stop the generator when the output of a pass is different\nto the last time it was run in the save, or current session", "某阶段的结果与此前记录或本次生成中的上次结果\n不同时，暂停生成" },
            { "Save current settings to ", "保存快速加载设置：" },
            { "Future launches of the game will automatically load the world\nfrom the most recent snapshot, and run to the current pass", "以后启动游戏时，将自动从最近的生成快照恢复世界，\n并运行至当前阶段" },
            { "Controls", "操作说明" },
            { "Cancel", "取消生成" },
            { "[c/FFF014:Space] to pause/resume\n[c/FFF014:R] to rerun current step\n[c/FFF014:Up]/[c/FFF014:Down] or [c/FFF014:Left]/[c/FFF014:Right] to step back/forward\n[c/FFF014:H] to hide UI\n[c/FFF014:M] to toggle map\n[c/FFF014:C] to hide chat log\n", "[c/FFF014:空格]：暂停/继续\n[c/FFF014:R]：重跑当前阶段\n[c/FFF014:↑]/[c/FFF014:↓] 或 [c/FFF014:←]/[c/FFF014:→]：后退/前进一步\n[c/FFF014:H]：显示/隐藏界面\n[c/FFF014:M]：显示/隐藏地图\n[c/FFF014:C]：显示/隐藏聊天记录\n" },
            { " map scans/s", " 次地图扫描/秒" },
            { "Pass was not run with worldgen debugging enabled, please re-run", "该阶段运行时未记录生成校验信息，请重跑该阶段" },
            { "World has been modified since last gen pass completed. Please rerun or use /snapshot instead", "上一阶段完成后世界已被修改，请重跑该阶段或改用 /snapshot" },
            { "World Reset", "世界已重置" },
            { "Quick Load configuration set. Hold shift while loading to clear it.", "快速加载设置已保存。加载时按住 Shift 可清除设置。" }
        };

        internal static string Step(string raw)
        {
            string value;
            return raw != null && Steps.TryGetValue(raw, out value) ? value : raw;
        }

        internal static bool TryLiteral(string raw, out string translated)
        {
            return Literals.TryGetValue(raw, out translated);
        }

        internal static string Display(string raw)
        {
            if (raw == null) return null;
            string value;
            if (Literals.TryGetValue(raw, out value)) return value;
            if (raw == "World Cleared") return "世界已清空";
            if (raw == "Exception occured while creating snapshot") return "创建生成快照时发生异常";
            if (raw == "Exception occured while restoring snapshot") return "恢复生成快照时发生异常";
            string restored;
            if (TryRestoredSnapshot(raw, out restored)) return restored;
            foreach (var template in PrefixTemplates)
                if (raw.StartsWith(template.Key, StringComparison.Ordinal) && raw.Length > template.Key.Length)
                    return template.Value + Step(raw.Substring(template.Key.Length));

            // Only the three complete Controller mismatch shapes are accepted;
            // commands remain literal and the pass operand is translated once.
            const string mismatch = " output changed since last gen.";
            foreach (var suffix in MismatchSuffixes)
            {
                string tail = mismatch + suffix.Key;
                if (raw.EndsWith(tail, StringComparison.Ordinal) && raw.Length > tail.Length)
                    return Step(raw.Substring(0, raw.Length - tail.Length)) + "：生成结果与上次不同。" + suffix.Value;
            }
            return Step(raw);
        }

        private static bool TryRestoredSnapshot(string raw, out string translated)
        {
            translated = null;
            const string prefix = "Restored Pass - ";
            const string randomMarker = ", rand - ";
            const string hashMarker = ", hash - ";
            if (!raw.StartsWith(prefix, StringComparison.Ordinal)) return false;
            int random = raw.IndexOf(randomMarker, prefix.Length, StringComparison.Ordinal);
            if (random <= prefix.Length) return false;
            int hash = raw.IndexOf(hashMarker, random + randomMarker.Length, StringComparison.Ordinal);
            if (hash < 0) return false;
            string randomValue = raw.Substring(random + randomMarker.Length, hash - random - randomMarker.Length);
            string hashValue = raw.Substring(hash + hashMarker.Length);
            if (!IsHex(randomValue, false) || !IsHex(hashValue, true)) return false;
            translated = "已恢复生成快照：" + Step(raw.Substring(prefix.Length, random - prefix.Length)) + "，随机数 " + randomValue + "，校验值 " + hashValue;
            return true;
        }

        private static bool IsHex(string value, bool allowEmpty)
        {
            if (allowEmpty && value.Length == 0) return true;
            if (value.Length != 8) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'F')) return false;
            return true;
        }

        private static readonly KeyValuePair<string, string>[] PrefixTemplates =
        {
            new KeyValuePair<string, string>("Paused after ", "已暂停："),
            new KeyValuePair<string, string>("Skipped: ", "已跳过："),
            new KeyValuePair<string, string>("Disabled: ", "已禁用："),
            new KeyValuePair<string, string>("Run to ", "运行至："),
            new KeyValuePair<string, string>("Rerun to ", "重跑至："),
            new KeyValuePair<string, string>("Exception in Pass: ", "生成阶段发生异常：")
        };

        private static readonly KeyValuePair<string, string>[] MismatchSuffixes =
        {
            new KeyValuePair<string, string>("", ""),
            new KeyValuePair<string, string>(" The previous output has been loaded as a snapshot (use /swap and /snapshotdiff to compare)", "已将上次结果加载为生成快照，可用 /swap 和 /snapshotdiff 比较。"),
            new KeyValuePair<string, string>("An attempt was made to load a snapshot of the previous output, but an exception occurred", "尝试加载上次结果的生成快照时发生异常。")
        };
    }
}
