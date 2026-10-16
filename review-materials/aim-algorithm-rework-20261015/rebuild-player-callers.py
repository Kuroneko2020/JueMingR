from pathlib import Path
import csv,hashlib,json,re
from collections import Counter

root=Path.cwd();out=root/'review-materials/aim-algorithm-rework-20261015'
source=root/'references/terraria/1.4.5.8/Terraria/Terraria/Player.cs'
lines=source.read_text(encoding='utf-8-sig').splitlines()
rows=[];method=''
finite={12479:'HostYoyoFamilyAttack counterweight/glove',12496:'HostYoyoFamilyAttack counterweight/glove',12500:'HostYoyoFamilyAttack counterweight/glove',42458:'HostPumpkinEffectAttack / HostArcSwingAttack',44500:'HostMeleeHitFamilyAttack121 -> deterministic978 geometry; real facing/primary/birth/selected Strike; public Known retained',44749:'1111 BoneSword1166 own finite hit child;1320 tool caller separate',44820:'524 Bladetongue3211 NPC-hit halfDamage; PvP0.7 separate',44868:'181/566 BeeKeeper1123 own NPC hit child; native bee type and strong condition',44900:'977 Muramasa155 one current use hit child; original private arm flag'}
equipment={'OnHit','ApplySetBonus_Stardust','DashMovement','Hurt','SporeSac','VolatileGelatin','CommandForbiddenStorm'}
world={'TorchAttack','DropTombstone','TorchGodsFlavor','SpawnHallucination','TileInteractionsUse'}
nonattack={'QuickGrapple','LaunchMinecartHook','TrySpawnDigtoiseAt','TryPlacingAGolfBallNearANearbyTee','BuffHandle_SpawnPetIfNeeded','UpdateAbigailStatus','UpdateStormTigerStatus'}
for index,line in enumerate(lines):
    match=re.match(r'\s*(?:public|private|internal|protected)\s+(?:(?:static|virtual|override|sealed|unsafe)\s+)*[\w<>\[\],.]+\s+(\w+)\(',line)
    if match:method=match[1]
    if 'Projectile.NewProjectile(' not in line:continue
    n=index+1;classification='';consumer='';status='CALLER_SOURCE_IDENTIFIED'
    if n in finite:
        classification='FINITE_WEAPON_STAGE';consumer=finite[n]
        if n>=44500:
            status='FINITE_FUTURE_HAND_NATIVE_COMPONENT_PASS_m2v; ordinary121_1166_PASS_m2v; supplemental_boundary_PASS_m2x; existing_born_Known_retained; ordinary_cost_07_separate'
            consumer+='; HostMeleeHitFamilyAttack finite future through currently observed hand-hittable receiver; no assumed past birth or live-RNG prediction; source/native component B-m2v-candidate-r04-melee-hit-family.log, ordinary121/1166 r05, x complete-boundary r10 and private-nano-damage r11; Timer6170 native deprecated-air is a negative, not current equip capability'
    elif method=='ItemCheck_Shoot':
        classification='REAL_ITEM_SHOOT_DISPATCH';consumer='2944-row attack-lifecycle-map: exact item/ammo/alt and actual resolved body; conditional native multi-birth cases retained'
        if 49335<=n<=49344:consumer='3788 HostOnyxBlastAttack / ordered HostHeldAmmoFamilyAttack; native four selected ammo plus661'
        native_birth={47870:3029,48237:2624,48394:4953,48398:4953,48776:1229}
        if n in native_birth:
            consumer=f'{native_birth[n]} HostNativeArrowBirthAttack -> ExplicitPreparedBirthBatch / complete HeldAmmo scene; selected-ammo AI/Tile/Kill after actual birth; Harpy/plain preserve origin/count/ordinal; 120legal map rows, no selected117'
            status='AP_NATIVE_BIRTH_STRICT240_AND_COMPOSED_LIFECYCLE_SAMPLES; ACTUAL_6MS_SPECIALIST_AND_MAIN_SEPARATE; INDEPENDENT_DELTA_PENDING'
    elif method in {'TentacleSpike_TrySpiking','BloodButcherer_TryButchering'}:
        classification='ATTACHED_STATUS_NO_NEW_STRIKE';consumer='971/975 bornai0=1; Projectile.CanDamage excludes attached state; preserve native StatusNPC/KillOldestJavelin'
    elif method=='ApplyNPCOnHitEffects':
        classification='ZERO_DAMAGE_PRESENTATION';consumer='zeroDamage289 native flask-confetti; not a new NPC Strike'
    elif method=='ItemCheck_MeleeHitPVP':
        classification='PVP_SOURCE';consumer='actual PVP recipient; not a new NPC-hit source assumption'
    elif method=='ItemCheck_OwnerOnlyCode':
        if n==43962:classification='EXTERNAL_EQUIPMENT';consumer='BoneGlove accessory532; real existing equipment condition preserved'
        else:classification='NO_COMBAT_ITEM_SOURCE';consumer='item1124 Hive placeable material is excluded from combat hand admission; no BeeKeeper1123 substitution'
    elif method=='ItemCheck_EmitHammushProjectiles':
        classification='TOOL_SOURCE';consumer='Hammush130/131 tool-only hand; original tool operation preserved'
    elif method in equipment:
        classification='EXTERNAL_EQUIPMENT_OR_AUTONOMOUS';consumer=method+' exact equipment/set/minion/retaliation predicate; not this weapon finite hand'
    elif method=='UpdateBuffs' or method=='SpawnMinionOnCursor':
        classification='AUTONOMOUS_SUMMON_OR_PET';consumer=method+' native buff/pet/minion placement; zeroDamage pet rows remain noContact; no sustained mouse authority'
    elif method in world:
        classification='WORLD_STATUS_OR_TILE_SOURCE';consumer=method+' actual world/status/tile interaction, not a held combat input source'
    elif method in nonattack:
        classification='NONATTACK_OPERATION';consumer=method+' original movement/pet/tool operation; do not manufacture attack Contact'
    else:raise AssertionError((n,method,line))
    rows.append(dict(nativeLine=n,nativeMethod=method,call=line.strip(),classification=classification,actualConsumer=consumer,evidenceState=status,proofBoundary='Caller inventory and exact source classification, not 197 runtime tests or all-input lifecycle PASS'))
assert len(rows)==197,len(rows)
with (out/'native-player-callers.csv').open('w',encoding='utf-8-sig',newline='') as stream:
    writer=csv.DictWriter(stream,fieldnames=rows[0]);writer.writeheader();writer.writerows(rows)
summary=dict(calls=len(rows),methods=dict(Counter(r['nativeMethod'] for r in rows)),classes=dict(Counter(r['classification'] for r in rows)),pendingFiniteKnown=[r['nativeLine'] for r in rows if r['evidenceState']=='FINITE_KNOWN_RECEIVE_PENDING'],nativeSourceSha=hashlib.sha256(source.read_bytes()).hexdigest().upper(),extraction='Exact NewProjectile call-site text scan with nearest native method declaration, not a C# semantic AST claim')
(out/'native-player-callers-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False))
