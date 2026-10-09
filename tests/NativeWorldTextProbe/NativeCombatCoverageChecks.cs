using System;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // This is an isolated native oracle. The search domain is prescribed by
    // native mechanisms, before reading the observer's shapes. In particular,
    // neither a missing beam nor an empty sample can shrink the examination.
    internal static class NativeCombatCoverageChecks
    {
        internal static void Run(object geometry)
        {
            int[] types={76,77,78,85,121,122,123,124,125,126,294,301,452,454,455,461,464,466,537,554,580,597,598,607,611,614,623,632,636,642,661,684,686,687,697,698,699,707,711,756,758,802,842,871,872,877,878,879,919,923,927,932,933,938,939,940,941,942,943,944,945,961,963,965,973,974,985,1041,1093,1100,1106,1112,1115,1116,1117,1118,1124,1125,1127,688,689,690,972,982,983,984,997,1043,1126,1091,1122,163};
            Array.Resize(ref types,types.Length+22);int extraIndex=types.Length-22;
            foreach(int whip in new[]{847,841,848,849,912,913,914,915,952,1028,1029,1030,1031,1032,1033,1034,1035,1104,25,46,105,153})types[extraIndex++]=whip;
            var samples=(Array)Get(geometry,"Attacks");
            // Boundary points are independent of the rendered result; coarse
            // spatial grids alone miss integer rounding at satellite edges.
            var satellite=Main.projectile[97];satellite.SetDefaults(464);satellite.active=true;satellite.whoAmI=97;satellite.owner=Main.myPlayer;satellite.friendly=true;satellite.damage=25;satellite.Center=new Vector2(500,500);satellite.velocity=Vector2.UnitX;satellite.ai[0]=22;satellite.ai[1]=0;
            var satelliteBox=satellite.Damage_GetHitbox();Call(geometry,"BeginDamage",satellite);Call(geometry,"Projectile",satellite,satelliteBox,false);
            foreach(var target in new[]{new Rectangle(514,140,1,1),new Rectangle(484,140,1,1)})Require(satellite.Colliding(satelliteBox,target)==Hit(samples.GetValue(97),target,satellite),"464 satellite integer edge "+target);
            foreach(int type in types)
            {
                var p=Main.projectile[97];p.SetDefaults(type);p.active=true;p.whoAmI=97;p.owner=Main.myPlayer;p.position=new Vector2(700.25f,700.75f);p.damage=25;p.friendly=true;p.hostile=false;p.npcProj=p.trap=false;p.scale=1.3f;p.alpha=0;p.velocity=new Vector2(.8f,.6f);p.rotation=.43f;p.direction=p.spriteDirection=-1;
                p.ai[0]=type==923?0:type==871?0:65;p.ai[1]=1;p.localAI[0]=65;p.localAI[1]=240;
                if(type==623 || type==758 || type==1093 || type==1112 || type==1118 || type==963)p.ai[0]=2;
                if(type==687)p.ai[0]=40;
                if(type==464)p.ai[1]=0;
                if(type==121 || type==122 || type==123 || type==124 || type==125 || type==126 || type==597)
                {var features=new Projectile.GemStaffFeatures(0){FastThenSlow=true,BiggerHitbox=true};p.ai[0]=features.Bits;}
                if(type==933 || type==1100)p.localAI[0]=40;
                if(p.aiStyle==190){p.ai[0]=-1;p.ai[1]=20;p.localAI[0]=7;}
                if(p.aiStyle==15)p.ai[0]=0;
                if(type==46 || type==105 || type==153){Main.LocalPlayer.itemAnimationMax=30;Main.LocalPlayer.itemAnimation=17;Main.LocalPlayer.meleeSpeed=.8f;}
                if(p.aiStyle==137){p.width=p.height=240;p.localAI[0]=1;}
                if(Terraria.ID.ProjectileID.Sets.IsAWhip[type]){p.ai[0]=10;Main.LocalPlayer.itemAnimationMax=Main.LocalPlayer.itemAnimation=30;}
                for(int k=0;k<p.oldPos.Length;k++){p.oldPos[k]=p.position+new Vector2(k*3,-k*2);p.oldRot[k]=k*.13f;}
                if(p.aiStyle==203)
                {p.localAI[0]=1;var points=new Vector2[251];for(int k=0;k<points.Length;k++)points[k]=p.Center+new Vector2(k*2,-k);p.customHitbox=new Terraria.DataStructures.MultiPointHitbox(new Point(16,16),points);}
                var box=p.Damage_GetHitbox();Call(geometry,"BeginDamage",p);Call(geometry,"Projectile",p,box,false);
                object sample=samples.GetValue(97);Require(sample!=null,"geometry sample exists for "+type);
                int matches=0,missed=0,extra=0;
                // 1x1, player-size, and a large receiver exercise native target
                // size transforms and cone/line cases that point-only tests miss.
                foreach(var size in new[]{new Point(1,1),new Point(20,42),new Point(120,90)})
                    for(int x=32;x<1760;x+=19)for(int y=32;y<1510;y+=19)
                    {
                        var target=new Rectangle(x,y,size.X,size.Y);bool native=p.Colliding(box,target),actual=Hit(sample,target,p);
                        if(native)matches++;if(native && !actual)missed++;if(!native && actual)extra++;
                        Require(native==actual,"native coverage type="+type+" style="+p.aiStyle+" size="+size+" target="+target+" native="+native+" observed="+actual+" shapeCount="+Get(sample,"Count"));
                    }
                Require(matches>0,"independent domain contains an active native area for "+type);
                Console.WriteLine("COVERAGE native type="+type+" inside="+matches+" missed="+missed+" extra="+extra);
            }
            Variants(geometry);
        }
        private static void Variants(object geometry)
        {
            var p=Main.projectile[97];var samples=(Array)Get(geometry,"Attacks");int variants=0;
            foreach(int type in new[]{1028,1035,25,121,597,85,1106,661})foreach(float scale in new[]{.5f,1f,2f})foreach(int phase in new[]{0,1,2,3})
            {
                p.SetDefaults(type);p.active=true;p.whoAmI=97;p.owner=Main.myPlayer;p.damage=25;p.friendly=true;p.hostile=false;p.position=new Vector2(700.25f,700.75f);p.velocity=new Vector2(-.8f,.6f);p.scale=scale;p.rotation=.7f;p.direction=-1;
                Main.LocalPlayer.itemAnimationMax=Main.LocalPlayer.itemAnimation=30;p.ai[0]=phase*8+1;p.ai[1]=1;p.localAI[0]=53;p.localAI[1]=240;
                if(type==25)p.ai[0]=0;
                if(type==121 || type==597)p.ai[0]=new Projectile.GemStaffFeatures(0){FastThenSlow=(phase&1)!=0,BiggerHitbox=(phase&2)!=0}.Bits;
                for(int k=0;k<p.oldPos.Length;k++){p.oldPos[k]=p.position+new Vector2(k*8,-k*5);p.oldRot[k]=k*.13f;}
                bool wall=(phase&1)!=0;for(int y=28;y<72;y++){Main.tile[50,y].active(wall);Main.tile[50,y].type=Terraria.ID.TileID.Stone;}
                try
                {
                    var box=p.Damage_GetHitbox();Call(geometry,"BeginDamage",p);Call(geometry,"Projectile",p,box,false);var sample=samples.GetValue(97);
                    foreach(var size in new[]{new Point(1,1),new Point(20,42),new Point(120,90)})for(int x=128;x<1640;x+=31)for(int y=128;y<1640;y+=31)
                    {var target=new Rectangle(x,y,size.X,size.Y);Require(p.Colliding(box,target)==Hit(sample,target,p),"variant independent domain type="+type+" phase="+phase+" scale="+scale+" wall="+wall+" target="+target);}
                    variants++;
                }
                finally{for(int y=28;y<72;y++)Main.tile[50,y].active(false);}
            }
            Console.WriteLine("COVERAGE additional "+variants+" native variants: whip clocks/scales, flail ellipse, four Gem feature combinations and real LOS walls; independent three-size domains.");
        }
        internal static bool Hit(object sample,Rectangle target,Projectile source=null)
        {
            var shapes=(Array)Get(sample,"Shapes");
            for(int i=0;i<(int)Get(sample,"Count");i++)
            {
                var shape=shapes.GetValue(i);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");float width=(float)Get(shape,"Width");int kind=(int)Get(shape,"Kind"),condition=(int)Get(shape,"Condition");
                var victim=target;if(condition==4 && victim.Width>8 && victim.Height>8)victim.Inflate(-victim.Width/8,-victim.Height/8);
                if((bool)Get(shape,"HasBounds") && !((Rectangle)Get(shape,"Bounds")).Intersects(victim))continue;
                bool hit;
                if((bool)Get(shape,"Line")){float point=0;hit=Collision.CheckAABBvLineCollision(victim.TopLeft(),victim.Size(),a,b,width,ref point);}
                else if(kind==1)hit=victim.Distance(a)<width;
                else if(kind==6)hit=(victim.ClosestPointInRect(a)-a).Length()<=width;
                else if(kind==7)hit=Utils.LineRectangleDistance(victim,a,b)<=width;
                else if(kind==2){var offset=victim.ClosestPointInRect(a)-a;offset.Y/=offset.Y>0?b.Y:b.X;hit=offset.Length()<=width;}
                else if(kind==3)hit=victim.IntersectsConeFastInaccurate(a,width,b.X,b.Y);
                else if(kind==4)hit=victim.IntersectsConeSlowMoreAccurate(a,width,b.X,b.Y);
                else if(kind==5)
                {
                    var points=(Vector2[])Get(sample,"Points");var ps=(Point)Get(sample,"PointSize");victim.Inflate(ps.X/2,ps.Y/2);hit=false;
                    if(((Rectangle)Get(sample,"PointBounds")).Intersects(victim))for(int k=0;k<(int)Get(sample,"PointCount");k++)if(victim.Contains(points[k].ToPoint())){hit=true;break;}
                }
                else hit=new Rectangle((int)a.X,(int)a.Y,(int)(b.X-a.X),(int)(b.Y-a.Y)).Intersects(victim);
                if(!hit)continue;
                var sight=(Vector2)Get(shape,"Sight");
                if(condition==1 && !Collision.CanHit(sight,0,0,victim.Center.ToVector2(),0,0))continue;
                if(condition==2 && !Collision.CanHit(a,(int)(b.X-a.X),(int)(b.Y-a.Y),victim.TopLeft(),victim.Width,victim.Height))continue;
                if(condition==3 && (Vector2.Distance((a+b)/2,victim.Center.ToVector2())>500 || !Collision.CanHitLine((a+b)/2,0,0,victim.Center.ToVector2(),0,0)))continue;
                if(condition==5 && !AuraHit(sight,victim.Center.ToVector2()) && !AuraHit(sight,victim.TopLeft()+new Vector2(victim.Width/2,0)))continue;
                if(condition==6 && !(Vector2.Distance(a,victim.Center.ToVector2())<500 && Utils.PlotLine(a.ToTileCoordinates(),victim.Center.ToVector2().ToTileCoordinates(),(x,y)=>WorldGen.InWorld(x,y) && Main.tile[x,y].liquid!=0)))continue;
                return true;
            }
            return false;
        }
        private static bool AuraHit(Vector2 origin,Vector2 target)
        {
            // Evaluate from captured sight origin, never from a mutable source
            // projectile. The comparison side remains native Colliding.
            if(WorldGen.SolidTile((int)target.X/16,(int)target.Y/16))return false;
            bool Clear(Vector2 start,Vector2 end){return Collision.CanHitLine(start,0,0,end,0,0);}
            if(Clear(origin,target))return true;var delta=target-origin;var normal=delta.SafeNormalize(Vector2.UnitY);var middle=Vector2.Lerp(origin,target,.5f);
            var a=middle+normal.RotatedBy(1.5707963705062866)*delta.Length()*.2f;if(Clear(origin,a) && Clear(a,target))return true;
            var b=middle+normal.RotatedBy(-1.5707963705062866)*delta.Length()*.2f;return Clear(origin,b) && Clear(b,target);
        }
    }
}
