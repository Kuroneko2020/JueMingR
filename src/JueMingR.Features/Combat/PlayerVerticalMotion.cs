using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Ordinary vertical continuation under sampled equipment/effects and held
    // input. Each step chooses its base from OLD fluid state, then collision
    // produces the next fluid state. It never executes Player.Update/equipment.
    public static class PlayerVerticalMotion
    {
        public static void Parameters(ref PredictionPlayerMotion p,PredictionEnvironment e)
        {
            if(!p.VerticalProfile)return;
            float gravity=p.DefaultGravity,fall=p.PortalPhysics?35:10,speed=5.01f;int height=15;
            if(p.Wet && p.DownDash && !p.Shimmer){gravity*=.85f;fall*=.85f;}
            else if(p.Shimmer){gravity=.15f;height=23;speed=5.51f;}
            else if(p.Wet)
            {
                if(p.Honey){gravity=.1f;fall=3;}
                else if(p.Merman){gravity=.3f;fall=7;}
                else if(p.Trident && !p.Lava){gravity=p.Up?.1f:.25f;fall=p.Up?2:6;height=25;speed=5.51f;}
                else{gravity=.2f;fall=5;height=30;speed=6.01f;}
            }
            if(p.Vortex)gravity=0;
            float world=(float)e.WorldWidth/4200;world*=world;
            if(e.GravityWorldSurface>0)
            {
                float altitude=(float)((double)(p.Y/16-(60+10*world))/(e.GravityWorldSurface/(e.Remix?1.0:6.0)));
                gravity*=Math.Max(e.Remix?.1f:.25f,Math.Min(1,altitude));
            }
            if(p.JumpBoost){height=Math.Max(height,20);speed=Math.Max(speed,6.51f);}
            if(p.WereWolf){height+=2;speed+=.2f;}
            if(p.MoonLordLegs)height++;
            height+=p.JumpHeightExtra;
            // Boost already contains frog/empress/moon and other completed
            // accessory increments. Reapplying those flags doubles the effect.
            speed+=p.JumpSpeedBoost;
            if(p.Mounted){if(p.MountAdditive){height+=p.MountJumpHeight;speed+=p.MountJumpSpeed;}else{height=p.MountJumpHeight;speed=p.MountJumpSpeed;}}
            if(p.Sticky){height/=10;speed/=5;}if(p.Dazed){height/=5;speed/=2;}
            p.Gravity=gravity;p.MaxFall=fall+.01f;p.JumpHeight=height;p.JumpSpeed=speed;
        }
        public static void Step(ref PredictionPlayerMotion p,PredictionEnvironment e)
        {
            Parameters(ref p,e);
            // Native liquid bases precede equipment's merfolk refresh. A
            // newly entered water sample uses its OLD merfolk base this step,
            // then updates swimming and the NEXT step's base.
            if(p.VerticalProfile)
            {
                p.Merman=p.MerfolkEquipment && p.Wet && !p.Lava;
                // Equipment rebuilds this flag every native update. Merfolk
                // and floating are conditional producers, not permanent gear.
                p.Flipper=p.BaseFlipper || p.Merman || p.Wet && p.FloatInWater;
                if(p.Merman)p.ReleaseJump=true;
            }
            if(p.HoldJump)
            {
                if(p.Jump>0)
                {
                    if(p.Vy==0)p.Jump=0;
                    else{p.Vy=-p.JumpSpeed*p.GravityDirection;if(p.Merman && !p.Cart){if(p.SwimTime<=10)p.SwimTime=30;}else p.Jump--;}
                }
                else if((p.Vy==0 || p.Wet && p.Flipper && !p.Cart) && (p.ReleaseJump || p.AutoJump && p.Vy==0))
                {p.Vy=-p.JumpSpeed*p.GravityDirection;p.Jump=p.JumpHeight;if(p.Wet && p.Flipper && p.SwimTime==0)p.SwimTime=30;}
            }
            else p.Jump=0;
            p.ReleaseJump=!p.HoldJump;
            float acceleration=p.Gravity;
            if(p.SlowFall && !p.HoverDown && !p.DownDash)acceleration/=p.HoverUp?10:3;
            p.Vy+=acceleration*p.GravityDirection;
            if(p.Vy*p.GravityDirection>p.MaxFall)p.Vy=p.MaxFall*p.GravityDirection;
            if(p.SlowFall)
            {
                if(!p.HoverDown && p.Vy*p.GravityDirection>p.MaxFall/3)p.Vy=p.MaxFall/3*p.GravityDirection;
                if(p.HoverUp && p.Vy*p.GravityDirection>p.MaxFall/5)p.Vy=p.MaxFall/10*p.GravityDirection;
            }
        }
        public static void AfterFluid(ref PredictionPlayerMotion p,bool wet,bool honey,bool lava,bool shimmer)
        {
            if(p.Wet && !wet && p.WetSlime==0 && p.Jump>p.JumpHeight/5)p.Jump=p.JumpHeight/5;
            p.Wet=wet;p.Honey=honey;p.Lava=lava;p.Shimmer=shimmer;
            if(p.WetSlime>0)p.WetSlime--;
            if(p.SwimTime>0){p.SwimTime--;if(!wet)p.SwimTime=0;}
        }
    }
}
