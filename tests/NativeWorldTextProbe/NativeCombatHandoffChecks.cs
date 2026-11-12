using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Features.QuickItems;
using JueMingR.Features.Movement;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHandoffChecks
    {
        private static readonly List<int> uses=new List<int>();
        private static bool cobwebFilelessReceiver;
        private static bool cobwebResourceAudit;
        private static int cobwebActualDebits,cobwebActualPlacements,cobwebActualPickups;
        private struct CobwebUseReceipt { internal Item Source;internal int Stack,X,Y;internal bool Empty; }
        private struct CobwebPickupReceipt { internal WorldItem World;internal Item Source;internal int WorldStack,SourceStack; }
        private static void CobwebBeforeUse(Player __instance,out CobwebUseReceipt __state)
        {
            __state=default(CobwebUseReceipt);
            if(!cobwebResourceAudit || !ReferenceEquals(__instance,Main.LocalPlayer) || __instance.selectedItem!=17 || __instance.HeldItem.type!=ItemID.Cobweb)return;
            int x=Player.tileTargetX,y=Player.tileTargetY;
            __state=new CobwebUseReceipt{Source=__instance.inventory[17],Stack=__instance.inventory[17].stack,X=x,Y=y,Empty=!Main.tile[x,y].active()};
        }
        private static void CobwebAfterUse(Player __instance,bool __runOriginal,CobwebUseReceipt __state)
        {
            if(__state.Source==null)return;
            int after=__state.Source.IsAir?0:__state.Source.stack,debit=__state.Stack-after;if(debit==0)return;
            Require(__runOriginal && ReferenceEquals(__instance.inventory[17],__state.Source) && debit==1 && __state.Empty && Main.tile[__state.X,__state.Y].nactive() && Main.tile[__state.X,__state.Y].type==51,
                "Independent original ItemCheck receipt must debit exactly one real source unit and create the actual target Tile.");
            cobwebActualDebits+=debit;cobwebActualPlacements++;
            Console.WriteLine("COBWEB ORIGINAL RECEIPT source-ref=true stack="+__state.Stack+"->"+after+" tile="+__state.X+","+__state.Y+" native-original=true");
        }
        private static void CobwebBeforePickup(Player __instance,WorldItem __0,out CobwebPickupReceipt __state)
        {
            __state=default(CobwebPickupReceipt);
            if(!cobwebResourceAudit || !ReferenceEquals(__instance,Main.LocalPlayer) || __0.type!=ItemID.Cobweb)return;
            __state=new CobwebPickupReceipt{World=__0,WorldStack=__0.stack,Source=__instance.inventory[17],SourceStack=__instance.inventory[17].stack};
        }
        private static void CobwebAfterPickup(Player __instance,bool __runOriginal,CobwebPickupReceipt __state)
        {
            if(__state.World==null)return;
            int remaining=__state.World.type==ItemID.Cobweb?__state.World.stack:0;
            int after=__instance.inventory[17].type==ItemID.Cobweb?__instance.inventory[17].stack:0,gain=after-__state.SourceStack;
            if(gain==0)return;
            Require(__runOriginal && ReferenceEquals(__instance.inventory[17],__state.Source) && gain>0 && gain==__state.WorldStack-remaining,
                "Original PickupItem/GetItem must consume the observed real world stack for every source-slot increase.");
            cobwebActualPickups+=gain;
            Console.WriteLine("COBWEB ORIGINAL PICKUP world="+__state.WorldStack+"->"+remaining+" source-slot="+__state.SourceStack+"->"+after+" native-PickupItem/GetItem=true");
        }
        private static void StartCobwebResourceAudit()
        {cobwebActualDebits=cobwebActualPlacements=cobwebActualPickups=0;cobwebResourceAudit=true;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var processing=Get(context,"Processing");var input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});
            var audit=new Harmony("JueMingR.Tests.CombatHandoff");audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck_StartActualUse"),postfix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(Started)));
            try
            {
                foreach(int empty in new[]{0,3})foreach(int weapon in new[]{ItemID.CopperShortsword,98,198})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);
                    if(weapon==198)p.inventory[1].SetDefaults(671);
                    p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
                    p.inventory[12].SetDefaults(ItemID.StaffofRegrowth);p.inventory[10].SetDefaults(ItemID.SiltBlock);p.inventory[10].stack=20;
                    NativeToolsChecks.Tile(38,42,1);Require(WorldGen.PlaceTile(38,41,78,mute:true,forced:true,plr:0),"combat mixed native pot");NativeToolsChecks.Tile(38,40,84);
                    NativeExtractionChecks.Machine(43,40,219);uses.Clear();NativeToolsChecks.SetMode(tools,1,1);
                    Call(processing,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Value",1);});
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions(weapon==198?4:1));
                    for(int step=0;step<700;step++)NativeCombatCadenceChecks.Step(context,weapon!=198,weapon==198,empty);
                    Require(uses.Count(x=>x==weapon)>=3,"combat continues while lending real uses: weapon="+weapon+" empty="+empty+" uses="+string.Join(",",uses));
                    Require(uses.Contains(ItemID.StaffofRegrowth) && Main.tile[38,40].active() && Main.tile[38,40].type==82,"held combat yields a complete native harvest: weapon="+weapon+" empty="+empty+" uses="+string.Join(",",uses));
                    Require(p.inventory[10].IsAir || p.inventory[10].stack<20,"held combat yields actual extraction without starving it: weapon="+weapon+" empty="+empty+" selected="+p.selectedItem+" handoff="+Get(Get(combat,"Handoff"),"Waiting"));
                    NativeToolsChecks.SetMode(tools,1,0);Call(processing,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});
                    for(int i=0;i<90;i++)NativeCombatCadenceChecks.Step(context,false,false,0);
                }
                Quick(context,combat,tools,input,false,false);
                Quick(context,combat,tools,input,true,false);
                Quick(context,combat,tools,input,true,true);
                Quick(context,combat,tools,input,false,false,true);
                Console.WriteLine("PASS G11A held left/right combat, native autoReuse weapon, actual harvest/extraction fairness and single quick-item pulse with 0/3 unsampled outer updates.");
            }
            finally
            {
                NativeToolsChecks.SetMode(tools,1,0);if((bool)Call(processing,"Controls",1))Call(processing,"Set",1,false);
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());foreach(var m in audit.GetPatchedMethods().ToArray())audit.Unpatch(m,HarmonyPatchType.All,audit.Id);
            }
        }
        private static void Quick(object context,object combat,object tools,object input,bool right,bool interact,bool nativeVeto=false)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            int weapon=right?198:ItemID.CopperShortsword;
            var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);p.inventory[1].SetDefaults(671);p.inventory[17].SetDefaults(98);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=100;
            NativeCombatCadenceChecks.Step(context,false,false,0);if(interact)NativeCombatInteractionChecks.Chest();
            var quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");
            var entry=new QuickItemEntry("11001100110011001100110011001100",98,QuickItemMode.Use,false,true);string reason;
            Require(settings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out reason),"combat quick entry "+reason);NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            var bindings=(HotkeyBindings)Get(Get(Get(context,"Shell"),"hotkeys"),"Bindings");NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"J");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(right?4:1));uses.Clear();bool pressed=false;
            var veto=new Harmony("JueMingR.Tests.QuickBorrowNativeVeto");vetoQuick=nativeVeto?quick:null;vetoCombat=combat;vetoObserved=false;
            if(nativeVeto)veto.Patch(AccessTools.Method(typeof(Player),"LookForTileInteractions"),prefix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(VetoBorrow)));
            try
            {
            for(int i=0;i<240;i++)
            {
                bool press=!pressed && (bool)Get(Get(combat,"Use"),"CanYield");if(press)pressed=true;
                if(press && interact)p.releaseUseTile=true;
                NativeCombatCadenceChecks.Step(context,!right,right,1,press?new[]{Keys.J}:null,press && interact?(Microsoft.Xna.Framework.Vector2?)new Microsoft.Xna.Framework.Vector2(696,664):null);
                if(press || i==239)Console.WriteLine("QUICK BORROW veto="+nativeVeto+" press="+press+" delay="+p.delayUseItem+" block="+Main.blockMouse+" waiting="+Get(Get(combat,"Handoff"),"Waiting")+" revoked="+Get(Get(combat,"Handoff"),"revoked")+" quick="+Get(Get(quick,"Use"),"Active")+" use="+p.controlUseItem+" selected="+p.selectedItem);
            }
            if(nativeVeto)
            {
                Require(pressed && vetoObserved && uses.Count(t=>t==98)==0 && p.inventory[54].stack==100 && p.selectedItem==0 && !p.selectedItemState.HasActiveOverride && !(bool)Get(Get(quick,"Use"),"Active") && !p.controlUseItem,
                    "real held-use permit revoked after pulse before ItemCheck; no quick shot/debit and same-frame cleanup cannot restore rejected hold");
                Console.WriteLine("PASS actual quick-item borrow revoked at native veto: pulse existed, permit false, no actual-use receipt, token retired and hold not restored.");
            }
            else
            Require(pressed && uses.Count(t=>t==98)==(interact?0:1) && (interact?p.chest==0 && p.inventory[54].stack==100:uses.Count(t=>t==weapon)>3) && !(bool)Get(Get(quick,"Use"),"Active"),
                "fresh quick request honors native interaction/one pulse and original return: right="+right+" interaction="+interact+" uses="+string.Join(",",uses));
            }
            finally{vetoQuick=null;Main.blockMouse=false;p.delayUseItem=false;foreach(var m in veto.GetPatchedMethods().ToArray())veto.Unpatch(m,HarmonyPatchType.All,veto.Id);}
            NativeCombatCadenceChecks.Step(context,false,false,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions());p.chest=-1;Main.playerInventory=false;Main.chest[0]=null;
        }
        private static object vetoQuick,vetoCombat;
        private static bool vetoObserved;
        private static void VetoBorrow(Player __instance)
        {
            if(vetoQuick==null || vetoObserved)return;var use=Get(vetoQuick,"Use");
            if(!(bool)Get(use,"Active") || !(bool)Get(use,"pulsed"))return;
            var permit=(Func<bool>)Get(vetoQuick,"HeldUsePermit");
            Require(permit(),"actual borrowed hold has a valid permit before native veto");
            Require(__instance.controlUseItem,"real quick pulse exists before veto");
            __instance.delayUseItem=true;Require(!permit(),"native veto invalidates existing held-use permit");
            Require(!(bool)Get(Get(vetoCombat,"Use"),"CanOffer"),"veto cannot offer a new hold");vetoObserved=true;
        }
        private static void Started(Player __instance,Item __0){if(__instance.whoAmI==Main.myPlayer)uses.Add(__0.type);}
        internal static void RunCobweb(object context)
        {
            object movement=Get(context,"Movement"),fall=Get(movement,"FallHost"),combat=Get(context,"Combat"),tools=Get(context,"Tools"),input=Get(context,"Input");
            var before=(SmartFallProtectionOptions)Get(movement,"SmartFallProtection");
            var audit=new Harmony("JueMingR.Tests.CobwebContinuousOwners");
            audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck_StartActualUse"),postfix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(Started)));
            double oldSurface=Main.worldSurface;
            var oldCamera=Main.screenPosition;
            var oldLighting=Lighting.Mode;Main createdMain=null;
            Tile[,] oldTiles=Main.tile;int oldHeight=Main.maxTilesY;var oldMap=Main.Map;
            Exception firstFailure=null;
            try
            {
                // Stay below .8's real space-gravity reset band while keeping
                // the owned fixture finite. Original tile objects are shared;
                // only the added lower rows need empty original Tile values.
                var finite=new Tile[Main.maxTilesX,200];
                for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<200;y++)finite[x,y]=y<oldHeight?oldTiles[x,y]:new Tile();
                Main.tile=finite;Main.maxTilesY=200;
                Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
                NativeToolExecutionChecks.Initialize(context,null);
                // Same finite full-Update prerequisites as CombatCadence.Run:
                // the native held-item/interaction code reads Main.instance,
                // and Color uses the real constructor-initialized light list.
                // Keep Steam achievement outlets outside this CPU receiver;
                // no movement, item, damage or owner consumer is suppressed.
                Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
                if(Main.instance==null){createdMain=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(createdMain);Main.instance=createdMain;}
                foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})
                    audit.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper),name),prefix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(SkipCobwebAchievement)));
                audit.Patch(AccessTools.Method(typeof(WorldGen),"saveToonWhilePlaying"),prefix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(CobwebSaveOutlet)));
                audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck"),prefix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(CobwebBeforeUse)){priority=Priority.Last},postfix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(CobwebAfterUse)){priority=Priority.First});
                audit.Patch(AccessTools.Method(typeof(Player),"PickupItem",new[]{typeof(WorldItem)}),prefix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(CobwebBeforePickup)),postfix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(CobwebAfterPickup)));
                cobwebFilelessReceiver=true;
                foreach(bool mining in new[]{false,true})
                {
                    Call(movement,"SetEnabled",3,false);Call(fall,"ClearSession");NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    int weapon=mining?ItemID.CopperPickaxe:ItemID.CopperShortsword;
                    Player p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);
                    p.position.Y=90*16-p.height;p.fallStart=(int)(p.position.Y/16);
                    for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,90,1);
                    p.inventory[17].SetDefaults(ItemID.Cobweb);p.inventory[17].stack=40;uses.Clear();
                    if(mining)
                    {
                        for(int x=44;x<47;x++)for(int y=87;y<117;y++)NativeToolsChecks.Tile(x,y,7);
                        NativeToolsChecks.SetMode(tools,2,1);
                        Require((bool)Call(Get(tools,"Mining"),"Select",p,44,88,7,false),"Real mining region is established before the rescue.");
                    }
                    else NativeCombatCadenceChecks.Save(combat,new CombatOptions(1));
                    object owner=mining?Get(tools,"Use"):Get(combat,"Use");
                    for(int frame=0;frame<80;frame++)
                    {
                        CobwebOwnerStep(context,p,!mining);
                        if((bool)Get(owner,"Active") && uses.Count(t=>t==weapon)>1)break;
                    }
                    Require((bool)Get(owner,"Active") && uses.Count(t=>t==weapon)>1,"Continuous native owner made real progress before a fall.");
                    for(int i=0;i<SmartFallProtectionOptions.CapabilityCount;i++)Call(movement,"SetSmartFallPermission",i,i==12);
                    Call(movement,"SetEnabled",3,true);Main.worldSurface=10;
                    // A changed world opens a real fall while the established
                    // owner still has its exact source and natural animation.
                    for(int x=35;x<65;x++)
                    {
                        Main.tile[x,90].active(false);NativeToolsChecks.Tile(x,117,1);
                        for(int y=86;y<117;y++)Main.tile[x,y].wall=1;
                    }
                    p.fallStart=0;p.velocity.Y=8;
                    int beforeStack=p.inventory[17].stack,atReturnUses=-1,atReturnOre=-1;
                    StartCobwebResourceAudit();
                    bool placed=false,touched=false,returned=false,continued=false;
                    for(int step=0;step<360;step++)
                    {
                        CobwebOwnerStep(context,p,!mining);
                        placed|=(bool)Get(fall,"CobwebPlaced");touched|=(bool)Get(fall,"CobwebProtected");
                        if(placed && !returned && !(bool)Get(fall,"HasPending") && p.selectedItem==0)
                        {returned=true;atReturnUses=uses.Count(t=>t==weapon);atReturnOre=CobwebOre();}
                        if(returned && (mining?CobwebOre()<atReturnOre:uses.Count(t=>t==weapon)>atReturnUses)){continued=true;break;}
                        if(p.dead)break;
                    }
                    Require(placed && touched && returned && continued && cobwebActualDebits==1 && cobwebActualPlacements==1 && p.inventory[17].stack==beforeStack-cobwebActualDebits+cobwebActualPickups,
                        "Continuous owner legally lends one native web/debit/touch and makes NEW real progress after return: mining="+mining+" placed="+placed+" touched="+touched+" returned="+returned+" continued="+continued+" stack="+p.inventory[17].stack+" pos="+p.position+" fallStart="+p.fallStart+" result="+Get(fall,"Result")+" pending="+Get(fall,"HasPending")+" owner="+Get(owner,"Active"));
                    cobwebResourceAudit=false;
                    Console.WriteLine("SMART-FALL COBWEB CONTINUITY owner="+(mining?"mining":"combat")+" real-before=true original-debit="+cobwebActualDebits+" original-pickup="+cobwebActualPickups+" native-touch=true returned=true new-real-progress=true");
                    NativeToolsChecks.SetMode(tools,2,0);NativeCombatCadenceChecks.Step(context,false,false,0);
                }
                CobwebFishing(context,movement,fall,tools,input);
            }
            catch(Exception error)
            {firstFailure=error;Console.Error.WriteLine("COBWEB OWNER first failure retained: "+error);}
            finally
            {
                // A preference failure is diagnostic evidence, not permission
                // to strand world/Map/camera or overwrite an earlier native
                // failure. Try each owned settlement; always return globals.
                Action<string,Action> settle=(name,action)=>
                {
                    try{action();}
                    catch(Exception error){if(firstFailure==null)firstFailure=error;Console.Error.WriteLine("COBWEB OWNER cleanup "+name+": "+error);}
                };
                try
                {
                    settle("fall",()=>Call(fall,"ClearSession"));
                    settle("mining-mode",()=>NativeToolsChecks.SetMode(tools,2,0));
                    settle("combat-settings",()=>NativeCombatCadenceChecks.Save(combat,new CombatOptions()));
                    settle("fishing-settings",()=>NativeFishingChecks.Save(Get(context,"Fishing"),new FishingOptions()));
                    for(int i=0;i<SmartFallProtectionOptions.CapabilityCount;i++){int permission=i;settle("fall-permission",()=>Call(movement,"SetSmartFallPermission",permission,before.Allows(permission)));}
                    settle("fall-enabled",()=>Call(movement,"SetEnabled",3,before.Enabled));
                }
                finally
                {
                    cobwebFilelessReceiver=cobwebResourceAudit=false;Main.worldSurface=oldSurface;Main.tile=oldTiles;Main.maxTilesY=oldHeight;Main.Map=oldMap;Main.screenPosition=oldCamera;
                    Lighting.Mode=oldLighting;if(createdMain!=null && ReferenceEquals(Main.instance,createdMain))Main.instance=null;
                    foreach(var method in audit.GetPatchedMethods().ToArray())settle("native-audit",()=>audit.Unpatch(method,HarmonyPatchType.All,audit.Id));
                }
            }
            if(firstFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
        private static bool SkipCobwebAchievement(){return false;}
        private static bool CobwebSaveOutlet()
        {
            if(!cobwebFilelessReceiver)return true;
            Console.Error.WriteLine("COBWEB FILELESS SAVE outlet excluded; real death preserved dead="+Main.LocalPlayer.dead+" life="+Main.LocalPlayer.statLife);return false;
        }
        internal static void CobwebWorldStep(object context,Action nativeStep)
        {
            var movement=Get(context,"Movement");Type hooks=movement.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks",true);
            Require(ReferenceEquals(AccessTools.Field(hooks,"owner").GetValue(null),movement),"The real Movement hooks belong to this exact receiver.");
            // Same finite world-entry callbacks as the existing native social/
            // fault fixtures. BeginWorldStep alone advances only the clock;
            // the real Player prefix also requires the native world scope.
            AccessTools.Method(hooks,"BeginWorld").Invoke(null,null);
            try{nativeStep();}
            finally{AccessTools.Method(hooks,"EndWorld").Invoke(null,new object[]{null});}
        }
        private static int CobwebOre()
        {int count=0;for(int x=44;x<47;x++)for(int y=87;y<117;y++)if(Main.tile[x,y].active() && Main.tile[x,y].type==7)count++;return count;}
        private static void CobwebOwnerStep(object context,Player p,bool combat)
        {
            Main.screenPosition=p.position-new Microsoft.Xna.Framework.Vector2(300,280);
            CobwebWorldStep(context,()=>NativeCombatCadenceChecks.Step(context,combat,false,0,null,p.Center+new Microsoft.Xna.Framework.Vector2(100,0)));
        }
        private static void CobwebFishing(object context,object movement,object fall,object tools,object input)
        {
            object fishing=Get(context,"Fishing"),session=Get(fishing,"Session");
            NativeQuickItemChecks.Until(()=>{Call(fishing,"Poll");return (bool)Get(Get(fishing,"Settings"),"Loaded");});
            if(Main.FishDropsDB==null)
            {
                Main.FishDropsDB=new Terraria.GameContent.FishDropRules.FishDropRuleList();
                new Terraria.GameContent.FishDropRules.GameContentFishDropPopulator(Main.FishDropsDB).Populate();
            }
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            var chum=AccessTools.Field(typeof(Main),"ChumBucketProjectileHelper");
            if(chum.GetValue(Main.instance)==null)chum.SetValue(Main.instance,Activator.CreateInstance(chum.FieldType));
            Call(movement,"SetEnabled",3,false);Call(fall,"ClearSession");
            NativeFishingChecks.Save(fishing,new FishingOptions());
            Player p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.WoodFishingPole,0,0);
            p.position.Y=90*16-p.height;p.fallStart=(int)(p.position.Y/16);
            p.armor[3].SetDefaults(ItemID.HighTestFishingLine);p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            p.inventory[17].SetDefaults(ItemID.Cobweb);p.inventory[17].stack=20;
            for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,90,1);
            for(int x=44;x<74;x++)for(int y=89;y<111;y++)
            {Main.tile[x,y].ClearEverything();if(y>=92 && y<110)Main.tile[x,y].liquid=255;if(y==110)NativeToolsChecks.Tile(x,y,1);}
            NativeFishingChecks.Save(fishing,new FishingOptions(auto:true));
            var target=new Microsoft.Xna.Framework.Vector2(850,1470);Projectile original=null;
            for(int frame=0;frame<180;frame++)
            {
                Main.screenPosition=p.position-new Microsoft.Xna.Framework.Vector2(300,280);
                CobwebWorldStep(context,()=>NativeFishingChecks.Step(context,input,target,frame==0,0));
                original=Main.projectile.FirstOrDefault(q=>q.active && q.bobber && q.owner==p.whoAmI && q.wet && q.ai[0]<1);
                if(original!=null && Get(session,"Phase").ToString()=="Waiting")break;
            }
            Require(original!=null && Get(session,"Phase").ToString()=="Waiting","Real rod casts into original liquid and establishes the session before rescue.");
            long sessionToken=(long)Get(session,"Token");int originalKey=(int)original.key;
            for(int i=0;i<SmartFallProtectionOptions.CapabilityCount;i++)Call(movement,"SetSmartFallPermission",i,i==12);
            Call(movement,"SetEnabled",3,true);Main.worldSurface=10;
            for(int x=35;x<44;x++)
            {Main.tile[x,90].active(false);NativeToolsChecks.Tile(x,117,1);for(int y=86;y<117;y++)Main.tile[x,y].wall=1;}
            p.fallStart=0;p.velocity.Y=8;
            StartCobwebResourceAudit();
            bool lent=false,returned=false,continued=false;
            for(int frame=0;frame<360;frame++)
            {
                Main.screenPosition=p.position-new Microsoft.Xna.Framework.Vector2(300,280);
                CobwebWorldStep(context,()=>NativeFishingChecks.Step(context,input,target,false,0));
                lent|=(bool)Get(fall,"CobwebPlaced");
                if(lent && !(bool)Get(fall,"HasPending") && p.selectedItem==0)returned=true;
                if(returned && (bool)Get(session,"Active") && (long)Get(session,"Token")==sessionToken && Get(session,"Phase").ToString()=="Waiting")
                {
                    continued=Main.projectile.Any(q=>q.active && q.bobber && q.owner==p.whoAmI && q.wet &&
                        (original.active && (int)original.key==originalKey?ReferenceEquals(q,original):(int)q.key!=originalKey));
                    if(continued)break;
                }
            }
            Require(lent && (bool)Get(fall,"CobwebProtected") && returned && continued && cobwebActualDebits==1 && cobwebActualPlacements==1 && p.inventory[17].stack==20-cobwebActualDebits+cobwebActualPickups,
                "Exact rescue loan preserves the fishing session and follows the ORIGINAL bobber's real survival/loss into waiting or one ordinary recast.");
            cobwebResourceAudit=false;
            p.inventory[1].SetDefaults(ItemID.StoneBlock);p.selectedItemState.Select(1);
            NativeFishingChecks.Step(context,input,target,false,0);
            Require(!(bool)Get(session,"Active"),"A real manual non-rod selection still exits fishing after the finite rescue permit ends.");
            NativeFishingChecks.Save(fishing,new FishingOptions());
            Console.WriteLine("SMART-FALL COBWEB FISHING original-liquid-session=true debit=1 native-touch=true exact-session-retained=true real-bobber-continuation=true manual-switch-exits=true");
        }
    }
}
