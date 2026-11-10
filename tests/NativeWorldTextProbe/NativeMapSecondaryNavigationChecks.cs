using System;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using JueMingR.Features.MapMarkers;
using JueMingR.Platform.Settings;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeMapSecondaryNavigationChecks
    {
        internal static void Run(object context,object host)
        {
            // The visual map scope previously drew only. Use the same native
            // trigger-table initialization as the CPU map input fixture before
            // Gesture.Reset/Update can consume complete mapped samples.
            Terraria.GameInput.PlayerInput.Triggers.Initialize();
            object originalMarkers=Get(host,"Markers"),originalWorkspace=Get(host,"Workspace");
            var shell=Get(context,"Shell");var input=Get(context,"Input");var popup=Get(shell,"MapPopup");var state=Get(shell,"State");
            object text=Get(popup,"TextInput"),oldIme=Get(text,"ime");
            Set(text,"ime",new Ime(oldIme.GetType().GetInterfaces().First(t=>t.Name=="INotesIme")).GetTransparentProxy());
            try
            {
                foreach(int outcome in new[]{0,1,2,3})
                {
                    var storage=new Storage();var library=new MarkerLibrary(_=>storage);var workspace=new MarkerWorkspace(library);
                    var gesture=new NativeHiddenEntryChecks.Gesture(context);
                    try
                    {
                        Call(shell,"CloseAndSubmitPosition");Set(host,"<Markers>k__BackingField",library);Set(host,"<Workspace>k__BackingField",workspace);
                        library.BeginSession(100+outcome,Terraria.Main.maxTilesX,Terraria.Main.maxTilesY);library.UsePair((string)Get(host,"Pair"));
                        NativeQuickItemChecks.Until(()=>{library.Poll();return library.Loaded;});
                        const string id="00000000000000000000000000000071";
                        long create=library.Create(new MarkerRecord(id,50,60,8,"原名"));NativeQuickItemChecks.Until(()=>{library.Poll();return library.LastOperation==create;});
                        gesture.Frame(0,0);gesture.Frame(0,0);gesture.Open(2);Click(gesture,state,input,host,popup,"MarkerManage");Call(shell,"AfterUpdate");Trace(gesture,state,input,host,popup,"after-prepare");
                        Require((bool)Get(popup,"Visible") && (int)Get(popup,"mode")==1,"actual Shell manage entry opens original popup");
                        Require(workspace.BeginEdit(id),"real workspace admits marker edit");workspace.Editor.SelectAll();workspace.Editor.Insert("可靠草稿");Call(shell,"AfterUpdate");
                        storage.Fail=outcome==1;storage.Entered.Reset();storage.Release.Reset();
                        var entry=gesture.Find("ExplorationDetails");gesture.Point(entry);
                        Require(!(bool)Call(popup,"ContainsPointer",gesture.X,gesture.Y),"switch entry is visible and uncovered in the real prepared geometry");
                        Click(gesture,state,input,host,popup,"ExplorationDetails");
                        Require(storage.Entered.Wait(3000) && library.Busy && workspace.Editor!=null && (int)Get(popup,"mode")==1,"complete real entry accepts save while retaining old popup and draft");
                        if(outcome==2){Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));Call(input,"BeginUpdate");Call(shell,"CancelForFocusLoss");}
                        if(outcome==3){Call(shell,"CloseAndSubmitPosition");gesture.Open(9);}
                        storage.Release.Set();
                        NativeQuickItemChecks.Until(()=>{Call(input,"BeginUpdate");Call(context,"UpdateRuntime");return !library.Busy;});
                        Require(!(bool)Get(input,"SampleFocused"),"completion runs after a genuine empty outer BeginUpdate without native input mapping");
                        if(outcome==0)
                        {
                            Require((bool)Get(input,"IsFocused") && (bool)Get(popup,"Visible") && (int)Get(popup,"mode")==2 && workspace.Editor==null && library.Saved.Records[0].Name=="可靠草稿","empty outer runtime receipt saves then executes accepted switch exactly once");
                            Call(shell,"AfterUpdate");int generation=(int)Get(popup,"Generation");for(int i=0;i<3;i++){Call(input,"BeginUpdate");Call(context,"UpdateRuntime");}
                            Require((int)Get(popup,"mode")==2 && (int)Get(popup,"Generation")==generation,"later empty updates do not repeat the accepted navigation");
                        }
                        else if(outcome==1)Require((bool)Get(popup,"Visible") && (int)Get(popup,"mode")==1 && workspace.Editor?.Text=="可靠草稿","failed save leaves original window and whole draft");
                        else Require(!(bool)Get(popup,"Visible") && (outcome!=3 || (int)Get(state,"Page")==9),"late receipt cannot reopen after focus loss or explicit departure");
                    }
                    finally
                    {
                        storage.Release.Set();Call(popup,"Suspend");gesture.Restore();Set(host,"<Markers>k__BackingField",originalMarkers);Set(host,"<Workspace>k__BackingField",originalWorkspace);Require(library.Stop(5000),"isolated marker worker exits");
                    }
                }
            }
            finally{Set(text,"ime",oldIme);Call(shell,"CloseAndSubmitPosition");}
            Console.WriteLine("PASS real Shell marker entry -> blocked save -> empty outer runtime completion: one switch, failure retains draft, focus/departure late receipts stay retired.");
        }
        private static void Click(NativeHiddenEntryChecks.Gesture gesture,object state,object input,object host,object popup,string command)
        {
            var entry=gesture.Find(command);var rect=Get(entry,"Rect");var view=Get(Get(state,"Layout"),"Viewport");
            Call(state,"ScrollTo",Math.Max(0,(float)Get(rect,"Bottom")-(float)Get(view,"Height")+4));gesture.Point(entry);
            Trace(gesture,state,input,host,popup,command+"-before");
            gesture.Sample(false);gesture.Sample(true);Trace(gesture,state,input,host,popup,command+"-press");
            gesture.Sample(true);gesture.Sample(false);Trace(gesture,state,input,host,popup,command+"-release");
        }
        private static void Trace(NativeHiddenEntryChecks.Gesture gesture,object state,object input,object host,object popup,string stage)
        {
            var layout=Get(state,"Layout");var hit=Call(state,"HitButton",gesture.X-(float)Get(state,"X"),gesture.Y-(float)Get(state,"Y"));var armed=GetOptional(state,"armed");
            Console.WriteLine("MAP ENTRY "+stage+" focused="+Get(input,"IsFocused")+" sample="+Get(input,"SampleFocused")+" usable="+Get(input,"CanUseInput")+" nativeTransition="+Get(input,"NativeUiTransitionRequest")+" hotkeyOwner="+Get(input,"HotkeyPointerOwned")+" page="+Get(state,"Page")+" ready="+Get(state,"Ready")+" visible="+Get(state,"Visible")+" present="+Get(gesture.Shell,"CanPresentNow")+" controls="+Get(host,"ControlsEnabled")+" matches="+Call(layout,"Matches",960f,640f,1f,Get(state,"Page"))+" generation="+Get(layout,"Generation")+" pointer="+gesture.X+","+gesture.Y+" scroll="+Get(state,"Scroll")+" hit="+(hit==null?"none":Get(hit,"Command"))+" armed="+(armed==null?"none":Get(armed,"Command"))+" command="+Get(state,"Command")+" left="+Get(input,"RawMouseLeft")+" tail="+Get(state,"ConsumeLeft")+" blocked="+Get(state,"PointerBlocked")+" popup="+Get(popup,"Visible")+" mode="+Get(popup,"mode"));
        }
        private sealed class Storage:IPreferenceStorage
        {
            internal readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim(true);internal bool Fail;private int revision;
            public PreferenceReadResult Read(){return new PreferenceReadResult(PreferenceReadStatus.Missing,null,"0",null);}
            public PreferenceWriteResult Write(string identity,byte[] contents){Entered.Set();if(!Release.Wait(5000))throw new TimeoutException("controlled map save");return Fail?new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,identity,"controlled failure"):new PreferenceWriteResult(PreferenceWriteStatus.Saved,(++revision).ToString(),null);}
            public void Dispose(){}
        }
        private sealed class Ime:RealProxy
        {
            internal Ime(Type type):base(type){}
            public override IMessage Invoke(IMessage message){var call=(IMethodCallMessage)message;object value=call.MethodName=="get_Composition"?(object)"":call.MethodName=="get_Candidates"?(object)false:null;return new ReturnMessage(value,null,0,call.LogicalCallContext,call);}
        }
    }
}
