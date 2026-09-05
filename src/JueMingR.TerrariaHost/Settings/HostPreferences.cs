using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Biomes;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Settings;

namespace JueMingR.TerrariaHost.Settings
{
    // Composition/lifetime adapter for the two real documents. It never samples
    // game objects and never derives desired settings from Feature fault state.
    internal sealed class HostPreferences
    {
        private readonly PreferenceDocument<bool> biome;
        private readonly PreferenceDocument<WindowPosition> ui;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private PreferenceSnapshot<bool> seenBiome;
        private PreferenceSnapshot<WindowPosition> seenUi;

        internal HostPreferences(string verifiedGameDirectory)
        {
            // The caller supplies the directory of the already identity-verified
            // Terraria assembly, not our sidecar, repository or working directory.
            string config = Path.Combine(verifiedGameDirectory, "JueMingRData", "config");
            biome = new PreferenceDocument<bool>(new FilePreferenceStorage(Path.Combine(config, "features", "biome-display.json")),
                new BiomePreferenceCodec(), true);
            ui = new PreferenceDocument<WindowPosition>(new FilePreferenceStorage(Path.Combine(config, "ui.json")), new UiPreferenceCodec(), null);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        internal bool BiomeLoaded { get { return biome.Snapshot.IsLoaded; } }
        internal bool UiLoaded { get { return ui.Snapshot.IsLoaded; } }
        internal bool IsLoaded { get { return BiomeLoaded && UiLoaded; } }
        internal bool BiomeEnabled { get { return biome.Snapshot.Value; } }
        internal WindowPosition Position { get { return ui.Snapshot.Value; } }
        internal void SetBiomeEnabled(bool enabled) { biome.Set(enabled); }
        internal void SetPosition(WindowPosition position) { ui.Set(position); }

        internal void Update()
        {
            if (!IsLoaded && startup.ElapsedMilliseconds >= 2000)
            {
                // Bounded readiness, not file polling. Late reads cannot replace
                // defaults or user choices after this explicit abandonment.
                biome.AbandonSlowLoad(); ui.AbandonSlowLoad();
            }
        }

        internal string TakeFeedback()
        {
            string message = Feedback(biome.Snapshot, ref seenBiome, "群系选择", "config/features/biome-display.json");
            return message ?? Feedback(ui.Snapshot, ref seenUi, "F5 位置", "config/ui.json");
        }

        private static string Feedback<T>(PreferenceSnapshot<T> current, ref PreferenceSnapshot<T> seen, string label, string path)
        {
            if (!current.IsLoaded || ReferenceEquals(current, seen)) return null;
            PreferenceSnapshot<T> previous = seen;
            seen = current;
            if (previous != null && previous.Status == current.Status && current.Status == PreferenceStatus.Pending) return null;
            switch (current.Status)
            {
                case PreferenceStatus.Missing: return null;
                case PreferenceStatus.Saved: return current.Revision == 0 ? null : label + "已保存。";
                case PreferenceStatus.Pending: return label + "已应用，正在保存。";
                case PreferenceStatus.UnsupportedVersion:
                case PreferenceStatus.UnknownFields:
                    return label + "仅本次有效：配置含当前程序不认识的版本或字段，原件已保留。请退出游戏后使用匹配程序检查 " + path + "。";
                case PreferenceStatus.Invalid:
                    return label + "仅本次有效：配置格式有误，原件已保留。请退出游戏，备份并移走 JueMingRData/" + path + " 后重启。";
                case PreferenceStatus.Conflict:
                    return label + "未能可靠保存：文件在运行中发生变化，已停止覆盖。请退出所有游戏，保留该文件及 .bak，检查后重启。";
                case PreferenceStatus.Busy:
                    return label + "仅本次有效：另一进程正在使用配置。请退出使用此安装的所有游戏后重启。";
                default:
                    return label + "未能可靠加载或保存：请退出游戏后检查 JueMingRData/" + path + " 的访问权限及 .tmp 文件；原件保持保护。";
            }
        }

        private void OnProcessExit(object sender, EventArgs args)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            // One total shutdown budget. Stop wakes the coalescer and drains accepted
            // values; a native I/O stall cannot make process exit wait indefinitely.
            var budget = Stopwatch.StartNew();
            biome.Stop(750);
            ui.Stop(Math.Max(0, 750 - (int)budget.ElapsedMilliseconds));
        }
    }
}
