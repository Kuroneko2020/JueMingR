using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Buff flags use pre-decrement timers. Residual damage is settled even
    // after all buffs expire; expected damage selects a batch threshold, not
    // extra damage. This state is private to the bounded forecast.
    public static class NpcHealth
    {
        internal static bool Step(ref NpcMotionState n,NpcMotionState[] group,int count,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;var h=n.Health;
            bool poison=Tick(ref h.Poison),fire=Tick(ref h.Fire),cursed=Tick(ref h.Cursed),venom=Tick(ref h.Venom),frost=Tick(ref h.Frost),fire3=Tick(ref h.Fire3),frost2=Tick(ref h.Frost2);
            bool slimed=Tick(ref h.Slimed),oil=Tick(ref h.Oil),accelerated=Tick(ref h.Accelerated),wet=Tick(ref h.WetBuff),shimmer=Tick(ref h.ShimmerTicks);
            bool shadow=Tick(ref h.Shadow);
            if(n.Identity.Type==1 && n.A1==9){if(fire)h.Fire=60;if(frost)h.Frost=60;}
            if(h.Buffs.Captured)
            {
                for(int i=0;i<20;i++){int time=h.Buffs.Time(i);if(time>0)h.Buffs.Set(i,h.Buffs.Type(i),time-1,h.Buffs.IsDebuff(i));}
                SetTimer(ref h.Buffs,24,h.Fire);SetTimer(ref h.Buffs,323,h.Fire3);
                if(n.Identity.Type==1 && n.A1==9)SetTimer(ref h.Buffs,44,h.Frost);
            }
            if(wet && !e.Multiplayer)Extinguish(ref h);
            if(h.Buffs.Captured && !e.Multiplayer)for(int i=0;i<20;i++)if(h.Buffs.Type(i)>0 && h.Buffs.Time(i)<=0)Delete(ref h.Buffs,i);
            if(h.Fire==0 && h.Fire3==0 && h.Buffs.Type(19)==0)h.Buffs=default(NpcBuffLayout);
            h.Regen=0;
            if(!h.DontTakeDamage)
            {
                int expected=0,loss=0;
                if(poison)loss+=12;if(venom){loss+=60;expected=15;}
                if(fire && !(n.Identity.Type==1 && n.A1==8 && e.GoodWorld))loss+=Burn(8,slimed,n);
                if(cursed){loss+=Burn(48,slimed,n);expected=Math.Max(expected,10);}
                if(frost){loss+=Burn(16,slimed,n);expected=Math.Max(expected,2);}
                if(fire3){loss+=Burn(30,slimed,n);expected=Math.Max(expected,5);}
                if(frost2){loss+=Burn(50,slimed,n);expected=Math.Max(expected,10);}
                if(shadow){loss+=Burn(30,slimed,n);expected=Math.Max(expected,5);}
                if(accelerated)loss*=2;
                if(oil && (fire || cursed || frost || fire3 || frost2 || shadow)){loss+=50;expected=Math.Max(expected,10);}
                int regen=n.Identity.Type==1 && n.A1==29?16:0;
                if(n.Identity.Type==59 && n.A1==174 && n.Lava)regen=32;
                if(n.Identity.Type==1 && (n.A1==364 || n.A1==365 || n.A1==366 || n.A1==1104 || n.A1==1105 || n.A1==1106))regen=24;
                h.Regen=regen-loss;h.RegenCount+=h.Regen;
                int batch=expected>0?expected:-1;if(batch==-1 && h.RegenCount/-120>1)batch=h.RegenCount/-120;
                while(h.RegenCount>=120){h.RegenCount-=120;if(!h.Immortal)n.Life=Math.Min(n.LifeMax,n.Life+1);}
                int damage=Math.Max(1,batch);
                while(h.RegenCount<=-120*damage)
                {
                    h.RegenCount+=120*damage;
                    if(h.RealLife>=0 && h.RealLife!=n.Identity.Slot)
                    {
                        int root=-1;for(int i=0;i<count;i++)if(group[i].Identity.Slot==h.RealLife && group[i].Active){root=i;break;}
                        if(root<0){stop=PredictionStop.MissingDependency;return false;}
                        var owner=group[root];if(!Hurt(ref owner,damage,e.Multiplayer,out stop))return false;group[root]=owner;
                    }
                    else if(!Hurt(ref n,damage,e.Multiplayer,out stop)){n.Health=h;return false;}
                }
            }
            if(shimmer)
            {
                h.ShimmerTransparency+=.01f;
                if(!e.Multiplayer && h.ShimmerAction && (double)h.ShimmerTransparency>.9){stop=PredictionStop.PhaseBoundary;return false;}
                h.ShimmerTransparency=Math.Min(1,h.ShimmerTransparency);
            }
            else{if(n.JustHit)h.ShimmerTransparency-=.1f;h.ShimmerTransparency=Math.Max(0,h.ShimmerTransparency-(h.ShimmerImmune?.015f:.001f));}
            if(h.Immune255>0)h.Immune255--;n.Health=h;return true;
        }
        private static bool Tick(ref int timer){bool active=timer>0;if(active)timer--;return active;}
        private static void SetTimer(ref NpcBuffLayout layout,int type,int time)
        {for(int i=0;i<20;i++)if(layout.Type(i)==type){layout.Set(i,type,time,layout.IsDebuff(i));return;}}
        private static void Delete(ref NpcBuffLayout layout,int index)
        {
            layout[index]=0;
            // Native DelBuff compacts all slots, but its caller still advances
            // i. Adjacent 24/323 therefore survive different water passes.
            for(int i=0;i<19;i++)if(layout.Time(i)==0 || layout.Type(i)==0)for(int j=i+1;j<20;j++){layout[j-1]=layout[j];layout[j]=0;}
        }
        public static void Extinguish(ref NpcHealthState h)
        {
            if(!h.Buffs.Captured){h.Fire=h.Fire3=0;return;}
            for(int i=0;i<20;i++)if(h.Buffs.Type(i)==24 || h.Buffs.Type(i)==323)Delete(ref h.Buffs,i);
            h.Fire=h.Fire3=0;for(int i=0;i<20;i++){if(h.Buffs.Type(i)==24)h.Fire=h.Buffs.Time(i);if(h.Buffs.Type(i)==323)h.Fire3=h.Buffs.Time(i);}
            if(h.Fire==0 && h.Fire3==0 && h.Buffs.Type(19)==0)h.Buffs=default(NpcBuffLayout);
        }
        public static void ApplyFire(ref NpcHealthState h,int duration)
        {
            int timer=Math.Max(h.Fire,duration);
            if(!h.Buffs.Captured){h.Fire=timer;return;}
            for(int i=0;i<20;i++)if(h.Buffs.Type(i)==24){h.Fire=timer;h.Buffs.Set(i,24,timer);return;}
            int removable=-1;for(int i=0;i<20;i++)if(!h.Buffs.IsDebuff(i)){removable=i;break;}
            if(removable<0)return; // Native AddBuff cannot evict a full debuff set.
            int empty=-1;for(int i=removable;i<20;i++)if(h.Buffs.Type(i)==0){empty=i;break;}
            if(empty<0){Delete(ref h.Buffs,removable);for(int i=0;i<20;i++)if(h.Buffs.Type(i)==0){empty=i;break;}}
            if(empty>=0){h.Fire=timer;h.Buffs.Set(empty,24,timer);}
        }
        private static int Burn(int loss,bool slimed,NpcMotionState n){return loss*(slimed?2:1)+(n.Identity.Type==1 && n.A1==9?16:0);}
        private static bool Hurt(ref NpcMotionState n,int amount,bool multiplayer,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(n.Health.Immortal)return true;n.Life-=amount;if(n.Life>0)return true;n.Life=1;if(multiplayer)return true;
            int type=n.Identity.Type;
            // Vanilla checkDead keeps these actors alive in a new phase. The
            // current model does not own that transition or its spawned parts.
            if(type==396 || type==397 || type==398 && n.A0!=2 || (type==517 || type==422 || type==507 || type==493) && n.A2!=1 || type==548 && n.A1!=1)
            {stop=PredictionStop.PhaseBoundary;return false;}
            n.Active=false;stop=PredictionStop.Despawn;return false;
        }
    }
}
