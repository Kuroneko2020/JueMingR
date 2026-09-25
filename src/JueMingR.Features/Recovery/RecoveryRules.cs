using System;

namespace JueMingR.Features.Recovery
{
    public static class RecoveryRules
    {
        // Modes are persisted: 0 off, 1 quick, 2 smart. Widen BEFORE arithmetic:
        // the one-way gate never excludes an undersized potion (D >= H).
        public static long LifeScore(int mode, int deficit, int maximum, int healing)
        {
            if (mode < 1 || mode > 2 || deficit <= 0 || maximum <= 0 || healing <= 0) return -1;
            long d=deficit, h=healing;
            if (mode==1) return Math.Abs(h-d);
            if (d<h && 2*d<=Math.Min(h,maximum)) return -1;
            return Math.Max(h-d,0)+2*Math.Max(d-h,0);
        }
        public static bool BetterLife(int mode,int deficit,int maximum,int healing,int previous)
        {
            long next=LifeScore(mode,deficit,maximum,healing), old=LifeScore(mode,deficit,maximum,previous);
            if(next<0)return false;
            if(old<0 || next<old)return true;
            if(next>old)return false;
            return mode==1 ? healing>previous : Math.Max((long)deficit-healing,0)<Math.Max((long)deficit-previous,0);
        }
    }
}
