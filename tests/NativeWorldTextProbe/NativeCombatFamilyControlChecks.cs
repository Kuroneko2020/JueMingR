using System;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFamilyControlChecks
    {
        internal static void Eyes(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            foreach(int type in new[]{116,170,171,180,317,318,2,133,190,191,192,193,194})foreach(int side in new[]{-1,1})foreach(int branch in new[]{0,1,2})
            {
                foreach(var npc in Main.npc)npc.active=false;for(int y=35;y<60;y++)Main.tile[50,y].active(branch==1);
                var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(side==1?650:1100,700);n.velocity=new Vector2(side*-.5f,branch==2?2:0);n.target=p.whoAmI;n.direction=-side;n.directionY=-1;n.timeLeft=750;n.ai[0]=branch==1?299:0;n.ai[1]=branch==2?1:0;n.wet=branch==2;n.wetCount=(byte)(branch==2?1:0);n.collideX=branch==2;n.oldVelocity=Vector2.UnitX;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI2 Source real actor has future type="+type);
                n.oldTarget=n.target;n.AI();Call(n,"UpdateCollision");
                Require(Math.Abs(n.position.X-path[1].Bounds.X)<.0001f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.0001f && Math.Abs(n.velocity.X-path[1].Vx)<.0001f && Math.Abs(n.velocity.Y-path[1].Vy)<.0001f,"AI2 before-action full native motor type="+type+" side="+side+" branch="+branch+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);cases++;
            }
            for(int y=35;y<60;y++)Main.tile[50,y].active(false);
            Console.WriteLine("PASS AI2 direct78 Source before-action movement/phase/collision/wet controls="+cases);
        }
        internal static void Bats(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            foreach(int type in new[]{48,121,156,158,226,660,49,51,60,62,66,93,137,150,151,152,634})foreach(int side in new[]{-1,1})foreach(int branch in new[]{0,1,2})
            {
                foreach(var npc in Main.npc)npc.active=false;for(int y=35;y<60;y++)Main.tile[50,y].active(branch==1);
                var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(side==1?650:1100,700);n.velocity=new Vector2(-side*.5f,branch==2?2:0);n.target=p.whoAmI;n.direction=-side;n.directionY=-1;n.timeLeft=750;n.ai[1]=branch==0?0:branch==1?199:1000;n.ai[2]=branch==1?-150:0;n.wet=branch==2;n.wetCount=(byte)(branch==2?1:0);n.collideX=branch==2;n.oldVelocity=Vector2.UnitX;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI14 Source actual actor future type="+type);
                n.oldTarget=n.target;n.AI();Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.0001f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.0001f && Math.Abs(n.velocity.X-path[1].Vx)<.0001f && Math.Abs(n.velocity.Y-path[1].Vy)<.0001f,"AI14 native motor type="+type+" side="+side+" branch="+branch+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);cases++;
            }
            for(int y=35;y<60;y++)Main.tile[50,y].active(false);Console.WriteLine("PASS AI14 Source direct controls="+cases);
        }
    }
}
