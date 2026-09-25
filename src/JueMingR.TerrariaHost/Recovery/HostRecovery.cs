using System;
using System.IO;
using System.Threading;
using JueMingR.Features.Recovery;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Recovery
{
    // Composition, admission and lifetime only. Native operation scopes belong
    // to the leaf adapters, and persistence workers only see immutable data.
    internal sealed class HostRecovery : IRuntimeFeature
    {
        internal readonly SingleFeatureRuntime Runtime;
        internal readonly HostItems Items;
        internal readonly HostInputState Input;
        internal readonly RecoverySettings Potions, Buffs, Services;
        internal readonly PotionRecovery PotionsUse;
        internal readonly RecoveryCatalog Catalog;
        internal Func<bool> CanGameplay;
        internal Func<bool> IsQuickUse;
        internal bool Available {get;private set;}
        internal Exception SetupError {get;private set;}
        internal string Error {get;private set;}
        private bool feedback;
        private readonly int thread=Thread.CurrentThread.ManagedThreadId;
        private Player identityPlayer;
        private object identityWorld,identitySocket;
        private int identityMode;
        internal readonly ulong[] UnknownSlots=new ulong[5];
        internal ulong Tick {get;private set;}
        internal long ManualEpoch {get;private set;}
        internal static readonly string[] Actions={"recovery.life","recovery.mana","services.nurse","recovery.furniture","recovery.buffs","services.tax"};
        internal static readonly string[] Names={"自动回血","自动回蓝","自动护士","家具增益","自动增益","自动收税"};
        internal Player Player {get{return Runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId==thread?Items.World.Player:null;}}
        public bool Enabled {get{return Available && (Potions.Ready && (Potions.Value.LifeMode!=0 || Potions.Value.Mana) || Buffs.Ready && Buffs.Value.Buffs || Services.Ready && (Services.Value.Nurse || Services.Value.Furniture || Services.Value.Tax));}}
        internal HostRecovery(string directory,SingleFeatureRuntime runtime,HostItems items,HostInputState input)
        {
            Runtime=runtime;Items=items;Input=input;
            string root=Path.Combine(directory,"JueMingRData","config","features");
            Potions=new RecoverySettings(new AtomicFileDocument(Path.Combine(root,"recovery-potions.json"),65536),0);
            Buffs=new RecoverySettings(new AtomicFileDocument(Path.Combine(root,"recovery-buffs.json"),65536),1);
            Services=new RecoverySettings(new AtomicFileDocument(Path.Combine(root,"nearby-services.json"),65536),2);
            PotionsUse=new PotionRecovery(this);Catalog=new RecoveryCatalog(this);
            try{RecoveryHooks.Install(this);Available=Items.Available;}
            catch(Exception e){SetupError=e;RecoveryHooks.Uninstall();Report("自动恢复暂不可用，原版使用方式不受影响。");}
            Items.AllowsOwnedRecovery=PotionsUse.Owns;
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal RecoverySettings Settings(int feature){return feature<2?Potions:feature==4 || feature>=6?Buffs:Services;}
        internal bool Controls(int feature){var s=Settings(feature);return Available && Player!=null && s.Loaded && !s.Protected && !s.Busy;}
        internal void Set(int feature,int value){if(Controls(feature))Settings(feature).Set(Settings(feature).Value.Change(feature,value));}
        internal int Value(int feature)
        {var s=Settings(feature);if(!s.Ready)return 0;var v=s.Value;return feature==0?v.LifeMode:feature==1?(v.Mana?1:0):feature==2?(v.Nurse?1:0):feature==3?(v.Furniture?1:0):feature==4?(v.Buffs?1:0):feature==5?(v.Tax?1:0):feature==6?(v.FollowAdd?1:0):(v.FollowRemove?1:0);}
        internal void Register(HotkeyRegistry registry)
        {for(int i=0;i<6;i++){int id=i;registry.Register(new HotkeyAction(Actions[i],Names[i],HotkeyContext.Gameplay,()=>Controls(id),()=>Set(id,Value(id)==0?1:0)));}}
        internal void Poll(){Potions.Poll();Buffs.Poll();Services.Poll();}
        internal bool Admit(Player p)
        {
            return Available && p!=null && ReferenceEquals(p,Player) && Input.CanStartActions && !Main.gamePaused &&
                CanGameplay!=null && CanGameplay() && !p.dead && !p.CCed && !p.cursed && !p.noItems && !p.isOperatingAnotherEntity &&
                !p.HasLockedInventory() && !Main.LocalPlayerHasPendingInventoryActions() && !Items.World.Busy &&
                p.chest==-1 && p.talkNPC<0 && p.sign<0 && Main.npcShop==0 && Main.mouseItem!=null && Main.mouseItem.IsAir &&
                !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !PlayerInput.WritingText && !Main.blockInput &&
                !Main.ServerSideCharacter;
        }
        internal bool Protected(Player p,Item[] array,int account,int slot)
        {
            Item item=array[slot];
            return item==null || item.IsAir || ReferenceEquals(item,Main.mouseItem) || Items.Ownership.IsProtected(account,slot) ||
                Items.World.ManualMaterials.Contains(item) || account==0 && (slot==Items.World.ManualSlot || p.inventoryChestStack[slot]) ||
                Items.World.AdditionalProtection!=null && Items.World.AdditionalProtection(item);
        }
        internal RecoverySource Source(Player p,Item[] array,int account,int slot,long revision)
        {return new RecoverySource{Player=p,Items=array,Item=array[slot],Account=account,Slot=slot,Type=array[slot].type,Stack=array[slot].stack,Session=Runtime.Generation,Revision=revision};}
        public void OnSessionStarted()
        {
            var socket=Main.netMode==1?Netplay.Connection.Socket:null;
            if(!ReferenceEquals(identityPlayer,Main.LocalPlayer) || !ReferenceEquals(identityWorld,Main.ActiveWorldFileData) || !ReferenceEquals(identitySocket,socket) || identityMode!=Main.netMode)
            {Array.Clear(UnknownSlots,0,5);PotionsUse.Reset();Error=null;feedback=false;}
            identityPlayer=Main.LocalPlayer;identityWorld=Main.ActiveWorldFileData;identitySocket=socket;identityMode=Main.netMode;
            Items.Ownership.HoldRecovery(Runtime.Generation,UnknownSlots);
        }
        public void OnSessionEnded(){if(Main.gameMenu){identityPlayer=null;identityWorld=null;identitySocket=null;}Catalog.Close();}
        public void Update(ulong tick)
        {
            Tick=tick;if(!Enabled)return;
            Player p=Player;if(!Admit(p))return;
            Items.World.RefreshManualRelease();
            if(Value(0)!=0)PotionsUse.Heal(p);
            if(Value(1)!=0)PotionsUse.Mana(p);
        }
        internal void ObserveManual(){ManualEpoch++;}
        internal void Report(string message){if(Error==message)return;Error=message;feedback=true;}
        internal string Hint(int feature){return Settings(feature).Message??Error;}
        internal void TakeFeedback(Action<string> display){Potions.TakeFeedback(display);Buffs.TakeFeedback(display);Services.TakeFeedback(display);if(feedback){display(Error);feedback=false;}}
        public void FailClosed(){Available=false;Report("自动恢复已停止；结果未确认的操作不会重试。");}
        internal void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;Potions.Stop(750);Buffs.Stop(750);Services.Stop(750);}
    }
}
