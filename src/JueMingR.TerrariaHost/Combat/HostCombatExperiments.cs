using System;
using JueMingR.Features.Combat;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Hidden experiments own live mutations. Aim remains an input borrower;
    // neither forecast clones nor prediction workers install this live owner.
    internal sealed partial class HostCombatExperiments : IRuntimeFeature,F5.ICombatExperimentControls,IDisposable
    {
        internal static readonly string[] Actions={"combat.hard-lock","combat.fast-fire","combat.projectile-through-wall"};
        internal static readonly string[] Names={"强锁模式","加快射速","子弹穿墙"};
        internal readonly CombatExperimentSettings Settings;
        private readonly HostCombat combat;
        private readonly HostCombatObservation observation;
        private readonly HiddenFeatureAccess hidden;
        private readonly bool[] ready=new bool[3];
        private readonly Exception[] errors=new Exception[3];
        private readonly bool[] reported=new bool[3];
        private int acceptedMask;
        private bool disposed;
        [ThreadStatic]private static HostCombatExperiments worldOwner;
        [ThreadStatic]private static int worldDepth;
        private readonly Projectile[] syncObjects=new Projectile[Main.maxProjectiles];
        private readonly int[] syncTypes=new int[Main.maxProjectiles];
        private readonly int[] syncKeys=new int[Main.maxProjectiles];
        private readonly long[] syncTicks=new long[Main.maxProjectiles];
        private readonly Vector2[] syncDirections=new Vector2[Main.maxProjectiles];
        internal HostCombatExperiments(string directory,HostCombat combat,HostCombatObservation observation,HiddenFeatureAccess hidden)
        {
            this.combat=combat;this.observation=observation;this.hidden=hidden;
            Settings=new CombatExperimentSettings(new AtomicFileDocument(System.IO.Path.Combine(directory,"JueMingRData","config","features","combat-experiments.json"),65536));
            combat.Experiments=this;observation.Experiments=this;
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        private bool Access {get{return !disposed && hidden!=null && hidden.IsUnlocked;}}
        internal int EffectiveMask
        {
            get
            {
                if(!Access || !Settings.CanRun || !combat.Runtime.IsSessionActive || !LocalPlayer(combat.Player))return 0;
                int mask=0;for(int i=0;i<3;i++)if(ready[i] && Settings.Value.Enabled(i))mask|=1<<i;return mask;
            }
        }
        internal bool HardLockEffective {get{return (EffectiveMask&1)!=0;}}
        internal bool FastFireEffective {get{return (EffectiveMask&2)!=0;}}
        internal bool ThroughWallEffective {get{return (EffectiveMask&4)!=0;}}
        public bool CanConfigure(int field){return field>=0 && field<3 && Access && combat.Runtime.IsSessionActive && Settings.Ready && ready[field];}
        public bool IsEnabled(int field){return field>=0 && field<3 && (EffectiveMask&(1<<field))!=0;}
        public string Unavailable(int field)
        {
            if(field<0 || field>=3 || !Access)return "隐藏战斗功能尚未解锁。";
            if(!ready[field])return Names[field]+"暂不可用，设置已保留；需要重新进入游戏。";
            if(!Settings.Loaded)return "正在读取隐藏战斗设置。";
            if(!Settings.CanRun)return Settings.Message??"隐藏战斗设置暂不可用，原文件已保留。";
            if(!combat.Runtime.IsSessionActive || !LocalPlayer(combat.Player))return "当前无法使用战斗功能，设置已保留。";
            return null;
        }
        public void Set(int field,bool enabled){if(CanConfigure(field) && Settings.Value.Enabled(field)!=enabled)Settings.Set(Settings.Value.WithEnabled(field,enabled));}
        public bool Enabled {get{return EffectiveMask!=0;}}
        internal void Poll(){Settings.Poll();AcceptModeEdge();}
        internal void AcceptModeEdge()
        {
            int mask=EffectiveMask;if(mask==acceptedMask)return;acceptedMask=mask;
            observation.Attack?.RetireOriginalPrediction();
        }
        public void Update(ulong tick){}
        public void OnSessionStarted(){ClearTransient();AcceptModeEdge();}
        public void OnSessionEnded(){ClearTransient();AcceptModeEdge();}
        public void FailClosed(){for(int i=0;i<3;i++)ready[i]=false;ClearTransient();AcceptModeEdge();}
        private void ClearTransient()
        {
            timing=default(TimingReceipt);Array.Clear(syncObjects,0,syncObjects.Length);Array.Clear(syncTicks,0,syncTicks.Length);
            if(ReferenceEquals(worldOwner,this)){worldOwner=null;worldDepth=0;}
        }
        internal struct WorldScope {internal HostCombatExperiments Prior;internal int Depth;}
        internal WorldScope BeginWorld()
        {
            var scope=new WorldScope{Prior=worldOwner,Depth=worldDepth};
            if(EffectiveMask!=0){worldOwner=this;worldDepth++;}return scope;
        }
        internal static void EndWorld(WorldScope scope){worldOwner=scope.Prior;worldDepth=scope.Depth;}
        private bool Live(Projectile shot)
        {
            return ReferenceEquals(worldOwner,this) && worldDepth>0 && LocalPlayer(combat.Player) && shot!=null && shot.active &&
                shot.whoAmI>=0 && Main.projectile!=null && shot.whoAmI<Main.projectile.Length && ReferenceEquals(Main.projectile[shot.whoAmI],shot) &&
                shot.owner==Main.myPlayer && !shot.hostile && shot.type>0 && shot.type<ProjectileID.Count;
        }
        private static bool LocalPlayer(Player player)
        {return player!=null && player.active && !player.dead && !player.ghost && player.whoAmI==Main.myPlayer && Main.player!=null && player.whoAmI>=0 && player.whoAmI<Main.player.Length && ReferenceEquals(Main.player[player.whoAmI],player);}
        private bool AttackBody(Projectile p)
        {
            // Summon bodies often disable friendly between attack states. Their
            // real owner/category still authorizes attack LOS and movement.
            return Live(p) && !p.bobber && p.aiStyle!=7 && p.type!=651 && !ProjectileID.Sets.IsAGolfBall[p.type] &&
                (p.minion || p.sentry || p.friendly && p.damage>0) &&
                (!Main.projPet[p.type] || p.minion || p.sentry) && !ProjectileID.Sets.LightPet[p.type];
        }
        internal bool Wall(Projectile p){return ThroughWallEffective && AttackBody(p);}
        internal static bool FreeFlight(Projectile p)
        {
            if(p==null || p.damage<=0 || !p.friendly || p.hostile || p.minion || p.sentry || p.bobber || p.ownerHitCheck || p.aiStyle==7 || Main.projPet[p.type] || ProjectileID.Sets.IsAWhip[p.type])return false;
            // Native constrained motion: returning weapons, anchored/held beams,
            // yoyo/rope, spear/whip and world-placeable controllers own velocity.
            switch(p.aiStyle)
            {
                case 3:case 4:case 10:case 15:case 19:case 20:case 26:case 37:case 38:case 53:case 62:case 67:
                case 75:case 84:case 93:case 99:case 100:case 123:case 130:case 134:case 137:case 138:case 148:case 190:case 191:case 194:return false;
            }
            return p.type!=633 && p.type!=632 && p.type!=460 && p.type!=461 && p.type!=537 && p.type!=642 && p.type!=651 &&
                !HostHeldAttack.Handles(p.type) && !HostDirectionalControl.Handles(p.type) && !HostGuidedAttack.Handles(p.type);
        }
        internal void Guide(Projectile p)
        {
            if(!HardLockEffective || !AttackBody(p) || !FreeFlight(p))return;
            var selection=observation.Selection;
            if(!selection.HasTarget || !CombatSelection.Valid(selection.Target,observation.Session))return;
            var npc=Main.npc[selection.Target.Slot];if(!CombatSelection.Receives(npc,observation.Options.Dummy))return;
            Vector2 next;if(!TryDirection(p.velocity,npc.Center+npc.netOffset-p.Center,out next))return;
            var previous=p.velocity;p.velocity=next;
            // Native bullets/arrows use velocity for their travel-facing sprite.
            if(p.aiStyle==1){p.rotation=next.ToRotation()+(float)Math.PI/2;p.direction=next.X>=0?1:-1;}
            int slot=p.whoAmI,key=(int)p.key;long now=Main.GameUpdateCount;
            if(!ReferenceEquals(syncObjects[slot],p) || syncKeys[slot]!=key || syncTypes[slot]!=p.type){syncObjects[slot]=p;syncKeys[slot]=key;syncTypes[slot]=p.type;syncTicks[slot]=now-6;syncDirections[slot]=previous;}
            if(now-syncTicks[slot]>=6 && Vector2.DistanceSquared(syncDirections[slot].SafeNormalize(Vector2.Zero),next.SafeNormalize(Vector2.Zero))>.0001f)
            {p.netUpdate=true;syncTicks[slot]=now;syncDirections[slot]=next;}
        }
        internal static bool TryDirection(Vector2 velocity,Vector2 offset,out Vector2 result)
        {
            result=velocity;float speed=velocity.Length(),distance=offset.Length();
            if(speed<=0 || distance<=0 || float.IsNaN(speed) || float.IsInfinity(speed) || float.IsNaN(distance) || float.IsInfinity(distance))return false;
            result=offset*(speed/distance);return true;
        }
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            for(int i=0;i<3;i++)
            {
                int field=i;Action command=()=>Set(field,!Settings.Value.Enabled(field));
                if(feedback!=null)command=feedback.Committed(Actions[i],Names[i],command,()=>IsEnabled(field)?1:0,()=>CanConfigure(field),()=>Settings.AcceptedCommandId,()=>Settings.CompletedCommandId,()=>Settings.CompletionSucceeded);
                registry.Register(new HotkeyAction(Actions[i],Names[i],HotkeyContext.Gameplay,()=>CanConfigure(field),command,()=>Access));
            }
        }
        public void TakeFeedback(Action<string> show)
        {Settings.TakeFeedback(show);for(int i=0;i<3;i++)if(Access && errors[i]!=null && Settings.Value.Enabled(i) && !reported[i]){reported[i]=true;show(Unavailable(i));}}
        private void Exit(object sender,EventArgs e){Dispose();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;AppDomain.CurrentDomain.ProcessExit-=Exit;
            DisposeHooks();ClearTransient();Settings.Stop(750);
        }
    }
}
