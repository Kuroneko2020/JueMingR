using System;
using JueMingR.Features.Combat;
using Terraria;
using Terraria.Chat;
using Terraria.Chat.Commands;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum ReportAttempt { NotInvoked, Pending, Invoked, Observed, Unknown }
    internal sealed class CombatReports : IDisposable
    {
        private readonly HostCombat host;
        private readonly ReportHooks hooks;
        private readonly BattleEndLedger ledger=new BattleEndLedger(Main.maxNPCs);
        private readonly NPCDamageTracker[] recent=new NPCDamageTracker[3];
        private readonly NPCDamageTracker[] scratch=new NPCDamageTracker[3];
        private Player player;
        private object world,socket;
        private long session;
        private int mode,count;
        private bool baseline,dirty,pending;
        private uint pendingAt,lastEndAt,lastStep,nextAttempt;
        internal ReportAttempt State {get;private set;}
        internal bool Ready {get{return hooks.Ready;}}
        internal Exception Error {get{return hooks.Error;}}
#if DEBUG
        internal int BaselineNpcReads {get;private set;}
        internal int RecentReads {get;private set;}
        internal int Requests {get;private set;}
        internal int ObservedReports {get;private set;}
#endif
        internal CombatReports(HostCombat host){this.host=host;hooks=new ReportHooks(this);}
        // Death ends action permission, not the observer's identity: a boss
        // commonly escapes while its player waits to respawn.
        private Player SessionPlayer {get{return host.Tools.Items.World.SessionPlayer;}}
        private bool Enabled {get{return Ready && host.Available && host.Settings.CanRun && host.Settings.Value.Enabled(6);}}
        private bool SameSession()
        {
            return baseline && Enabled && host.Runtime.IsSessionActive && !Main.gameMenu && Main.netMode==mode &&
                session==host.Runtime.Generation && ReferenceEquals(player,SessionPlayer) && ReferenceEquals(world,Main.ActiveWorldFileData) &&
                (mode==0 || ReferenceEquals(socket,Netplay.Connection.Socket));
        }
        internal bool CanObserve {get{return SameSession();}}
        internal void Poll()
        {
            // Called outside the feature-enabled lane: disabling, exit and a
            // new socket really discard pending work and observed identities.
            if(!Enabled || !host.Runtime.IsSessionActive || SessionPlayer==null || Main.gameMenu || Main.netMode<0 || Main.netMode>1){Reset();return;}
            if(!SameSession())
            {
                Reset();player=SessionPlayer;world=Main.ActiveWorldFileData;session=host.Runtime.Generation;mode=Main.netMode;
                socket=mode==1?Netplay.Connection.Socket:null;baseline=true;lastStep=Main.GameUpdateCount;
                if(mode==0)ReadRecent(true);
                else
                {
                    ledger.Events(EventMask(),true);
                    // Some composite members are not boss=true and are absent
                    // from AnyActiveBossNPC. One bounded baseline is necessary;
                    // all subsequent client updates use actual message receipts.
                    for(int i=0;i<Main.maxNPCs;i++)
                        {
#if DEBUG
                            BaselineNpcReads++;
#endif
                            ObserveNpc(i,true);
                        }
                    ledger.TakeEnded();
                }
                return;
            }
            uint now=Main.GameUpdateCount;if(lastStep==now)return;lastStep=now;
            if(mode==0 && dirty){dirty=false;ReadRecent(false);}
            if(mode==1 && ledger.TakeEnded())Queue(now);
            if(!pending || unchecked(now-lastEndAt)<(mode==1?2u:0u) || unchecked((int)(now-nextAttempt))<0)return;
            // The two client steps coalesce original end packets and allow the
            // server's tracker update to run. They are not a readiness ACK.
            if(mode==1 && (!Netplay.Connection.IsActive || Netplay.Connection.PendingTermination || socket==null || !Netplay.Connection.IsConnected()))
            {
                if(unchecked(now-pendingAt)>=300){pending=false;State=ReportAttempt.NotInvoked;}
                else nextAttempt=unchecked(now+15);
                return;
            }
            ChatMessage message;
            try{message=new ChatMessage(string.Empty);message.SetCommand<BossDamageCommand>();}
            catch{nextAttempt=unchecked(now+15);if(unchecked(now-pendingAt)>=300){pending=false;State=ReportAttempt.NotInvoked;}return;}
            pending=false;State=ReportAttempt.Invoked;
#if DEBUG
            Requests++;
#endif
            try
            {
                if(mode==1)ChatHelper.SendChatMessageFromClient(message);
                else ChatManager.Commands.ProcessIncomingMessage(message,Main.myPlayer);
                // Vanilla's socket outlet can swallow AsyncSend exceptions.
                // Returning proves invocation only. Never replay unknown work.
                if(State==ReportAttempt.Invoked)State=ReportAttempt.Unknown;
            }
            catch{if(State!=ReportAttempt.Observed)State=ReportAttempt.Unknown;}
        }
        private void Queue(uint now)
        {lastEndAt=now;if(pending)return;pending=true;pendingAt=nextAttempt=now;State=ReportAttempt.Pending;}
        private void ReadRecent(bool first)
        {
            int nextCount=0;bool added=false;
            foreach(var tracker in NPCDamageTracker.RecentAttempts())
            {
#if DEBUG
                RecentReads++;
#endif
                if(nextCount>=scratch.Length)break;scratch[nextCount++]=tracker;
                bool known=false;for(int i=0;i<count;i++)if(ReferenceEquals(recent[i],tracker)){known=true;break;}
                if(!known)added=true;
            }
            Array.Clear(recent,0,recent.Length);Array.Copy(scratch,recent,nextCount);Array.Clear(scratch,0,scratch.Length);count=nextCount;
            if(!first && added)Queue(Main.GameUpdateCount);
        }
        internal void RecentChanged(){if(SameSession() && mode==0)dirty=true;}
        internal void Message(MessageBuffer buffer,int start,int length,int type)
        {
            if(!SameSession() || mode!=1)return;
            if(type!=7 && type!=23 || buffer.readBuffer==null || start<0 || length<1 || length>buffer.readBuffer.Length-start ||
                buffer.reader==null || buffer.reader.BaseStream.Position!=(long)start+length)return;
            if(type==7){ledger.Events(EventMask());return;}
            if(type!=23 || length<3 || buffer.readBuffer==null || start<0 || start>buffer.readBuffer.Length-3)return;
            int index=buffer.readBuffer[start+1];int generation=buffer.readBuffer[start+2];
            if(index>=Main.maxNPCs || Main.npc[index]==null || Main.npc[index].generation!=generation)return;
            ObserveNpc(index);
        }
        private void ObserveNpc(int index,bool initial=false)
        {
            var n=Main.npc[index];if(n==null)return;int group=0;
            if(n.type>0 && n.type<NPCDamageTracker.CustomBossDefinitions.Length)
            {
                var definition=NPCDamageTracker.CustomBossDefinitions[n.type];
                if(n.boss || definition!=null)group=definition!=null && definition.NPCTypes!=null && definition.NPCTypes.Count>0?definition.NPCTypes[0]:n.type;
            }
            // An active NPC can have life<=0 after local pending damage is
            // subtracted by vanilla. Only server-applied active ends a member.
            bool active=n.active;
            if(initial && group>0 && !active)
            {int damage;bool phaseChange;NPC.GetPendingDamage(n,out damage,out phaseChange);if(damage>0)active=true;}
            ledger.Observe(index,n.generation,n.type,group,active);
        }
        private static int EventMask()
        {return (Main.invasionType>=1 && Main.invasionType<=4?1<<(Main.invasionType-1):0) | (Main.pumpkinMoon?16:0) | (Main.snowMoon?32:0);}
        internal void Observed()
        {
            if(!SameSession())return;
#if DEBUG
            ObservedReports++;
#endif
            // A report has no request ID. Observation cannot cancel a pending
            // new battle merely because an older report has the same name.
            if(State==ReportAttempt.Invoked || State==ReportAttempt.Unknown)State=ReportAttempt.Observed;
        }
        internal void Reset()
        {
            if(!baseline && !pending)return;
            baseline=dirty=pending=false;player=null;world=socket=null;count=0;ledger.Clear();Array.Clear(recent,0,recent.Length);Array.Clear(scratch,0,scratch.Length);State=ReportAttempt.NotInvoked;
        }
        public void Dispose(){Reset();hooks.Dispose();}
    }
}
