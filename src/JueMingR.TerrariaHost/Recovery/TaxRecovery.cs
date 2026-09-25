using System;
using Terraria;
using JueMingR.TerrariaHost.Npcs;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class TaxRecovery
    {
        private readonly HostRecovery host;
        internal readonly NearbyServiceTarget Target;
        private bool unconfirmed,refused;
#if DEBUG
        internal long NativeCalls;
        internal Exception LastFailure;
#endif
        internal TaxRecovery(HostRecovery host,NativeNpcObservation npcs){this.host=host;Target=new NearbyServiceTarget(npcs,441);}
        internal void Reset(){unconfirmed=refused=false;Target.Reset();}
        internal void ObserveSettlement()
        {
            // The unknown cycle belongs to the player's tax balance, not an
            // NPC. Growth, another collector and toggles cannot settle it.
            var p=host.Player;if(unconfirmed && p!=null && p.taxMoney==0)unconfirmed=false;
        }
        internal void Update(Player p,ulong tick)
        {
            if(p.taxMoney<=0 || unconfirmed)return;
            if(refused){if(Target.Valid(p))return;refused=false;}
            if(Target.Find(p,tick)==null || !host.Admit(p))return;
            bool started=false;
            try
            {
                if(!host.Dialog.Open(p,Target) || !host.Dialog.Owns || !Target.Valid(p) || p.taxMoney<=0)return;
                // Vanilla requests world items, then clears taxMoney. There is
                // no request-specific server ACK, and pickup is a later action.
                started=true;
#if DEBUG
                NativeCalls++;
#endif
                Main.NPCChatText_DoTaxCollector();
                if(p.taxMoney==0)host.Dialog.Success();else unconfirmed=true;
            }
#if DEBUG
            catch(Exception error)
#else
            catch(Exception)
#endif
            {
#if DEBUG
                LastFailure=error;
#endif
                // A failure after vanilla cleared this balance still observes
                // settlement now; later tax growth must not hide that boundary.
                if(started)unconfirmed=p.taxMoney!=0;else refused=true;
                host.Report(started?"收税结果未确认，保留原版对话；本次税款不会重复领取。":"税收官对话未完成，已停止重复打开。");
            }
        }
    }
}
