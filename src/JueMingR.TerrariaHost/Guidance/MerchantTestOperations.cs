using System;
using System.Threading;
using JueMingR.Platform.Guidance;
using JueMingR.Platform.Operations;
using Terraria;

namespace JueMingR.TerrariaHost.Guidance
{
    internal sealed class MerchantTestOperations : IMerchantTestPort
    {
        private readonly Func<long> session;
        private readonly Func<bool> inputAllowed;
        private readonly Action spawn;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        internal MerchantTestOperations(Func<long> session, Func<bool> inputAllowed) : this(session, inputAllowed, WorldGen.SpawnTravelNPC) { }
        // The isolated Host probe supplies an operation seam. Production binds
        // exactly .8's zero-argument entry, never NewNPC or a copied house scan.
        internal MerchantTestOperations(Func<long> session, Func<bool> inputAllowed, Action spawn)
        { this.session = session; this.inputAllowed = inputAllowed; this.spawn = spawn; }
#if DEBUG
        internal int NativeCalls { get; private set; }
#endif
        internal static string UnavailableReason
        {
            get
            {
                if (!GuidanceObservationReader.ValidPlayer) return "需要有效且存活的本地玩家。";
                if (Main.netMode != 0) return "游商测试仅限单人世界。";
                if (!Main.dayTime) return "原版旅商到访需要白天。";
                if (Main.eclipse) return "日食期间不能尝试到访。";
                if (Main.invasionType > 0 && Main.invasionDelay == 0 && Main.invasionSize > 0) return "正在入侵，不能尝试到访。";
                return null;
            }
        }
        public MerchantTestReceipt Execute(MerchantTestRequest request)
        {
            if (request == null || thread != Thread.CurrentThread.ManagedThreadId || request.Session != session() || !inputAllowed())
                return Result(GameOperationOutcome.Cancelled, "输入或世界已改变，本次未召唤。");
            string unavailable = UnavailableReason;
            if (unavailable != null) return Result(GameOperationOutcome.Rejected, unavailable);
            bool found;
            if (!TryFind(out found)) return Result(GameOperationOutcome.Rejected, "NPC资料未可靠取得，本次未召唤。");
            // Hidden active merchants also block vanilla's entry. Never refresh
            // their shop or fabricate a client-only target for the direction.
            if (found) return Result(GameOperationOutcome.Rejected, "当前已有活动旅商，本次未召唤。");
            try
            {
#if DEBUG
                NativeCalls++;
#endif
                spawn();
                if (TryFind(out found) && found) return Result(GameOperationOutcome.Succeeded, "已观察到旅商到访。");
                return Result(GameOperationOutcome.Unconfirmed, "已尝试原版到访，未确认生成；可能缺少合适住房或位置，不会自动重试。");
            }
            catch (Exception) { return Result(GameOperationOutcome.Failed, "原版到访发生异常；可能已有商品或世界副作用，不会自动重试。"); }
        }
        private static bool TryFind(out bool found)
        {
            found = false; if (Main.npc == null || Main.npc.Length < Main.maxNPCs) return false;
            for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n != null && n.active && n.type == 368) { found = true; return true; } }
            return true;
        }
        private static MerchantTestReceipt Result(GameOperationOutcome outcome, string text) { return new MerchantTestReceipt(outcome, text); }
    }
}
