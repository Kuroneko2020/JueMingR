using System;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    public sealed partial class LibraryState
    {
        public bool ExchangeBusy { get { return worker.ExchangeBusy; } }
        public BlueprintExchangePhase ExchangePhase { get { return worker.ExchangePhase; } }
        public void ConfigureExchange(IBlueprintExchangeIo io, IPreferenceStorage counter, Func<DateTime> localClock = null)
        { worker.ConfigureExchange(io, counter, localClock ?? (() => DateTime.Now)); }
        public bool TryBeginExchange(BlueprintExchangeRequest request) { return worker.TryBeginExchange(request); }
        public bool TryReserveExchange(Guid id) { return worker.TryReserveExchange(id); }
        public bool TryTakeExchangeReceipt(out BlueprintExchangeReceipt value) { return worker.TryTakeExchangeReceipt(out value); }
        public void CancelExchange(Guid id) { worker.CancelExchange(id); }
    }
}
