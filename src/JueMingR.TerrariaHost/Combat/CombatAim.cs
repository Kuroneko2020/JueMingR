using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum CombatAimStage { ItemRelease, FlailRelease, FlintCharge, GlacierCharge }
    // A future solver may answer this exact current request with an already
    // prepared point. This seam performs no targeting or prediction. Other
    // item owners never call it and retain their own tile/capture/cast targets.
    internal sealed class CombatAimRequest
    {
        internal readonly Player Player;
        internal readonly Item Weapon;
        internal readonly Projectile Projectile;
        internal readonly long Session,Selection,Operation;
        internal readonly int Slot,Type,Prefix,ProjectileKey;
        internal readonly uint Step;
        internal readonly CombatAimStage Stage;
        internal CombatAimRequest(Player player,Item weapon,Projectile projectile,long session,long selection,long operation,int slot,CombatAimStage stage)
        {Player=player;Weapon=weapon;Projectile=projectile;Session=session;Selection=selection;Operation=operation;Slot=slot;Type=weapon.type;Prefix=weapon.prefix;ProjectileKey=projectile==null?0:(int)projectile.key;Step=Main.GameUpdateCount;Stage=stage;}
    }
    internal sealed class CombatAimPoint
    {
        internal readonly CombatAimRequest Request;
        internal readonly Vector2 World;
        internal CombatAimPoint(CombatAimRequest request,Vector2 world){Request=request;World=world;}
    }
    internal sealed class CombatAim
    {
        internal Func<CombatAimRequest,CombatAimPoint> Provider {get;set;}
        internal bool TryPoint(CombatAimRequest request,out Vector2 point)
        {
            point=default(Vector2);var provider=Provider;if(provider==null)return false;
            CombatAimPoint result;
            try{result=provider(request);}catch{return false;}
            if(result==null || !ReferenceEquals(request,result.Request) || request.Step!=Main.GameUpdateCount ||
                float.IsNaN(result.World.X) || float.IsInfinity(result.World.X) || float.IsNaN(result.World.Y) || float.IsInfinity(result.World.Y))return false;
            point=result.World;return true;
        }
    }
}
