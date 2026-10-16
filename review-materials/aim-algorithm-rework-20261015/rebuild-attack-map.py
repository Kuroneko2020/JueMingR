from pathlib import Path
import csv, re, hashlib
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent

def read(p):
    with p.open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
def write(p, rows):
    with p.open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
rows=read(OUT/'attack-routes-m2k.csv')
assert len(rows)==2944
source=ROOT/'src/JueMingR.TerrariaHost/Combat'
tests=list((ROOT/'tests/NativeWorldTextProbe').glob('NativeCombat*Checks.cs'))
text={p:p.read_text(encoding='utf-8-sig') for p in tests}
byhelper={}
for p in source.glob('Host*Attack.cs'):
    h=p.stem
    byhelper[h]=';'.join(str(t.relative_to(ROOT)).replace('\\','/') for t,s in text.items() if h in s)
catalogs=read(OUT/'weapon-mechanisms-A.csv')+read(OUT/'weapon-mechanisms-C.csv')
for row in read(OUT/'weapon-mechanisms-B.csv'):
    catalogs.append(dict(projectile=row['resolvedProjectile'],helper=row['helper'],hand=row['handConsumer'],knownConsumer=row['knownConsumer'],lifecycle=row['damageLifecycle'],leaf=row['leaf'],evidence=row['evidence'],status=row['status'],owner='B'))
bytype={}
for x in catalogs:bytype.setdefault(int(x['projectile']),[]).append(x)
# Metadata handlers enumerate candidate predicates, not their final precedence.
# Explicit dispatch below accounts for hand/controller routes preceding them.
result=[]
for r in rows:
    typ=int(r['resolvedProjectile']);item=int(r['item']);handlers=r['mechanismHandlers'].split(';') if r['mechanismHandlers'] else []
    route=r['route']; deps='current item/ammo instance; receiver identity; phase/sample; terrain; owner input permission'
    ability='directional hand proposal and original finite receive'
    if item==3788:
        helpers=['HostOnyxBlastAttack','HostHeldAmmoFamilyAttack'];consumer='native Onyx same ItemCheck -> four actual selected ammo plus doubled661 -> ordered shared receive';ability='legal representative multishot; actual custom collider and160 Kill, no bolt-only proof'
    elif item in (1229,2624,3029,4953):
        # Native birth precedes selected-ammo motor dispatch. Harpy and plain
        # callers must share the real count/origin/ordinal before AI/Tile/Kill.
        helpers=['HostNativeArrowBirthAttack','HostHeldAmmoFamilyAttack']+handlers
        consumer='PrepareCore/Preview -> actual four-caller native birth -> ExplicitPreparedBirthBatch; Harpy/plain share ordered scene; Known Flight uses complete family when covered'
        ability='1229 legal2/3 representative2;2624 fixed5;3029 legal3/4 or2/3 representative3;4953 one actual ordinal and central932 doubleDamage; sealed children grant no cursor'
        deps+='; selected-ammo damage/KB float32 capture-consume; Quiver/Harpy/Seraph/Phoenix OFF scalar leases; actual Raw/world facing and current/next/delayed native phase'
    elif item in (121,155,1166,3211,1123):
        helpers=['HostSwingAttack','HostMeleeHitFamilyAttack','HostBeeAttack' if item==1123 else 'HostMeleeHitChildAttack'];consumer='actual BeforeNpc hand-family proof -> legal facing at native StartActualUse -> observed other-receiver hand hit -> own finite child -> selected; subsequent public Known observation retained';ability='finite own-hand chain; deterministic978 geometry and legal random representatives; direction consumer only, no invented Shoot cursor';deps+='; observed trigger identity/life/defense/eligibility; native slot/handCD/once flags; exact start transition; shared own-family history'
    elif typ in (974,982,997) and route=='NULL_CAPTURE':
        helpers=['HostLightsBaneAttack' if typ==974 else 'HostArcSwingAttack'];consumer='HostSwingAttack native hand + autonomous near-target Known974' if typ==974 else 'HostArcSwingAttack native owner-facing zero-speed arc;997->321 finite family';ability='native near-neighbour autonomous birth and independent hand' if typ==974 else 'owner-facing arc and finite derived pumpkin; no generic zero-speed exception'
    elif r['stageNote']=='held-gun+selected-ammo+derived':
        helpers=['HostHeldAttack','HostHeldAmmoFamilyAttack','HostVortexRocketAttack']+handlers;consumer='Control.BeginOpening -> registered source -> real Main.UpdateWorld_Projectiles once after all NPC slots -> held AI validates/consumes -> selected-ammo whole batch,615 phase616,slot/Harpy conditions'
    elif typ in (36,207,225,285):
        helpers=['HostAmmoSteeringAttack','HostHeldAmmoFamilyAttack'];consumer='actual selected-ammo native hand -> finite steering/tile retarget/receive; shared held batch and Harpy callbacks where applicable';ability='actual native steering and sealed ammo; random hand proposal stays Representative'
    elif typ in (41,89,283,286):
        helpers=['HostAmmoImpactAttack','HostHeldAmmoFamilyAttack'];consumer='actual selected-ammo native hand -> complete body/Kill/derived receive; shared held/Eventide/Harpy explicit source where applicable';ability='finite explosion/split;283 inactive Kill is StatusNPC only and creates no new Strike'
    elif int(r['style'])==99:
        helpers=['HostYoyoNavigation','HostYoyoReleaseAttack']+(['HostYoyoFamilyAttack'] if typ in (545,546,554,555,603,999) else []);consumer='registered held AI / magic-string native release / detached Known';ability='whole-body navigation; legal held input only; finite autonomous children sealed'
    elif typ==342:
        helpers=['HostNorthPoleAttack'];consumer='HostMeleeAttack actual342 -> late343 -> periodic344 + finite Flight'
    elif typ==535:
        helpers=['HostMedusaScanAttack'];consumer='Flight known535 -> native channel scan -> synchronous536 Damage';ability='observation/reachability only; native320/LOS/nearest3/mana/cooldown; no directional enhancement'
    elif typ==976:
        helpers=['HostSwingAttack','HostGrassCurveAttack'];consumer='native hand facing/body + autonomous native near-target birth + Known976';ability='original near-neighbour spawn; no invented remote initial-speed consumer'
    elif typ==1115:
        helpers=['HostCadenceAim'];consumer='CombatUse GlacierCharge -> native each-three-update direction reader'
    elif typ==1117:
        helpers=['HostDirectionalControl','HostDirectionalStormAttack'];consumer='registered native AI203 unlocked cursor; acquired-target and clock30 Kill sealed'
    elif typ==1040:
        helpers=['HostCadenceAim'];consumer='CombatUse/Flint release and finite grounded-spike native source'
    elif typ==1035:
        helpers=['HostWhipAttack'];consumer='registered native special-whip AI and finite actual emissions'
    elif typ==756:
        helpers=['HostSharpTearsAttack'];consumer='HostEffectAttack native MouseWorld/FindSharpTearsSpot -> AI157 finite receive';ability='actual position consumer with finite terrain/scale/owner lease; not cadence'
    elif route=='placement':
        helpers=['HostPlacementAttack'];consumer='native placement/use receipt';ability='placement/summon operation semantics; no promised hostile damage redirection'
    elif route=='sky':
        helpers=['HostSkyAttack']+handlers;consumer='native actual hand spawn geometry -> private full native motor/receive'
    elif route=='swing':
        helpers=['HostSwingAttack']+handlers;consumer='native hand melee body and captured mixed projectile family'
    elif route=='melee':
        helpers=['HostMeleeAttack']+handlers;consumer='native actual melee/whip/spear source -> finite body/derived receive'
    elif handlers:
        helpers=handlers;consumer='HostAttackAim/Control actual source dispatch; Flight Known where autonomous'
    elif r['motionKnown']=='True':
        helpers=['HostAttackModels','AttackIntercept','AttackMotionCandidates'];consumer='actual ItemCheck_Shoot cursor -> native projectile Update/Damage';ability='discrete native motor; full ordinary receive/retirement, special events indexed separately'
    else:raise RuntimeError('Unclassified actual route '+str(r))
    helpers=list(dict.fromkeys(helpers))
    evidence=set()
    for h in helpers:
        evidence.update(x for x in byhelper.get(h,'').split(';') if x)
    if any(x in helpers for x in ('HostAttackModels','AttackIntercept','AttackMotionCandidates')):
        evidence.update(['tests/NativeWorldTextProbe/NativeCombatBallisticMotionChecks.cs','tests/NativeWorldTextProbe/NativeCombatBasicAimChecks.cs'])
    if typ==756:evidence.add('tests/NativeWorldTextProbe/NativeCombatSharpTearsChecks.cs')
    family=bytype.get(typ,[])+ (bytype.get(661,[]) if item==3788 else [])
    # The inventory resolves selected ammo, so its projectile14 alone cannot
    # stand for the registered615 consumer or its616 phase. Bind both roles;
    # the controller table keeps kernel/phase/runtime/cost evidence separate.
    parent=int(r['weaponShoot'])
    controller_refs=[]
    if r['stageNote']=='held-gun+selected-ammo+derived' and parent in (615,630,705):
        family+=bytype.get(parent,[])
        if parent==615:family+=bytype.get(616,[])
        if parent==705:family+=bytype.get(706,[])
        controller_refs.append(f'weapon-mechanisms-C.csv:projectile{parent} separate kernel/phase/runtime/offWork/range/cost columns')
        if parent==705:controller_refs.append('weapon-mechanisms-A.csv:projectile706 original Known and shared before-pass third consumer')
    own_child={121:978,155:977,1166:1111,3211:524}
    if item in own_child:family+=bytype.get(own_child[item],[])
    if item==1123:
        family+=bytype.get(181,[])+bytype.get(566,[])
    # Preserve each author's stage/helper boundary. An older Known-only bee
    # statement must not read as cancelling the later own-hand family route.
    lifecycle=';'.join(dict.fromkeys(x['helper']+': '+x['lifecycle'] for x in family)) or ('ordinary discrete motor/receive; event-specific exits classified in native-event-closure.csv' if r['motionKnown']=='True' else 'actual controller/placement/cadence/native hand stage as above; direct event index distinguishes visual/external stages')
    proof=';'.join(dict.fromkeys([x['evidence'] for x in family]+controller_refs))
    status=(';'.join(dict.fromkeys(x['status'] for x in family))) or 'EXISTING_APPLICABLE_MECHANISM; related final propagation/reuse index required'
    if controller_refs:status+=';CURRENT_CONTROLLER_PHASE_RUNTIME_COST_ARE_SEPARATE_COLUMNS_NOT_KERNEL_STATUS'
    if item in (1229,2624,3029,4953):
        lifecycle+='; native birth batch preserves selected AI/Tile/Kill,932 native line collision and ambient/children/slot/history;117 is not legal selected ammo for these callers (5348->1006)'
        proof+=';.local/aim-algorithm-rework-20261015/B-native-birth-composition-fix/r54-ap-components-native-birth.log:strict240births/4parameterstates/8revocations/composed225_41_91_639_932;final-author-evidence.json retains actual identities and shared-consumer reuse'
        status+=';AP_NATIVE_BIRTH_AND_SAMPLED_LIFECYCLE_COMPOSITION;NOT_120_COMBINATIONS_ALL_STATE_RUNTIME_PASS;INDEPENDENT_DELTA_PENDING'
        evidence.add('tests/NativeWorldTextProbe/NativeCombatNativeArrowBirthChecks.cs')
    result.append(dict(item=r['item'],ammo=r['ammo'],alt=r['alt'],resolvedProjectile=r['resolvedProjectile'],style=r['style'],metadataRoute=route,mechanisms=';'.join(helpers),actualConsumer=consumer,capability=ability,dependencies=deps,lifecycle=lifecycle,mechanismEvidence=proof,mechanismEvidenceState=status,ordinaryBudget='07 ordinary matrix; component PASS does not imply every cold action Enhanced',eventIndex='native-event-closure.csv: exact source/case/caller classification; OnHit/primary causality retained',directChecks=';'.join(sorted(evidence)),evidenceState='SOURCE_CONSUMER_CLASSIFIED; family evidence above, combination itself not runtime PASS'))
assert len({(r['item'],r['ammo'],r['alt']) for r in result})==2944
write(OUT/'attack-lifecycle-map.csv',result)
print('2944 classified routes; no per-combination runtime PASS inferred')
