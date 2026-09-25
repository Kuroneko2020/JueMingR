using System;
using JueMingR.Features.Recovery;
using Terraria;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryFurnitureChecks
    {
        internal static void Run(object context)
        {
            TileObjectData.Initialize();
            object host=Get(context,"Recovery"),input=Get(context,"Input");var s=(RecoverySettings)Get(host,"Services");
            NativeQuickItemChecks.Until(()=>{s.Poll();return s.Loaded;});NativeRecoveryChecks.Save(s,new RecoveryOptions(furniture:true));
            int[] types={125,287,354,377,464,621,699},buffs={29,93,150,159,348,192,366},widths={2,2,3,3,5,2,4},heights={2,2,3,2,4,2,4};
            var p=Main.LocalPlayer;p.position=new Microsoft.Xna.Framework.Vector2(40*16,40*16);p.releaseUseTile=true;p.tileInteractionHappened=false;p.controlUseTile=false;p.itemAnimation=p.itemTime=0;p.controlUseItem=p.channel=false;p.mouseInterface=false;
            for(int n=0;n<types.Length;n++)
            {
                for(int x=35;x<51;x++)for(int y=35;y<51;y++)Main.tile[x,y]=new Tile();
                for(int x=0;x<widths[n];x++)for(int y=0;y<heights[n];y++){var t=Main.tile[42+x,40+y];t.active(true);t.type=(ushort)types[n];t.frameX=(short)(18*x);t.frameY=(short)(18*y);}
                Main.playerInventory=n%2==1;bool wasOpen=Main.playerInventory;
                Call(Get(context,"worldTiles"),"BeginTick");
                NativeRecoveryChecks.Frame(host,input,(ulong)(100+n*60));
                int index=p.FindBuffIndex(buffs[n]);Require(index>=0 && p.buffTime[index]==(n==5?7200:108000),"real placed furniture grants exact native buff "+types[n]);
                Require(Main.playerInventory==wasOpen && !p.tileInteractAttempted && p.releaseUseTile,"automatic furniture preserves UI and interaction input "+types[n]);
            }
            p.ClearBuff(366);Main.tile[43,41].frameX=0;
            Call(Get(context,"worldTiles"),"BeginTick");
            NativeRecoveryChecks.Frame(host,input,700);Require(p.FindBuffIndex(366)<0,"incomplete/mismatched multiframe object is refused");
            NativeRecoveryChecks.Save(s,new RecoveryOptions());Main.playerInventory=false;
            Console.WriteLine("PASS G07 furniture: all seven native effects/durations, full frame validation and unchanged inventory state.");
        }
    }
}
