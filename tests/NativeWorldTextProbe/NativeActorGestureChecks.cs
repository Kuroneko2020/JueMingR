using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Chat;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeActorGestureChecks
    {
        internal const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int created,client,local,notices;private static string sent;
        internal static int Created {get{return created;}}internal static int ClientCount {get{return client;}}internal static int LocalCount {get{return local;}}internal static int Notices {get{return notices;}}internal static string Text {get{return sent;}}
        internal static void Run(object context)
        {
            using(var f=new Fixture(context))
            foreach(int mode in new[]{0,1})
            {
                Main.netMode=mode;f.Reset();NPC actor=f.Actor(0,1,new Vector2(320,320),40);
                f.Neutral(328,328);f.Proof();f.Sample(true,328,328);
                Require((bool)Get(f.Targets,"Busy"),"real registry/chord admits one original actor request");
                f.Proof();actor.position.X+=16;actor.life=19;f.Sample(false,344,328);f.Update();
                Console.WriteLine("ACTOR-ORACLE mode="+mode+" case=proof-then-pointer-and-NPC-move created="+created+" client="+client+" local="+local+" notices="+notices+" text="+sent);
                Require(created==1 && client==(mode==1?1:0) && local==(mode==0?1:0) && sent.Contains("生命 40/100"),"accepted moving actor survives pointer tracking and keeps trigger-time text through the actual Host/native outlet");
            }
            BeforeProof(context);
            Boundaries(context);
            UiSources(context);
            Console.WriteLine("PASS actor gesture L2: actual registry -> Host request -> controlled native UI observation boundaries -> final HostAnnouncements SP/client native chat outlet; no OS/game/network effects.");
        }
        internal static void BeforeProof(object context)
        {
            using(var f=new Fixture(context))foreach(int mode in new[]{0,1})
            {
                Main.netMode=mode;f.Reset();NPC actor=f.Actor(0,1,new Vector2(320,320),40);
                f.Neutral(288,328);f.Proof(); // Last completed Draw point P.
                f.Sample(true,328,328);long first=(long)Get(f.Input,"Mapping"); // Q freezes this NPC.
                f.Update();Require((bool)Get(f.Targets,"Busy") && created==0,"first real Browser.Update awaits its UI proof");
                actor.position.X+=16;f.Sample(false,344,328);long second=(long)Get(f.Input,"Mapping"); // New native mapping R, no Draw.
                f.Update();Console.WriteLine("ACTOR-PQR-ORACLE mode="+mode+" mappings="+first+"->"+second+" outerBrowserUpdates=2 busy="+Get(f.Targets,"Busy")+" created="+created+" notices="+notices);
                Require(second==first+1 && (bool)Get(f.Targets,"Busy") && created==0,"P->Q request->next real mapping R retains original actor until the first complete UI proof");
                f.Proof();f.Update();Require(created==1 && client==(mode==1?1:0) && local==(mode==0?1:0) && sent.Contains("生命 40/100"),"first complete geometry proof authenticates Q without borrowing R hover or retargeting");
            }
            Console.WriteLine("PASS actor PQR L2: two native input mappings and two real Browser.Update consumers before any new Draw observation; original actor/text once via SP/client chat. NPC movement and UI observer boundaries controlled; no Main/Game.Tick execution.");
        }
        private static void Boundaries(object context)
        {
            using(var f=new Fixture(context))foreach(int mode in new[]{0,1})
            {
                Main.netMode=mode;
                foreach(string change in new[]{"npc-only","pointer-to-other","camera","dead","instance","type","outside","lease"})
                {
                    f.Reset();Main.screenPosition=Vector2.Zero;NPC actor=f.Actor(0,1,new Vector2(320,320));
                    f.Neutral(288,328);f.Proof();f.Sample(true,328,328);f.Update();
                    if(change=="npc-only")actor.position.X+=16;
                    if(change=="pointer-to-other")f.Actor(1,2,new Vector2(380,320),7);
                    if(change=="camera")Main.screenPosition=new Vector2(16,0);
                    if(change=="dead")actor.life=0;
                    if(change=="instance")f.Actor(0,1,new Vector2(320,320),7);
                    if(change=="type")actor.type=2;
                    if(change=="outside")actor.position.X=2000;
                    int x=change=="npc-only"?328:change=="pointer-to-other"?388:344;
                    f.Sample(false,x,328);f.Update();
                    if(change=="lease")for(int i=0;i<5;i++)f.Update();else{f.Proof();f.Update();}
                    bool success=change=="npc-only"||change=="pointer-to-other"||change=="camera";
                    Require(created==(success?1:0) && (!success || sent.Contains("生命 40/100") && !sent.Contains("7/100")),"frozen actor identity/visibility/source text: "+change);
                    Require(success || notices>0,"rejected actor only reports locally: "+change);
                    Console.WriteLine("ACTOR-BOUNDARY mode="+mode+" case="+change+" public="+created+" notices="+notices);
                }
            }
        }
        internal static void ToolbarConsumers(object context)
        {
            object host=Get(context,"Blueprint"),toolbar=Get(host,"Toolbar"),layout=Get(toolbar,"Layout");
            bool enabled=(bool)Get(host,"ToolbarEnabled");var player=Main.LocalPlayer;Item item=player.inventory[4];int selected=player.selectedItem;
            var back=Terraria.GameContent.TextureAssets.InventoryBack;var pixel=Terraria.GameContent.TextureAssets.MagicPixel;
            try
            {
                using(var f=new Fixture(context))using(var ui=new UiHarness())
                {
                    ui.Configure("none");NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(host,"ControlsEnabled");});Call(host,"SetToolbar",true);
                    NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(host,"ToolbarEnabled");});
                    player.inventory[4]=new Item();player.inventory[4].SetDefaults(Terraria.ID.ItemID.Gel);player.inventory[4].stack=3;player.selectedItemState.Select(4);player.selectedItemState.Update();
                    Terraria.GameContent.TextureAssets.InventoryBack=ui.Texture;Terraria.GameContent.TextureAssets.MagicPixel=ui.Texture;
                    Action receipt=()=>
                    {
                        Set(toolbar,"font",Terraria.GameContent.FontAssets.MouseText.Value);Set(toolbar,"skin",ui.Texture.Value);Set(toolbar,"pixel",ui.Texture.Value);Set(toolbar,"drawnMatrix",Matrix.Identity);
                        Call(layout,"SetPreviewRevision",Get(host,"TrimPreviewRevision"));Call(layout,"SetPlacedListVisible",false);
                        Require((bool)Call(layout,"EnsureProjection",960f,640f,1f,Terraria.GameContent.FontAssets.MouseText.Value,Get(toolbar,"measure"),false,0),"actual toolbar layout prepared for controlled visual receipt");
                        Set(toolbar,"visibleClip",Activator.CreateInstance(layout.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"),F,null,new object[]{0f,0f,960f,640f},null));
                        Set(toolbar,"presented",true);Set(toolbar,"visibleKnown",true);Set(toolbar,"visibleReceipt",true);
                    };
                    foreach(string scenario in new[]{"outside","q-inside","r-inside","draw-hidden","asset-change","viewport-change","draw-error"})
                    {
                        f.Reset();Terraria.GameContent.TextureAssets.InventoryBack=ui.Texture;typeof(PlayerInput).GetField("_originalScreenWidth",F).SetValue(null,960);Call(toolbar,"Cancel");f.Neutral(30,328);receipt();var panel=Get(layout,"Panel");int bx=(int)((float)Get(panel,"X")+2),by=(int)((float)Get(panel,"Y")+2);
                        if(scenario=="draw-error")
                        {
                            Require((bool)Get(host,"ShowToolbar"),"actual visible Draw exception case has a genuine eligible toolbar");
                            bool failed=false;try{Call(toolbar,"Draw");}catch(Exception){failed=true;}Require(failed && !(bool)Get(toolbar,"visibleKnown"),"controlled missing graphics-device exception in actual Draw cannot publish a visible or hidden receipt");
                        }
                        int qx=scenario=="q-inside"?bx:30,qy=scenario=="q-inside"?by:328;f.Actor(0,1,new Vector2(qx-8,qy-8));f.Proof();f.Sample(true,qx,qy);
                        if(scenario!="q-inside")Require((bool)Get(f.Targets,"Busy") && !(bool)Get(toolbar,"presented"),"actual request's Blueprint.ProcessInput cancels interactive toolbar while retaining last visual receipt");
                        if(scenario=="asset-change")Terraria.GameContent.TextureAssets.InventoryBack=back;
                        if(scenario=="viewport-change")typeof(PlayerInput).GetField("_originalScreenWidth",F).SetValue(null,1000);
                        f.Update();f.Sample(false,scenario=="r-inside"?bx:46,scenario=="r-inside"?by:328);f.Update();
                        if(scenario=="draw-hidden"){Call(toolbar,"Draw");Require((bool)Get(toolbar,"visibleKnown") && !(bool)Get(toolbar,"visibleReceipt"),"actual no-toolbar Draw publishes known hidden state without GPU");}
                        f.Proof();f.Update();
                        bool success=scenario=="outside"||scenario=="draw-hidden";Require(created==(success?1:0),"toolbar pure point original/current ownership "+scenario);
                        Console.WriteLine("ACTOR-TOOLBAR case="+scenario+" public="+created+" notices="+notices+" interactive="+Get(toolbar,"presented")+" visual="+Get(toolbar,"visibleReceipt"));
                    }
                    f.Reset();f.Neutral(30,328);receipt();long revision=(long)Get(layout,"builtPreviewRevision");Set(layout,"builtPreviewRevision",revision+1);
                    Require((bool)Call(toolbar,"OwnsPoint",new Vector2(30,328)),"stale toolbar business revision cannot certify outside as free");Set(layout,"builtPreviewRevision",revision);
                    Set(toolbar,"visibleKnown",false);Require((bool)Call(toolbar,"OwnsPoint",new Vector2(30,328)),"missing toolbar visual receipt is unknown, not an all-clear");
                }
                Console.WriteLine("PASS actor toolbar L2: controlled original layout/visual receipt, actual full owner ProcessInput after each mapping, outside PQR once, Q/R inside zero, real hidden Draw boundary and missing/revision negatives. Initial visible GPU producer NOT RUN; existing command/tail checks remain separate.");
            }
            finally
            {
                Call(host,"SetToolbar",enabled);NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(host,"ToolbarEnabled")==enabled;});Call(toolbar,"Cancel");Set(toolbar,"visibleKnown",false);
                player.inventory[4]=item;player.selectedItemState.Select(selected);player.selectedItemState.Update();Terraria.GameContent.TextureAssets.InventoryBack=back;Terraria.GameContent.TextureAssets.MagicPixel=pixel;
            }
        }
        private static void UiSources(object context)
        {
            using(var f=new Fixture(context))using(var ui=new UiHarness())
            {
                Main.netMode=0;
                foreach(string owner in new[]{"buff","minimap","chat","resource","classic"})
                foreach(bool blocked in new[]{false,true})
                {
                    f.Reset();ui.Configure(owner);int qx=blocked?ui.X:owner=="chat"?500:328,qy=blocked?ui.Y:owner=="chat"?590:328;
                    f.Actor(0,1,new Vector2(qx-8,qy-8));f.Neutral(288,328);f.Proof(false,ui.Draw);
                    f.Sample(true,qx,qy);object frozen=GetOptional(f.Targets,"frozen");bool rejected=(bool)Get(f.Targets,"sourceRejected");
                    Require((bool)Get(f.Targets,"Busy") && (frozen!=null && (bool)Get(frozen,"ActorIdentity") || blocked && rejected),"fresh request reaches observed source/actor gate before UI veto "+owner);
                    Console.WriteLine("ACTOR-UI-ADMISSION owner="+owner+" blocked="+blocked+" busy="+Get(f.Targets,"Busy")+" sourceRejected="+rejected+" frozenActor="+(frozen!=null && (bool)Get(frozen,"ActorIdentity")));
                    f.Update();f.Sample(false,344,328);f.Update();f.Proof(false,ui.Draw);f.Update();
                    if(blocked)for(int i=0;i<5;i++)f.Update();
                    Require(created==(blocked?0:1),"actual "+owner+" producer authenticates original Q independently of R hover");
                    Require(!blocked || notices>0,"blocked Q is local-only "+owner);
                    Console.WriteLine("ACTOR-UI-PRODUCER owner="+owner+" originalBlocked="+blocked+" public="+created+" notices="+notices);
                }
                ui.Configure("buff");f.Reset();f.Actor(0,1,new Vector2(320,320));f.Neutral(328,328);f.Proof(false,ui.Draw);f.Sample(true,328,328);f.Proof(false,ui.Draw);
                f.Sample(false,ui.X,ui.Y);f.Update();Require(created==0 && notices>0,"already-proved actor still yields to current pointer's real native geometry owner");
                foreach(bool inside in new[]{true,false})
                {
                    f.Reset();ui.Configure("chat");int qx=inside?112:113,qy=606;
                    f.Actor(0,1,new Vector2(qx-8,qy-8));f.Neutral(288,328);f.Proof(false,ui.Draw);f.Sample(true,qx,qy);f.Update();f.Sample(false,344,328);f.Update();f.Proof(false,ui.Draw);f.Update();
                    for(int i=0;i<5 && (bool)Get(f.Targets,"Busy");i++)f.Update();
                    Require(created==(inside?0:1),"native ItemSnippet24px at88,582 includes its right/bottom edge but not the adjacent point");
                }
                f.Reset();ui.Configure("chat");f.Actor(0,1,new Vector2(492,582));
                for(int round=0;round<2;round++)
                {
                    if(round==1)System.Threading.Thread.Sleep(550); // Real existing 500ms success cooldown, no reset/rewrite.
                    f.Neutral(288,328);f.Proof(false,ui.Draw);f.Sample(true,500,590);f.Update();f.Sample(false,344,328);f.Update();f.Proof(false,ui.Draw);f.Update();
                    Require(created==round+1,"same-Y space to the right of a short visible item-tag message remains a valid PQR actor target after genuine cooldown elapsed");
                }
                Console.WriteLine("PASS native chat foreground geometry: real ItemTag CheckForHover, right/bottom inclusion, adjacent exclusion and same-Y right whitespace; repeated normal declaration respects real500ms cooldown.");
                foreach(string missing in new[]{"buff","chat"})
                {
                    f.Reset();ui.Configure(missing);f.Actor(0,1,new Vector2(320,320));f.Neutral(288,328);f.Proof(false,ui.Draw);f.Sample(true,328,328);f.Update();f.Sample(false,344,328);f.Update();
                    // Model the documented outer ignoreErrors catch by letting
                    // the complete UI boundary run after the actual inner
                    // producer is skipped. No full Draw/IME loop is executed.
                    using(var skip=new ProducerSkip(missing))f.Proof(false,ui.Draw);
                    f.Update();for(int i=0;i<5 && (bool)Get(f.Targets,"Busy");i++)f.Update();
                    Require(created==0 && notices>0,"missing actual "+missing+" producer cannot be interpreted as a clear Q by a normal outer UI return");
                }
                ui.Configure("none");
                var actorUi=Get(f.Targets,"actorUi");Call(f.Announcements,"SetEnabled",false);HotkeyChord query;string reason;Require(HotkeyChord.TryParse("LeftControl+Mouse1",out query,out reason),"query-only chord");f.Bind("item-browser.query",query);
                Require((bool)((Delegate)Get(f.Targets,"CanPick")).DynamicInvoke(),"query remains genuinely available during actor-only shutdown workload");
                long passes=(long)Get(actorUi,"Passes"),regions=(long)Get(actorUi,"Regions");for(int i=0;i<120;i++)f.Proof();
                Require((long)Get(actorUi,"Passes")==passes && (long)Get(actorUi,"Regions")==regions,"query remains available without actor-only source passes/regions while announcement is disabled");
                f.Bind("item-browser.query",null);f.Bind("announcement.send",null);for(int i=0;i<120;i++)f.Proof();
                Require((long)Get(actorUi,"Passes")==passes && !(bool)Get(actorUi,"Recording"),"fully disabled target demand performs no actor geometry collection");
                Console.WriteLine("PASS actor-only shutdown workload: 120 query-enabled/announcement-disabled +120 no-demand passes; added actor pass/region deltas0, shared query slot responsibility retained.");
            }
        }
        private static void Creating(){created++;}
        private static bool Client(ChatMessage __0){client++;sent=__0.Text;return false;}
        private static bool Local(ChatMessage __0){local++;sent=__0.Text;return false;}
        private static bool Notice(){notices++;return false;}
        private sealed class ProducerSkip:IDisposable
        {
            private readonly Harmony hook=new Harmony("JueMingR.Tests.ActorSourceSkip."+Guid.NewGuid().ToString("N"));
            internal ProducerSkip(string source)
            {hook.Patch(source=="buff"?typeof(Main).GetMethod("DrawBuffIcon",F):typeof(Terraria.GameContent.UI.Chat.RemadeChatMonitor).GetMethod("DrawChat",F),prefix:new HarmonyMethod(typeof(ProducerSkip).GetMethod(nameof(Skip),F)));}
            private static bool Skip(){return false;}
            public void Dispose(){hook.UnpatchAll(hook.Id);}
        }

        // Original per-family producers run with prepared resource/state inputs.
        // Only terminal drawing is skipped; no UI Update/click is replayed.
        internal sealed class UiHarness:IDisposable
        {
            private readonly Harmony sink=new Harmony("JueMingR.Tests.ActorUi."+Guid.NewGuid().ToString("N"));
            private readonly SpriteBatch batch=Main.spriteBatch;private readonly object chat=Main.chatMonitor;
            private readonly Main instance=Main.instance;
            private readonly ReLogic.Content.Asset<Texture2D> item=Terraria.GameContent.TextureAssets.Item[1];
            private readonly bool mapEnabled=Main.mapEnabled,mapReady=Main.mapReady;private readonly int mapStyle=Main.mapStyle;
            private readonly float mapScale=Main.MapScale;private readonly object manager=Main.MinimapFrameManagerInstance;
            private readonly int[] buffs=(int[])Main.LocalPlayer.buffType.Clone();
            private readonly ReLogic.Content.Asset<Texture2D> oldBuff=Terraria.GameContent.TextureAssets.Buff[1],heart=Terraria.GameContent.TextureAssets.Heart,heart2=Terraria.GameContent.TextureAssets.Heart2,mana=Terraria.GameContent.TextureAssets.Mana;
            private readonly ReLogic.Content.Asset<Texture2D> texture;
            internal ReLogic.Content.Asset<Texture2D> Texture {get{return texture;}}
            private Terraria.GameContent.UI.Minimap.MinimapFrame frame;
            private Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet classic;
            private string family;
            internal int X,Y;
            internal UiHarness()
            {
                texture=(ReLogic.Content.Asset<Texture2D>)typeof(NativeTargetMotionInputChecks).GetMethod("CpuTexture",F).Invoke(null,new object[]{40,40});
                Main.spriteBatch=(SpriteBatch)FormatterServices.GetUninitializedObject(typeof(SpriteBatch));GC.SuppressFinalize(Main.spriteBatch);
                if(Main.instance==null){Main.instance=(Main)FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
                foreach(var method in typeof(SpriteBatch).GetMethods().Where(m=>m.Name=="Draw"))sink.Patch(method,prefix:new HarmonyMethod(typeof(UiHarness).GetMethod(nameof(NoDisplay),F)));
                foreach(var method in typeof(SpriteBatch).GetMethods(F).Where(m=>m.Name=="InternalDraw"))sink.Patch(method,prefix:new HarmonyMethod(typeof(UiHarness).GetMethod(nameof(NoDisplay),F)));
                foreach(var method in typeof(SpriteBatch).GetMethods(F).Where(m=>m.Name=="Begin" || m.Name=="End" || m.Name=="InternalBegin"))sink.Patch(method,prefix:new HarmonyMethod(typeof(UiHarness).GetMethod(nameof(NoDisplay),F)));
                foreach(var method in typeof(ReLogic.Graphics.DynamicSpriteFont).GetMethods().Where(m=>m.Name=="DrawString"))sink.Patch(method,prefix:new HarmonyMethod(typeof(UiHarness).GetMethod(nameof(NoDisplay),F)));
                sink.Patch(typeof(Terraria.UI.ItemSlot).GetMethod("Draw",F,null,new[]{typeof(SpriteBatch),typeof(Item).MakeByRefType(),typeof(int),typeof(Vector2),typeof(Color)},null),prefix:new HarmonyMethod(typeof(UiHarness).GetMethod(nameof(NoDisplay),F)));
                Terraria.GameContent.TextureAssets.Buff[1]=texture;Terraria.GameContent.TextureAssets.Heart=Terraria.GameContent.TextureAssets.Heart2=Terraria.GameContent.TextureAssets.Mana=texture;
                Terraria.GameContent.TextureAssets.Item[1]=texture;
            }
            private static bool NoDisplay(){return false;}
            internal void Configure(string owner)
            {
                family=owner;Array.Clear(Main.LocalPlayer.buffType,0,Main.LocalPlayer.buffType.Length);Main.mapEnabled=Main.mapReady=false;Main.mapStyle=1;Main.MapScale=1;
                Main.chatMonitor=new Terraria.GameContent.UI.Chat.RemadeChatMonitor();X=620;Y=300;
                if(owner=="buff")Main.LocalPlayer.buffType[0]=1;
                if(owner=="chat")
                {
                    X=100;Y=590;var message=new Terraria.UI.Chat.ChatMessageContainer();
                    var snippets=Terraria.UI.Chat.ChatManager.ParseMessage("[i:1]",Color.White).ToArray();
                    Set(message,"_prepared",true);Set(message,"_timeLeft",60);Set(message,"_parsedText",new List<Terraria.UI.Chat.TextSnippet[]>{snippets});
                    ((System.Collections.IList)Get(Main.chatMonitor,"_messages")).Add(message);
                }
                if(owner=="minimap")
                {
                    X=720;Y=300;Main.mapEnabled=Main.mapReady=true;
                    frame=new Terraria.GameContent.UI.Minimap.MinimapFrame(texture,Vector2.Zero){MinimapPosition=new Vector2(700,200)};
                    frame.SetResetButton(texture,new Vector2(150,240));frame.SetZoomInButton(texture,new Vector2(200,240));frame.SetZoomOutButton(texture,new Vector2(175,240));
                    var next=new Terraria.GameContent.UI.Minimap.MinimapFrameManager();next.GetType().BaseType.GetField("ActiveSelection",F).SetValue(next,frame);Main.MinimapFrameManagerInstance=next;
                }
                if(owner=="classic"){X=700;Y=40;classic=new Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet("classic","classic");}
            }
            internal void Draw()
            {
                if(family=="buff")Main.DrawBuffIcon(-1,0,600,280);
                if(family=="minimap")frame.DrawForeground(Main.spriteBatch);
                if(family=="resource")
                {
                    var settings=new Terraria.GameContent.UI.ResourceSets.ResourceDrawSettings{TopLeftAnchor=new Vector2(600,280),ElementCount=1,OffsetSpriteAnchor=new Vector2(2,3),GetTextureMethod=Resource};bool hovered=false;settings.Draw(Main.spriteBatch,ref hovered);
                    Require(!hovered,"native modern resource hit uses current R, while receipt separately preserves original Q");
                }
                if(family=="classic"){classic.Draw();classic.TryToHover();}
            }
            private void Resource(int index,int first,int last,out ReLogic.Content.Asset<Texture2D> asset,out Vector2 offset,out float scale,out Rectangle? source)
            {asset=texture;offset=Vector2.Zero;scale=.5f;source=new Rectangle(7,9,40,40);}
            public void Dispose()
            {
                sink.UnpatchAll(sink.Id);Main.spriteBatch=batch;Main.instance=instance;Main.chatMonitor=(Terraria.GameContent.UI.Chat.IChatMonitor)chat;Terraria.GameContent.TextureAssets.Item[1]=item;
                Main.mapEnabled=mapEnabled;Main.mapReady=mapReady;Main.mapStyle=mapStyle;Main.MapScale=mapScale;Main.MinimapFrameManagerInstance=(Terraria.GameContent.UI.Minimap.MinimapFrameManager)manager;
                Array.Copy(buffs,Main.LocalPlayer.buffType,buffs.Length);Terraria.GameContent.TextureAssets.Buff[1]=oldBuff;Terraria.GameContent.TextureAssets.Heart=heart;Terraria.GameContent.TextureAssets.Heart2=heart2;Terraria.GameContent.TextureAssets.Mana=mana;
            }
        }

        // A real composed Host and its exact public input order. Only physical
        // providers and final display/network endpoints are controlled. Proof()
        // names the CPU UI observer boundary; it is not an original full Draw.
        internal sealed class Fixture:IDisposable
        {
            internal readonly object Browser,Targets,Input,Shell,State,Announcements;
            private readonly object context;
            private readonly HotkeyBindings bindings;private readonly HotkeyChord oldSend,oldQuery;
            private readonly object oldWindow,oldForeground,oldKeyboard;
            private readonly bool oldEnabled,oldLayers,oldReady,oldVisible,oldInventory,oldHide;
            private readonly int oldPage,oldMode,oldWidth,oldHeight,oldTilesX,oldTilesY;
            private readonly Tile[,] oldTiles;private readonly NPC[] oldNpcs;private readonly WorldItem[] oldItems;
            private readonly Vector2 oldPosition,oldCamera,oldScreen,oldMouseScale;
            private readonly Matrix oldUi;private readonly Terraria.Graphics.SpriteViewMatrix oldView;
            private readonly int oldChest;private readonly float oldGravity;
            private readonly MouseState oldMouse;private readonly KeyboardState oldKeys;
            private readonly WorldSections oldSections;
            private readonly Terraria.GameContent.UI.Chat.IChatMonitor oldChat;
            private bool chatStateLogged;
            private readonly Harmony isolation=new Harmony("JueMingR.Tests.ActorConsumer."+Guid.NewGuid().ToString("N"));
            internal Fixture(object context,int worldWidth=64)
            {
                this.context=context;
                Browser=Get(context,"Browser");Targets=Get(Browser,"Targets");Input=Get(context,"Input");Shell=Get(context,"Shell");State=Get(Shell,"State");Announcements=Get(Browser,"Announcements");
                bindings=(HotkeyBindings)Get(Get(Shell,"hotkeys"),"Bindings");oldSend=bindings.Get("announcement.send");oldQuery=bindings.Get("item-browser.query");
                oldWindow=Get(Input,"gameWindow");oldForeground=Get(Input,"foregroundWindow");oldKeyboard=Get(Input,"ReadOnlyKeyboardSource");
                oldEnabled=(bool)Get(Announcements,"Enabled");oldLayers=(bool)Get(Shell,"LayersReady");oldReady=(bool)Get(State,"Ready");oldVisible=(bool)Get(State,"Visible");oldPage=(int)Get(State,"Page");
                oldInventory=Main.playerInventory;oldHide=Main.hideUI;oldMode=Main.netMode;oldWidth=Main.screenWidth;oldHeight=Main.screenHeight;oldTilesX=Main.maxTilesX;oldTilesY=Main.maxTilesY;
                oldTiles=Main.tile;oldNpcs=Main.npc;oldItems=Main.item;oldPosition=Main.LocalPlayer.position;oldChest=Main.LocalPlayer.chest;oldGravity=Main.LocalPlayer.gravDir;
                oldSections=Main.sectionManager;
                oldChat=Main.chatMonitor;
                // Ordinary-world cases own an empty chat surface. The aggregate
                // can retain prior scenario messages while its display sink is
                // already gone; never draw those messages with null GPU state.
                // Real nonempty chat/hover producers run separately in UiSources.
                var priorMessages=GetOptional(oldChat,"_messages") as System.Collections.IList;
                Console.WriteLine("ACTOR-CHAT-SCOPE-ENTRY messages="+priorMessages?.Count+" spriteBatchNull="+(Main.spriteBatch==null)+" mainInstanceNull="+(Main.instance==null));
                Main.chatMonitor=new Terraria.GameContent.UI.Chat.RemadeChatMonitor();
                oldCamera=Main.screenPosition;oldUi=Main.UIScaleMatrix;oldView=Main.GameViewMatrix;oldScreen=PlayerInput.OriginalScreenSize;oldMouseScale=PlayerInput.RawMouseScale;oldMouse=PlayerInput.MouseInfo;oldKeys=Main.keyState;
                Set(Input,"gameWindow",(Func<IntPtr>)(()=>new IntPtr(1)));Set(Input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));FocusHelper.IsSelectedApplication=true;
                Main.maxTilesX=worldWidth;Main.maxTilesY=64;Main.tile=new Tile[worldWidth,64];for(int x=0;x<worldWidth;x++)for(int y=0;y<64;y++)Main.tile[x,y]=new Tile();Main.sectionManager=new WorldSections((worldWidth+199)/200,1);Main.sectionManager.SetAllSectionsLoaded();
                Main.npc=new NPC[oldNpcs.Length];Main.item=new WorldItem[oldItems.Length];Main.LocalPlayer.position=new Vector2(800,800);Main.LocalPlayer.chest=-1;Main.LocalPlayer.gravDir=1;
                Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=Vector2.Zero;Main.playerInventory=Main.hideUI=false;
                Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,960,640));Main.GameViewMatrix.Zoom=Vector2.One;
                PlayerInput.Triggers.Initialize();
                typeof(Main).GetField("_uiScaleMatrix",F).SetValue(null,Matrix.Identity);typeof(PlayerInput).GetField("_originalScreenWidth",F).SetValue(null,960);typeof(PlayerInput).GetField("_originalScreenHeight",F).SetValue(null,640);typeof(PlayerInput).GetField("RawMouseScale",F).SetValue(null,Vector2.One);
                Set(Shell,"LayersReady",true);Set(State,"Ready",true);Call(Shell,"CloseAndSubmitPosition");
                Bind("announcement.send",null);Bind("item-browser.query",null);HotkeyChord chord;string reason;Require(HotkeyChord.TryParse("LeftShift+LeftAlt+Mouse1",out chord,out reason),"actor declaration chord parses");Bind("announcement.send",chord);Require((bool)Call(Announcements,"SetEnabled",true),"actual announcement owner enabled");
                isolation.Patch(typeof(ChatCommandProcessor).GetMethod("CreateOutgoingMessage",F),prefix:new HarmonyMethod(typeof(NativeActorGestureChecks).GetMethod(nameof(Creating),F)));
                foreach(var endpoint in new[]{typeof(ChatHelper).GetMethod("SendChatMessageFromClient",F),typeof(ChatCommandProcessor).GetMethod("ProcessIncomingMessage",F)})
                {var info=Harmony.GetPatchInfo(endpoint);Console.WriteLine("ACTOR-OUTLET-SCOPE endpoint="+endpoint.Name+" prior="+(info==null?"none":string.Join(",",info.Prefixes.Select(p=>p.owner+":"+p.priority))));}
                // This nested fixture owns the terminal sink. The aggregate's
                // earlier false-prefix must not skip this scope's observation;
                // native construction/arguments and exact counts stay real.
                isolation.Patch(typeof(ChatHelper).GetMethod("SendChatMessageFromClient",F),prefix:new HarmonyMethod(typeof(NativeActorGestureChecks).GetMethod(nameof(Client),F)){priority=Priority.First});
                isolation.Patch(typeof(ChatCommandProcessor).GetMethod("ProcessIncomingMessage",F),prefix:new HarmonyMethod(typeof(NativeActorGestureChecks).GetMethod(nameof(Local),F)){priority=Priority.First});
                isolation.Patch(typeof(Main).GetMethod("NewText",F,null,new[]{typeof(string),typeof(byte),typeof(byte),typeof(byte)},null),prefix:new HarmonyMethod(typeof(NativeActorGestureChecks).GetMethod(nameof(Notice),F)){priority=Priority.First});
            }
            internal void Bind(string id,HotkeyChord chord){long command;string reason;Require(bindings.TrySet(id,chord,null,out command,out reason),"isolated actor binding "+reason);NativeQuickItemChecks.Until(()=>{bindings.Poll();return !bindings.Busy;});}
            internal void Reset(){Call(Targets,"Cancel",(object)null);Call(Get(Announcements,"cooldown"),"Clear");Set(Announcements,"feedbackAt",-1L);created=client=local=notices=0;sent=null;Array.Clear(Main.npc,0,Main.npc.Length);}
            internal NPC Actor(int slot,int type,Vector2 position,int life=40){var actor=new NPC{active=true,type=type,netID=type,position=position,width=32,height=32,life=life,lifeMax=100};Main.npc[slot]=actor;return actor;}
            internal void Neutral(int x,int y){Sample(false,x,y,false);Sample(false,x,y,false);}
            internal void Sample(bool down,int x,int y,bool modifiers=true)
            {
                var keys=modifiers?new KeyboardState(Keys.LeftShift,Keys.LeftAlt):new KeyboardState();Set(Input,"ReadOnlyKeyboardSource",(Func<KeyboardState>)(()=>keys));Call(Input,"BeginUpdate");
                PlayerInput.MouseInfo=new MouseState(x,y,0,down?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);PlayerInput.MouseX=Main.mouseX=x;PlayerInput.MouseY=Main.mouseY=y;
                Main.LocalPlayer.mouseInterface=Main.mouseText=false;var tokens=new List<string>();if(down)tokens.Add("Mouse1");Call(Input,"AfterNativeMouse",tokens);
                PlayerInput.Triggers.Reset();PlayerInput.Triggers.Current.MouseLeft=tokens.Contains("Mouse1");PlayerInput.Triggers.Update();Main.mouseLeft=PlayerInput.Triggers.Current.MouseLeft;Main.mouseLeftRelease=true;
                Call(Input,"AfterMapping");PlayerInput.CacheMousePositionForZoom();PlayerInput.SetZoom_MouseInWorld();Main.keyState=keys;Call(Input,"AfterKeyboardRefresh");
                Call(Targets,"ProcessInput");Call(Get(Get(context,"Footprints"),"Layer"),"ProcessInput");Call(Shell,"ProcessInput");Call(Get(Get(context,"MapFeatures"),"Layer"),"ProcessInput");
                var blueprint=GetOptional(context,"Blueprint");if(blueprint!=null)Call(blueprint,"ProcessInput");Require(!(bool)Get(Shell,"Failed"),"actor actual Shell stays healthy");
            }
            internal void Proof(bool blocked=false,Action draw=null,Action afterBoundary=null)
            {
                if(!chatStateLogged){chatStateLogged=true;var messages=GetOptional(Main.chatMonitor,"_messages") as System.Collections.IList;Console.WriteLine("ACTOR-CHAT-PRESTATE monitor="+Main.chatMonitor?.GetType().FullName+" messages="+messages?.Count+" spriteBatchNull="+(Main.spriteBatch==null)+" mainInstanceNull="+(Main.instance==null)+" fontAssetNull="+(Terraria.GameContent.FontAssets.MouseText==null));}
                Call(Targets,"BeginPass");draw?.Invoke();Main.chatMonitor.DrawChat(false);Main.LocalPlayer.mouseInterface=blocked;Main.mouseText=false;Call(Targets,"BeforeWorldHover");if(afterBoundary!=null)afterBoundary();else Call(Targets,"AfterResources",true);Call(Targets,"EndPass",true);
            }
            internal void Update(){Call(Browser,"Update",(long)Get(Targets,"tick")+1);}
            public void Dispose()
            {
                Call(Targets,"Cancel",(object)null);Neutral(328,328);Bind("announcement.send",null);Bind("item-browser.query",oldQuery);Bind("announcement.send",oldSend);Call(Announcements,"SetEnabled",oldEnabled);isolation.UnpatchAll(isolation.Id);
                Set(Input,"ReadOnlyKeyboardSource",oldKeyboard);Set(Input,"gameWindow",oldWindow);Set(Input,"foregroundWindow",oldForeground);Set(Shell,"LayersReady",oldLayers);Set(State,"Ready",oldReady);Set(State,"Visible",oldVisible);Set(State,"Page",oldPage);
                Main.tile=oldTiles;Main.npc=oldNpcs;Main.item=oldItems;Main.maxTilesX=oldTilesX;Main.maxTilesY=oldTilesY;Main.LocalPlayer.position=oldPosition;Main.LocalPlayer.chest=oldChest;Main.LocalPlayer.gravDir=oldGravity;Main.netMode=oldMode;Main.screenWidth=oldWidth;Main.screenHeight=oldHeight;Main.screenPosition=oldCamera;Main.playerInventory=oldInventory;Main.hideUI=oldHide;Main.GameViewMatrix=oldView;
                Main.sectionManager=oldSections;
                Main.chatMonitor=oldChat;
                typeof(Main).GetField("_uiScaleMatrix",F).SetValue(null,oldUi);typeof(PlayerInput).GetField("_originalScreenWidth",F).SetValue(null,(int)oldScreen.X);typeof(PlayerInput).GetField("_originalScreenHeight",F).SetValue(null,(int)oldScreen.Y);typeof(PlayerInput).GetField("RawMouseScale",F).SetValue(null,oldMouseScale);PlayerInput.MouseInfo=oldMouse;Main.keyState=oldKeys;
            }
        }
    }
}
