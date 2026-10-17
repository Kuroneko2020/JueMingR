using System;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain
    {
        // CanHitLine is not CanHit: the native trace alternates fractional
        // runs and rejects any of three cells, including the starting strip.
        // Read only those cells through the existing bounded terrain lease.
        // This permits long narrow queries without a bounding-rectangle scan.
        internal bool CanHitLine(MotionRect source,MotionRect target,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;
            if(!FreeBody(source.X,source.Y,source.Width,source.Height,out stop,true) || !FreeBody(target.X,target.Y,target.Width,target.Height,out stop,true))return false;
            if(Main.maxTilesX<2 || Main.maxTilesY<=40){stop=PredictionStop.InvalidState;return false;}
            // Unlike CanHit, position remains float until division by16;
            // width/height use integer halves before adding to that position.
            int x=Math.Max(1,Math.Min(Main.maxTilesX-1,(int)((source.X+(int)source.Width/2)/16f)));
            int y=Math.Max(1,Math.Min(Main.maxTilesY-40,(int)((source.Y+(int)source.Height/2)/16f)));
            int tx=Math.Max(1,Math.Min(Main.maxTilesX-1,(int)((target.X+(int)target.Width/2)/16f)));
            int ty=Math.Max(1,Math.Min(Main.maxTilesY-40,(int)((target.Y+(int)target.Height/2)/16f)));
            // The locked net472/x86 native trace retains its ratio/carry precision
            // across integer-run decisions. Float spills in this cell-reader
            // port change a run at near-integral carries (including the last
            // strip), reversing both blocked and clear results. Explicit
            // double arithmetic matches those original decisions; positions
            // and tile conversion above keep their native float semantics.
            double dx=Math.Abs(x-tx),dy=Math.Abs(y-ty);
            if(dx==0 && dy==0){clear=true;return true;}
            double strideX=1,strideY=1;
            if(dx==0 || dy==0){if(dx==0)strideX=0;if(dy==0)strideY=0;}
            else if(dx>dy)strideX=dx/dy;else strideY=dy/dx;
            double carryX=0,carryY=0;int axis=y<ty?2:1,remainingX=(int)dx,remainingY=(int)dy,sx=Math.Sign(tx-x),sy=Math.Sign(ty-y);
            bool reached=false,lastSingle=false;
            do
            {
                if(axis==2)
                {
                    carryX+=strideX;int steps=(int)carryX;carryX-=steps;
                    for(int i=0;i<steps;i++)
                    {
                        bool blocked;if(!LineStrip(x,y,true,out blocked,out stop))return false;if(blocked)return true;
                        if(remainingX==0 && remainingY==0){reached=true;break;}
                        x+=sx;remainingX--;
                        if(remainingX==0 && remainingY==0 && steps==1)lastSingle=true;
                    }
                    if(remainingY!=0)axis=1;
                }
                else
                {
                    carryY+=strideY;int steps=(int)carryY;carryY-=steps;
                    for(int i=0;i<steps;i++)
                    {
                        bool blocked;if(!LineStrip(x,y,false,out blocked,out stop))return false;if(blocked)return true;
                        if(remainingX==0 && remainingY==0){reached=true;break;}
                        y+=sy;remainingY--;
                        if(remainingX==0 && remainingY==0 && steps==1)lastSingle=true;
                    }
                    if(remainingX!=0)axis=2;
                }
                bool solid;if(!LineCell(x,y,out solid,out stop))return false;if(solid)return true;
            }while(!reached && !lastSingle);
            clear=true;return true;
        }
        private bool LineStrip(int x,int y,bool horizontal,out bool blocked,out PredictionStop stop)
        {
            blocked=true;stop=PredictionStop.None;
            // Original catches a neighbour outside Main.tile and returns
            // false. This is a known border result, not a missing world cell.
            if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY || horizontal && (y<1 || y+1>=Main.maxTilesY) || !horizontal && (x<1 || x+1>=Main.maxTilesX))return true;
            Cell a,b,c;
            if(!CellAt(horizontal?x:x-1,horizontal?y-1:y,out a,out stop) || !CellAt(x,y,out b,out stop) || !CellAt(horizontal?x:x+1,horizontal?y+1:y,out c,out stop))return false;
            blocked=a.Solid && !a.Platform || b.Solid && !b.Platform || c.Solid && !c.Platform;return true;
        }
        private bool LineCell(int x,int y,out bool solid,out PredictionStop stop)
        {
            solid=true;stop=PredictionStop.None;if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return true;
            Cell cell;if(!CellAt(x,y,out cell,out stop))return false;solid=cell.Solid && !cell.Platform;return true;
        }
    }
}
