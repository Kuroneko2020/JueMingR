using System;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.CoinDeposit
{
    // Publish enabling only after a reliable preference commit. Disabling stops
    // new actions at command acceptance, including a failed/unknown file write.
    public sealed class CoinSettings : IDisposable
    {
        private readonly DocumentWorker<bool> worker;
        private long command;
        private bool suspended;
        private bool feedbackPending;
        public bool Value { get; private set; }
        public bool Enabled { get { return Loaded && Value && !suspended && !Protected; } }
        public bool Loaded { get; private set; }
        public bool Busy { get; private set; }
        public bool Protected { get; private set; }
        public long Revision { get; private set; }
        public string Message { get; private set; }
        public CoinSettings(IPreferenceStorage storage)
        {
            var codec = new CoinPreferenceCodec();
            worker = new DocumentWorker<bool>(storage, codec.Decode, codec.Encode, false);
        }
        public bool Set(bool value)
        {
            if (!Loaded || Busy || Protected || value == Enabled) return false;
            if (!worker.TrySubmit(++command, value)) return false;
            suspended = true; Busy = true; Revision++; return true;
        }
        public void Poll()
        {
            DocumentResult<bool> result;
            if (!worker.TryTake(out result)) return;
            if (result.CommandId == 0) Loaded = true;
            Busy = false; Protected = result.IsProtected || result.CommitUnconfirmed || result.CommandId == 0 && !result.Success;
            if (result.Success) { Value = result.Value; suspended = false; Message = null; feedbackPending = false; }
            else
            {
                Message = result.CommitUnconfirmed ? "存钱设置保存结果未确认，已暂停并保护文件。" : "存钱设置无法保存或读取，已暂停；原文件保留。";
                feedbackPending = true;
            }
            Revision++;
        }
        // A retry is not recovery. Keep the failure available to hover until a
        // reliable result replaces it; each new failed result earns one alert.
        public void TakeFeedback(Action<string> display)
        { if (feedbackPending) { display(Message); feedbackPending = false; } }
        public bool Stop(int milliseconds) { return worker.Stop(milliseconds); }
        public void Dispose() { worker.Dispose(); }
    }
}
