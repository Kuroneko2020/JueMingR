using System;

namespace JueMingR.Platform.Combat
{
    // Independent fixed-size values preserve native buff slot order without
    // retaining a live array or allocating an array for every forecast step.
    // Actors with fire or a saturated buff set need this layout: adding a
    // lava burn must also obey native non-debuff eviction eligibility.
    public struct NpcBuffLayout : IEquatable<NpcBuffLayout>
    {
        public bool Captured;
        private long s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11,s12,s13,s14,s15,s16,s17,s18,s19;
        public long this[int index]
        {
            get{switch(index){case 0:return s0;case 1:return s1;case 2:return s2;case 3:return s3;case 4:return s4;case 5:return s5;case 6:return s6;case 7:return s7;case 8:return s8;case 9:return s9;case 10:return s10;case 11:return s11;case 12:return s12;case 13:return s13;case 14:return s14;case 15:return s15;case 16:return s16;case 17:return s17;case 18:return s18;case 19:return s19;default:throw new ArgumentOutOfRangeException(nameof(index));}}
            set{switch(index){case 0:s0=value;break;case 1:s1=value;break;case 2:s2=value;break;case 3:s3=value;break;case 4:s4=value;break;case 5:s5=value;break;case 6:s6=value;break;case 7:s7=value;break;case 8:s8=value;break;case 9:s9=value;break;case 10:s10=value;break;case 11:s11=value;break;case 12:s12=value;break;case 13:s13=value;break;case 14:s14=value;break;case 15:s15=value;break;case 16:s16=value;break;case 17:s17=value;break;case 18:s18=value;break;case 19:s19=value;break;default:throw new ArgumentOutOfRangeException(nameof(index));}}
        }
        public int Type(int index){return (int)(this[index]&0x7fffffff);}
        public int Time(int index){return (int)(this[index]>>32);}
        public bool IsDebuff(int index){return (this[index]&0x80000000L)!=0;}
        public void Set(int index,int type,int time,bool debuff=true){this[index]=((long)time<<32)|(uint)type|(debuff?0x80000000L:0);}
        public bool Equals(NpcBuffLayout b)
        {if(Captured!=b.Captured)return false;if(!Captured)return true;for(int i=0;i<20;i++)if(this[i]!=b[i])return false;return true;}
    }
}

