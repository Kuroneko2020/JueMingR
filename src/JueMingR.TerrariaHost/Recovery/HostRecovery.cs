using System;
using System.IO;
using System.Threading;
using JueMingR.Features.Recovery;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
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
        internal readonly BuffRecovery BuffUse;
        internal readonly BuffLearning Learning;
        internal readonly FurnitureRecovery Furniture;
        internal readonly NurseRecovery Nurse;
        internal readonly ServiceDialog Dialog;
        internal readonly TaxRecovery Tax;
        internal readonly RecoveryCatalog Catalog;
        internal Func<bool> CanGameplay;
        internal Func<bool> CanBackgroundBuff;
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
        internal long PresentationRevision {get;private set;}
        internal static readonly string[] Actions={"recovery.life","recovery.mana","services.nurse","recovery.furniture","recovery.buffs","services.tax"};
        internal static readonly string[] Names={"自动回血","自动回蓝","自动护士","家具增益","自动增益","自动收税"};
        internal Player Player {get{return Runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId==thread?Items.World.Player:null;}}
        public bool Enabled {get{return Available && (Potions.Ready && (Potions.Value.LifeMode!=0 || Potions.Value.Mana) || Buffs.Ready && Buffs.Value.Buffs || Services.Ready && (Services.Value.Nurse || Services.Value.Furniture || Services.Value.Tax));}}
        internal HostRecovery(string directory,SingleFeatureRuntime runtime,HostItems items,HostInputState input,World.WorldTileObservation tiles,Npcs.NativeNpcObservation npcs)
        {
            Runtime=runtime;Items=items;Input=input;
            string root=Path.Combine(directory,"JueMingRData","config","features");
            var potionsFile=new AtomicFileDocument(Path.Combine(root,"recovery-potions.json"),65536,true,".schema1-original");
            Potions=new RecoverySettings(potionsFile,0,new RetainingPotionCodec(potionsFile));
            Buffs=new RecoverySettings(new AtomicFileDocument(Path.Combine(root,"recovery-buffs.json"),65536),1);
            Services=new RecoverySettings(new AtomicFileDocument(Path.Combine(root,"nearby-services.json"),65536),2);
            PotionsUse=new PotionRecovery(this);BuffUse=new BuffRecovery(this);Learning=new BuffLearning(this);Furniture=new FurnitureRecovery(this,tiles);Catalog=new RecoveryCatalog(this);
            Dialog=new ServiceDialog(this);Nurse=new NurseRecovery(this,npcs);Tax=new TaxRecovery(this,npcs);
            try{RecoveryHooks.Install(this);Available=Items.Available;}
            catch(Exception e){SetupError=e;RecoveryHooks.Uninstall();Report("自动恢复暂不可用，原版使用方式不受影响。");}
            Items.AllowsOwnedRecovery=(array,slot)=>PotionsUse.Owns(array,slot) || BuffUse.Owns(array,slot);
            Items.AllowsOwnedPayment=()=>Nurse.Executing;
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal RecoverySettings Settings(int feature){return feature<2?Potions:feature==4 || feature>=6?Buffs:Services;}
        internal bool Controls(int feature){var s=Settings(feature);return Available && Player!=null && s.Loaded && !s.Protected && !s.Busy;}
        internal void Set(int feature,int value){if(Controls(feature) && Settings(feature).Set(Settings(feature).Value.Change(feature,value)) && feature==2 && value==0)Nurse.Rearm();}
        internal int Value(int feature)
        {var s=Settings(feature);if(!s.Ready)return 0;var v=s.Value;return feature==0?v.LifeMode:feature==1?(v.Mana?1:0):feature==2?(v.Nurse?1:0):feature==3?(v.Furniture?1:0):feature==4?(v.Buffs?1:0):feature==5?(v.Tax?1:0):feature==6?(v.FollowAdd?1:0):(v.FollowRemove?1:0);}
        internal void Register(HotkeyRegistry registry, Hotkeys.HotkeyStateFeedback feedback = null)
        {
            for(int i=0;i<6;i++)
            {
                int id=i; var settings=Settings(id); Action command=()=>Set(id,Value(id)==0?(id==0?settings.Value.LastLifeMode:1):0);
                if(feedback!=null)command=feedback.Committed(Actions[id],Names[id],command,()=>Value(id),()=>Controls(id),
                    ()=>settings.AcceptedCommandId,()=>settings.CompletedCommandId,()=>settings.CompletionSucceeded,
                    id==0?(Func<int,string>)(mode=>mode==2?"智能":"快速"):null);
                registry.Register(new HotkeyAction(Actions[id],Names[id],HotkeyContext.Gameplay,()=>Controls(id),command));
            }
        }
        internal void Poll(){Potions.Poll();Buffs.Poll();Services.Poll();Learning.Poll();Tax.ObserveSettlement();}
        internal bool Admit(Player p)
        {return Input.CanStartActions && CanGameplay!=null && CanGameplay() && SafePlayer(p);}
        // Only automatic buffs may run without an input gesture in background.
        // Never unpause, manufacture input, or discard unknown source ownership.
        internal bool AdmitBuff(Player p)
        {return Input.IsFocused?Admit(p):Main.CanUpdateGameplay && CanBackgroundBuff!=null && CanBackgroundBuff() && SafePlayer(p);}
        private bool SafePlayer(Player p)
        {
            return Available && p!=null && ReferenceEquals(p,Player) && !Main.gamePaused &&
                !p.dead && !p.CCed && !p.cursed && !p.noItems && !p.isOperatingAnotherEntity &&
                !p.HasLockedInventory() && !Main.LocalPlayerHasPendingInventoryActions() && !Items.World.Busy &&
                p.chest==-1 && p.talkNPC<0 && p.sign<0 && Main.npcShop==0 && Main.mouseItem!=null && Main.mouseItem.IsAir &&
                !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && !PlayerInput.WritingText && !Main.blockInput &&
                !Main.ServerSideCharacter && (Main.ActivePlayerFileData==null || !Main.ActivePlayerFileData.ServerSideCharacter) && !WorldGen.isGeneratingOrLoadingWorld;
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
            if(!ReferenceEquals(identityPlayer,Main.LocalPlayer) || Main.ActiveWorldFileData!=null && !ReferenceEquals(identityWorld,Main.ActiveWorldFileData) || socket!=null && !ReferenceEquals(identitySocket,socket) || identityMode!=Main.netMode)
            {Array.Clear(UnknownSlots,0,5);PotionsUse.Reset();BuffUse.Reset();Furniture.Reset();Nurse.Reset();Tax.Reset();Error=null;feedback=false;}
            identityPlayer=Main.LocalPlayer;if(Main.ActiveWorldFileData!=null)identityWorld=Main.ActiveWorldFileData;if(socket!=null || Main.netMode==0)identitySocket=socket;identityMode=Main.netMode;
            Items.Ownership.HoldRecovery(Runtime.Generation,UnknownSlots);
        }
        public void OnSessionEnded(){if(Main.gameMenu){identityPlayer=null;identityWorld=null;identitySocket=null;}Learning.Reset();Catalog.Close();}
        public void Update(ulong tick)
        {
            Tick=tick;if(!Enabled)return;
            Player p=Player;if(!Admit(p)){if(Value(4)!=0 && AdmitBuff(p))BuffUse.Update(p,tick);return;}
            Items.World.RefreshManualRelease();
            if(Value(0)!=0)PotionsUse.Heal(p);
            if(Value(1)!=0)PotionsUse.Mana(p);
            if(Value(2)!=0)Nurse.Update(p,tick);
            if(!Admit(p))return;
            if(Value(3)!=0)Furniture.Update(p,tick);
            if(Value(4)!=0)BuffUse.Update(p,tick);
            if(Value(5)!=0)Tax.Update(p,tick);
        }
        internal void ObserveManual(){ManualEpoch++;}
        internal void Report(string message){if(Error==message)return;Error=message;feedback=true;PresentationRevision++;}
        internal string Hint(int feature){return Settings(feature).Message??Error;}
        internal void TakeFeedback(Action<string> display){Potions.TakeFeedback(display);Buffs.TakeFeedback(display);Services.TakeFeedback(display);if(feedback){display(Error);feedback=false;}}
        public void FailClosed(){Available=false;Report("自动恢复已停止；结果未确认的操作不会重试。");}
        internal void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;Potions.Stop(750);Buffs.Stop(750);Services.Stop(750);}
        private sealed class RetainingPotionCodec : IPreferenceCodec<RecoveryOptions>
        {
            private readonly AtomicFileDocument file;
            private readonly RecoveryCodec codec=new RecoveryCodec(0);
            internal RetainingPotionCodec(AtomicFileDocument file){this.file=file;}
            public RecoveryOptions Decode(byte[] contents)
            {
                int version;var value=codec.Decode(contents,out version);
                // Loading never rewrites the source. Archive validated v1 bytes
                // before the first explicit save, with the existing conflict gate.
                if(version==1)file.RetainLoadedSource();return value;
            }
            public byte[] Encode(RecoveryOptions value){return codec.Encode(value);}
        }
    }
}
