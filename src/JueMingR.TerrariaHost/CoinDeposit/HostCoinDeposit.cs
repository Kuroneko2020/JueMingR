using System;
using System.IO;
using System.Threading;
using JueMingR.Features.CoinDeposit;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using Terraria;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    internal sealed class HostCoinDeposit : IRuntimeFeature
    {
        internal const string ActionId = "items.coin-deposit.toggle";
        internal readonly CoinSettings Settings;
        internal readonly CoinIntent Intent = new CoinIntent();
        internal readonly CoinRange Range = new CoinRange();
        internal readonly CoinTransfer Transfer;
        internal readonly HostItems Items;
        internal readonly SingleFeatureRuntime Runtime;
        internal readonly HostInputState Input;
        internal Func<bool> CanGameplay;
        // Material/manual guards belong to shared Items, independently of its
        // automation preference. Missing installation is a capability failure.
        private readonly bool hooksReady;
        internal bool Available { get { return hooksReady && Items.Available && Items.World.AdditionalProtection != null; } }
        internal Exception SetupError { get; private set; }
        internal string Status { get; private set; } = "已关闭";
        internal string Detail { get; private set; }
        private bool reportedUnavailable, reportedSsc, operationFeedback;
        private long reportedFaultSession = -1;
        private string operationError, noProgressHint;
        private int hintBank = -1;
        private long hintWalletRevision, hintBankRevision;
        internal string NameHint
        {
            get
            {
                if (Intent.Faulted) return "存钱结果未确认，已暂停。重新开关不会重做这笔交易，请保留当前状态供检查。";
                if (!Available) return "自动存钱暂不可用，原版手动存取不受影响。";
                if (Settings.Message != null) return Settings.Message;
                if (operationError != null) return operationError;
                if (!Settings.Enabled) return null;
                if (ServerCharacter) return "此服务器管理角色存档，暂不支持自动存钱。";
                if (Intent.Protected || Intent.Pending) return "主动取出的钱先留在身上；离开全部个人银行范围，或关闭再开启后恢复。";
                return hintBank >= 0 && Range.Count != 0 && hasCoins && wallet.Revision == hintWalletRevision &&
                    accounts[hintBank].Revision == hintBankRevision ? noProgressHint : null;
            }
        }
        private static bool ServerCharacter { get { return Main.ServerSideCharacter || Main.ActivePlayerFileData != null && Main.ActivePlayerFileData.ServerSideCharacter; } }
        internal bool ControlsEnabled { get { return Available && Settings.Loaded && !Settings.Busy && !Settings.Protected; } }
        internal Player Player { get { return Runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId == threadId && TrustedIdentity ? Items.World.Player : null; } }
        internal bool Observing { get { return Available && Settings.Enabled && Player != null && !Transfer.Executing && !Items.World.AutomaticOperation; } }
        // Completion can preserve an already observed user's source loss while
        // a world/socket read is temporarily missing. This grants no write or
        // successful-arrival permission; a positively different identity rejects
        // the old scope, and normal observation still requires TrustedIdentity.
        internal bool CanRetainWithdrawal(Player p, long session)
        {
            object world = Main.ActiveWorldFileData, socket = Main.netMode == 1 ? Netplay.Connection?.Socket : null;
            return !stopping && Available && Settings.Enabled && Thread.CurrentThread.ManagedThreadId == threadId &&
                !Main.gameMenu && !Main.dedServ && session == identity && identityMode == Main.netMode &&
                ReferenceEquals(p, identityPlayer) && ReferenceEquals(p, Main.LocalPlayer) &&
                (world == null || ReferenceEquals(world, identityWorld)) &&
                (Main.netMode == 0 || socket == null || ReferenceEquals(socket, identitySocket));
        }
        public bool Enabled { get { return true; } }
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Facts wallet = new Facts(58);
        private readonly Facts[] accounts = { new Facts(40), new Facts(40), new Facts(40), new Facts(40) };
        private readonly long[] triedWallet = { -1, -1, -1, -1 }, triedBank = { -1, -1, -1, -1 }, triedIntent = { -1, -1, -1, -1 };
        private Player identityPlayer;
        private object identityWorld, identitySocket;
        private long identity;
        private int identityMode = -1;
        private bool TrustedIdentity { get { return identityPlayer != null && ReferenceEquals(identityPlayer, Items.World.Player) && identityWorld != null &&
            ReferenceEquals(identityWorld, Main.ActiveWorldFileData) && identityMode == Main.netMode &&
            (Main.netMode == 0 || identitySocket != null && ReferenceEquals(identitySocket, Netplay.Connection?.Socket)); } }
        private bool voidClosed, stopping, hasCoins;
        private ulong nextWallet, nextRangeFailure, nextCommit;
#if DEBUG
        internal long WalletReads, CapacityReads, Updates;
#endif
        internal HostCoinDeposit(string directory, SingleFeatureRuntime runtime, HostItems items, HostInputState input)
        {
            Runtime = runtime; Items = items; Input = input;
            Settings = new CoinSettings(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "coin-deposit.json"), 4096, true));
            Transfer = new CoinTransfer(this);
            try { CoinWithdrawalHooks.Install(this); hooksReady = true; }
            catch (Exception error) { SetupError = error; CoinWithdrawalHooks.Uninstall(); Status = "暂不可用"; Detail = "当前存钱入口未就绪，原版手动存取不受影响。"; }
            AppDomain.CurrentDomain.ProcessExit += Exit;
        }
        internal void Register(HotkeyRegistry registry, Hotkeys.HotkeyStateFeedback feedback = null)
        {
            Action command = () => SetEnabled(!Settings.Enabled);
            if (feedback != null) command = feedback.Committed(ActionId,"自动存钱",command,()=>Settings.Enabled?1:0,()=>ControlsEnabled,
                ()=>Settings.AcceptedCommandId,()=>Settings.CompletedCommandId,()=>Settings.CompletionSucceeded);
            registry.Register(new HotkeyAction(ActionId, "自动存钱", HotkeyContext.Gameplay, () => ControlsEnabled, command));
        }
        internal void SetEnabled(bool enabled) { if (ControlsEnabled && Settings.Set(enabled)) Poll(); }
        internal void Poll()
        {
            Settings.Poll(); Intent.Configure(Settings.Enabled, Settings.Revision);
            // Off stops new work; it does not settle an unknown transaction or
            // hide the reason its original source range remains protected.
            if (Intent.Faulted) { UnconfirmedStatus(); return; }
            if (!Available) { Unavailable(); return; }
            if (!Settings.Enabled) { Status = "已关闭"; Detail = Settings.Message; }
        }
        internal void TakeFeedback(Action<string> display)
        {
            Settings.TakeFeedback(display);
            if (!Available && !reportedUnavailable) { display("自动存钱暂不可用，原版手动存取不受影响。"); reportedUnavailable = true; }
            if (Available) reportedUnavailable = false;
            if (Intent.Faulted && reportedFaultSession != identity)
            { display("存钱结果未确认，已暂停。请保留当前状态供检查。"); reportedFaultSession = identity; }
            if (!Intent.Faulted) reportedFaultSession = -1;
            bool ssc = Settings.Enabled && ServerCharacter;
            if (ssc && !reportedSsc) { display("此服务器管理角色存档，暂不支持自动存钱。"); reportedSsc = true; }
            if (!ssc) reportedSsc = false;
            if (operationFeedback) { display(operationError); operationFeedback = false; }
        }
        private void Unavailable()
        { Status = "暂不可用"; Detail = "存钱或物品保护入口未就绪，原版手动存取不受影响。"; }
        private void UnconfirmedStatus()
        { Status = "结果未确认"; Detail = "已暂停新的自动存钱；重新开关不会重做这笔交易。请保留当前状态供检查。"; }
        public void OnSessionStarted()
        {
            ConfirmIdentity();
            // Runtime generations can restart after an unreadable world/socket.
            // Restore only the unknown operation's finite conflict range for the
            // same native player/session, before any feature updates. No token,
            // Item reference, transaction permission or replay survives here.
            object world = Main.ActiveWorldFileData, socket = Main.netMode == 1 ? Netplay.Connection?.Socket : null;
            if (Intent.Faulted && ReferenceEquals(identityPlayer, Items.World.SessionPlayer) && identityPlayer != null && identityMode == Main.netMode &&
                (world == null || ReferenceEquals(world, identityWorld)) && (Main.netMode == 0 || socket == null || ReferenceEquals(socket, identitySocket)))
                Items.Ownership.HoldInterruptedSource(Runtime.Generation, Transfer.UnconfirmedSources);
            nextWallet = nextRangeFailure = nextCommit = 0;
        }
        private void ConfirmIdentity()
        {
            Player p = Items.World.Player; object world = Main.ActiveWorldFileData;
            object socket = Main.netMode == 1 ? Netplay.Connection?.Socket : null;
            // Runtime may stop on a temporary identity read failure. Retain user
            // intent for the same positively identified native session; a null
            // reading never becomes evidence of a different world.
            if (p == null || world == null || Main.netMode == 1 && socket == null) { Intent.Invalidate(); return; }
            if (!ReferenceEquals(p, identityPlayer) || !ReferenceEquals(world, identityWorld) || !ReferenceEquals(socket, identitySocket) || identityMode != Main.netMode)
            { identityPlayer = p; identityWorld = world; identitySocket = socket; identityMode = Main.netMode; Intent.BeginSession(++identity); Transfer.BeginSession(); Range.Clear(); ResetAttempts(); ClearPresentationResult(); }
        }
        public void OnSessionEnded()
        {
            Intent.Invalidate(); CoinWithdrawalHooks.Retire(); Range.Clear();
            if (Main.gameMenu) { identityPlayer = null; identityWorld = identitySocket = null; Intent.BeginSession(++identity); Transfer.BeginSession(); ClearPresentationResult(); }
        }
        private void ClearPresentationResult() { operationError = noProgressHint = null; operationFeedback = false; hintBank = -1; }
        public void FailClosed() { if (Transfer.Executing) Intent.Unconfirmed(); else Intent.Invalidate(); }
        internal long Session { get { return identity; } }
        public void Update(ulong tick)
        {
#if DEBUG
            Updates++;
#endif
            if (stopping) return;
            if (!Available) { Unavailable(); return; }
            if (!Settings.Enabled) return;
            if (!TrustedIdentity) ConfirmIdentity();
            Player p = Player;
            if (p == null || !Input.CanStartActions || Main.gamePaused) return;
            if (Intent.Faulted) { UnconfirmedStatus(); return; }
            if (Intent.Protected || Intent.Pending)
            {
                Status = Intent.Pending ? "正在确认取出" : "取出保护中";
                Detail = "钱币先留在身上；离开全部个人银行范围，或关闭再开启后恢复。关闭容器不会解除。";
                // A non-void witness needs no wallet/capacity scan. Only void
                // availability depends on the closed bag in the finite wallet.
                if (Range.HasWitness(p, true)) return;
                if (!ReadWallet(p)) return;
                if (!Range.HasWitness(p, voidClosed) && tick >= nextRangeFailure)
                {
                    // A negative proof must include current void eligibility,
                    // even when the old positive witness was a different bank.
                    bool any;
                    if (Range.TryComplete(p, voidClosed, out any)) Intent.ObserveRange(true, any);
                    else nextRangeFailure = tick + 60;
                }
                return;
            }
            if (Main.ServerSideCharacter || Main.ActivePlayerFileData != null && Main.ActivePlayerFileData.ServerSideCharacter)
            { Status = "服务器角色暂不支持"; Detail = "此服务器管理角色存档，自动存钱已暂停；普通多人客户端可用。"; return; }
            if (!CanAct(p)) { Status = "等待手动操作"; Detail = "关闭容器、放下鼠标物品并结束有关操作后继续。"; return; }
            if (tick < nextWallet) { if (hasCoins) Range.Step(p, voidClosed); return; }
            nextWallet = tick + 6;
            if (!ReadWallet(p)) { Status = "等待可读状态"; return; }
            hasCoins = HasMovableCoins(p);
            if (!hasCoins) { Status = "没有可存钱币"; Detail = "手持或收藏的钱币不自动存入。"; return; }
            ulong sourceMask;
            if (!TrySources(p, out sourceMask))
            { hasCoins = false; Status = "等待有关操作"; Detail = "正在选用或处理的钱币会先留在身上。"; return; }
            // Wallet reads are coalesced separately from the bounded discovery
            // cursor. A static full sweep completes in at most 57 active frames.
            Range.Step(p, voidClosed);
            if (Range.Count == 0) { Status = "未找到个人银行"; Detail = "靠近原版可用的个人银行入口后继续；普通箱子不是目的地。"; return; }
            bool attempted = false, freshOrder = false;
            for (int i = 0; i < Range.Count; i++)
            {
                var entrance = Range.Banks[i]; int kind = entrance.Kind;
                if (!entrance.Valid(p, voidClosed)) continue;
                Facts bank = accounts[kind];
#if DEBUG
                CapacityReads += entrance.Bank.item?.Length ?? 0;
#endif
                if (!bank.Read(entrance.Bank.item)) { Status = "等待可读状态"; continue; }
                if (triedWallet[kind] == wallet.Revision && triedBank[kind] == bank.Revision &&
                    triedIntent[kind] == Intent.Generation) continue;
                if (!freshOrder)
                {
                    // A split sweep cannot certify current native order. Only
                    // actual new work pays for the complete synchronous order
                    // check, coalesced to at most once per 60 active updates.
                    // No-progress states skip it entirely; per-coin pickups do
                    // not trigger a region query. The write still checks its own
                    // current entrance and exact participants immediately.
                    if (tick < nextCommit) break;
                    nextCommit = tick + 60;
                    bool any;
                    if (!ReadWallet(p) || !Range.TryComplete(p, voidClosed, out any)) { Status = "等待可读状态"; break; }
                    freshOrder = true; i = -1; continue;
                }
                CoinOutcome result = Transfer.Execute(entrance, Intent.Generation);
                ReadWallet(p); bank.Read(entrance.Bank.item);
                // These cached facts only explain the last verified result.
                // Hover never re-reads inventory, bank capacity or native state.
                hintBank = result == CoinOutcome.None ? kind : -1;
                hintWalletRevision = wallet.Revision; hintBankRevision = bank.Revision;
                noProgressHint = bank.HasEmpty ? "无币账户首存仅支持存钱罐。" : "上次存入时账户没有可用空间；腾出空间后会继续。";
                if (result == CoinOutcome.Failed) { operationError = "本次自动存钱失败，钱币未转移。"; operationFeedback = true; }
                else if (result == CoinOutcome.Completed || result == CoinOutcome.Partial) { operationError = null; operationFeedback = false; }
                if (result != CoinOutcome.Rejected && result != CoinOutcome.Cancelled)
                { triedWallet[kind] = wallet.Revision; triedBank[kind] = bank.Revision; triedIntent[kind] = Intent.Generation; }
                attempted = true;
                Status = result == CoinOutcome.Completed ? "已存入" : result == CoinOutcome.Partial ? "部分已存入" : result == CoinOutcome.Unconfirmed ? "结果未确认" : result == CoinOutcome.Failed ? "本次未存入" : result == CoinOutcome.Rejected || result == CoinOutcome.Cancelled ? "等待有关操作" : "暂无可用空间";
                Detail = result == CoinOutcome.None ? "已有钱币的银行会按原版整理；空账户首存仅限存钱罐。" : null;
                break; // One bounded attempt. Never loop seed/MoveCoins on zero.
            }
            if (!attempted && Status == "未找到个人银行") Status = "等待账户变化";
        }
        private bool ReadWallet(Player p)
        {
#if DEBUG
            WalletReads += 58;
#endif
            if (!wallet.Read(p.inventory)) return false;
            voidClosed = false;
            for (int i = 0; i < 58; i++) if (p.inventory[i].type == 5325 && p.inventory[i].stack > 0) voidClosed = true;
            return true;
        }
        internal bool HasMovableCoins(Player p)
        { for (int i = 0; i < 58; i++) { Item item = p.inventory[i]; if (item != null && item.stack > 0 && !item.favorited && CoinRules.IsCoin(item.type)) return true; } return false; }
        private bool CanAct(Player p)
        {
            return !Main.ServerSideCharacter && (Main.ActivePlayerFileData == null || !Main.ActivePlayerFileData.ServerSideCharacter) &&
                !p.dead && p.chest == -1 && p.talkNPC < 0 && p.sign < 0 && Main.npcShop == 0 && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !Main.blockInput &&
                (CanGameplay == null || CanGameplay()) && Main.mouseItem != null && Main.mouseItem.IsAir &&
                PlayerInput.MouseInfo.LeftButton != ButtonState.Pressed && PlayerInput.MouseInfo.RightButton != ButtonState.Pressed && !Items.World.Busy;
        }
        internal bool Admit(CoinRange.Entrance entrance, long generation, out ulong mask)
        {
            mask = 0; Player p = Player;
            if (p == null || !Available || !Settings.Enabled || !Intent.Allows(identity, generation) || !Input.CanStartActions || Main.gamePaused || !CanAct(p)) return false;
            if (!TrySources(p, out mask)) return false;
            // MoveCoins may normalize or fill target coin/Air slots. G07
            // payment and void-use uncertainty must protect these accounts too.
            for(int i=0;i<40;i++)if(Items.Ownership.IsProtected(entrance.Kind+1,i))
            {Item item=entrance.Bank.item[i];if(item==null || item.IsAir || CoinRules.IsCoin(item.type))return false;}
            bool currentVoidClosed = false;
            for (int i = 0; i < 58; i++) if (p.inventory[i].type == 5325 && p.inventory[i].stack > 0) currentVoidClosed = true;
            return entrance.Valid(p, currentVoidClosed);
        }
        private bool TrySources(Player p, out ulong mask)
        {
            mask = 0;
            Items.World.RefreshManualRelease();
            for (int i = 0; i < 58; i++)
            {
                Item item = p.inventory[i]; if (item == null || item.stack < 0) return false;
                if (!CoinRules.IsCoin(item.type) || item.stack <= 0 || item.favorited) continue;
                if (Items.World.IsProtected(p, item, i)) return false;
                mask |= 1UL << i;
            }
            return mask != 0;
        }
        private void ResetAttempts() { for (int i = 0; i < 4; i++) triedWallet[i] = triedBank[i] = triedIntent[i] = -1; }
        private void Exit(object sender, EventArgs args)
        { if (stopping) return; stopping = true; Intent.Invalidate(); AppDomain.CurrentDomain.ProcessExit -= Exit; Settings.Stop(750); CoinWithdrawalHooks.Uninstall(); }

        private sealed class Facts
        {
            private struct Slot { internal Item Ref; internal int Type, Stack, Max; internal byte Prefix; internal bool Favorite; }
            private readonly Slot[] values; private Item[] array;
            internal long Revision { get; private set; }
            internal bool HasEmpty { get; private set; }
            internal Facts(int count) { values = new Slot[count]; }
            internal bool Read(Item[] current)
            {
                if (current == null || current.Length < values.Length) return false;
                // Validate the complete finite read before publishing any member.
                // A late unreadable slot must not swallow an earlier space change.
                for (int i = 0; i < values.Length; i++) if (current[i] == null || current[i].stack < 0) return false;
                bool changed = !ReferenceEquals(current, array); array = current; HasEmpty = false;
                for (int i = 0; i < values.Length; i++)
                {
                    Item item = current[i]; if (item == null || item.stack < 0) return false;
                    HasEmpty |= item.type < 1 || item.stack < 1;
                    Slot old = values[i];
                    // This is a no-progress cache, never a write permission.
                    // Native equivalent Item reconstruction cannot awaken another
                    // full bank and create a loop. Every real write separately
                    // captures/revalidates exact current member references.
                    changed |= old.Type != item.type || old.Stack != item.stack || old.Max != item.maxStack || old.Prefix != item.prefix || old.Favorite != item.favorited;
                    values[i] = new Slot { Ref = item, Type = item.type, Stack = item.stack, Max = item.maxStack, Prefix = item.prefix, Favorite = item.favorited };
                }
                if (changed) Revision++; return true;
            }
        }
    }
}
