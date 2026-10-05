using System;
using System.IO;
using System.Linq;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Presentation stress uses real sample owners and real XNA Draw. It does
    // not claim that these deliberately dense rectangles occur in gameplay.
    internal static class NativeCombatMarkerCapacityChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var marker=Get(world,"Marker");var geometry=Get(host,"Geometry");
            foreach(bool path in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:path,marker:true));NativeCombatObservationChecks.Fresh(context,host);
                Call(geometry,"Clear");var attacks=(Array)Get(geometry,"Attacks");var sampleType=attacks.GetType().GetElementType();
                for(int slot=0;slot<40;slot++)
                {
                    var shot=Main.projectile[slot];shot.SetDefaults(4);shot.whoAmI=slot;shot.active=true;shot.owner=Main.myPlayer;
                    var sample=Activator.CreateInstance(sampleType,true);Set(sample,"Token",shot);Set(sample,"Owner",shot.owner);Set(sample,"Identity",(int)shot.key);Set(sample,"Type",shot.type);Set(sample,"Tick",Main.GameUpdateCount);Set(sample,"Session",Get(host,"Session"));
                    for(int shape=0;shape<160;shape++)Call(sample,"Rectangle",new Rectangle(320+shape%20*5,400+shape/20*5,4,4),0,false);
                    Require(!(bool)Get(sample,"Overflow") && (int)Get(sample,"Count")==160,"Every dense current sample remains inside its own shape capacity.");attacks.SetValue(sample,slot);
                }
                Call(world,"Prepare");int collision=(int)Get(world,"eventEnd"),total=(int)Get(world,"StrokeCount");
                Require((bool)Get(marker,"Visible") && collision==(path?8192:16384) && total<=16384 && (!path || total>collision),"Full collision presentation cannot consume selected path reservation or constant marker.");
                var visible=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                Set(marker,"Visible",false);var withoutMarker=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Call(world,"Prepare");
                var pieces=(Array)Get(marker,"Pieces");var center=Vector2.Zero;foreach(var piece in pieces)center+=(Vector2)Get(piece,"Position");center/=pieces.Length;
                int changed=0;for(int y=Math.Max(0,(int)center.Y-60);y<Math.Min(640,(int)center.Y+60);y++)for(int x=Math.Max(0,(int)center.X-60);x<Math.Min(960,(int)center.X+60);x++)if(visible[y*960+x]!=withoutMarker[y*960+x])changed++;
                Require(changed>20,"Actual marker pixels remain visible when the collision stroke pool is saturated.");
                if(path)Require(visible.Take(960*450).Where((p,i)=>i%960>180).Any(p=>p.A>0 && p.R>p.G+10 && p.G>p.B+20),"Actual selected approximate path pixels survive the reserved collision half.");
                graphics.Image(Path.Combine(output,path?"marker-capacity-dual.png":"marker-capacity-full.png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                Console.WriteLine("MARKER CAPACITY collision="+collision+" total="+total+" markerPixels="+changed+" path="+path);
                Call(geometry,"Clear");for(int slot=0;slot<40;slot++)Main.projectile[slot].active=false;
            }
            Console.WriteLine("PASS MARKER CAPACITY real XNA full 16384 pool and reserved selected path; synthetic presentation density, no gameplay/FPS claim.");
        }
    }
}
