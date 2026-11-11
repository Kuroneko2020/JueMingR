using System;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeSmartFallActionsChecks
    {
        private static void CobwebOnly()
        {for(int i=0;i<SmartFallProtectionOptions.CapabilityCount;i++)Call(movement,"SetSmartFallPermission",i,i==(int)SmartFallFamily.Cobweb);Call(host,"ClearSession");}
        private static void CobwebBand()
        {
            for(int x=35;x<65;x++)for(int y=90;y<Floor;y++)
            {Main.tile[x,y]=new Tile();Main.tile[x,y].wall=1;}
        }
        private static Player CobwebPlayerAt(float gap)
        {
            Player p=PlayerAt(gap);
            // These held-item consumers use a real on-screen cursor. Earlier
            // pure motion cases intentionally never needed a following camera.
            Main.screenPosition=p.position-new Vector2(300,280);return p;
        }
        private static void CobwebNativePhases()
        {
            // Independent control: original ItemCheck really places a stack
            // unit, but only the subsequent original StickyMovement consumes
            // overlap and resets fallStart. No production receipt is an oracle.
            CobwebOnly();Call(movement,"SetEnabled",3,false);CobwebBand();var p=CobwebPlayerAt(70);
            p.inventory[0].SetDefaults(ItemID.Cobweb);p.inventory[0].stack=9;
            int x=(int)(p.Center.X/16),y=(int)(p.Center.Y/16);
            int oldX=Main.mouseX,oldY=Main.mouseY,oldTX=Player.tileTargetX,oldTY=Player.tileTargetY;
            bool sm=Main.SmartCursorWanted_Mouse,sp=Main.SmartCursorWanted_GamePad;
            try
            {
                p.StickyMovement();Require(p.fallStart==0,"No-web original StickyMovement preserves dangerous baseline.");
                Main.SmartCursorWanted_Mouse=Main.SmartCursorWanted_GamePad=false;
                Main.mouseX=(int)(x*16+8-Main.screenPosition.X);Main.mouseY=(int)(y*16+8-Main.screenPosition.Y);Player.tileTargetX=x;Player.tileTargetY=y;
                p.controlUseItem=p.releaseUseItem=Main.mouseLeft=true;p.ItemCheck();
                Require(Main.tile[x,y].nactive() && Main.tile[x,y].type==51 && p.inventory[0].stack==8,"Original selection/ItemCheck creates a web and really debits one material.");
                Require(p.fallStart==0,"Appearance after ItemCheck does not retroactively consume earlier sticky phase.");
                p.StickyMovement();Require(p.fallStart==(int)(p.position.Y/16) && p.velocity.Y<=.301f,"Next original discrete overlap performs the native reset and slowdown.");
                Console.WriteLine("SMART-FALL COBWEB original-phase-control placed=true debit=1 protection-before-touch=false native-touch=true");
            }
            finally{Main.mouseX=oldX;Main.mouseY=oldY;Player.tileTargetX=oldTX;Player.tileTargetY=oldTY;Main.SmartCursorWanted_Mouse=sm;Main.SmartCursorWanted_GamePad=sp;Main.mouseLeft=false;p.controlUseItem=false;}
        }
        private static void CobwebStep(Player p)
        {
            Begin(p);
            try
            {
                Main.screenPosition=p.position-new Vector2(300,280);
                p.selectedItemState.Update();Call(host,"BeforeSync",p);
                // Preserve locked .8 phase order: previous landing Hurt precedes
                // sticky; ItemCheck follows this update's actual collision.
                Damage(p);p.UpdateEquips(p.whoAmI);Motion(p);p.GrappleMovement();p.StickyMovement();NativeCollision(p);p.ItemCheck();
            }
            finally{End(p);}
        }
        private static void CobwebNativeChain(int stack,float horizontal,bool destroy,bool reverse=false)
        {
            CobwebOnly();CobwebBand();var p=CobwebPlayerAt(80);p.inventory[17].SetDefaults(ItemID.Cobweb);p.inventory[17].stack=stack;
            p.velocity.X=horizontal;p.controlRight=horizontal>0;p.releaseUseItem=true;
            if(reverse)
            {
                p.gravDir=-1;p.velocity.Y=-10;p.fallStart=200;
                for(int x=35;x<65;x++){Main.tile[x,107].active(true);Main.tile[x,107].type=1;}
            }
            long debits=(long)Get(host,"CobwebDebits"),placements=(long)Get(host,"CobwebPlacements"),touches=(long)Get(host,"CobwebTouches");
            int health=p.statLife;bool placed=false,protectedNative=false;int px=-1,py=-1;
            for(int tick=0;tick<260;tick++)
            {
                CobwebStep(p);
                if(!placed && (bool)Get(host,"CobwebPlaced"))
                {
                    placed=true;px=(int)Get(host,"webPlacedX");py=(int)Get(host,"webPlacedY");
                    Require(!(bool)Get(host,"CobwebProtected"),"World placement is not recorded as protection before a later native sample.");
                    if(destroy){Main.tile[px,py]=new Tile();Call(movement,"SetSmartFallPermission",12,false);}
                }
                protectedNative|=(bool)Get(host,"CobwebProtected");
                if(protectedNative && !(bool)Get(host,"HasPending") && p.velocity.Y==0)break;
                if(destroy && placed && tick>30)break;
            }
            Require(placed,"Production normal native use places real carried web.");
            int remaining=p.inventory[17].IsAir?0:p.inventory[17].stack;
            Require(remaining==stack-1 && (long)Get(host,"CobwebDebits")==debits+1 && (long)Get(host,"CobwebPlacements")==placements+1,"Large stack/last piece is debited once; no quantity restoration or second use.");
            if(destroy)Require(!protectedNative,"Destroyed-before-touch web never claims protection.");
            else
            {
                Require(p.velocity.Y==0,"Rescued player eventually reaches the real native landing.");Damage(p);
                Require(protectedNative && (long)Get(host,"CobwebTouches")>touches && p.statLife==health,"Production protection requires the live player's original sticky reset and final native landing damage consumer.");
            }
            Require(p.selectedItem==0 && !(bool)Get(host,"HasPending"),"Only the original held selection is returned, including a consumed Air endpoint.");
            Console.WriteLine("SMART-FALL COBWEB chain stack="+stack+" horizontal="+horizontal+" reverse="+reverse+" destroyed="+destroy+" debit=1 placed="+px+","+py+" actual-touch="+protectedNative+" returned=true");
        }
        private static void CobwebNaturalAndNearby()
        {
            foreach(bool intersects in new[]{true,false})
            {
                CobwebOnly();CobwebBand();var p=CobwebPlayerAt(80);p.inventory[17].SetDefaults(ItemID.Cobweb);p.inventory[17].stack=20;
                int x=(int)(p.Center.X/16)+(intersects?0:4),y=(int)(p.Center.Y/16);
                Require(WorldGen.PlaceTile(x,y,51,mute:true,forced:true,plr:p.whoAmI),"Natural-web fixture is a real native world object.");
                long debit=(long)Get(host,"CobwebDebits");
                for(int tick=0;tick<4;tick++)CobwebStep(p);
                if(intersects)Require(p.inventory[17].stack==20 && (long)Get(host,"CobwebDebits")==debit && (bool)Get(host,"CobwebProtected"),"Existing body-path web really protects with zero extra material.");
                else Require(p.inventory[17].stack==19 && (long)Get(host,"CobwebDebits")==debit+1,"Nearby off-path web is not a protection certificate and does not move the player toward it.");
                Call(host,"ClearSession");
            }
        }
        private static void CobwebNativeMaskSeparation()
        {
            CobwebOnly();var p=PlayerAt(70);p.canJumpAgain_Cloud=true;
            Begin(p);try{Require(!(bool)Get(host,"HasPending") && (int)Get(host,"sourceCount")==0,"Unused-jump fact bit 12 cannot create a cobweb source without real material.");}finally{End(p);}
        }
    }
}
