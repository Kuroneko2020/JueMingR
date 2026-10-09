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
            foreach(int type in new[]{116,170,171,180,317,318,2,133,190,191,192,193,194})foreach(int side in new[]{-1,1})foreach(int branch in new[]{0,1,2,3})
            {
                Main.dayTime=branch==3;Main.worldSurface=branch==3?60:20;foreach(var npc in Main.npc)npc.active=false;for(int y=35;y<60;y++)Main.tile[50,y].active(branch==1);
                var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(side==1?650:1100,700);n.velocity=new Vector2(side*-.5f,branch==2?2:0);n.target=p.whoAmI;n.direction=-side;n.directionY=-1;n.timeLeft=750;n.ai[0]=branch==1?299:0;n.ai[1]=branch==2?1:0;n.wet=branch==2;n.wetCount=(byte)(branch==2?1:0);n.collideX=branch==2;n.oldVelocity=Vector2.UnitX;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI2 Source real actor has future type="+type);
                n.oldTarget=n.target;n.AI();Call(n,"UpdateCollision");
                Require(Math.Abs(n.position.X-path[1].Bounds.X)<.0001f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.0001f && Math.Abs(n.velocity.X-path[1].Vx)<.0001f && Math.Abs(n.velocity.Y-path[1].Vy)<.0001f,"AI2 before-action full native motor type="+type+" side="+side+" branch="+branch+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Main.dayTime=false;Main.worldSurface=20;for(int y=35;y<60;y++)Main.tile[50,y].active(false);
            Console.WriteLine("PASS AI2 direct104 Source before-action movement/phase/collision/wet controls="+cases);
        }
        internal static void Bats(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            foreach(int type in new[]{48,121,156,158,226,660,49,51,60,62,66,93,137,150,151,152,634})foreach(int side in new[]{-1,1})foreach(int branch in new[]{0,1,2,3})
            {
                Main.dayTime=branch==3;Main.worldSurface=branch==3?60:20;foreach(var npc in Main.npc)npc.active=false;for(int y=35;y<60;y++)Main.tile[50,y].active(branch==1);
                var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(side==1?650:1100,700);n.velocity=new Vector2(-side*.5f,branch==2?2:0);n.target=p.whoAmI;n.direction=-side;n.directionY=-1;n.timeLeft=750;n.ai[1]=branch==0?0:branch==1?199:1000;n.ai[2]=branch==1?-150:0;n.wet=branch==2;n.wetCount=(byte)(branch==2?1:0);n.collideX=branch==2;n.oldVelocity=Vector2.UnitX;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI14 Source actual actor future type="+type);
                n.oldTarget=n.target;n.AI();Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.0001f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.0001f && Math.Abs(n.velocity.X-path[1].Vx)<.0001f && Math.Abs(n.velocity.Y-path[1].Vy)<.0001f,"AI14 native motor type="+type+" side="+side+" branch="+branch+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Main.dayTime=false;Main.worldSurface=20;for(int y=35;y<60;y++)Main.tile[50,y].active(false);Console.WriteLine("PASS AI14 Source direct controls="+cases);
        }
        internal static void BatForms(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;Main.netMode=0;for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
            foreach(int type in new[]{158,159})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(type==158?880:520,type==158?875:850);n.velocity=new Vector2(.5f,0);n.target=p.whoAmI;n.direction=1;n.timeLeft=750;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>20,"AI14 actual Source has form transition prefix type="+type);
                for(int step=1;step<=20;step++)
                {
                    object[] gravity={0f};var method=n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);method.Invoke(n,gravity);float max=(float)gravity[0];n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min(max,n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");
                    var point=path[step];Require(Math.Abs(n.position.X-point.Bounds.X)<.01f && Math.Abs(n.position.Y-point.Bounds.Y)<.01f && n.width==point.Bounds.Width && n.height==point.Bounds.Height,"AI14 frozen20 form and motion type="+type+" step="+step+" actualType="+n.type+" native="+n.position+" model="+point.Bounds.X+","+point.Bounds.Y+" dimensions="+n.width+","+n.height+" predicted="+point.Bounds.Width+","+point.Bounds.Height);
                    if(step==1)Require(n.type==(type==158?159:158),"AI14 known native Transform actually occurred");
                }
                Console.WriteLine("PASS AI14 Source frozen20 same-object form initial="+type+" final="+n.type);
            }
            Main.netMode=1;
        }
        internal static void Vultures(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;Main.netMode=0;
            foreach(int type in new[]{61,301})foreach(int branch in new[]{0,1,2,3,4})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(branch==0?880:650,branch==0?875:700);n.velocity=branch<2?Vector2.Zero:new Vector2(-.5f,0);n.ai[0]=branch>=3?1:0;n.target=p.whoAmI;n.direction=-1;n.timeLeft=750;if(branch==1)n.life--;n.wet=branch==4;n.wetCount=(byte)(branch==4?1:0);n.collideX=branch==4;n.oldVelocity=Vector2.UnitX;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI17 actual Source future type="+type+" branch="+branch);
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");
                Require(Math.Abs(n.position.X-path[1].Bounds.X)<.0001f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.0001f && Math.Abs(n.velocity.X-path[1].Vx)<.0001f && Math.Abs(n.velocity.Y-path[1].Vy)<.0001f,"AI17 before-action launch/flying type="+type+" branch="+branch+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Main.netMode=1;Console.WriteLine("PASS AI17 Source launch injury motion flight wet/collision="+cases);
        }
    }
}
