using System;
using JueMingR.Features.Fishing;
using Terraria;

namespace JueMingR.TerrariaHost.Fishing
{
    internal struct FishingBobber
    {
        internal Projectile Projectile;
        internal int Slot;
        internal int Key;
        internal long Bite;
        internal bool InLiquid {get{return Projectile.wet && !Projectile.shimmerWet && Projectile.ai[0]<1;}}
        internal FishKey? Candidate
        {
            get
            {
                float value=Projectile.localAI[1];
                // Truffle Worm uses 1 as a summon marker, not an item ID.
                if(Projectile.localAI[2]==Terraria.ID.ItemID.TruffleWorm)return null;
                if(Projectile.ai[1]>=0 || float.IsNaN(value) || float.IsInfinity(value) || value==0 || value!=Math.Truncate(value) || value>=int.MaxValue || value<=int.MinValue)return null;
                int id=(int)Math.Abs(value);
                if(value>0 && id>=Terraria.ID.ItemID.Count || value<0 && id>=Terraria.ID.NPCID.Count)return null;
                return new FishKey(value>0?FishKind.Item:FishKind.Npc,id);
            }
        }
    }
    // Demanded reads share a simulation revision; native creation invalidates it.
    // The full native key and reference both protect projectile-slot reuse.
    internal sealed class FishingObservation
    {
        internal readonly FishingBobber[] Bobbers=new FishingBobber[Main.maxProjectiles];
        private readonly Projectile[] previous=new Projectile[Main.maxProjectiles];
        private readonly int[] keys=new int[Main.maxProjectiles];
        private readonly bool[] biting=new bool[Main.maxProjectiles];
        private readonly float[] fish=new float[Main.maxProjectiles];
        private readonly long[] bites=new long[Main.maxProjectiles];
        private uint tick;
        private Player player;
        private bool valid;
        internal int Count {get;private set;}
        internal long Scans {get;private set;}
        internal void Update(Player p,bool demanded)
        {
            if(!demanded){Clear();return;}
            // Discover once when demand resumes, then refresh only while a
            // previously observed native bobber is live. Creation wakes Read
            // through the existing Player.TryUpdateChannel callback, including
            // dry first casts and borrowed/automatic recasts. Empty steady
            // updates inspect no projectile slots and do not clear big arrays.
            bool live=false;
            for(int i=0;i<Count;i++)if(Live(Bobbers[i],p)){live=true;break;}
            if(!valid || !ReferenceEquals(player,p) || live){Read(p);return;}
            if(Count!=0)ClearBobbers();
        }
        internal void Read(Player p)
        {
            if(valid && ReferenceEquals(player,p) && tick==Main.GameUpdateCount)return;
            int oldCount=Count;
            valid=true;player=p;tick=Main.GameUpdateCount;Count=0;Scans++;
            for(int i=0;i<Main.maxProjectiles;i++)
            {
                var b=Main.projectile[i];
                if(b==null || !b.active || !b.bobber || b.owner!=p.whoAmI || (int)b.key==-1){previous[i]=null;biting[i]=false;continue;}
                int key=(int)b.key;bool bite=b.ai[0]<1 && b.ai[1]<0;
                if(!ReferenceEquals(previous[i],b) || keys[i]!=key || bite && (!biting[i] || fish[i]!=b.localAI[1]))bites[i]++;
                previous[i]=b;keys[i]=key;biting[i]=bite;fish[i]=b.localAI[1];
                Bobbers[Count++]=new FishingBobber{Projectile=b,Slot=i,Key=key,Bite=bites[i]};
            }
            if(Count<oldCount)Array.Clear(Bobbers,Count,oldCount-Count);
        }
        internal void Invalidate(){valid=false;}
        private void ClearBobbers(){Array.Clear(Bobbers,0,Count);Count=0;Array.Clear(previous,0,previous.Length);Array.Clear(biting,0,biting.Length);}
        internal void Clear(){if(player==null){valid=false;return;}ClearBobbers();valid=false;player=null;}
        internal static bool Live(FishingBobber b,Player p)
        {return b.Projectile!=null && b.Slot>=0 && b.Slot<Main.maxProjectiles && ReferenceEquals(Main.projectile[b.Slot],b.Projectile) && b.Projectile.active && b.Projectile.bobber && b.Projectile.owner==p.whoAmI && (int)b.Projectile.key==b.Key && b.Key!=-1;}
    }
}
