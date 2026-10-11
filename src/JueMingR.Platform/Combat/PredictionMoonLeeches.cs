using System;

namespace JueMingR.Platform.Combat
{
    // Opaque tokens only identify this sample. No forecast method dereferences
    // a native projectile/player; packed Key is the complete ProjectileKey.
    public struct PredictionMoonLeech
    {
        public readonly object Token,PlayerToken;
        public readonly int Slot,Key,Owner,TimeLeft,ExtraUpdates,Penetrate,PlayerIndex;
        public readonly NpcIdentity HeadIdentity;
        public readonly MotionRect Area,PlayerArea;
        public readonly float Vx,Vy,A0,L0,L1;
        public readonly bool HeadCaptured,PlayerCaptured,PlayerActive,PlayerDead,PlayerGodMode,OwnerActive,SettingsKnown,IgnoreWater,Wet;
        public PredictionMoonLeech(object token,int slot,int key,int owner,int timeLeft,int extraUpdates,int penetrate,bool headCaptured,NpcIdentity headIdentity,bool playerCaptured,object playerToken,int playerIndex,MotionRect playerArea,bool playerActive,bool playerDead,bool playerGodMode,bool ownerActive,MotionRect area,float vx,float vy,float a0,float l0,float l1,bool settingsKnown,bool ignoreWater,bool wet)
        {Token=token;Slot=slot;Key=key;Owner=owner;TimeLeft=timeLeft;ExtraUpdates=extraUpdates;Penetrate=penetrate;HeadCaptured=headCaptured;HeadIdentity=headIdentity;PlayerCaptured=playerCaptured;PlayerToken=playerToken;PlayerIndex=playerIndex;PlayerArea=playerArea;PlayerActive=playerActive;PlayerDead=playerDead;PlayerGodMode=playerGodMode;OwnerActive=ownerActive;Area=area;Vx=vx;Vy=vy;A0=a0;L0=l0;L1=l1;SettingsKnown=settingsKnown;IgnoreWater=ignoreWater;Wet=wet;}
        public bool Same(PredictionMoonLeech b)
        {return ReferenceEquals(Token,b.Token) && ReferenceEquals(PlayerToken,b.PlayerToken) && Slot==b.Slot && Key==b.Key && Owner==b.Owner && TimeLeft==b.TimeLeft && ExtraUpdates==b.ExtraUpdates && Penetrate==b.Penetrate && PlayerIndex==b.PlayerIndex && HeadIdentity.Equals(b.HeadIdentity) && PredictionPlayers.Same(Area,b.Area) && PredictionPlayers.Same(PlayerArea,b.PlayerArea) && Vx==b.Vx && Vy==b.Vy && A0==b.A0 && L0==b.L0 && L1==b.L1 && HeadCaptured==b.HeadCaptured && PlayerCaptured==b.PlayerCaptured && PlayerActive==b.PlayerActive && PlayerDead==b.PlayerDead && PlayerGodMode==b.PlayerGodMode && OwnerActive==b.OwnerActive && SettingsKnown==b.SettingsKnown && IgnoreWater==b.IgnoreWater && Wet==b.Wet;}
    }
    // Immutable input belonging to the same rolling sample/candidate, not a
    // projectile cache or an independently advanced copy of the live world.
    public sealed class PredictionMoonLeeches : IEquatable<PredictionMoonLeeches>
    {
        private readonly PredictionMoonLeech[] values;
        public readonly float Left,Right,Top,Bottom;
        public int Count {get{return values.Length;}}
        public bool Captured {get{return true;}}
        public PredictionMoonLeeches(PredictionMoonLeech[] source,int count,float left,float right,float top,float bottom)
        {if(source==null || count<0 || count>source.Length)throw new ArgumentOutOfRangeException();values=new PredictionMoonLeech[count];Array.Copy(source,values,count);Left=left;Right=right;Top=top;Bottom=bottom;}
        public PredictionMoonLeech this[int index] {get{return values[index];}}
        public bool Equals(PredictionMoonLeeches b)
        {if(b==null || Count!=b.Count || Left!=b.Left || Right!=b.Right || Top!=b.Top || Bottom!=b.Bottom)return false;for(int i=0;i<Count;i++)if(!values[i].Same(b.values[i]))return false;return true;}
    }
}
