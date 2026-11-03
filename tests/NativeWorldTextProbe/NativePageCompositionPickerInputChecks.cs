using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Items;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Runs inside the existing isolated original-game process. In particular,
    // do not CopyInto unconditionally: that hides the native blockInput gate.
    internal static class NativePageCompositionPickerInputChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly Keys[] Movement={Keys.A,Keys.W,Keys.Space,Keys.D1};
        // Observe only these isolated documents' real storage calls. Keep the
        // worker/commit path intact, and detach even when a business check fails.
        private sealed class WriteAudit:IDisposable
        {
            private static WriteAudit current;
            private readonly object[] stores;
            private readonly int[] counts;
            private readonly Harmony patch=new Harmony("JueMingR.Tests.PickerCancellationWrites");
            internal WriteAudit(params object[] stores)
            {
                this.stores=stores;counts=new int[stores.Length];current=this;
                try
                {
                    foreach(var method in stores.Select(s=>s.GetType().GetMethod("Write",Flags)).Distinct())
                        patch.Patch(method,prefix:new HarmonyMethod(typeof(WriteAudit).GetMethod(nameof(Observe),Flags)));
                }
                catch { Dispose();throw; }
            }
            private static void Observe(object __instance)
            {var audit=current;if(audit!=null)for(int i=0;i<audit.stores.Length;i++)if(ReferenceEquals(__instance,audit.stores[i]))Interlocked.Increment(ref audit.counts[i]);}
            internal int Count(int index){return Volatile.Read(ref counts[index]);}
            public void Dispose(){patch.UnpatchAll(patch.Id);current=null;}
        }
        internal static void Run(object context)
        {
            var test=new Consumer(context);var native=typeof(Player).GetMethod("Update",new[]{typeof(int)});
            var patch=new Harmony("JueMingR.Tests.PointerPickerNativeControlGate");
            patch.Patch(native,transpiler:new HarmonyMethod(typeof(NativePageCompositionPickerInputChecks).GetMethod(nameof(ControlGate),Flags)));
            object oldToken=Main.CurrentInputTextTakerOverride;bool oldBlock=Main.blockInput,oldWriting=PlayerInput.WritingText;
            var oldChroma=Main.ChromaPainter;var oldKeys=Main.keyState;var oldMouse=PlayerInput.MouseInfo;
            Main.ChromaPainter=new Terraria.GameContent.ChromaHotkeyPainter();
            var inventory=(Item[])Main.LocalPlayer.inventory.Clone();var host=Get(Get(Get(context,"Shell"),"items"),"host");
            var preferences=(ItemAutomationSettings)Get(Get(host,"Preferences"),"Value");
            try
            {
                Main.CurrentInputTextTakerOverride=null;Main.blockInput=false;PlayerInput.WritingText=false;
                Main.LocalPlayer.inventory[0]=new Item();Main.LocalPlayer.inventory[0].SetDefaults(8);
                Main.LocalPlayer.inventory[1]=new Item();Main.LocalPlayer.inventory[1].SetDefaults(50);
                Main.LocalPlayer.inventory[2]=new Item();Main.LocalPlayer.inventory[2].SetDefaults(5358);
                test.Run();
            }
            finally
            {
                Call(Get(context,"Shell"),"CloseAndSubmitPosition");patch.Unpatch(native,HarmonyPatchType.All,patch.Id);
                Array.Copy(inventory,Main.LocalPlayer.inventory,inventory.Length);
                if(!preferences.Equals(Get(Get(host,"Preferences"),"Value")))Call(host,"Change",preferences);
                Main.CurrentInputTextTakerOverride=oldToken;Main.blockInput=oldBlock;PlayerInput.WritingText=oldWriting;
                Main.ChromaPainter=oldChroma;Main.keyState=oldKeys;PlayerInput.MouseInfo=oldMouse;
            }
        }
        internal static void Visual(object context,ProbeGraphics graphics,string output)
        {
            var test=new Consumer(context);var inventory=(Item[])Main.LocalPlayer.inventory.Clone();
            bool block=Main.blockInput,writing=PlayerInput.WritingText;object owner=Main.CurrentInputTextTakerOverride;
            var chroma=Main.ChromaPainter;Main.ChromaPainter=new Terraria.GameContent.ChromaHotkeyPainter();
            var native=typeof(Player).GetMethod("Update",new[]{typeof(int)});
            var patch=new Harmony("JueMingR.Tests.PointerPickerVisualControlGate");
            patch.Patch(native,transpiler:new HarmonyMethod(typeof(NativePageCompositionPickerInputChecks).GetMethod(nameof(ControlGate),Flags)));
            try
            {
                // Real inventory representatives make Add render candidates,
                // rather than accidentally checking only an empty selector.
                foreach(int slot in new[]{0,1,2})Main.LocalPlayer.inventory[slot]=new Item();
                Main.LocalPlayer.inventory[0].SetDefaults(8);Main.LocalPlayer.inventory[1].SetDefaults(50);Main.LocalPlayer.inventory[2].SetDefaults(3000);
                test.Visual(graphics,output);
            }
            finally
            {
                Call(Get(context,"Shell"),"CloseAndSubmitPosition");Array.Copy(inventory,Main.LocalPlayer.inventory,inventory.Length);
                Main.blockInput=block;PlayerInput.WritingText=writing;Main.CurrentInputTextTakerOverride=owner;Main.ChromaPainter=chroma;
                patch.Unpatch(native,HarmonyPatchType.All,patch.Id);
            }
        }
        // Retain the original compiled condition and CopyInto call only. This
        // avoids running unrelated physics while preserving the actual gate.
        private static IEnumerable<CodeInstruction> ControlGate(IEnumerable<CodeInstruction> source,ILGenerator generator)
        {
            var code=source.ToList();
            var copies=Enumerable.Range(0,code.Count).Where(i=>(code[i].operand as MethodInfo)?.DeclaringType==typeof(TriggersSet) && (code[i].operand as MethodInfo)?.Name=="CopyInto").ToArray();
            Require(copies.Length==1,"one actual Player.Update control-copy outlet");int end=copies[0];
            int block=Enumerable.Range(Math.Max(0,end-40),Math.Min(40,end)).Last(i=>Equals(code[i].operand,typeof(Main).GetField("blockInput")));
            int start=-1;
            for(int i=Math.Max(0,block-30);i+1<block;i++)if(code[i].opcode==OpCodes.Ldarg_1 && Equals(code[i+1].operand,typeof(Main).GetField("myPlayer")))start=i;
            Require(start>=0 && start<block && block<end && code.Skip(start).Take(block-start).Any(c=>Equals(c.operand,typeof(Player).GetField("isControlledByFilm"))),
                "retain actual i/local-player/film/chat/sign/chest/blockInput control gate");
            var body=code.Skip(start).Take(end-start+1).Select(c=>new CodeInstruction(c)).ToList();
            Require(body.All(c=>c.blocks.Count==0),"native input gate has no crossed exception region");
            var labels=new HashSet<Label>(body.SelectMany(c=>c.labels));var stop=generator.DefineLabel();
            foreach(var c in body)if(c.operand is Label && !labels.Contains((Label)c.operand))c.operand=stop;
            var ret=new CodeInstruction(OpCodes.Ret);ret.labels.Add(stop);body.Add(ret);return body;
        }
        private sealed class Consumer
        {
            private readonly object context,shell,state,renderer,page,input;
            private readonly MethodInfo keyboard=typeof(PlayerInput).GetMethod("KeyboardInput",Flags);
            private readonly List<string> failures=new List<string>();
            private Vector2 point=new Vector2(-10,-10);
            internal Consumer(object context)
            {this.context=context;shell=Get(context,"Shell");state=Get(shell,"State");renderer=Get(shell,"renderer");page=Get(shell,"items");input=Get(context,"Input");}
            private static float F(object value,string field){return (float)Get(value,field);}
            private void Check(bool value,string reason)
            {Console.WriteLine("PICKER "+(value?"PASS ":"FAIL ")+reason);if(!value)failures.Add(reason);}
            private void Prepare()
            {
                var screen=PlayerInput.OriginalScreenSize;
                Call(renderer,"Prepare",state,screen.X,screen.Y,1f);
                if((int)Get(state,"Page")==0)Call(page,"PrepareLayout",Matrix.Identity,screen);
                else if((int)Get(state,"Page")==10){Call(Get(shell,"RecoveryUi"),"PrepareLayout",Matrix.Identity);Call(Get(shell,"RecoveryUi"),"PreparePopup",Matrix.Identity,screen,false);}
                var popup=Get(shell,"HotkeyPopup");
                if((bool)Get(popup,"Visible"))
                {
                    var prepare=popup.GetType().GetMethod("Prepare",Flags);
                    var measure=Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType,renderer,renderer.GetType().GetMethod("PopupMeasure",Flags));
                    prepare.Invoke(popup,new object[]{screen.X,screen.Y,Get(renderer,"FontIdentity"),measure,Get(renderer,"SkinGeneration")});
                }
            }
            private void Frame(string label,Keys[] keys,bool left=false,bool inspect=false)
            {
                Call(input,"BeginUpdate");Call(shell,"BeforeInput");
                bool writing=PlayerInput.WritingText,blocked=Main.blockInput;object token=Main.CurrentInputTextTakerOverride;
                // Original KeyboardInput consumes Terraria's cached sample;
                // original DoUpdate_HandleInput then refreshes the frozen sample.
                PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
                PlayerInput.Triggers.Reset();PlayerInput.MouseKeys.Clear();keyboard.Invoke(null,null);PlayerInput.Triggers.Update();
                bool mapped=PlayerInput.Triggers.Current.Left && PlayerInput.Triggers.Current.Jump;
                PlayerInput.WritingText=false;Call(input,"AfterMapping");Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState(keys);Call(input,"AfterKeyboardRefresh");Call(shell,"ProcessInput");
                var player=Main.LocalPlayer;player.Update(Main.myPlayer);
                if(inspect)
                {
                    Console.WriteLine("PICKER FACT "+label+" before-native-writing="+writing+" block="+blocked+" token="+(token!=null)+" original-mapped="+mapped+" consumed-left="+player.controlLeft+" jump="+player.controlJump+" frozen-F5="+((KeyboardState)Get(input,"KeyboardSample")).IsKeyDown(Keys.F5)+" visible="+Get(state,"Visible")+" ready="+Get(state,"Ready")+" shellFailed="+Get(shell,"Failed"));
                    Check(!writing && !blocked && token==null,"pointer-only "+label+" has no text lease at original keyboard mapping");
                    Check(mapped && player.controlLeft && player.controlJump,"original mapping and gated player control consume movement/jump in "+label);
                    Check(Main.keyState.IsKeyDown(Keys.A) && Main.keyState.IsKeyDown(Keys.Space),"final keyboard sample retains unrelated keys in "+label);
                }
                Prepare();
            }
            private object Control(string command,int argument=-1)
            {return ((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()==command && (argument<0 || (int)Get(c,"Argument")==argument));}
            private void Click(object control,Keys[] keys=null)
            {var rect=Get(control,"Rect");point=new Vector2(F(rect,"X")+2,F(rect,"Y")+2);keys=keys??new Keys[0];Frame("pointer",keys);Frame("pointer",keys,true);Frame("pointer",keys);point=new Vector2(-10,-10);}
            private void Open(int pageNumber)
            {Set(state,"Ready",true);Call(state,"Navigate",pageNumber);Call(state,"RestoreVisible");Prepare();Frame("neutral",new Keys[0]);Frame("neutral",new Keys[0]);}
            private void Settle()
            {
                NativeQuickUseMatrix.Until(()=>
                {
                    Call(Get(page,"host"),"PollPreferences");Call(Get(context,"QuickItems"),"Poll");
                    var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");bindings.Poll();
                    var snapshot=Get(Get(page,"host"),"Preferences");
                    return (bool)Get(snapshot,"IsLoaded") && Get(snapshot,"Status").ToString()!="Pending" &&
                        !(bool)Get(Get(Get(context,"QuickItems"),"Settings"),"Busy") && !bindings.Busy;
                });
                Prepare();
            }
            private void RevealItems(int row)
            {Call(state,"ScrollTo",((float[])Get(Get(page,"layout"),"RowY"))[row]);Prepare();}
            private object QuickControl(string command,int type=0)
            {
                var panel=Get(page,"QuickPanel");
                var logical=(IList)Get(panel,"logical");
                var part=logical.Cast<object>().First(p=>Get(p,"Command").ToString()==command && (type==0 || (int)Get(p,"Type")==type));
                Call(state,"ScrollTo",F(Get(Get(part,"Element"),"Rect"),"Y")-20);Prepare();
                return ((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="Quick" &&
                    Get(logical[(int)Get(c,"Argument")],"Command").ToString()==command && (type==0 || (int)Get(logical[(int)Get(c,"Argument")],"Type")==type));
            }
            private void PopupClick(string command)
            {
                var popup=Get(shell,"HotkeyPopup");var layout=Get(popup,"Layout");var commands=(IList)Get(layout,"Commands");
                int index=Enumerable.Range(0,commands.Count).First(i=>commands[i].ToString()==command);
                var rect=Get(((IList)Get(layout,"Buttons"))[index],"Rect");var panel=Get(layout,"Panel");
                point=new Vector2(F(panel,"X")+F(rect,"X")+4,F(panel,"Y")+F(rect,"Y")+4);
                Frame("popup-"+command,new Keys[0]);Frame("popup-"+command,new Keys[0],true);Frame("popup-"+command,new Keys[0]);point=new Vector2(-10,-10);
            }
            private void Record(object control)
            {
                // Start at the projected row, including the real double click
                // callback. A direct OpenHotkey/Capturing assignment cannot
                // establish that the lower editor actually coexists with it.
                Click(control);Click(control);Prepare();
                var popup=Get(shell,"HotkeyPopup");Require((bool)Get(popup,"Visible"),"actual projected key entry opens public popup");
                PopupClick("Record");
                Require((bool)Get(popup,"Capturing") && (bool)Get(input,"HotkeyCapture"),"actual Record press/release acquires public capture");
            }
            private int[] Selected()
            {return ((IEnumerable)Get(Get(page,"selection"),"selected")).Cast<int>().OrderBy(t=>t).ToArray();}
            private bool CancelRecording(string label)
            {
                Frame(label+"-record-escape",new[]{Keys.Escape});
                var popup=Get(shell,"HotkeyPopup");
                Check(!(bool)Get(popup,"Capturing") && !(bool)Get(input,"HotkeyCapture") && (bool)Get(popup,"Visible"),label+" Escape only ends capture and retains result popup");
                bool retained=(bool)Get(page,"Selecting");Check(retained,label+" upper capture cancellation preserves lower editor");
                return retained;
            }
            private void CloseRecordedPopup()
            {
                Frame("record-escape-held",new[]{Keys.Escape});Frame("record-escape-release",new Keys[0]);
                PopupClick("Close");Frame("popup-tail-release",new Keys[0]);
            }
            private void ReleaseWithEscape(object control,string label)
            {
                var rect=Get(control,"Rect");point=new Vector2(F(rect,"X")+2,F(rect,"Y")+2);
                Frame(label+"-hover",new Keys[0]);Frame(label+"-armed",new Keys[0],true);
                Require(GetOptional(page,"armed")!=null,label+" real pointer press arms the lower control");
                Frame(label+"-release-and-escape",new[]{Keys.Escape});point=new Vector2(-10,-10);
            }
            private void NestedCancellation()
            {
                var host=Get(page,"host");var quick=Get(context,"QuickItems");var panel=Get(page,"QuickPanel");
                var settings=(QuickItemSettings)Get(quick,"Settings");var original=settings.Current;
                var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
                var entry=new QuickItemEntry("c34d58fc56234fb9a84105bf5358caae",5358,QuickItemMode.SetStateAndUse,true,true);
                string itemAction=null;bool itemBound=false;HotkeyChord oldItem=null,oldEntry=bindings.Get(entry.ActionId);
                var itemDocument=Get(host,"preferences");
                Settle();
                var originalItems=(ItemAutomationSettings)Get(Get(host,"Preferences"),"Value");
                using(var writes=new WriteAudit(Get(itemDocument,"storage"),Get(Get(settings,"worker"),"storage"),Get(Get(bindings,"worker"),"storage")))
                try
                {
                    string reason;
                    Require(settings.TryChange(new QuickItemDocument(original.KeepFavorited,original.Enabled,new[]{entry}),null,out reason),"existing legacy-mode Quick fixture admitted");Settle();
                    foreach(bool commit in new[]{false,true})
                    {
                        Open(0);
                        var value=(ItemAutomationSettings)Get(Get(host,"Preferences"),"Value");
                        Call(host,"Change",value.WithTypes(ItemListKind.Sell,new int[0]));Settle();RevealItems(1);
                        Click(Control("Add",0));
                        foreach(int type in new[]{8,50})Click(((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="Select" && (int)Get(c,"Type")==type));
                        var selection=Get(page,"selection");
                        Require((int)Get(selection,"Count")==2 && Selected().SequenceEqual(new[]{8,50}) && Get(selection,"List").ToString()=="Sell" && (int)Get(selection,"Target")==0,"actual sale Add clicks create two selected types in the expected draft");
                        RevealItems(1);var key=Control("Hotkey",1);itemAction=(string)Get(Get(key,"Element"),"HotkeyTarget");
                        if(!itemBound){oldItem=bindings.Get(itemAction);NativeQuickGestureChecks.Bind(bindings,itemAction,"LeftControl+LeftShift+NumPad7");itemBound=true;Settle();}
                        var bound=bindings.Get(itemAction);long revision=(long)Get(Get(host,"Preferences"),"Revision"),command=(long)Get(bindings,"nextCommand");
                        int itemWrites=writes.Count(0),keyWrites=writes.Count(2);
                        Record(Control("Hotkey",1));Require((int)Get(selection,"Count")==2,"draft remains populated when Record starts");
                        bool retained=CancelRecording("sale");
                        Check(Selected().SequenceEqual(new[]{8,50}) && (int)Get(selection,"Count")==2 && GetOptional(selection,"List")?.ToString()=="Sell" && (int)Get(selection,"Target")==0,"sale capture Escape preserves exact list, target and selected types");
                        Settle();
                        Check((long)Get(Get(host,"Preferences"),"Revision")==revision && writes.Count(0)==itemWrites && writes.Count(2)==keyWrites && (long)Get(bindings,"nextCommand")==command && bound.Equals(bindings.Get(itemAction)),"sale capture cancellation submits no settings or binding write and preserves effective binding");
                        CloseRecordedPopup();
                        Check(!retained || (int)Get(selection,"Count")==2,"held recording Escape does not cancel the retained draft a second time");
                        if(retained)
                        {
                            if(commit)
                            {
                                RevealItems(1);Click(Control("Confirm",0));Settle();
                                var saved=(ItemAutomationSettings)Get(Get(host,"Preferences"),"Value");
                                Check(!((bool)Get(page,"Selecting")) && saved.SellTypes.SequenceEqual(new[]{8,50}) && (long)Get(Get(host,"Preferences"),"Revision")==revision+1 && writes.Count(0)==itemWrites+1,"the same retained sale draft confirms once through its real storage writer");
                            }
                            else
                            {
                                RevealItems(1);ReleaseWithEscape(Control("Confirm",0),"sale-confirm");Settle();
                                Check(!(bool)Get(page,"Selecting") && !PlayerInput.Triggers.Current.Inventory && writes.Count(0)==itemWrites && (long)Get(Get(host,"Preferences"),"Revision")==revision && GetOptional(page,"armed")==null,"fresh selector Escape retires armed Confirm before its release without saving or opening inventory");
                                Frame("fresh-lower-escape-release",new Keys[0]);
                            }
                        }
                        Call(shell,"CloseAndSubmitPosition");Frame("sale-neutral",new Keys[0]);
                    }
                    // Replacement owns a target, not a selected set. Protect
                    // its identity through the same real upper capture path.
                    Open(0);var prior=(ItemAutomationSettings)Get(Get(host,"Preferences"),"Value");
                    Call(host,"Change",prior.WithTypes(ItemListKind.Discard,new[]{8}));Settle();RevealItems(2);Click(Control("Replace",1));RevealItems(2);
                    Record(Control("Hotkey",2));bool replace=CancelRecording("discard-replacement");
                    Check(GetOptional(Get(page,"selection"),"List")?.ToString()=="Discard" && (int)Get(Get(page,"selection"),"Target")==8,"recording cancellation preserves the discard replacement target");
                    CloseRecordedPopup();
                    if(replace)
                    {
                        RevealItems(2);Click(((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="Select" && (int)Get(c,"Type")==50));Settle();
                        Check(((ItemAutomationSettings)Get(Get(host,"Preferences"),"Value")).DiscardTypes.SequenceEqual(new[]{50}),"retained replacement updates only its original target");
                    }
                    Call(shell,"CloseAndSubmitPosition");Frame("replace-neutral",new Keys[0]);
                    Open(0);Click(QuickControl("Edit"));Require((bool)Get(panel,"Editing") && (string)Get(panel,"draftId")==entry.Id,"actual existing Quick icon enters stable-ID edit");
                    Call(state,"ScrollTo",0f);Prepare();
                    NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"LeftControl+LeftShift+NumPad6");Settle();
                    var entryBinding=bindings.Get(entry.ActionId);long accepted=settings.AcceptedCommandId,quickRevision=settings.Revision,keyCommand=(long)Get(bindings,"nextCommand");
                    int quickWrites=writes.Count(1),bindingWrites=writes.Count(2);
                    // Reacquire geometry after the binding publication rebuilt
                    // the projected panel; no stale control object is clicked.
                    var quickKey=((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="QuickHotkey" && (string)Get(Get(c,"Element"),"HotkeyTarget")=="items.quick-items.toggle");
                    Record(quickKey);bool quickRetained=CancelRecording("existing-quick");Settle();
                    var savedEntry=settings.Current.Find(entry.Id);
                    Check((bool)Get(panel,"Editing") && (string)Get(panel,"draftId")==entry.Id && (int)Get(panel,"target")==5358 && Get(panel,"mode").ToString()=="Use","capture Escape retains the existing Quick edit identity, target and visible mode");
                    Check(savedEntry!=null && savedEntry.ActionId==entry.ActionId && savedEntry.Mode==QuickItemMode.SetStateAndUse && entryBinding.Equals(bindings.Get(entry.ActionId)),"capture cancellation preserves legacy mode-2 durable identity and effective binding");
                    Check(settings.AcceptedCommandId==accepted && settings.Revision==quickRevision && (long)Get(bindings,"nextCommand")==keyCommand && writes.Count(1)==quickWrites && writes.Count(2)==bindingWrites,"Quick capture cancellation accepts no command, publishes no revision and performs no document write");
                    CloseRecordedPopup();
                    if(quickRetained)
                    {
                        Frame("quick-independent-escape",new[]{Keys.Escape});Settle();
                        Check(!(bool)Get(panel,"Editing") && settings.AcceptedCommandId==accepted && writes.Count(1)==quickWrites && settings.Current.Find(entry.Id).Mode==QuickItemMode.SetStateAndUse,"independent Quick Escape exits without persisting mode normalization");
                        Frame("quick-independent-release",new Keys[0]);
                        Click(QuickControl("Edit"));ReleaseWithEscape(QuickControl("Mode"),"quick-mode");Settle();
                        Check(!(bool)Get(panel,"Editing") && GetOptional(page,"armed")==null && settings.AcceptedCommandId==accepted && settings.Revision==quickRevision && writes.Count(1)==quickWrites,"Quick cancellation retires the Items-owned armed mode control before its same-frame release");
                        Frame("quick-mode-release",new Keys[0]);
                        Click(QuickControl("Edit"));Click(QuickControl("Mode",0));Settle();
                        Check(settings.AcceptedCommandId==accepted+1 && writes.Count(1)==quickWrites+1 && settings.Current.Find(entry.Id).Mode==QuickItemMode.Use && entryBinding.Equals(bindings.Get(entry.ActionId)),"one real Quick mode click saves once and retains stable binding identity");
                    }
                }
                finally
                {
                    Call(shell,"CloseAndSubmitPosition");
                    long command;string reason;
                    if(itemAction!=null){Require(bindings.TrySet(itemAction,oldItem,null,out command,out reason),"restore nested sale action binding: "+reason);Settle();}
                    Require(bindings.TrySet(entry.ActionId,oldEntry,null,out command,out reason),"restore nested Quick entry binding: "+reason);Settle();
                    Require(settings.TryChange(original,null,out reason),"restore nested Quick document: "+reason);Settle();
                    // This prefix commits real list edits. Return its own input
                    // state before the following Add/replace/F5 consumers run;
                    // otherwise all inventory candidates can already be listed.
                    if(!originalItems.Equals(Get(Get(host,"Preferences"),"Value")))Call(host,"Change",originalItems);
                    Settle();
                    Frame("nested-cleanup-neutral",new Keys[0]);
                }
            }
            internal void Visual(ProbeGraphics graphics,string output)
            {
                Directory.CreateDirectory(output);var exceptions=new List<string>();bool observing=false;string phase="";
                EventHandler<FirstChanceExceptionEventArgs> capture=(sender,args)=>
                {
                    if(observing && (args.Exception.StackTrace??"").Contains("JueMingR.TerrariaHost"))exceptions.Add(phase+Environment.NewLine+args.Exception);
                };
                AppDomain.CurrentDomain.FirstChanceException+=capture;
                try
                {
                    foreach(int list in new[]{0,1})
                    {
                        Open(0);Click(Control("Add",list));Require((bool)Get(page,"Selecting"),"real visual Add opens its selector");
                        var candidate=((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="Select");
                        var rect=Get(candidate,"Rect");point=new Vector2(F(rect,"X")+2,F(rect,"Y")+2);Frame("visual-hover",new Keys[0]);
                        var types=((IEnumerable)Get(page,"controls")).Cast<object>().Select(c=>(int)Get(c,"Type")).Where(t=>t>0).Distinct().ToArray();
                        graphics.LoadItemTextures(types);var screen=PlayerInput.OriginalScreenSize;
                        phase="list-"+list+"-add-draw";observing=true;
                        try
                        {
                            Call(page,"Prepare",true,Matrix.Identity,screen);
                            graphics.Image(Path.Combine(output,"picker-add-"+list+".png"),()=>Call(shell,"DrawLayer"),Matrix.Identity,(int)screen.X,(int)screen.Y);
                        }
                        finally{observing=false;File.WriteAllLines(Path.Combine(output,"picker-first-chance.txt"),exceptions);}
                        Console.WriteLine("PICKER DRAW "+phase+" candidateTypes="+string.Join(",",types)+" ready="+Get(state,"Ready")+" failed="+Get(shell,"Failed")+" hintVisible="+Get(Get(renderer,"HintLayout"),"Visible")+" captured="+exceptions.Count);
                        Require(!(bool)Get(shell,"Failed") && (bool)Get(state,"Ready") && (bool)Get(Get(renderer,"HintLayout"),"Visible"),"real Add Prepare/Draw/DrawHints retains healthy ready shell and candidate hint");
                        Frame("visual-F5-close",new[]{Keys.F5});Require(!(bool)Get(state,"Visible"),"F5 closes after actual Add drawing");
                        Frame("visual-F5-release",Movement);Frame("visual-F5-reopen",Movement.Concat(new[]{Keys.F5}).ToArray());
                        Require((bool)Get(state,"Visible") && (bool)Get(state,"Ready") && !(bool)Get(shell,"Failed"),"fresh F5 reopens after actual Add drawing while movement remains held");
                        Call(shell,"CloseAndSubmitPosition");Frame("visual-neutral",new Keys[0]);
                    }
                }
                finally{observing=false;AppDomain.CurrentDomain.FirstChanceException-=capture;}
            }
            private void OpenCancellationOwner(int owner)
            {
                if(owner<2)
                {
                    Open(0);
                    if(owner==0)Click(Control("Add",0));
                    else
                    {
                        var quick=Get(page,"QuickPanel");
                        Click(((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="Quick" && Get(((IList)Get(quick,"logical"))[(int)Get(c,"Argument")],"Command").ToString()=="Add"));
                    }
                    Require((bool)Get(page,"Selecting"),"actual Items/Quick chooser is active before cancellation dispatch check");
                }
                else
                {
                    Open(10);var recovery=Get(shell,"RecoveryUi");
                    var configure=((IEnumerable)Get(recovery,"visible")).Cast<object>().First(p=>(int)Get(p,"Command")==-2 && (int)Get(p,"Value")==0);
                    var rect=Get(Get(configure,"Element"),"Rect");point=new Vector2(F(rect,"X")+2,F(rect,"Y")+2);
                    Frame("potion-cancel-open",new Keys[0]);Frame("potion-cancel-open",new Keys[0],true);Frame("potion-cancel-open",new Keys[0]);
                    Require((bool)Get(Get(recovery,"PotionPopup"),"Visible"),"actual medication overlay is active before cancellation dispatch check");
                }
                point=new Vector2(-10,-10);
            }
            private void CancellationDispatch()
            {
                // Use the existing registered query action and actual Shell
                // admission/dispatch, not the new cancellation helper: the same
                // test must reach a behavioral failure on the previous product.
                var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
                var original=bindings.Get("item-browser.query");var browser=Get(context,"Browser");var targets=Get(browser,"Targets");
                var hotkeys=(HotkeyInput)Get(input,"Hotkeys");
                var chat=typeof(Main).GetMethod("DoUpdate_Enter_ToggleChat",Flags);
                Require(chat!=null && (bool)Get(targets,"Ready"),"actual native chat outlet and registered query consumer are available");
                bool oldChat=Main.drawingPlayerChat,oldChatRelease=Main.chatRelease;
                try
                {
                    NativeQuickGestureChecks.Bind(bindings,"item-browser.query","LeftControl+LeftShift+NumPad8");
                    var chord=new[]{Keys.LeftControl,Keys.LeftShift,Keys.NumPad8};
                    foreach(int owner in new[]{0,1,2})
                    {
                        string label=new[]{"items","quick","potion"}[owner];OpenCancellationOwner(owner);
                        Frame(label+"-cancel-held-cache",Movement);Frame(label+"-cancel-held",Movement);
                        Main.drawingPlayerChat=false;Main.chatRelease=true;
                        Frame(label+"-cancel-and-fresh-query",Movement.Concat(chord).Concat(new[]{Keys.Escape,Keys.Enter}).ToArray());
                        bool closed=owner<2?!(bool)Get(page,"Selecting"):!(bool)Get(Get(Get(shell,"RecoveryUi"),"PotionPopup"),"Visible");
                        bool pending=(bool)Get(targets,"Busy");
                        Console.WriteLine("PICKER CANCEL "+label+" closed="+closed+" queryPending="+pending+" nativeInventory="+PlayerInput.Triggers.Current.Inventory+" nativeEnter="+Main.keyState.IsKeyDown(Keys.Enter)+" heldA="+hotkeys.IsDown((int)Keys.A)+" suppressedA="+hotkeys.IsSuppressed((int)Keys.A));
                        Check(closed && !pending,"accepted "+label+" Escape blocks the same frame's actual registered query dispatch");
                        Check(!PlayerInput.Triggers.Current.Inventory && !Main.keyState.IsKeyDown(Keys.Enter),label+" cancellation consumes already-mapped inventory and native Enter");
                        chat.Invoke(null,null);Check(!Main.drawingPlayerChat,label+" cancellation cannot open chat through the original Enter consumer");
                        Check(hotkeys.IsDown((int)Keys.A) && !hotkeys.IsSuppressed((int)Keys.A) && hotkeys.IsSuppressed((int)Keys.Escape),label+" cancellation owns only Escape's physical tail");
                        // Retire a leaked old-product request so remaining cases
                        // still exercise their own real dispatch admission.
                        if(pending)Call(targets,"Cancel",new object[]{null});
                        Frame(label+"-cancel-release-cache",Movement);Frame(label+"-cancel-release-held",Movement,inspect:true);
                        Check(!hotkeys.IsSuppressed((int)Keys.Escape),label+" Escape release retires while movement stays held");
                        Require((bool)Call(browser,"CanQuery"),"fresh query admission is available after "+label+" cancellation");
                        Frame(label+"-fresh-query-after-cancel",Movement.Concat(chord).ToArray());
                        Check((bool)Get(targets,"Busy"),"fresh registered query works after "+label+" cancellation without releasing movement");
                        Call(targets,"Cancel",new object[]{null});Call(shell,"CloseAndSubmitPosition");Frame(label+"-cancel-neutral",new Keys[0]);
                    }
                }
                finally
                {
                    Call(targets,"Cancel",new object[]{null});Call(shell,"CloseAndSubmitPosition");
                    long command;string reason;
                    Require(bindings.TrySet("item-browser.query",original,null,out command,out reason),"restore original query binding: "+reason);
                    NativeQuickUseMatrix.Until(()=>{bindings.Poll();return !bindings.Busy;});
                    Require(bindings.CompletionSucceeded,"original query binding restoration persisted");
                    Main.drawingPlayerChat=oldChat;Main.chatRelease=oldChatRelease;
                }
            }
            internal void Run()
            {
                NestedCancellation();
                CancellationDispatch();
                foreach(int list in new[]{0,1})
                {
                    Open(0);Click(Control("Add",list));Check((bool)Get(page,"Selecting"),"real Add release opens list "+list);
                    Frame("active-list-cache-"+list,Movement);Frame("active-list-"+list,Movement,inspect:true);
                    var host=Get(page,"host");long revision=(long)Get(Get(host,"Preferences"),"Revision");
                    Click(Control("Select",list),Movement);
                    Check((bool)Get(page,"Selecting") && (long)Get(Get(host,"Preferences"),"Revision")==revision,"selection stays draft while movement remains held");
                    Frame("selected-draft",Movement,inspect:true);Click(Control("Confirm",list),Movement);
                    Frame("confirmed-cache",Movement);Frame("confirmed-held-movement",Movement,inspect:true);
                    Check(!(bool)Get(page,"Selecting"),"confirmation closes only the chooser");
                    Click(Control("Replace",list),Movement);Frame("replacement-cache",Movement);Frame("replacement-held-movement",Movement,inspect:true);
                    Click(Control("Select",list),Movement);Frame("replaced-cache",Movement);Frame("replaced-held-movement",Movement,inspect:true);
                    Check(!(bool)Get(page,"Selecting"),"replacement commits and closes only the chooser");
                    Click(Control("Add",list),Movement);Click(Control("Cancel",list),Movement);Frame("mouse-cancel-cache",Movement);Frame("mouse-cancel-held-movement",Movement,inspect:true);
                    Click(Control("Add",list),Movement);
                    Frame("f5-close",Movement.Concat(new[]{Keys.F5}).ToArray());Check(!(bool)Get(state,"Visible"),"F5 closes an active list");
                    Frame("closed-held-cache",Movement);Frame("closed-held-movement",Movement,inspect:true);
                    Frame("f5-reopen",Movement.Concat(new[]{Keys.F5}).ToArray());Check((bool)Get(state,"Visible"),"fresh F5 reopens after list close while movement stays held");
                    Frame("released-f5",Movement);Click(Control("Add",list));
                    Frame("escape-cancel",Movement.Concat(new[]{Keys.Escape}).ToArray());
                    Check(!(bool)Get(page,"Selecting") && !PlayerInput.Triggers.Current.Inventory,"Escape cancels only selector without reaching original inventory action");
                    Frame("escape-release-native-cache",Movement);Frame("escape-released-movement-still-held",Movement,inspect:true);
                    Call(shell,"CloseAndSubmitPosition");
                }
                Open(0);var quick=Get(page,"QuickPanel");
                Click(((IEnumerable)Get(page,"controls")).Cast<object>().First(c=>Get(c,"Command").ToString()=="Quick" && Get(((IList)Get(quick,"logical"))[(int)Get(c,"Argument")],"Command").ToString()=="Add"));
                Check((bool)Get(quick,"Editing"),"real Quick Add opens its icon chooser");Frame("quick-icon-cache",Movement);Frame("quick-icon-picker",Movement,inspect:true);Call(shell,"CloseAndSubmitPosition");Frame("quick-close-cache",Movement);Frame("quick-closed-held-movement",Movement,inspect:true);
                Open(10);var recovery=Get(shell,"RecoveryUi");var configure=((IEnumerable)Get(recovery,"visible")).Cast<object>().First(p=>(int)Get(p,"Command")==-2 && (int)Get(p,"Value")==0);
                var rect=Get(Get(configure,"Element"),"Rect");point=new Vector2(F(rect,"X")+2,F(rect,"Y")+2);Frame("potion-open",new Keys[0]);Frame("potion-open",new Keys[0],true);Frame("potion-open",new Keys[0]);point=new Vector2(-10,-10);
                Check((bool)Get(Get(recovery,"PotionPopup"),"Visible"),"real medication pointer release opens icon-only overlay");Frame("potion-icon-cache",Movement);Frame("potion-icon-picker",Movement,inspect:true);
                Call(shell,"CloseAndSubmitPosition");Frame("potion-close-cache",Movement);Frame("potion-closed-held-movement",Movement,inspect:true);
                Open(0);Click(Control("Add",0));Call(state,"Navigate",9);Frame("page-leave-cache",Movement);Frame("page-leave-held-movement",Movement,inspect:true);
                Open(0);Click(Control("Add",0));
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));Frame("focus-loss",Movement);
                Check(!(bool)Get(page,"Selecting") && Main.CurrentInputTextTakerOverride==null && !Main.blockInput,"focus loss retires pointer chooser without a leaked text owner");
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));Frame("focus-rearm-neutral",new Keys[0]);Frame("focus-neutral",new Keys[0]);Frame("focus-fresh-F5",new[]{Keys.F5});
                Check((bool)Get(state,"Visible") && !(bool)Get(shell,"Failed") && (bool)Get(state,"Ready"),"fresh F5 works after real focus-neutral recovery");
                var foreign=new object();Main.CurrentInputTextTakerOverride=foreign;Main.blockInput=true;PlayerInput.WritingText=true;
                Call(shell,"BeforeInput");Call(shell,"CloseAndSubmitPosition");
                Check(ReferenceEquals(Main.CurrentInputTextTakerOverride,foreign) && Main.blockInput && PlayerInput.WritingText,"pointer cleanup preserves another real text owner's flags");
                Main.CurrentInputTextTakerOverride=null;Main.blockInput=false;PlayerInput.WritingText=false;
                Require(failures.Count==0,"native pointer-picker input failures: "+string.Join("; ",failures));
            }
        }
    }
}
