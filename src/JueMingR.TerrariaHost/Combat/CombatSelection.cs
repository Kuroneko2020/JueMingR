using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Npcs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class CombatSelection
    {
        private readonly NativeNpcObservation npcs;
        internal NpcIdentity Target {get;private set;}
        internal Vector2 RealMouse {get;private set;}
        internal bool HasMouse {get;private set;}
        internal bool HasTarget {get;private set;}
        internal bool ClearLine {get;private set;}
        internal Func<NPC,bool> CandidateAllowed {get;set;}
        internal CombatSelection(NativeNpcObservation npcs){this.npcs=npcs;}
#if DEBUG
        internal int Candidates {get;private set;}
        internal int LineQueries {get;private set;}
#endif
        internal void SampleMouse(HostInputState input)
        {
            if(!input.SampleFocused || Main.GameViewMatrix==null || Main.LocalPlayer==null)return;
            float zoom=Main.GameViewMatrix.RenderZoom.X;
            if(zoom<=0 || float.IsNaN(zoom) || float.IsInfinity(zoom))return;
            // This is the native input formula, before any combat input lease.
            // PhysicalMapX/Y were read once by HostInputState from MouseInfo.
            var center=PlayerInput.OriginalScreenSize*.5f;
            int x=(int)(center.X+(input.PhysicalMapX-center.X)/zoom),y=(int)(center.Y+(input.PhysicalMapY-center.Y)/zoom);
            RealMouse=Main.screenPosition+new Vector2(x,Main.LocalPlayer.gravDir<0?Main.screenHeight-y:y);HasMouse=true;
        }
        internal static bool ProjectileLike(NPC n){return n.type==372 || n.type==373 || n.type>=0 && n.type<NPCID.Sets.ProjectileNPC.Length && NPCID.Sets.ProjectileNPC[n.type];}
        internal static bool Receives(NPC n,bool dummy)
        {
            if(n==null || !n.active || n.life<=0 || n.dontTakeDamage)return false;
            if(n.type==NPCID.TargetDummy)return dummy;
            // Receiving damage is independent of contact damage and homing.
            // In particular, devotees and the vulnerable ritual clone can be
            // struck while damage==0 and chaseable==false.
            return !n.friendly && !n.immortal;
        }
        internal static Rectangle ReceiveBounds(NPC n)
        {
            var box=new Rectangle((int)(n.position.X+n.netOffset.X),(int)(n.position.Y+n.netOffset.Y),n.width,n.height);
            // The tail extension is available to projectiles; melee keeps the
            // body rectangle. Selection uses the union of legal attack areas.
            if(n.type==414)box.Inflate(8,8);return box;
        }
        internal static NpcIdentity Identity(NPC n,long session){return new NpcIdentity(session,n,n.whoAmI,n.generation,n.type,n.netID);}
        internal static bool Valid(NpcIdentity key,long session)
        {return key.Session==session && Main.npc!=null && key.Slot>=0 && key.Slot<Main.npc.Length && Main.npc[key.Slot]!=null && Main.npc[key.Slot].active && Identity(Main.npc[key.Slot],session).Equals(key);}
        internal void Clear(){HasTarget=HasMouse=false;Target=default(NpcIdentity);}
        internal void RetireTarget(){HasTarget=false;Target=default(NpcIdentity);}
        internal void Update(ObservationOptions options,long session,bool select,CombatGeometry geometry)
        {
            var player=Main.LocalPlayer;NpcIdentity prior=Target;RetireTarget();
            if(player==null || !player.active || player.dead)return;
            Vector2 center=options.MouseCenter?RealMouse:player.Center;
            bool canSelect=select && (!options.MouseCenter || HasMouse);
            float radius=options.MouseCenter?options.Radius*16:PlayerRadius(player.Center);
            float best=float.MaxValue;bool bestClear=false;int bestSlot=int.MaxValue;
            for(int i=0;i<npcs.Count;i++)
            {
                var n=npcs.Active(i);if(n==null)continue;
                geometry?.Npc(n);
                if(!canSelect || !Receives(n,options.Dummy))continue;
#if DEBUG
                Candidates++;
#endif
                var box=ReceiveBounds(n);
                float dx=center.X-MathHelper.Clamp(center.X,box.Left,box.Right),dy=center.Y-MathHelper.Clamp(center.Y,box.Top,box.Bottom),distance=dx*dx+dy*dy;
                if(distance>radius*radius)continue;
                if(CandidateAllowed!=null && !CandidateAllowed(n))continue;
                bool clear=false;
                if(options.ClearLine)
                {
#if DEBUG
                    LineQueries++;
#endif
                    // This is a finite current visibility preference. It never
                    // removes a blocked target or claims weapon reachability.
                    clear=Collision.CanHitLine(player.position,player.width,player.height,n.position,n.width,n.height);
                }
                var identity=Identity(n,session);
                bool preferred=!HasTarget || options.ClearLine && clear && !bestClear || (!options.ClearLine || clear==bestClear) &&
                    (distance<best || distance==best && (identity.Equals(prior) || !Target.Equals(prior) && i<bestSlot));
                if(preferred){HasTarget=true;Target=identity;best=distance;bestClear=clear;bestSlot=i;}
            }
            ClearLine=bestClear;
        }
        internal static float PlayerRadius(Vector2 center)
        {
            if(Main.GameViewMatrix==null)return 800;
            var inverse=Matrix.Invert(Main.GameViewMatrix.ZoomMatrix);float far=0;
            for(int i=0;i<4;i++)
            {var p=Vector2.Transform(new Vector2((i&1)==0?0:Main.screenWidth,(i&2)==0?0:Main.screenHeight),inverse);if(Main.LocalPlayer.gravDir<0)p.Y=Main.screenHeight-p.Y;p+=Main.screenPosition;far=Math.Max(far,Vector2.Distance(center,p));}
            return MathHelper.Clamp((float)Math.Ceiling(far/16)+10,50,140)*16;
        }
    }
}
