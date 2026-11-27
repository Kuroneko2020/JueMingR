using System;
using System.Reflection;
using System.Runtime.Serialization;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFacingChecks
    {
        internal static void Run(object context)
        {
            object combat=Get(context,"Combat"),facing=GetOptional(combat,"Facing"),input=Get(context,"Input");
            Require(facing!=null,"independent facing owner is absent");
            var settings=(CombatSettings)Get(combat,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return settings.Loaded;});
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(32));
            try
            {
                var p=Main.LocalPlayer;p.inventory[0].SetDefaults(ItemID.CopperShortsword);p.selectedItemState.Select(0);p.selectedItemState.Update();p.itemAnimation=10;p.itemTime=5;p.position=new Vector2(640,600);
                NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),true);Call(combat,"Sample");NativeQuickItemChecks.BeginWorldStep();
                foreach(var npc in Main.npc)npc.active=false;
                var target=Main.npc[0];target.SetDefaults(NPCID.BlueSlime);target.active=true;target.whoAmI=0;target.position=p.Center-new Vector2(100,10);
                p.direction=1;p.controlLeft=false;p.controlRight=false;Call(facing,"Apply",p);
                Require(p.direction==-1,"facing independently finds hostile NPC instead of rightward cursor");
                var property=facing.GetType().GetProperty("TargetProvider",BindingFlags.Instance|BindingFlags.NonPublic);
                var provided=Main.npc[1];provided.SetDefaults(NPCID.BlueSlime);provided.active=true;provided.whoAmI=1;provided.position=p.Center+new Vector2(150,0);
                object prepared=null;property.SetValue(facing,NativeCombatBoundaryChecks.Provider(property.PropertyType,r=>prepared));
                NativeQuickItemChecks.BeginWorldStep();Type stamp=facing.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.FacingTarget");
                prepared=Activator.CreateInstance(stamp,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{provided,(long)Get(Get(combat,"Runtime"),"Generation")},null);
                int searches=Counter(facing,"Searches");Call(facing,"Apply",p);
                Require(p.direction==1 && Counter(facing,"Searches")==searches,"fresh provided target supersedes fallback inside cooldown without another scan");
                prepared=null;NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);Require(p.direction==-1,"provider revocation promptly resumes independent target");
                property.SetValue(facing,null);provided.active=false;
                p.controlRight=true;Call(facing,"Apply",p);Require(p.direction==1,"actual manual right overrides enemy without waiting for automatic cooldown");
                searches=Counter(facing,"Searches");for(int i=0;i<30;i++){NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);}Require(Counter(facing,"Searches")==searches,"manual direction does not perform useless target search");
                p.controlRight=false;target.active=false;NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);
                Require(p.direction==1,"removed target cannot hold stale left direction");
                target.SetDefaults(NPCID.GoblinTinkerer);target.active=true;target.position=p.Center-new Vector2(100,10);p.direction=-1;NativeQuickItemChecks.BeginWorldStep();Call(facing,"Reset");Call(facing,"Apply",p);
                Require(p.direction==1,"friendly tinkerer never enters ordinary target selection");
                int reads=Counter(facing,"CandidateReads");NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),false);Call(combat,"Sample");p.itemAnimation=p.itemTime=0;
                for(int i=0;i<100;i++){NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);}Require(Counter(facing,"CandidateReads")==reads,"enabled without use intent does not read NPC candidates");
                p.inventory[0].SetDefaults(ItemID.CopperPickaxe);NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),true);Call(combat,"Sample");
                for(int i=0;i<30;i++){NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);}Require(Counter(facing,"CandidateReads")==reads,"ineligible tools do not search even with held input");
                SleepKeepsBedFacing(context,combat,facing,input,p);
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());p.direction=-1;Call(facing,"Apply",p);Require(p.direction==-1,"off releases coverage without restoring an old direction");
                Console.WriteLine("PASS G11A independent facing, native manual controls, stale target and friendly exclusion.");
            }
            finally{NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }

        private static void SleepKeepsBedFacing(object context,object combat,object facing,object input,Player player)
        {
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            var bed=TileObjectData.GetTileData(TileID.Beds,0);
            var priorAnchors=Main.sleepingManager;
            var priorScreen=Main.screenPosition;
            var priorMain=Main.instance;
            var priorBestiary=Main.BestiaryUI;
            int priorChatBubble=priorMain?.currentNPCShowingChatBubble??-1;
            try
            {
                // LookForTileInteractions compares the real UI identities, even
                // with no screen open. A headless fixture has no Bestiary view.
                if(Main.BestiaryUI==null)Main.BestiaryUI=(UIBestiaryTest)FormatterServices.GetUninitializedObject(typeof(UIBestiaryTest));
                if(Main.instance==null){Main.instance=(Main)FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
                Main.instance.currentNPCShowingChatBubble=-1;
                Main.sleepingManager=new AnchoredEntitiesCollection();
                Main.screenPosition=new Vector2(450,450);
                foreach(var npc in Main.npc)npc.active=false;
                player.inventory[0].SetDefaults(ItemID.CopperShortsword);
                player.itemAnimation=player.itemTime=0;
                const int top=50;
                foreach(int direction in new[]{-1,1})
                {
                    int left=direction<0?44:54;
                    for(int x=left;x<left+4;x++)
                    {
                        Main.tile[x,top].ClearEverything();
                        Main.tile[x,top+1].ClearEverything();
                        NativeToolsChecks.Tile(x,top+2,TileID.Stone);
                    }
                    Require(WorldGen.PlaceObject(left+bed.Origin.X,top+bed.Origin.Y,TileID.Beds,true,direction:direction),"native fixture places a supported bed");
                    int tileX=left+(direction<0?3:0);
                    Require(PlayerSleepingHelper.GetSleepingTargetInfo(tileX,top,out int bedDirection,out Vector2 anchor,out _) && bedDirection==direction,"fixture picks the sleeping end for each bed direction");
                    player.Bottom=anchor;
                    player.ChangeDir(-direction);
                    player.releaseUseTile=true;
                    var cursor=new Vector2(tileX*16+8,top*16+8);
                    NativeToolExecutionChecks.Sample(context,input,cursor,false);
                    // Supply the mapped right-click at the normal combat sample seam.
                    Main.mouseRight=PlayerInput.Triggers.Current.MouseRight=true;
                    Call(combat,"Sample");
                    PlayerInput.Triggers.Current.CopyInto(player);
                    Player.tileTargetX=tileX;
                    Player.tileTargetY=top;
                    NativeQuickItemChecks.BeginWorldStep();
                    Require((bool)Get(combat,"Right") && !(bool)Get(combat,"Left") && Math.Sign(Main.MouseWorld.X-anchor.X)==-direction,"combat sees the bed click alone on the side opposite its facing");
                    player.LookForTileInteractions();
                    Require(player.tileInteractionHappened && player.sleeping.isSleeping && player.direction==direction,"original right-click starts sleeping with the bed direction");
                    int reads=Counter(facing,"CandidateReads");
                    player.ItemCheck();
                    NativeQuickItemChecks.BeginWorldStep();
                    Main.sleepingManager.ClearPlayerAnchors();
                    player.sleeping.UpdateState(player);
                    Require(player.sleeping.isSleeping && player.direction==direction,"sleeping survives ItemCheck with automatic facing enabled for bed direction="+direction+" actual="+player.direction);
                    Require(Counter(facing,"CandidateReads")==reads,"sleeping never scans combat targets");
                    player.controlRight=true;
                    Main.sleepingManager.ClearPlayerAnchors();
                    player.sleeping.UpdateState(player);
                    Require(!player.sleeping.isSleeping,"original movement still gets the player out of bed");
                    player.controlRight=false;
                }
                Console.WriteLine("PASS native bed right-click and ItemCheck with automatic facing, both bed directions and normal movement wake.");
            }
            finally
            {
                player.sleeping.StopSleeping(player);
                player.controlRight=player.controlUseTile=player.tileInteractionHappened=false;
                Main.mouseRight=PlayerInput.Triggers.Current.MouseRight=false;
                Main.sleepingManager=priorAnchors;
                Main.screenPosition=priorScreen;
                Main.BestiaryUI=priorBestiary;
                if(priorMain!=null)priorMain.currentNPCShowingChatBubble=priorChatBubble;
                Main.instance=priorMain;
            }
        }
        private static int Counter(object value,string name){return (int)(GetOptional(value,name)??0);}
    }
}
