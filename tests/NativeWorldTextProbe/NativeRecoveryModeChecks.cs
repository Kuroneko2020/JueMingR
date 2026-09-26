using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JueMingR.Features.EntityLabels;
using JueMingR.Features.Recovery;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeShortFeedbackChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryModeChecks
    {
        internal static void Run(object context,HotkeyRegistry registry,object display)
        {
            object recovery=Get(context,"Recovery"),labels=Get(context,"Labels"),objects=Get(context,"WorldObjects");
            foreach(int mode in new[]{1,2})
            {
                Call(recovery,"Set",0,mode);Wait(recovery);
                RoundTrip(context,registry,display,"recovery.life",mode==1?"快速":"智能",()=> (int)Call(recovery,"Value",0),mode);
                Call(recovery,"Set",0,0);Wait(recovery);
                Call(recovery,"Set",1,1);Wait(recovery);
                Call(Get(Get(context,"Runtime"),"SharedRuntime"),"InvalidateSession");Call(context,"UpdateRuntime");
                Require(registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer),"F5 OFF/other option/session preserves restore admission");Wait(recovery);Call(context,"UpdateRuntime");
                Require((int)Call(recovery,"Value",0)==mode,"F5 OFF/other option/session preserves mode");
            }
            foreach(var mode in new[]{NpcLabelMode.Name,NpcLabelMode.Type})
            {
                Call(labels,"SetNpcMode",mode);Call(labels,"PollPreferences");
                RoundTrip(context,registry,display,"entity-labels.npc.toggle",mode==NpcLabelMode.Name?"名字":"类型",()=> (int)((EntityLabelSettings)Get(Get(labels,"Preferences"),"Value")).NpcMode,(int)mode);
            }
            foreach(var kind in new[]{WorldObjectKind.Chest,WorldObjectKind.Sign,WorldObjectKind.Tombstone})
            foreach(var mode in kind==WorldObjectKind.Chest?new[]{WorldObjectMode.Always,WorldObjectMode.Opened}:new[]{WorldObjectMode.All,WorldObjectMode.Lines,WorldObjectMode.Characters})
            {
                Call(objects,"SetMode",kind,mode);
                string name=mode==WorldObjectMode.Always?"始终":mode==WorldObjectMode.Opened?"开过":mode==WorldObjectMode.All?"全部":mode==WorldObjectMode.Lines?"前几行":"前几字";
                RoundTrip(context,registry,display,"world-object-text."+kind.ToString().ToLowerInvariant()+".toggle",name,()=> (int)((WorldObjectSettings)Get(Get(objects,"Preferences"),"Value")).Style(kind).Mode,(int)mode);
            }
            SaveFailures(context,registry,display,recovery);
            Migration(recovery.GetType());
            Call(display,"Clear");
        }
        private static void RoundTrip(object context,HotkeyRegistry registry,object display,string id,string mode,Func<int> state,int expected)
        {
            Call(display,"Clear");var action=registry.Find(id);
            Require(action.Invoke(HotkeyContext.SinglePlayer),"close selected mode: "+id);
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return Entries(display).Any(e=>((string)Get(e,"Text")).EndsWith("（"+mode+"） 已关闭"));});
            Require(state()==0,"closed actual state: "+id);
            Require(action.Invoke(HotkeyContext.SinglePlayer),"restore selected mode: "+id);
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return Entries(display).Any(e=>((string)Get(e,"Text")).EndsWith("（"+mode+"） 已开启"));});
            Require(state()==expected,"restored actual state: "+id);
        }
        private static void Wait(object recovery){NativeQuickItemChecks.Until(()=>{Call(recovery,"Poll");return !(bool)Get(Get(recovery,"Potions"),"Busy");});}
        private static void SaveFailures(object context,HotkeyRegistry registry,object display,object recovery)
        {
            var original=(RecoverySettings)Get(recovery,"Potions");var codec=new RecoveryCodec(0);
            var store=new NativeQuickUiChecks.UiStore(codec.Encode(new RecoveryOptions(2)));
            using(var settings=new RecoverySettings(store,0))
            {
                try
                {
                    Set(recovery,"Potions",settings);NativeQuickItemChecks.Until(()=>{settings.Poll();return settings.Loaded;});Call(display,"Clear");
                    // Registration captures its immutable settings owner. Bind a
                    // fresh real registry to this isolated owner, as composition does.
                    registry=new HotkeyRegistry();Call(recovery,"Register",registry,Get(Get(Get(context,"Shell"),"hotkeys"),"Feedback"));
                    store.Gate.Reset();Require(settings.Set(settings.Value.Change(0,1)),"mode change accepted");
                    Require(settings.Value.LastLifeMode==2 && !registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer),"pending candidate neither publishes memory nor admits hotkey");
                    store.Fail=true;store.Gate.Set();Wait(recovery);
                    Require(settings.Value.LastLifeMode==2 && !settings.CompletionSucceeded,"failed explicit selection preserves committed memory");
                    store.Fail=false;Require(registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer),"retry restores committed memory");Wait(recovery);Call(context,"UpdateRuntime");
                    Require(settings.Value.LifeMode==2 && Has(display,"自动回血（智能） 已开启"),"retry reports restored smart after reliable commit");
                    store.Fail=store.Unknown=true;Require(registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer),"unknown close accepted");Wait(recovery);Call(context,"UpdateRuntime");
                    Require(settings.Protected && settings.Value.LastLifeMode==2 && !registry.Find("recovery.life").Invoke(HotkeyContext.SinglePlayer) && Entries(display).Length==0,"unknown save protects memory and suppresses success");
                }
                finally{store.Gate.Set();Set(recovery,"Potions",original);Call(display,"Clear");}
            }
        }
        private static void Migration(Type host)
        {
            string root=Path.Combine(Path.GetTempPath(),"JueMingR-recovery-mode-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            // Exercise the production retaining codec and real atomic storage,
            // using only newly-created synthetic files, never user configuration.
            var type=host.GetNestedType("RetainingPotionCodec",BindingFlags.NonPublic);
            foreach(bool conflict in new[]{false,true})
            {
                string path=Path.Combine(root,conflict?"conflict.json":"potions.json");
                byte[] source=Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.RecoveryPotions\",\"version\":1,\"lifeMode\":2,\"mana\":false,\"noLife\":[],\"noMana\":[]}");File.WriteAllBytes(path,source);
                byte[] foreign={1,2,3};if(conflict)File.WriteAllBytes(path+".schema1-original",foreign);
                var file=new AtomicFileDocument(path,65536,true,".schema1-original");
                var codec=(IPreferenceCodec<RecoveryOptions>)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{file},null);
                using(var settings=new RecoverySettings(file,0,codec))
                {
                    NativeQuickItemChecks.Until(()=>{settings.Poll();return settings.Loaded;});
                    Require(settings.Ready && settings.Value.LastLifeMode==2 && File.ReadAllBytes(path).SequenceEqual(source),"known v1 read preserves bytes and mode");
                    Require(conflict || !File.Exists(path+".schema1-original"),"load creates no migration archive");
                    Require(settings.Set(settings.Value.Change(0,0)),"first explicit save admitted");NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});
                    if(conflict)Require(settings.Protected && File.ReadAllBytes(path).SequenceEqual(source) && File.ReadAllBytes(path+".schema1-original").SequenceEqual(foreign),"archive conflict preserves both sources");
                    else Require(settings.Ready && File.ReadAllBytes(path+".schema1-original").SequenceEqual(source) && File.ReadAllBytes(path+".bak").SequenceEqual(source),"first save retains exact old source before migration");
                }
                if(!conflict)using(var loaded=new RecoverySettings(new AtomicFileDocument(path,65536,true,".schema1-original"),0))
                {NativeQuickItemChecks.Until(()=>{loaded.Poll();return loaded.Loaded;});Require(loaded.Ready && loaded.Value.LifeMode==0 && loaded.Value.LastLifeMode==2,"restart while OFF retains smart mode");}
            }
        }
    }
}
