using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostCombatExperiments
    {
        // Fixed .8 SDMG has useTime=5/useAnimation=5. ApplyItemAnimation
        // adds one ranged-auto-reuse frame; AutoReuseLogic retires that last
        // frame before the next start. The resulting repeat interval is 5.
        internal const int SdmgTicks=5;
        private TimingReceipt timing;
        private struct TimingReceipt
        {
            internal Player Player;internal Item Item;internal int Type,Prefix;
            internal bool Enabled;internal int UseTicks,AnimationTicks,NativeAnimationTicks;
        }
        private void InstallTiming()
        {
            Patch(1,typeof(Player),"ApplyItemAnimation",nameof(BeforeAnimation),nameof(AfterAnimation),signature:new[]{typeof(Item)});
            Patch(1,typeof(Player),"ApplyItemTime",postfix:nameof(AfterItemTime),signature:new[]{typeof(Item)});
            Patch(1,typeof(Player),"ApplyItemTime",postfix:nameof(AfterItemTime),signature:new[]{typeof(Item),typeof(float)});
            Patch(1,typeof(Player),"ItemCheck_Shoot",transpiler:nameof(BurstPhaseTranspiler));
        }
        internal static bool Weapon(Item item)
        {
            return item!=null && !item.IsAir && item.damage>0 && item.useStyle>0 && item.useTime>0 && item.useAnimation>0 &&
                item.pick<=0 && item.axe<=0 && item.hammer<=0 && item.fishingPole<=0 && item.createTile<0 && item.createWall<0 &&
                item.ammo==0 && item.type!=2269 && item.type!=5095 && item.type!=3031 && item.type!=1991 && item.type!=3183 &&
                !item.potion && item.healLife<=0 && item.healMana<=0 && item.mountType<0 && (item.buffType<=0 || item.summon || item.sentry);
        }
        private bool Held(Player player,Item item)
        {return combat.Runtime.IsSessionActive && LocalPlayer(player) && ReferenceEquals(player,combat.Player) && ReferenceEquals(player.HeldItem,item) && Weapon(item);}
        private static void BeforeAnimation(Player __instance,Item __0)
        {
            var self=current;if(self==null || !self.Held(__instance,__0))return;
            // Freeze the accepted mode at vanilla's new-use setter. Toggling
            // during an animation never rescales its remaining clocks.
            self.timing=new TimingReceipt{Player=__instance,Item=__0,Type=__0.type,Prefix=__0.prefix,Enabled=self.FastFireEffective};
        }
        private static void AfterAnimation(Player __instance,Item __0)
        {
            var self=current;if(self==null || !self.TimingCurrent(__instance,__0) || !self.timing.Enabled)return;
            int nativeAnimation=__instance.itemAnimationMax;
            int padding=__0.autoReuse && __0.reuseDelay==0 && __0.useTime<=__0.useAnimation && __0.shoot>0 && __0.useStyle==5?1:0;
            int count=__0.channel || __0.shoot<=0?1:Math.Max(1,(Math.Max(1,nativeAnimation-padding)+__0.useTime-1)/__0.useTime);
            // Preserve vanilla's number of staggered uses per animation; one
            // native Shoot can still emit any number of pellets. Each legal
            // use consumes its original ammo/mana and all CanUse checks remain.
            int animation=checked(count*SdmgTicks+(__0.autoReuse?1:0));
            self.timing.UseTicks=SdmgTicks;self.timing.AnimationTicks=animation;self.timing.NativeAnimationTicks=nativeAnimation;
            __instance.itemAnimation=__instance.itemAnimationMax=animation;
            __instance.reuseDelay=0;
        }
        private static IEnumerable<CodeInstruction> BurstPhaseTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code=new List<CodeInstruction>(instructions);int matches=0;
            var clock=AccessTools.Field(typeof(Player),"itemAnimation");
            // Exactly the two fixed Clockwork scatter thresholds (<5/<10).
            // Other weapons that hand their actual animation duration to an
            // owned melee projectile must keep the accelerated real clock.
            for(int i=0;i<code.Count-1;i++)
            {
                if(code[i].opcode!=OpCodes.Ldfld || !Equals(code[i].operand,clock))continue;
                var next=code[i+1];bool five=next.opcode==OpCodes.Ldc_I4_5;
                bool ten=(next.opcode==OpCodes.Ldc_I4_S || next.opcode==OpCodes.Ldc_I4) && Convert.ToInt32(next.operand)==10;
                if(!five && !ten)continue;
                code[i]=new CodeInstruction(code[i]);code[i].opcode=OpCodes.Call;code[i].operand=AccessTools.Method(typeof(HostCombatExperiments),nameof(NativeBurstAnimation));matches++;
            }
            if(matches!=2)throw new InvalidOperationException("Fixed .8 Clockwork burst phase ABI changed: "+matches);
            return code;
        }
        private static int NativeBurstAnimation(Player player)
        {
            var self=current;var item=player.HeldItem;
            if(self==null || item.type!=434 || !self.TimingCurrent(player,item) || !self.timing.Enabled || self.timing.AnimationTicks<=1)return player.itemAnimation;
            // Shoot reads the native burst phase, not the new remaining clock.
            // ItemAnimationJustStarted itself compares actual animation/max;
            // both were set together, so PickAmmo's first-shot-only consumption
            // remains true exactly once per burst without patching PickAmmo.
            return (int)Math.Ceiling((double)Math.Max(0,player.itemAnimation)*(self.timing.NativeAnimationTicks-1)/(self.timing.AnimationTicks-1));
        }
        private bool TimingCurrent(Player player,Item item)
        {return Access && ready[1] && Held(player,item) && ReferenceEquals(timing.Player,player) && ReferenceEquals(timing.Item,item) && timing.Type==item.type && timing.Prefix==item.prefix;}
        private static void AfterItemTime(Player __instance,Item __0)
        {
            var self=current;if(self==null || !self.TimingCurrent(__instance,__0) || !self.timing.Enabled || self.timing.UseTicks<=0)return;
            // Only a real native setter is transformed, never a countdown or
            // SetDummyItemTime. Held prism/yoyo keep their own channel clocks.
            __instance.itemTime=__instance.itemTimeMax=self.timing.UseTicks;
        }
    }
}
