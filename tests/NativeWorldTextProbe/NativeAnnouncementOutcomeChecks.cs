using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeAnnouncementOutcomeChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int created,client,local,notices;
        private static string sent,notice;
        internal static void QueryConsumers(object context,Func<int> outputCount)
        {
            object browser=Get(context,"Browser"),targets=Get(browser,"Targets"),state=Get(Get(context,"Shell"),"State");
            object workspace=Get(Get(browser,"Knowledge"),"Workspace");
            var fields=workspace.GetType().GetFields(F).Where(f=>!f.IsStatic && !f.IsInitOnly).ToDictionary(f=>f,f=>f.GetValue(workspace));
            var history=(System.Collections.IList)workspace.GetType().GetField("history",F).GetValue(workspace);object[] savedHistory=history.Cast<object>().ToArray();
            bool visible=(bool)Get(state,"Visible"),ready=(bool)Get(state,"Ready");int page=(int)Get(state,"Page");
            var oldView=Main.GameViewMatrix;var oldCamera=Main.screenPosition;var oldSize=Terraria.GameInput.PlayerInput.OriginalScreenSize;
            var silence=new Harmony("JueMingR.Tests.QueryOutcomeLocal");var failures=new List<string>();
            try
            {
                silence.Patch(typeof(Main).GetMethod("NewText",F,null,new[]{typeof(string),typeof(byte),typeof(byte),typeof(byte)},null),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Notice),F)));
                silence.Patch(typeof(ChatHelper).GetMethod("SendChatMessageFromClient",F),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Client),F)));
                silence.Patch(typeof(ChatCommandProcessor).GetMethod("ProcessIncomingMessage",F),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Local),F)));
                int interceptedBefore=client+local;
                Set(state,"Ready",true);int beforePublic=outputCount();
                Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0,0,960,640));Main.GameViewMatrix.Zoom=Vector2.One;Main.screenPosition=Vector2.Zero;
                typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth",F).SetValue(null,960);typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenHeight",F).SetValue(null,640);
                foreach(int mode in new[]{0,1}) foreach(bool warm in new[]{true,false})
                foreach(var row in new[]{new[]{395,3270},new[]{20,27}}.Concat(NativeLegacyPlacementIdentityChecks.Samples(380).Concat(new[]{19,10,314,387,388}.Select(t=>NativeLegacyPlacementIdentityChecks.Samples(t).First())).Select(i=>new[]{i.createTile,i.type})))
                using(var world=new NativeNaturalNameChecks.World(browser.GetType().Assembly,warm))
                {
                    Main.netMode=mode;var origin=new Point16(20,20);TileEntity saved;bool had=TileEntity.ByPosition.TryGetValue(origin,out saved);
                    try
                    {
                        if(row[0]==395){Frame();TileEntity.ByPosition.Remove(origin);}else if(row[0]!=20)NativeLegacyPlacementIdentityChecks.PlaceSource(ContentSamples.ItemsByType[row[1]]);else world.At(20,0);
                        Call(workspace,"Navigate",9,false);object value=world.Observe(328,328,false);
                        int first=(int)Get(value,"ItemType");bool pending=GetOptional(value,"Placement")!=null;
                        if(!warm){world.Warm();if(pending)Require((bool)browser.GetType().Assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("ResolvePlacement",F).Invoke(null,new[]{value,world.Catalog}),"stable cold query resolves original body");}
                        Require((bool)Call(value,"StillMatches"),"complete visible received query retains the real identity/visibility receipt");
                        ((Delegate)Get(targets,"Picked")).DynamicInvoke(value);
                        Console.WriteLine("QUERY-ORACLE mode="+mode+" warm="+warm+" type="+row[0]+" initial="+first+" placement="+pending+" resolved="+Get(value,"ItemType")+" selected="+Get(workspace,"Selected")+" expected="+row[1]);
                        if((int)Get(workspace,"Selected")!=row[1] || !warm && !pending)failures.Add("query "+row[0]+" warm="+warm+" mode="+mode);
                        if(row[0]==395)
                        {
                            var item=new Item();item.SetDefaults(4);TileEntity.ByPosition[origin]=new TEItemFrame{Position=origin,item=item};
                            object contents=world.Observe(328,328,false);((Delegate)Get(targets,"Picked")).DynamicInvoke(contents);
                            Require((int)Get(workspace,"Selected")==4,"received actual contents keep query priority over the carrier");
                        }
                        Main.tile[20,20]=Tile(TileID.MagicalIceBlock,0,0);Call(workspace,"Navigate",9,false);
                        ((Delegate)Get(targets,"Picked")).DynamicInvoke(world.Observe(328,328,false));
                        Require((int)Get(workspace,"Selected")==9,"truly unknown query never invents a navigation target");
                    }
                    finally{if(had)TileEntity.ByPosition[origin]=saved;else TileEntity.ByPosition.Remove(origin);}
                }
                Require(outputCount()==beforePublic,"query-only consumers never use public declaration outlets");
                Require(client+local==interceptedBefore,"query-only intercepted native terminals stay unused");
                Require(failures.Count==0,"actual World(false) -> installed Host Picked -> page navigation: "+string.Join(";",failures));
                Console.WriteLine("PASS query consumers L2: real Host-installed Picked/page navigation for received unknown-content display and sapling, hot/cold bounded native placement, SP/client; actual contents priority and truly unknown nonnavigation. Controlled world/visibility, no input/Main loop/network.");
            }
            finally{silence.UnpatchAll(silence.Id);history.Clear();foreach(object position in savedHistory)history.Add(position);foreach(var pair in fields)pair.Key.SetValue(workspace,pair.Value);Set(state,"Page",page);Set(state,"Visible",visible);Set(state,"Ready",ready);Main.GameViewMatrix=oldView;Main.screenPosition=oldCamera;typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth",F).SetValue(null,(int)oldSize.X);typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenHeight",F).SetValue(null,(int)oldSize.Y);}
        }
        internal static void Run(Assembly assembly)
        {
            NativeLegacyPlacementIdentityChecks.Run(assembly);
            var isolation=new Harmony("JueMingR.Tests.AnnouncementOutcomes");
            bool menu=Main.gameMenu,hidden=Main.hideUI;object owner=null;
            try
            {
                // Exact native construction/dispatch entries remain real;
                // intercept only public chat terminals and local NewText, so
                // no socket, actual chat display or sound can be produced.
                isolation.Patch(typeof(ChatCommandProcessor).GetMethod("CreateOutgoingMessage",F),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Creating),F)));
                isolation.Patch(typeof(ChatHelper).GetMethod("SendChatMessageFromClient",F),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Client),F)));
                isolation.Patch(typeof(ChatCommandProcessor).GetMethod("ProcessIncomingMessage",F),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Local),F)));
                isolation.Patch(typeof(Main).GetMethod("NewText",F,null,new[]{typeof(string),typeof(byte),typeof(byte),typeof(byte)},null),prefix:new HarmonyMethod(typeof(NativeAnnouncementOutcomeChecks).GetMethod(nameof(Notice),F)));
                Main.gameMenu=Main.hideUI=false;
                owner=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Announcements.HostAnnouncements"),F,null,new object[]{Path.Combine(Terraria.Program.SavePath,"natural-outcomes-"+Guid.NewGuid().ToString("N"))},null);
                NativeQuickItemChecks.Until(()=>{Call(owner,"Update");return (bool)Get(owner,"CanConfigure");});Require((bool)Call(owner,"SetEnabled",true),"enable isolated actual announcement owner");
                var observation=assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation");
                Func<int,int,object> ui=(type,quantity)=>observation.GetMethod("UiItem",F).Invoke(null,new object[]{type,quantity});
                using(var world=new NativeNaturalNameChecks.World(assembly))
                {
                    foreach(int mode in new[]{1,0})
                    {
                        Main.netMode=mode;
                        Action<string,Func<object>> failed=(label,produce)=>
                        {
                            Call(Get(owner,"cooldown"),"Clear");Set(owner,"feedbackAt",-1L);created=client=local=notices=0;sent=notice=null;
                            object value=produce();Call(owner,"Submit",value);
                            Console.WriteLine("OUTCOME-ORACLE mode="+mode+" case="+label+" chatCreated="+created+" client="+client+" localChat="+local+" localNotice="+notices+" publicText="+sent+" privateText="+notice);
                            Require(created==0 && client==0 && local==0 && notices==1 && !string.IsNullOrEmpty(notice),"uncertain World/UI result uses only actual local NewText before all three chat entries: "+label);
                            // Do not clear/rewind the production success clock
                            // between rejection and this legitimate next send.
                            Call(owner,"Submit",ui(9,23));
                            Require(created==1 && client==(mode==1?1:0) && local==(mode==0?1:0) && sent.Contains("23 个 "+Lang.GetItemNameValue(9)),"failure does not take success cooldown or poison following ordinary declaration: "+label);
                        };
                        failed("unreceived",()=>{if(mode==1){Main.sectionManager=new WorldSections(1,1);return world.Observe();}return world.Observe(-8,328);});
                        Main.sectionManager=new WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();
                        failed("missing-cell",()=>{Main.tile[20,20]=null;return world.Observe();});
                        failed("real-unknown-primary+water+wires",()=>{
                            Require(!ContentSamples.ItemsByType.Values.Any(i=>i.createTile==TileID.MagicalIceBlock),"actual native item samples have no placeable MagicalIceBlock identity");
                            world.At(TileID.MagicalIceBlock);var tile=Main.tile[20,20];tile.inActive(true);tile.liquid=80;tile.wire(true);object value=world.Observe();
                            Require(NativeNaturalNameChecks.Text(value).Contains("水") && NativeNaturalNameChecks.Text(value).Contains("红线"),"known secondary water/wire facts actually accompany unknown primary");return value;
                        });
                        failed("invalid-natural-frame+water",()=>{world.At(3,145);Main.tile[20,20].liquid=80;return world.Observe();});
                        failed("invalid-moss-frame",()=>world.At(184,18,0));
                        failed("echo-hidden-primary+wires",()=>{world.At(3,144);Main.tile[20,20].invisibleBlock(true);Main.tile[20,20].wire(true);return world.Observe();});
                        failed("empty-slot",()=>ui(0,0));
                        failed("invalid-item",()=>ui(ItemID.Count,1));
                        var origin=new Point16(20,20);TileEntity previous;bool had=TileEntity.ByPosition.TryGetValue(origin,out previous);
                        try
                        {
                            failed("unreceived-display-entity",()=>{Frame();TileEntity.ByPosition.Remove(origin);return world.Observe();});
                            failed("wrong-display-entity",()=>{Frame();TileEntity.ByPosition[origin]=new TEFoodPlatter{Position=origin,item=new Item()};return world.Observe();});
                            failed("invalid-display-content",()=>{Frame();TileEntity.ByPosition[origin]=new TEItemFrame{Position=origin,item=new Item{type=ItemID.Count,stack=1}};return world.Observe();});
                            failed("incomplete-display-body",()=>{Frame();Main.tile[21,21]=new Tile();TileEntity.ByPosition[origin]=new TEItemFrame{Position=origin,item=new Item()};return world.Observe();});
                            failed("overlapping-jar-outlines",()=>{
                                for(int x=19;x<24;x++)for(int y=19;y<24;y++)Main.tile[x,y]=new Tile();
                                for(int x=20;x<=21;x++){Main.tile[x,20]=Tile(698,(x-20)*18,0);Main.tile[x,21]=Tile(698,(x-20)*18,18);TileEntity.ByPosition[new Point16(x,20)]=new TEDeadCellsDisplayJar{Position=new Point16(x,20),item=new Item()};}
                                return world.Observe(328,358);
                            });
                            Positive(owner,mode,()=>{Frame();TileEntity.ByPosition[origin]=new TEItemFrame{Position=origin,item=new Item()};return world.Observe();},"confirmed-empty-display");
                        }
                        finally{TileEntity.ByPosition.Remove(new Point16(21,20));if(had)TileEntity.ByPosition[origin]=previous;else TileEntity.ByPosition.Remove(origin);}
                        Positive(owner,mode,()=>{world.At(3,144);Main.tile[20,20].liquid=80;return world.Observe();},"known-plant+water",Lang.GetItemNameValue(5),"水");
                        foreach(var sample in NativeLegacyPlacementIdentityChecks.Samples(380).Concat(new[]{19,10,314,387,388}.Select(t=>NativeLegacyPlacementIdentityChecks.Samples(t).First())))
                            Positive(owner,mode,()=>{NativeLegacyPlacementIdentityChecks.PlaceSource(sample);object value=world.Observe();Require((int)Get(value,"ItemType")==sample.type,"native legacy body reaches Submit with exact identity");return value;},"canonical-legacy-"+sample.type,Lang.GetItemNameValue(sample.type),"[i:"+sample.type+"]");
                        Positive(owner,mode,()=>world.At(184,22,0),"nonzero-moss-frame","长苔藓");
                        Positive(owner,mode,()=>world.At(20,0),"sapling-description",Terraria.Localization.Language.GetTextValue("MapObject.Sapling"));
                        Require(!sent.Contains("[i:27]") && !sent.Contains(Lang.GetItemNameValue(27)),"query's native Acorn identity never replaces the public sapling description");
                        Positive(owner,mode,()=>{for(int x=19;x<24;x++)for(int y=19;y<24;y++)Main.tile[x,y]=new Tile();return world.Observe();},"confirmed-literal-air");
                        Positive(owner,mode,()=>{Main.player[1].active=true;Main.player[1].name="未确认资料的旅人";Main.player[1].position=new Vector2(320,320);Main.player[1].statLife=20;Main.player[1].statLifeMax2=100;return world.Observe();},"legitimate-name-contains-unknown-words","未确认资料的旅人");Main.player[1].active=false;
                    }
                }
                Console.WriteLine("PASS announcement outcomes L2: actual World/UiItem -> HostAnnouncements -> native chat construction and SP/client outlets versus real local NewText; unknown mixed primary, display receipt/invalid data, hidden/overlap/empty; confirmed air/name words/known plant+water and immediate recovery. All terminal effects intercepted; no real network/game/OS input.");
            }
            finally{if(owner!=null)((IDisposable)owner).Dispose();isolation.UnpatchAll(isolation.Id);Main.gameMenu=menu;Main.hideUI=hidden;}
        }
        private static void Positive(object owner,int mode,Func<object> produce,string label,params string[] expected)
        {
            Call(Get(owner,"cooldown"),"Clear");created=client=local=notices=0;sent=null;Call(owner,"Submit",produce());
            Require(created==1 && client==(mode==1?1:0) && local==(mode==0?1:0) && notices==0 && expected.All(t=>sent.Contains(t)),"normal outcome uses ordinary chat regardless of Literal or name words: "+label);
        }
        private static Tile Tile(int type,int x,int y){var tile=new Tile{type=(ushort)type,frameX=(short)x,frameY=(short)y};tile.active(true);tile.fullbrightBlock(true);return tile;}
        private static void Frame(){for(int x=20;x<22;x++)for(int y=20;y<22;y++)Main.tile[x,y]=Tile(395,(x-20)*18,(y-20)*18);}
        private static void Creating(){created++;}
        private static bool Client(ChatMessage __0){client++;sent=__0.Text;return false;}
        private static bool Local(ChatMessage __0){local++;sent=__0.Text;return false;}
        private static bool Notice(string __0){notices++;notice=__0;return false;}
    }
}
