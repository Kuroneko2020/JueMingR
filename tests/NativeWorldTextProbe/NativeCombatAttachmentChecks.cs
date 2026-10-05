using System;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using System.Collections.Generic;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAttachmentChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");foreach(var n in Main.npc)n.active=false;
            Main.LocalPlayer.position=new Vector2(600,700);Main.dayTime=false;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var owner=Main.npc[0];owner.SetDefaults(113);owner.whoAmI=0;owner.active=true;owner.position=new Vector2(800,900);owner.velocity=new Vector2(3,0);owner.noTileCollide=owner.noGravity=true;
            var eye=Main.npc[2];eye.SetDefaults(114);eye.whoAmI=2;eye.active=true;eye.position=new Vector2(800,owner.Bottom.Y+10);eye.velocity=Vector2.Zero;eye.realLife=0;eye.dontTakeDamage=false;eye.immortal=false;eye.target=0;eye.ai[1]=-1000;
            Main.wofNPCIndex=0;Main.wofDrawAreaTop=800;Main.wofDrawAreaBottom=1200;
            Main.screenPosition=new Vector2(300,650);PlayerInput.CacheOriginalScreenDimensions();var input=Get(context,"Input");Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState(510,(int)(eye.Center.Y-650),0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));Call(host,"SampleMouse");NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);var identity=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");
            Require(path!=null && identity.Slot==2 && identity.Type==114 && ReferenceEquals(identity.Token,eye) && path.Identity.Equals(identity) && path.Count>1 && path[0].Bounds.X==eye.position.X && path[0].Bounds.Y==eye.position.Y,"Actual zero-radius physical mouse selected full eye identity/current body before owner movement.");
            owner.position.X+=3;eye.AI();
            Console.WriteLine("ATTACHMENT114 nativeX="+eye.position.X+" frozenX="+path[1].Bounds.X+" nativeVx="+eye.velocity.X);
            Require(Math.Abs(eye.position.X-path[1].Bounds.X)<.015f && eye.velocity.X==0,"Original 114 assigns updated owner X despite zero eye velocity.");
            foreach(int ownerSlot in new[]{0,3})foreach(int vx in new[]{-3,3})
            {
                foreach(var n in Main.npc)n.active=false;owner=Main.npc[ownerSlot];owner.SetDefaults(113);owner.whoAmI=ownerSlot;owner.active=true;owner.position=new Vector2(800,900);owner.velocity=new Vector2(vx,0);owner.noGravity=owner.noTileCollide=true;owner.direction=vx>0?1:-1;
                eye=Main.npc[2];eye.SetDefaults(114);eye.whoAmI=2;eye.active=true;eye.position=new Vector2(800,owner.Bottom.Y+10);eye.velocity=Vector2.Zero;eye.realLife=ownerSlot;eye.dontTakeDamage=eye.immortal=false;eye.target=0;eye.ai[1]=-1000;
                Main.wofNPCIndex=ownerSlot;PlayerInput.MouseInfo=new MouseState(510,(int)(eye.Center.Y-650),0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(input,"BeginUpdate");Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(host,"SampleMouse");NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);
                Require(path!=null && path.Count==121 && path.Identity.Slot==2 && ReferenceEquals(path.Identity.Token,eye),"Full unique selected eye path before native action, owner slot="+ownerSlot);
                // Owner motion is the explicit observed constant-velocity
                // premise. The child's original AI and native free movement
                // remain independent; do not replay whole boss AI as an oracle.
                for(int future=1;future<=120;future++)
                {
                    if(ownerSlot<2)owner.position+=owner.velocity;eye.AI();Require(eye.noTileCollide,"Locked eye native free movement branch.");eye.position+=eye.velocity;if(ownerSlot>2)owner.position+=owner.velocity;
                    if(future==15 || future==30 || future==60 || future==120)
                    {float error=Vector2.Distance(eye.position,new Vector2(path[future].Bounds.X,path[future].Bounds.Y));Console.WriteLine("ATTACHMENT114 FROZEN ownerSlot="+ownerSlot+" vx="+vx+" future="+future+" error="+error);Require(error<.015f,"Native eye assignment respects before/after slot order and direction reversal.");}
                }
                owner.active=false;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"Inactive position owner cannot retain old eye future.");
                owner.active=true;Main.wofNPCIndex=-1;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"Cleared native singleton is not rescued by child's realLife health relationship.");
            }
            Main.wofNPCIndex=-1;NativeCombatObservationChecks.Save(host,new ObservationOptions());Console.WriteLine("PASS ATTACHMENT114 native direct position vs child velocity.");
        }
    }
}
