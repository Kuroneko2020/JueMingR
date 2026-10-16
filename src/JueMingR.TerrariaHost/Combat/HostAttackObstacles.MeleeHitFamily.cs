using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        // Only this use's own five OnHit families own these private lives.
        // A hand may kill the trigger before any projectile slot runs. Sharing
        // a receiver snapshot without that retirement would spend the child's
        // penetration on a corpse. No live NPC or unrelated actor is advanced.
        internal sealed class MeleeFamilyState
        {
            internal sealed class Row
            {
                internal NPC Npc;internal NpcIdentity Identity;internal Vector2 Position,Offset,Velocity;
                internal int Width,Height,Life,Defense,OwnerImmune,MeleeCooldown,RealLife;
                internal bool Immortal,Friendly,DontTake,NoTile,Qualified,Rewards,Ichor,BrokenArmor,BetsysCurse,RedHat;
                internal float Taken,Banner;
                internal Row(NPC n,Player p,long session)
                {Npc=n;Identity=CombatSelection.Identity(n,session);Position=n.position;Offset=n.netOffset;Velocity=n.velocity;Width=n.width;Height=n.height;Life=n.life;Defense=n.defense;OwnerImmune=n.immune[p.whoAmI];MeleeCooldown=p.meleeNPCHitCooldown[n.whoAmI];RealLife=n.realLife;Immortal=n.immortal;Friendly=n.friendly;DontTake=n.dontTakeDamage;NoTile=n.noTileCollide;Qualified=p.CanNPCBeHitByPlayerOrPlayerProjectile(n);Rewards=n.HittableForOnHitRewards();Taken=n.takenDamageMultiplier;Ichor=n.ichor;BrokenArmor=n.brokenArmor;BetsysCurse=n.betsysCurse;RedHat=n.RedHatSkeletronAdjustmentsEnabled();Banner=BannerFor(p,n);}
                private static float BannerFor(Player p,NPC n){ItemID.BannerEffect effect;return p.GetBannerBuffEffect(n,out effect)?effect.DamageDealt.Sample(Main.Difficulty):1;}
                internal bool Current(Player p)
                {var n=Npc;return ReferenceEquals(Main.npc[Identity.Slot],n) && n.active && CombatSelection.Identity(n,Identity.Session).Equals(Identity) && n.position==Position && n.netOffset==Offset && n.velocity==Velocity && n.width==Width && n.height==Height && n.life==Life && n.defense==Defense && n.immune[p.whoAmI]==OwnerImmune && n.realLife==RealLife && n.immortal==Immortal && n.friendly==Friendly && n.dontTakeDamage==DontTake && n.noTileCollide==NoTile && n.takenDamageMultiplier==Taken && n.ichor==Ichor && n.brokenArmor==BrokenArmor && n.betsysCurse==BetsysCurse && n.RedHatSkeletronAdjustmentsEnabled()==RedHat && BannerFor(p,n)==Banner && p.CanNPCBeHitByPlayerOrPlayerProjectile(n)==Qualified && n.HittableForOnHitRewards()==Rewards;}
                internal MotionRect Bounds {get{return new MotionRect((int)(Position.X+Offset.X),(int)(Position.Y+Offset.Y),Width,Height);}}
            }
            private static readonly FieldInfo volcano=typeof(Player).GetField("_spawnVolcanoExplosion",BindingFlags.Instance|BindingFlags.NonPublic),muramasa=typeof(Player).GetField("_spawnMuramasaCut",BindingFlags.Instance|BindingFlags.NonPublic),rapidBonus=typeof(Player).GetField("rapidAttackBonus",BindingFlags.Instance|BindingFlags.NonPublic);
            internal readonly Row[] Rows;internal readonly int[] InitialLife;internal int[] Life;
            internal readonly Player Owner;internal readonly Item Item;internal readonly bool NewHand;
            private sealed class DamageMember
            {
                internal Projectile Shot;internal int Armor,Crit;internal bool Melee,Ranged,Magic,StopDamage;
                internal DamageMember(Projectile p){Shot=p;Armor=p.armorPenetration;Crit=p.bonusCritChance;Melee=p.melee;Ranged=p.ranged;Magic=p.magic;StopDamage=p.stopsDealingDamageAfterPenetrateHits;}
                internal bool Current {get{return Shot.armorPenetration==Armor && Shot.bonusCritChance==Crit && Shot.melee==Melee && Shot.ranged==Ranged && Shot.magic==Magic && Shot.stopsDealingDamageAfterPenetrateHits==StopDamage;}}
            }
            private DamageMember[] damageMembers=Array.Empty<DamageMember>();
            private readonly int ownerSlot,slot,type,prefix,damage,attack,animation,animationMax,direction,crit,armor,useAnimation,useStyle;
            private readonly int meleeCrit,rangedCrit,magicCrit,meleeArmor,nonMeleeArmor,itemArmor;private readonly float itemTag;
            private readonly bool timerCrit;internal readonly bool CapturedUsing;private readonly float capturedTimerCharge;private readonly byte enchant;
            internal float TimerCharge;private float nativeStartTimerCharge;
            private readonly float kb,scale,rapid,meleeSpeed;private readonly bool strong,parry,guide,clothier,volcanoArmed,muramasaArmed,itemMelee;
            internal int Facing;
            internal MeleeFamilyState(Player value,Item item,long session,bool newHand)
            {
                Owner=Main.player[value.whoAmI];Item=item;NewHand=newHand;ownerSlot=value.whoAmI;slot=Owner.selectedItem;type=item.type;prefix=item.prefix;
                damage=Owner.GetWeaponDamage(item);kb=Owner.GetWeaponKnockback(item,item.knockBack);scale=Owner.GetAdjustedItemScale(item);rapid=(float)rapidBonus.GetValue(Owner);
                attack=Owner.attackCD;animation=Owner.itemAnimation;animationMax=Owner.itemAnimationMax;direction=Owner.direction;strong=Owner.strongBees;parry=Owner.parryDamageBuff;guide=Owner.killGuide;clothier=Owner.killClothier;
                crit=Owner.GetWeaponCrit(item);armor=Owner.GetArmorPenetration(item.melee);useAnimation=item.useAnimation;useStyle=item.useStyle;meleeSpeed=Owner.meleeSpeed;itemMelee=item.melee;
                meleeCrit=Owner.meleeCrit;rangedCrit=Owner.rangedCrit;magicCrit=Owner.magicCrit;meleeArmor=Owner.GetArmorPenetration(true);nonMeleeArmor=Owner.GetArmorPenetration(false);itemArmor=item.armorPenetration;itemTag=item.bonusTagDamage;
                timerCrit=Owner.accTimerCrit;CapturedUsing=Owner.UsingOrReusingItem;TimerCharge=nativeStartTimerCharge=capturedTimerCharge=Owner.accTimerCritCharge;enchant=Owner.meleeEnchant;
                volcanoArmed=(bool)volcano.GetValue(Owner);muramasaArmed=(bool)muramasa.GetValue(Owner);
                var rows=new List<Row>();InitialLife=new int[Main.maxNPCs];
                foreach(var n in Main.npc)if(n!=null && n.active){var row=new Row(n,Owner,session);rows.Add(row);InitialLife[n.whoAmI]=n.life;}
                Rows=rows.ToArray();Life=(int[])InitialLife.Clone();Facing=direction;
            }
            private MeleeFamilyState(MeleeFamilyState s)
            {Rows=s.Rows;InitialLife=s.InitialLife;Life=(int[])s.InitialLife.Clone();Owner=s.Owner;Item=s.Item;NewHand=s.NewHand;ownerSlot=s.ownerSlot;slot=s.slot;type=s.type;prefix=s.prefix;damage=s.damage;kb=s.kb;scale=s.scale;rapid=s.rapid;attack=s.attack;animation=s.animation;animationMax=s.animationMax;direction=s.direction;strong=s.strong;parry=s.parry;guide=s.guide;clothier=s.clothier;volcanoArmed=s.volcanoArmed;muramasaArmed=s.muramasaArmed;Facing=s.Facing;crit=s.crit;armor=s.armor;useAnimation=s.useAnimation;useStyle=s.useStyle;meleeSpeed=s.meleeSpeed;itemMelee=s.itemMelee;meleeCrit=s.meleeCrit;rangedCrit=s.rangedCrit;magicCrit=s.magicCrit;meleeArmor=s.meleeArmor;nonMeleeArmor=s.nonMeleeArmor;itemArmor=s.itemArmor;itemTag=s.itemTag;damageMembers=s.damageMembers;timerCrit=s.timerCrit;CapturedUsing=s.CapturedUsing;capturedTimerCharge=s.capturedTimerCharge;TimerCharge=nativeStartTimerCharge=capturedTimerCharge;enchant=s.enchant;}
            internal void CaptureDamageMembers(List<Projectile> sources){var values=new List<DamageMember>();foreach(var p in sources)values.Add(new DamageMember(p));damageMembers=values.ToArray();}
            internal MeleeFamilyState Candidate(){return new MeleeFamilyState(this);}
            internal bool NativeStart {get{return NewHand && animation<=0 && Owner.itemAnimation>0 && Owner.itemAnimation==Owner.itemAnimationMax;}}
            // One private charge belongs to the same ordered hand/projectile
            // world. Calling native TryConsumingTimerCrit would mutate the
            // owner and consume visual RNG. Keep only its damage transition.
            internal void AdvanceTimerCrit(bool usingItem,Vector2 velocity)
            {if(!timerCrit || Owner.whoAmI!=Main.myPlayer || !(TimerCharge<1000))return;if(TimerCharge<0){TimerCharge+=1;return;}TimerCharge=Math.Min(1000,TimerCharge+3+(usingItem?0:3)+(velocity.Length()<.1f?3:0));}
            internal void CaptureNativeStartTimer(){nativeStartTimerCharge=TimerCharge;}
            internal bool Critical(bool value)
            {if(timerCrit && (TimerCharge>=1000 || TimerCharge<0)){if(TimerCharge>0)TimerCharge=-15;return true;}return value;}
            internal int HandRandomized(int damage){return enchant==6?(int)(damage*Player.GetNanoFlaskDamageBoost()):damage;}
            internal void Damage(int slot,int damage)
            {
                Row row=null;foreach(var r in Rows)if(r.Identity.Slot==slot){row=r;break;}if(row==null || row.Immortal)return;
                int head=row.RealLife>=0?row.RealLife:slot;Life[head]=Math.Max(0,Life[head]-Math.Max(1,damage));
                if(Life[head]==0)foreach(var r in Rows)if(r.Identity.Slot==head || r.RealLife==head)Life[r.Identity.Slot]=0;
            }
            internal int ProjectileDamage(int slot,Projectile p)
            {foreach(var r in Rows)if(r.Identity.Slot==slot){int bonus=Math.Max(0,p.bonusCritChance);bool critical=Critical(p.melee && meleeCrit+bonus>=100 || p.ranged && rangedCrit+bonus>=100 || p.magic && magicCrit+bonus>=100);int damage=p.damage;if(Main.netMode!=2)damage=(int)(damage*r.Banner);damage+=r.Npc.checkArmorPenetration((p.melee?meleeArmor:nonMeleeArmor)+p.armorPenetration,0);if(p.melee && enchant==6)damage=(int)(damage*Player.GetNanoFlaskDamageBoost());int d=(int)Math.Max(1,Main.CalculateDamageNPCsTake(damage,r.Defense)*(critical?2:1));if(r.RedHat)d=Math.Max(1,(int)(d*.7f));return r.Taken>1?(int)(d*r.Taken):d;}return 1;}
            internal bool InputsMatch(bool afterStart)
            {
                var p=Owner;if(!ReferenceEquals(Main.player[ownerSlot],p) || !p.active || p.dead || p.selectedItem!=slot || !ReferenceEquals(p.HeldItem,Item) || Item.type!=type || Item.prefix!=prefix || p.GetWeaponDamage(Item)!=damage || p.GetWeaponKnockback(Item,Item.knockBack)!=kb || p.GetAdjustedItemScale(Item)!=scale || (float)rapidBonus.GetValue(p)!=rapid || p.strongBees!=strong || p.parryDamageBuff!=parry || p.killGuide!=guide || p.killClothier!=clothier)return false;
                if(p.GetWeaponCrit(Item)!=crit || p.GetArmorPenetration(Item.melee)!=armor || Item.useAnimation!=useAnimation || Item.useStyle!=useStyle || p.meleeSpeed!=meleeSpeed || Item.melee!=itemMelee)return false;
                if(p.meleeCrit!=meleeCrit || p.rangedCrit!=rangedCrit || p.magicCrit!=magicCrit || p.GetArmorPenetration(true)!=meleeArmor || p.GetArmorPenetration(false)!=nonMeleeArmor || Item.armorPenetration!=itemArmor || Item.bonusTagDamage!=itemTag)return false;
                foreach(var member in damageMembers)if(!member.Current)return false;
                bool started=NewHand && afterStart && (NativeStart || p.ItemAnimationJustStarted);
                if(p.accTimerCrit!=timerCrit || p.meleeEnchant!=enchant || timerCrit && (!started && p.UsingOrReusingItem!=CapturedUsing || p.accTimerCritCharge!=capturedTimerCharge && !(started && p.accTimerCritCharge==nativeStartTimerCharge)))return false;
                if(p.direction!=direction && !(NewHand && p.direction==Facing))return false;
                if(!started && (p.itemAnimation!=animation || p.itemAnimationMax!=animationMax || p.attackCD!=attack))return false;
                // StartedHand is the native StartActualUse postfix: animation
                // and melee reset already happened, OwnerOnlyCode's arming is
                // still ahead. Both sides of that ONE transition are legal;
                // a real hit changes the captured life/CD rows and revokes it.
                bool currentVolcano=(bool)volcano.GetValue(p),currentMuramasa=(bool)muramasa.GetValue(p);
                if(currentVolcano!=volcanoArmed && !(started && type==121 && currentVolcano) || currentMuramasa!=muramasaArmed && !(started && type==155 && currentMuramasa))return false;
                foreach(var r in Rows)if(!r.Current(p) || p.meleeNPCHitCooldown[r.Identity.Slot]!=(started?0:r.MeleeCooldown))return false;
                return true;
            }
            internal bool DependenciesCurrent
            {get{int at=0;foreach(var n in Main.npc)if(n!=null && n.active){if(at>=Rows.Length || !ReferenceEquals(Rows[at++].Npc,n))return false;}return at==Rows.Length;}}
        }
        private MeleeFamilyState meleeFamily;
        internal MeleeFamilyState CaptureMeleeFamily(Player value,Item item,bool newHand)
        {return meleeFamily??(meleeFamily=new MeleeFamilyState(value,item,timeline.Identity.Session,newHand));}
        private void ForkMeleeFamilyFrom(HostAttackObstacles parent,bool candidate)
        {meleeFamily=parent.meleeFamily==null?null:candidate?parent.meleeFamily.Candidate():parent.meleeFamily;}
        private bool MeleeFamilyInputsMatch(bool afterHandStart){return meleeFamily==null || meleeFamily.InputsMatch(afterHandStart);}
        private bool MeleeFamilyDependenciesCurrent {get{return meleeFamily==null || meleeFamily.DependenciesCurrent;}}
        private bool MeleeFamilyReceiverAlive(int slot){return meleeFamily==null || meleeFamily.Life[slot]>0;}
        internal bool MeleeFamilyStartsNow {get{return meleeFamily!=null && meleeFamily.NativeStart;}}
        internal bool MeleeFamilyHandAllows(int slot,int action,int capturedOwner)
        {
            // A projectile in an earlier world may replace the NPC's owner
            // timer, including clearing it. Hand runs BEFORE this world's NPC
            // timer decrement, one boundary before projectile Damage.
            if(history.OwnerWritten!=null && history.OwnerWritten[slot])return action>=history.OwnerNext[slot];
            return capturedOwner>=0 && capturedOwner<=action;
        }
    }
}
