using System;
using JueMingR.TerrariaHost.Announcements;
using JueMingR.TerrariaHost.ChestLocator;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Hotkeys;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.World;
using Terraria;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Composition only: each capability retains its own state and outlet.
    internal sealed class HostItemBrowser : IDisposable
    {
        internal readonly HostItemKnowledge Knowledge = new HostItemKnowledge();
        internal readonly HostChestLocator Locator;
        internal readonly HostAnnouncements Announcements;
        internal readonly ReadOnlyTargetGesture Targets;
        private readonly HostInputState input;
        private F5Shell shell;
        private BrowserPresentation page;
        private bool disposed;
        internal HostItemBrowser(string directory, WorldTileObservation world, HostInputState input)
        {
            this.input = input; Locator = new HostChestLocator(world, Knowledge);
            Announcements = new HostAnnouncements(directory);
            Targets = new ReadOnlyTargetGesture(input, Knowledge.Native);
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal bool CanAnnounce() { return !disposed && Targets.Ready && !Targets.Picking && Announcements.Enabled && shell != null && shell.CanTargetInput; }
        internal bool CanQuery() { return !disposed && Targets.Ready && !Targets.Busy && shell != null && shell.CanTargetInput; }
        internal void Query() { Targets.RequestQuery(); }
        internal void Announce() { Targets.RequestAnnouncement(); }
        internal void Attach(F5Shell owner, HostHotkeys hotkeys)
        {
            shell = owner; page = new BrowserPresentation(Knowledge, shell.State, input);
            shell.AttachBrowser(page); shell.TargetGestureBusy = () => Targets.Busy;
            Targets.CanPick = () => !disposed && shell.CanTargetInput;
            Targets.CanAnnounce = CanAnnounce;
            page.ConfigurePickHotkey = rect => shell.OpenHotkey("item-browser.query", rect);
            Targets.QueryBinding = () => hotkeys.Bindings.Get("item-browser.query");
            Targets.AnnouncementBinding = () => hotkeys.Bindings.Get(AnnouncementControls.SendActionId);
            Targets.Announced = Announcements.Submit; Targets.Feedback = Announcements.Feedback;
            Targets.PickCancelled = RestoreBrowser;
            Targets.Picked = target => { RestoreBrowser(); if (target.ItemType > 0) page.Navigate(target.ItemType, false); else Announcements.Feedback(target.UiSlot ? "这里是空槽，未改变查询" : "无法确定对应物品，未改变查询"); };
            page.PickRequested = () => { if (!Targets.Ready) { Announcements.Feedback("只读点选暂不可用"); return; } shell.CloseAndSubmitPosition(); Targets.BeginPick(); if (!Targets.Picking) RestoreBrowser(); };
            page.LocateRequested = Locator.Submit; page.ClearLocator = Locator.Clear;
            page.LocateSelectedRequested = type => Locator.Submit("#" + type);
            page.LocatorStatus = () => Locator.Status;
            page.LocatorDetails = () => Locator.Details; page.LocatorRevision = () => Locator.Revision;
            Locator.PositiveResult = () => { if (shell.State.Visible && shell.State.Page == 3) shell.CloseAndSubmitPosition(); };
        }
        private void RestoreBrowser()
        { if (!disposed && shell != null && !Main.gameMenu && input.IsFocused) { shell.State.Navigate(3); shell.State.RestoreVisible(); } }
        internal void Update(long tick) { if (disposed) return; Announcements.Update(); Targets.Update(tick); Locator.Update(tick); }
        internal void FailClosed() { Targets.Cancel(); Locator.Clear(); }
        private void OnExit(object sender, EventArgs e) { Dispose(); }
        public void Dispose()
        { if (disposed) return; disposed = true; AppDomain.CurrentDomain.ProcessExit -= OnExit; Targets.Dispose(); page?.Dispose(); Locator.Dispose(); Knowledge.Dispose(); Announcements.Dispose(); }
    }
}
