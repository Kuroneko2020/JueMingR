using System;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatGeometryChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));
            var geometry=Get(host,"Geometry");var shots=(Array)Get(geometry,"Attacks");var npcShapes=(Array)Get(geometry,"Npcs");
            foreach(var n in Main.npc)n.active=false;Main.LocalPlayer.position=new Vector2(640,640);
            // The oracle domain comes from native Sun Dance lengths, not from
            // the observer's output: a missing distal beam must fail this test.
            var sun=Main.projectile[3];sun.SetDefaults(923);sun.active=true;sun.whoAmI=3;sun.owner=255;sun.damage=50;sun.hostile=true;sun.friendly=false;sun.localAI[0]=61;sun.position=new Vector2(300,300);sun.rotation=0;sun.scale=1;
            NativeQuickItemChecks.BeginWorldStep();sun.Damage();
            var sunSample=shots.GetValue(3);Require(sunSample!=null,"natural Sun Dance damage sampled without a victim");
            CheckProjectileBoundary(sun,sunSample,sun.Hitbox,7,"Sun Dance full native range",new Rectangle((int)sun.Center.X-50,(int)sun.Center.Y-50,910,100));
            var p=Main.projectile[4];p.SetDefaults(301);p.active=true;p.whoAmI=4;p.owner=0;p.position=new Vector2(300,300);p.friendly=true;p.damage=10;p.localAI[0]=80;
            // A direct query is not a natural Damage scope and must not enter
            // the observer; then the native Damage call consumes state once.
            int before=(int)Get(geometry,"ProjectileSamples");var explosionBox=p.Damage_GetHitbox();Require((int)Get(geometry,"ProjectileSamples")==before,"unscoped GetHitbox is ignored");
            p.localAI[0]=80;NativeQuickItemChecks.BeginWorldStep();p.Damage();Require(p.localAI[0]==-1,"observer does not consume hitbox side effect twice");
            var eventSamples=(Array)Get(geometry,"Events");
            var sample=eventSamples.GetValue((int)Get(geometry,"EventCount")-1);Require(sample!=null && (int)Get(sample,"Count")>0,"destructive expansion is retained as a transient native event");
            CheckProjectileBoundary(p,sample,explosionBox,4,"expanded explosion");
            p.Damage();Require((int)Get(sample,"Count")>0,"subsequent ordinary Damage cannot erase the consumed one-shot expansion");
            p.SetDefaults(85);p.active=true;p.whoAmI=4;p.owner=0;p.damage=10;p.friendly=true;p.localAI[0]=53;p.Damage();Require((int)Get(shots.GetValue(4),"Count")>0,"damage substep is sampled");p.localAI[0]=54;p.Damage();Require((int)Get(shots.GetValue(4),"Count")==0,"final harmless substep retires earlier sample in the same tick");
            var remote=new Player{active=true,whoAmI=1,position=new Vector2(400,400),direction=1,gravDir=1};Main.player[1]=remote;var sword=new Item();sword.SetDefaults(Terraria.ID.ItemID.CopperBroadsword);remote.inventory[0]=sword;remote.selectedItemState.Select(0);remote.selectedItemState.Update();remote.itemAnimationMax=remote.itemAnimation=20;
            int melee=(int)Get(geometry,"MeleeSamples");remote.AnimatePlayerAndGetItemFrame(0,sword);Require((int)Get(geometry,"MeleeSamples")==melee,"unscoped decoration/dummy animation is not a melee damage sample");remote.ItemCheck();Require((int)Get(geometry,"MeleeSamples")==melee+1 && shots.GetValue(Main.maxProjectiles+1)!=null,"remote natural ItemCheck pose produces a melee sample without owner-only damage");
            remote.active=false;
            foreach(int type in new[]{1115,454,1124,965,452,756,961,1041,1125})
            {
                p=Main.projectile[4];p.SetDefaults(type);p.active=true;p.whoAmI=4;p.owner=0;p.damage=10;p.friendly=true;p.hostile=false;p.ai[0]=type==756 || type==961 || type==1041 || type==1125?-1:0;p.ai[1]=0;if(type==965)p.alpha=65;
                Call(geometry,"BeginDamage",p);Call(geometry,"Projectile",p,p.Hitbox,false);Require((int)Get(shots.GetValue(4),"Count")==0,"native Colliding harmless phase is not an approximate damage box: "+type);
            }
            p.SetDefaults(644);p.active=true;p.whoAmI=4;p.owner=0;p.damage=10;p.friendly=false;p.localAI[0]=1;p.localAI[1]=29;p.position=new Vector2(300,300);int events=(int)Get(geometry,"EventCount");p.AI();p.Damage();Require((int)Get(geometry,"EventCount")==events+1,"natural sentry AI instantaneous damage survives following harmless Damage");
            p.Kill();Require((int)Get(geometry,"EventCount")==events+2,"sentry terminal natural Kill is a distinct second damage event");
            for(int i=0;i<6;i++)
            {
                p=Main.projectile[10+i];p.SetDefaults(632);p.active=true;p.whoAmI=10+i;p.owner=0;p.damage=10;p.friendly=true;p.position=new Vector2(300,300+i*40);p.velocity=Vector2.UnitX;p.localAI[1]=900;p.scale=1;
                p.Damage();var beam=shots.GetValue(10+i);Require(beam!=null && (int)Get(beam,"Count")==2,"each real prism beam keeps its own rectangle and long line");
                CheckProjectileBoundary(p,beam,p.Hitbox,5,"prism beam "+i);
            }
            var gap=new Rectangle(750,(int)Main.projectile[10].Center.Y+20,1,1);for(int i=0;i<6;i++)Require(!Hit(shots.GetValue(10+i),gap) && !Main.projectile[10+i].Colliding(Main.projectile[10+i].Hitbox,gap),"space between actual prism beams stays empty");
            var bubble=Main.npc[0];bubble.SetDefaults(371);bubble.active=true;bubble.whoAmI=0;bubble.position=new Vector2(400,400);Call(geometry,"Npc",bubble);
            var npcSample=npcShapes.GetValue(0);var shapes=(Array)Get(npcSample,"Shapes");Require((int)Get(shapes.GetValue(0),"Category")==4,"attackable bubble is class E despite being NPC and 1 HP");
            bubble.dontTakeDamage=true;bubble.width=bubble.height=100;Call(geometry,"Npc",bubble);Require((int)Get(((Array)Get(npcSample,"Shapes")).GetValue(0),"Category")==3,"unbreakable explosion is harmful class D instead of safe/absent");
            var butcher=Main.npc[1];butcher.SetDefaults(460);butcher.active=true;butcher.whoAmI=1;butcher.position=new Vector2(400,400);butcher.direction=1;Call(geometry,"Npc",butcher);
            Require((int)Get(npcShapes.GetValue(1),"Count")>1,"harm outside attackable body is a separate region");
            for(int x=butcher.Hitbox.Left-3;x<=butcher.Hitbox.Right+32;x++)for(int y=butcher.Hitbox.Top-3;y<=butcher.Hitbox.Bottom+3;y++)
            {var target=new Rectangle(x,y,1,1);var native=butcher.Hitbox;int special=0;float multiplier=1;NPC.GetMeleeCollisionData(target,1,ref special,ref multiplier,ref native);Require(Hit(npcShapes.GetValue(1),target)==native.Intersects(target),"native butcher body/extra harm boundary at "+target);}
            foreach(var n in Main.npc)n.active=false; // Do not confuse vanilla Colliding's normal scratch writes with the display's reads.
            p=Main.projectile[20];p.SetDefaults(Terraria.ID.ProjectileID.BlandWhip);p.active=true;p.whoAmI=20;p.owner=0;p.damage=10;p.friendly=true;p.velocity=Vector2.UnitX;p.ai[0]=10;Main.LocalPlayer.itemAnimationMax=Main.LocalPlayer.itemAnimation=30;
            p.WhipPointsForCollision.Clear();p.WhipPointsForCollision.Add(new Vector2(17,19));Call(geometry,"Projectile",p,p.Hitbox,false);Require(p.WhipPointsForCollision.Count==1 && p.WhipPointsForCollision[0]==new Vector2(17,19),"display extraction uses private whip point lists, never native collision scratch");p.Damage();Require((int)Get(shots.GetValue(20),"Count")>5,"natural whip damage retains separate current/previous point unions (vanilla may itself update CutTiles scratch)");
            CheckProjectileBoundary(p,shots.GetValue(20),p.Damage_GetHitbox(),3,"whip point unions and gaps");
            NativeCombatCoverageChecks.Run(geometry);
            NativeQuickItemChecks.BeginWorldStep();before=(int)Get(geometry,"ProjectileSamples");
            for(int i=0;i<Main.maxProjectiles;i++)
            {var dense=Main.projectile[i];dense.SetDefaults(1);dense.active=true;dense.whoAmI=i;dense.owner=Main.myPlayer;dense.friendly=true;dense.damage=10;dense.position=new Vector2(300+i%40*4,300+i/40*4);dense.Damage();Require((int)Get(shots.GetValue(i),"Count")==1,"dense native callbacks retain exactly one current region per projectile");}
            Require((int)Get(geometry,"ProjectileSamples")-before==Main.maxProjectiles && shots.Length==Main.maxProjectiles+Main.maxPlayers,"full projectile array is observed once per natural callback with fixed capacity");
            Call(geometry,"Clear");Main.LocalPlayer.hostile=true;Main.LocalPlayer.team=0;remote.active=true;remote.hostile=false;
            for(int i=0;i<130;i++)
            {var sentry=Main.projectile[i];sentry.SetDefaults(644);sentry.active=true;sentry.whoAmI=i;sentry.owner=i%2;sentry.damage=10;sentry.localAI[0]=1;sentry.localAI[1]=29;sentry.friendly=false;sentry.AI();}
            Require((int)Get(geometry,"EventCount")==130 && !(bool)Get(geometry,"EventOverflow"),"all 130 native local/remote bursts survive; the old 128 ceiling is not normal coverage");
            NativeQuickItemChecks.BeginWorldStep();Main.projectile[0].localAI[1]=29;Main.projectile[0].AI();Require((int)Get(geometry,"EventCount")==131 && !(bool)Get(geometry,"EventOverflow"),"update without Draw retains unpresented events and adds the new burst");Main.LocalPlayer.hostile=false;remote.active=false;
            for(int age=0;age<5;age++)NativeQuickItemChecks.BeginWorldStep();Call(geometry,"PrepareEvents");Require((int)Get(geometry,"EventCount")==131 && (int)Get(geometry,"EventCount")<=eventSamples.Length && !(bool)Get(geometry,"EventOverflow"),"world updates do not consume presentation; all 131 pending events remain inside fixed capacity without overflow");
            for(int i=0;i<131;i++)Require(!(bool)Get(eventSamples.GetValue(i),"Presented"),"no Draw means no presentation receipt");
            Call(host,"OnSessionEnded");Require((int)Get(geometry,"EventCount")==0,"session exit retires all bounded pending events");for(int i=0;i<shots.Length;i++)Require(shots.GetValue(i)==null,"session exit releases every dense shape sample");Call(host,"OnSessionStarted");
            Console.WriteLine("WORKLOAD dense native geometry: 1000 natural Damage callbacks; all 130 mixed local/remote bursts retained; no-Draw retention and session cleanup verified.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());before=(int)Get(geometry,"ProjectileSamples");for(int i=0;i<600;i++)p.Damage();Require((int)Get(geometry,"ProjectileSamples")==before,"OFF native Damage scopes perform no geometry work");
            Console.WriteLine("PASS first batch geometry: natural hitbox scope, destructive getter once, six prism beams, NPC gameplay categories, extra harm and OFF native callbacks.");
        }
        private static bool Hit(object sample,Rectangle target)
        {
            var shapes=(Array)Get(sample,"Shapes");for(int i=0;i<(int)Get(sample,"Count");i++)
            {var shape=shapes.GetValue(i);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");if((bool)Get(shape,"Line")){float point=0;if(Collision.CheckAABBvLineCollision(new Vector2(target.X,target.Y),new Vector2(target.Width,target.Height),a,b,(float)Get(shape,"Width"),ref point))return true;}else if(new Rectangle((int)a.X,(int)a.Y,(int)(b.X-a.X),(int)(b.Y-a.Y)).Intersects(target))return true;}return false;
        }
        private static void CheckProjectileBoundary(Projectile p,object sample,Rectangle nativeBox,int stride,string name,Rectangle? nativeDomain=null)
        {
            var shapes=(Array)Get(sample,"Shapes");float left=float.MaxValue,top=float.MaxValue,right=float.MinValue,bottom=float.MinValue;
            for(int i=0;i<(int)Get(sample,"Count");i++){var s=shapes.GetValue(i);var a=(Vector2)Get(s,"A");var b=(Vector2)Get(s,"B");float width=(bool)Get(s,"Line")?(float)Get(s,"Width"):0;left=Math.Min(left,Math.Min(a.X,b.X)-width);top=Math.Min(top,Math.Min(a.Y,b.Y)-width);right=Math.Max(right,Math.Max(a.X,b.X)+width);bottom=Math.Max(bottom,Math.Max(a.Y,b.Y)+width);}
            if(nativeDomain.HasValue){var domain=nativeDomain.Value;left=Math.Min(left,domain.Left);top=Math.Min(top,domain.Top);right=Math.Max(right,domain.Right);bottom=Math.Max(bottom,domain.Bottom);}
            int inside=0,outside=0;for(int x=(int)left-3;x<right+4;x+=stride)for(int y=(int)top-3;y<bottom+4;y+=stride){var target=new Rectangle(x,y,1,1);bool native=p.Colliding(nativeBox,target);Require(Hit(sample,target)==native,name+" differs from native Colliding at "+target);if(native)inside++;else outside++;}
            Require(inside>0 && outside>0,name+" covers actual inside and outside points");Console.WriteLine("GEOMETRY ORACLE "+name+" inside="+inside+" outside="+outside);
        }
    }
}
