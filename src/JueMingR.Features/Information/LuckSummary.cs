using System;
using System.Collections.Generic;
using JueMingR.Platform.Information;

namespace JueMingR.Features.Information
{
    public sealed class LuckSummary
    {
        private Visible previous;
        private bool hasPrevious;
        public InformationText Content { get; private set; } = new InformationText();
#if DEBUG
        public int TextBuilds { get; private set; }
#endif
        public void Update(LuckObservation value)
        {
            var visible = new Visible(value);
            if (hasPrevious && previous.Same(visible)) return;
            previous = visible; hasPrevious = true;
#if DEBUG
            TextBuilds++;
#endif
            if (visible.Status != InformationAvailability.Ready)
            { Content.Publish(InformationText.Status("幸运值", visible.Status, "需要已解救巫师")); return; }
            var parts = new List<string>(11);
            if (visible.Ladybug != 0) Add(parts, "瓢虫", visible.Ladybug, "；游戏时钟剩余 " + (visible.Seconds / 60) + "分" + (visible.Seconds % 60) + "秒");
            if (visible.Torch != 0) Add(parts, "火把", visible.Torch, "；原值 " + InformationText.Signed(visible.RawTorch));
            if (visible.Potion != 0) Add(parts, "幸运药水", visible.Potion * 0.1, "；等级 " + visible.Potion);
            if (visible.Kite != 0) Add(parts, "风筝", visible.Kite * 0.1 / 3, "；等级 " + visible.Kite);
            Add(parts, "银河珍珠", (visible.Flags & 1) != 0 ? 0.03 : 0);
            Add(parts, "灯笼夜", (visible.Flags & 2) != 0 ? 0.3 : 0);
            Add(parts, "花园侏儒", (visible.Flags & 4) != 0 ? 0.2 : 0);
            Add(parts, "臭味", (visible.Flags & 8) != 0 ? -0.25 : 0);
            Add(parts, "装备", visible.Equipment);
            if (visible.Coin != 0) Add(parts, "钱币", visible.Coin, "；原值 " + InformationText.Number(visible.RawCoin) + "；" + CoinBand(visible.Coin));
            Add(parts, "破镜坏运", (visible.Flags & 16) != 0 ? -0.25 : 0);
            string details = parts.Count == 0 ? (visible.Complete ? "无" : "部分明细不可用") : String.Join("，", parts);
            Content.Publish("幸运值：" + InformationText.Signed(visible.Total) + "\n来源：" + details +
                (!visible.Complete && parts.Count != 0 ? "\n部分明细不可用" : visible.Mismatch ? "\n明细与总值不一致" : ""));
        }
        public void Clear() { hasPrevious = false; Content.Clear(); }
        public static double Ladybug(float time) { return time > 0 ? time / 43200.0 * 0.2 : time / 10800.0 * 0.2; }
        public static double Coin(float value)
        {
            if (value == 0) return 0;
            return value > 249000 ? 0.2 : value > 24900 ? 0.175 : value > 2490 ? 0.15 : value > 249 ? 0.125 :
                value > 24.9 ? 0.1 : value > 2.49 ? 0.075 : value > 0.249 ? 0.05 : 0.025;
        }
        private static string CoinBand(double contribution)
        {
            return contribution == 0.2 ? ">249000 档" : contribution == 0.175 ? ">24900 档" : contribution == 0.15 ? ">2490 档" :
                contribution == 0.125 ? ">249 档" : contribution == 0.1 ? ">24.9 档" : contribution == 0.075 ? ">2.49 档" : contribution == 0.05 ? ">0.249 档" : "非零基础档";
        }
        private static void Add(List<string> parts, string name, double value, string explanation = "")
        { if (Math.Abs(value) >= 0.0005) parts.Add(name + " " + InformationText.Signed(value) + (explanation.Length == 0 ? "" : "（" + explanation.Substring(1) + "）")); }

        // Fixed scalar key, no strings/arrays/boxing on a stable sample. Sum
        // every valid unrounded contribution before deciding completeness or
        // mismatch; formatting never becomes the authoritative luck value.
        private struct Visible
        {
            internal InformationAvailability Status;
            internal double Total, Ladybug, Torch, Equipment, Coin, RawTorch, RawCoin;
            internal int Seconds, Potion, Kite, Flags;
            internal bool Complete, Mismatch;
            internal Visible(LuckObservation value)
            {
                this = default(Visible); Status = value.Availability;
                if (Status != InformationAvailability.Ready) return;
                if (!InformationText.Finite(value.Total)) { Status = InformationAvailability.Unavailable; return; }
                Total = InformationText.Rounded(value.Total.Value);
                bool ladybug = InformationText.Finite(value.LadybugTime), torch = InformationText.Finite(value.Torch),
                    equipment = InformationText.Finite(value.Equipment), coin = InformationText.Finite(value.Coin),
                    potion = value.Potion.HasValue && value.Potion >= 0 && value.Potion <= 3, kite = value.Kite.HasValue && value.Kite >= 0 && value.Kite <= 3;
                Complete = ladybug && torch && equipment && coin && potion && kite && value.Pearl.HasValue && value.Lantern.HasValue &&
                    value.Gnome.HasValue && value.Stinky.HasValue && value.Mirror.HasValue;
                double rawLadybug = ladybug ? LuckSummary.Ladybug(value.LadybugTime.Value) : 0,
                    rawTorch = torch ? value.Torch.Value * 0.2 : 0, rawEquipment = equipment ? value.Equipment.Value : 0,
                    rawCoin = coin ? LuckSummary.Coin(value.Coin.Value) : 0;
                Ladybug = InformationText.Rounded(rawLadybug); Torch = InformationText.Rounded(rawTorch);
                Equipment = InformationText.Rounded(rawEquipment); Coin = rawCoin;
                RawTorch = Torch != 0 ? InformationText.Rounded(value.Torch.Value) : 0; RawCoin = Coin != 0 ? InformationText.Rounded(value.Coin.Value) : 0;
                // Bound rendering of pathological but finite values. Normal
                // vanilla timers are far below Int32.MaxValue seconds.
                Seconds = Ladybug != 0 ? (int)Math.Min(Int32.MaxValue, Math.Ceiling(Math.Abs((double)value.LadybugTime.Value) / 60)) : 0;
                Potion = potion ? value.Potion.Value : 0; Kite = kite ? value.Kite.Value : 0;
                Flags = (value.Pearl == true ? 1 : 0) | (value.Lantern == true ? 2 : 0) | (value.Gnome == true ? 4 : 0) |
                    (value.Stinky == true ? 8 : 0) | (value.Mirror == true ? 16 : 0);
                double rest = Potion * 0.1 + Kite * 0.1 / 3 + (value.Pearl == true ? 0.03 : 0) + (value.Lantern == true ? 0.3 : 0) +
                    (value.Gnome == true ? 0.2 : 0) + (value.Stinky == true ? -0.25 : 0) + (value.Mirror == true ? -0.25 : 0);
                double sum = rawLadybug + rawTorch + rawEquipment + rawCoin + rest;
                double magnitude = Math.Abs(rawLadybug) + Math.Abs(rawTorch) + Math.Abs(rawEquipment) + Math.Abs(rawCoin) + 2;
                Mismatch = Complete && Math.Abs(value.Total.Value - sum) > 0.0005 + 0.000002 * magnitude;
            }
            internal bool Same(Visible other)
            {
                return Status == other.Status && Total == other.Total && Ladybug == other.Ladybug && Torch == other.Torch && Equipment == other.Equipment &&
                    Coin == other.Coin && RawTorch == other.RawTorch && RawCoin == other.RawCoin && Seconds == other.Seconds && Potion == other.Potion &&
                    Kite == other.Kite && Flags == other.Flags && Complete == other.Complete && Mismatch == other.Mismatch;
            }
        }
    }
}
