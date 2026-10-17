# Selected original evidence

Only existing task files are excerpted. No checks were executed for this report. PASS lines inside a failed/cancelled process are partial results. Original line numbers and full-file SHA-256 follow. Paths are relative to the repository named in README. Encodings are recorded; originals are untouched.

## integration-r03.log

Origin: `.local/aim-comprehensive-repair-20261010/integration-r03.log`; SHA-256 `A67DBDD2359BEAD03C324C14DA72BBBEDF25DACB41F9B5E75C8A55B8C6375EE1`; encoding UTF-8; candidate **r03**.

exit=1; scope=CombatIntegrationCpu; seconds=34.5691213

```text
55: WALL RESULT weapon=3278 selected=2 openingContact=False firstDamage=17 lifeA=99993 lifeB=100000
56: PASS same-obstacle shared identity matrix; current passage preference is conditional, finite no-Contact is not a physical-impossibility claim.
57: DEMAND path=False required=17 samples=18 shot={X:9.998343 Y:-0.1820063}
58: DEMAND path=True required=120 samples=121 shot={X:9.998343 Y:-0.1820063}
59: PASS attack-only short preferred prefix, path+attack long prefix, marker-only OFF zero future steps
60: PASS finite actual candidate selection mouse=False clear=False selected=2
61: PASS finite actual candidate selection mouse=False clear=True selected=3
62: PASS finite actual candidate selection mouse=True clear=False selected=2
63: PASS finite actual candidate selection mouse=True clear=True selected=3
64: PASS natural idle path-only preview, centre/time mapping, pure observation and post-shot delayed preview
65: PASS natural preview fault isolation: bounded retry, healthy path/execution, new weapon and NPC identity recovery
66: System.InvalidOperationException: idle native controlled-family preview appears before any attack: 3475
67:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
68:    at NativeWorldTextProbe.NativeCombatControlPreviewChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatControlPreviewChecks.cs:line 27
69:    at NativeWorldTextProbe.NativeCombatIntegrationChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs:line 46
70:    at NativeWorldTextProbe.NativeChecks.<>c.<Run>b__0_7(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 146
71:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
72:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 146
73:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
74:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## integration-r04.log

Origin: `.local/aim-comprehensive-repair-20261010/integration-r04.log`; SHA-256 `4842E3AF2BAB555FFCCDF67B3C3E6D49FDEE039FC177BA8F260124ED925BB4DE`; encoding UTF-8; candidate **r04**.

exit=1; scope=CombatIntegrationCpu; seconds=30.3656322

```text
60: PASS finite actual candidate selection mouse=False clear=False selected=2
61: PASS finite actual candidate selection mouse=False clear=True selected=3
62: PASS finite actual candidate selection mouse=True clear=False selected=2
63: PASS finite actual candidate selection mouse=True clear=True selected=3
64: PASS natural idle path-only preview, centre/time mapping, pure observation and post-shot delayed preview
65: PASS natural preview static-immunity dependency/expiry recovery: item=1446 projectile=297 recovered=115
66: PASS natural preview fault isolation: bounded retry, healthy path/execution, new weapon and NPC identity recovery
67: PASS natural controlled preview/first birth item=3475 visible=9/9 sources=1
68: PASS natural controlled preview/first birth item=2797 visible=9/9 sources=4
69: PASS natural controlled preview/first birth item=3540 visible=9/9 sources=1
70: PASS natural controlled preview/first birth item=3854 visible=9/9 sources=1
71: System.InvalidOperationException: idle native controlled-family preview appears before any attack: 3930
72:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
73:    at NativeWorldTextProbe.NativeCombatControlPreviewChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatControlPreviewChecks.cs:line 27
74:    at NativeWorldTextProbe.NativeCombatIntegrationChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs:line 46
75:    at NativeWorldTextProbe.NativeChecks.<>c.<Run>b__0_7(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 146
76:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
77:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 146
78:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
79:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## integration-r08.log

Origin: `.local/aim-comprehensive-repair-20261010/integration-r08.log`; SHA-256 `2684829EE60C0D2FD425AE175551B038BDC89901C6400D30FB3F1D2D38E0C2FE`; encoding UTF-8; candidate **r08**.

exit=0; scope=CombatIntegrationCpu; seconds=43.6424065

```text
50: WALL CANDIDATE weapon=39 selected=2 solidColumn=48:y40..42 player={X:710 Y:667} A={X:825 Y:667} B={X:650 Y:667}
51: WALL RESULT weapon=39 selected=2 openingContact=False firstDamage=-1 lifeA=100000 lifeB=100000
52: WALL CANDIDATE weapon=3570 selected=2 solidColumn=48:y40..42 player={X:710 Y:667} A={X:825 Y:667} B={X:650 Y:667}
53: WALL RESULT weapon=3570 selected=2 openingContact=True firstDamage=19 lifeA=99913 lifeB=100000
54: WALL CANDIDATE weapon=3278 selected=2 solidColumn=48:y40..42 player={X:710 Y:667} A={X:825 Y:667} B={X:650 Y:667}
55: WALL RESULT weapon=3278 selected=2 openingContact=False firstDamage=17 lifeA=99993 lifeB=100000
56: PASS same-obstacle shared identity matrix; current passage preference is conditional, finite no-Contact is not a physical-impossibility claim.
57: DEMAND path=False required=17 samples=18 shot={X:9.998343 Y:-0.1820063}
58: DEMAND path=True required=120 samples=121 shot={X:9.998343 Y:-0.1820063}
59: PASS attack-only short preferred prefix, path+attack long prefix, marker-only OFF zero future steps
60: PASS finite actual candidate selection mouse=False clear=False selected=2
61: PASS finite actual candidate selection mouse=False clear=True selected=3
62: PASS finite actual candidate selection mouse=True clear=False selected=2
63: PASS finite actual candidate selection mouse=True clear=True selected=3
64: PASS natural idle path-only preview, centre/time mapping, pure observation and post-shot delayed preview
65: PASS natural preview static-immunity dependency/expiry recovery: item=1446 projectile=297 recovered=115
66: PASS natural preview fault isolation: bounded retry, healthy path/execution, new weapon and NPC identity recovery
67: PASS natural controlled preview/first birth item=3475 visible=9/9 sources=1
68: PASS natural controlled preview/first birth item=2797 visible=9/9 sources=4
69: PASS natural controlled preview/first birth item=3540 visible=9/9 sources=1
70: PASS natural controlled preview/first birth item=3854 visible=9/9 sources=1
71: PASS natural controlled preview/first birth item=3930 visible=1/9 sources=1
72: PASS natural controlled preview/first birth item=113 visible=8/9 sources=0
73: PASS natural controlled preview/first birth item=218 visible=9/9 sources=1
74: PASS natural controlled preview/first birth item=495 visible=9/9 sources=1
75: PASS natural controlled preview/first birth item=3030 visible=9/9 sources=1
76: PASS natural controlled preview/first birth item=3278 visible=9/9 sources=1
77: PASS natural controlled preview/first birth item=3541 visible=9/9 sources=1
78: PASS Celebration native full projectile-update motors: cases=32 subupdates=2496; no autonomous targets in this scalar motor oracle
79: PASS Celebration natural complete cycle: stages=0,1,2,3,4,5,6 actualDamage=13 visible=55/80; native random spread remains Representative
80: PASS native placement input families=46 births=50; CanUse and Shoot share one selected point; DD2 physical point is blocked; autonomous targeting is untouched
81: NEIGHBOR heal life=500 stack=3 delay=3600 weapon=True use=True cursor=True admit=True error=
82: NEIGHBOR recovery actual heal500/stack3/native cooldown; real Aim provider/weapon/cursor preserved
83: NEIGHBOR mine removed=True impact=False selected=1 held=3509 position={X:700 Y:646} use=True animation=14 toolTime=0 mode=2 error=
84: NEIGHBOR mining actual ore removed; Aim prior plan retired; tool cursor returned; gun plan restored
85: NEIGHBOR processing actual bag once; physical release stops; gameplay Aim restored after UI return
86: NEIGHBOR fishing actual Bass/pull/recast right velocity={X:8.299388 Y:3.246318}; current left mouse returns; gun source restored
87: PASS Aim-on neighbors: original recovery, mining, UI bag and fishing claim/return with useful real-provider attack preparation.
```

## weapons-r10.log

Origin: `.local/aim-comprehensive-repair-20261010/weapons-r10.log`; SHA-256 `4BFC26BA745A95CA9DC7EB5A3E143A896DF80C93DD50E85261941E779B4E7F2A`; encoding UTF-8; candidate **r10**.

exit=0; scope=CombatAimCpu; seconds=29.2821552

```text
1491: WEAPON CATALOG SUMMARY eligibleWeapons=465 resolvedWeaponAmmoPairs=1267 unknownPairs=222 missingAmmoGroups=0 excludedTrueWeaponHandStages=0 boundary=model-registration-only; UNKNOWN-is-unfinished-not-PASS
1492: PASS ammo -> native ShotOutcome/Damage: weapon=95 ammo=278 projectile=981 speed=10.5 birth={X:10.49997 Y:-0.02631574} updates=2 firstDamage=19
1493: PASS ammo -> native ShotOutcome/Damage: weapon=95 ammo=3104 projectile=14 speed=10 birth={X:9.999969 Y:-0.02506256} updates=2 firstDamage=20
1494: PASS ammo -> native ShotOutcome/Damage: weapon=1201 ammo=1334 projectile=278 speed=14.75 birth={X:14.74528 Y:-0.3732514} updates=2 firstDamage=14
1495: PASS ammo -> native ShotOutcome/Damage: weapon=905 ammo=71 projectile=158 speed=11 birth={X:10.99997 Y:-0.02756885} updates=2 firstDamage=18
1496: PASS ammo -> native ShotOutcome/Damage: weapon=905 ammo=72 projectile=159 speed=12 birth={X:11.99996 Y:-0.03007507} updates=2 firstDamage=17
1497: PASS ammo -> native ShotOutcome/Damage: weapon=905 ammo=73 projectile=160 speed=13 birth={X:12.99996 Y:-0.03258136} updates=2 firstDamage=15
1498: PASS ammo -> native ShotOutcome/Damage: weapon=905 ammo=74 projectile=161 speed=14 birth={X:13.99996 Y:-0.03508759} updates=2 firstDamage=14
1499: PASS ammo -> native ShotOutcome/Damage: weapon=120 ammo=40 projectile=2 speed=11 birth={X:10.97618 Y:-0.7234928} updates=1 firstDamage=36
1500: PASS ammo -> native ShotOutcome/Damage: weapon=682 ammo=40 projectile=117 speed=14 birth={X:13.9988 Y:-0.1834463} updates=3 firstDamage=17
1501: PASS ammo -> native ShotOutcome/Damage: weapon=725 ammo=40 projectile=120 speed=13 birth={X:12.9966 Y:-0.29749} updates=2 firstDamage=27
1502: PASS ammo -> native ShotOutcome/Damage: weapon=2223 ammo=40 projectile=357 speed=10.75 birth={X:10.74997 Y:-0.02694226} updates=3 firstDamage=13
1503: PASS ammo -> native ShotOutcome/Damage: weapon=3052 ammo=40 projectile=495 speed=14 birth={X:13.9985 Y:-0.205071} updates=1 firstDamage=50
1504: PASS ammo -> native ShotOutcome/Damage: weapon=1254 ammo=97 projectile=242 speed=20 birth={X:15.67482 Y:-0.03928529} updates=8 firstDamage=4
1505: PASS ammo -> native ShotOutcome/Damage: weapon=1254 ammo=278 projectile=981 speed=20.5 birth={X:15.58469 Y:-0.03905939} updates=2 firstDamage=13
1506: PASS ammo -> native ShotOutcome/Damage: weapon=759 ammo=771 projectile=134 speed=5 birth={X:4.999985 Y:-0.01253131} updates=1 firstDamage=29
1507: PASS ammo -> native ShotOutcome/Damage: weapon=758 ammo=771 projectile=133 speed=10 birth={X:9.885144 Y:-1.511263} updates=1 firstDamage=39
1508: PASS ammo -> native ShotOutcome/Damage: weapon=281 ammo=283 projectile=51 speed=11 birth={X:10.98835 Y:-0.5060794} updates=1 firstDamage=35
1509: PASS ammo -> native ShotOutcome/Damage: weapon=986 ammo=1310 projectile=267 speed=15 birth={X:14.99959 Y:0.110684} updates=1 firstDamage=26
1510: PASS ammo -> native ShotOutcome/Damage: weapon=1835 ammo=1836 projectile=323 speed=12 birth={X:11.99996 Y:-0.03007507} updates=4 firstDamage=9
1543: PASS native Hellwing launch-X gravity-clock motor: cases=5 outerUpdates=150
1544: PASS Hellwing representative directional clock contact: cases=12 native first-Damage exact; random births remain Representative
1545: PASS Pewmatic delayed horizontal damping inverse: directional native contacts=12; native random births remain Representative
1546: PASS native AI002 unconditional plus gated wind: 24 cases/960 outer updates
1547: PASS ordinary AI002 throw family real collision/body contacts: 28 directional cases
1548: PASS native VampireKnives integer damage window: 8 finite positive/zero-damage negative cases
1549: PASS real equipped magic/molten/stalker/Phoenix arrow clocks: 8 first Damage cases/396 native motor substeps
1550: PASS native sky input/birth: weapon=3029 representativeTick=43 cursor=1173.224,1020 births=3
1551: NATIVE-SKY-RESULT weapon=3029 seeded firstDamage=63 meanPlan=43
1552: PASS native sky input/birth: weapon=4381 representativeTick=28 cursor=1168.584,1020 births=2
1553: NATIVE-SKY-RESULT weapon=4381 seeded firstDamage=30 meanPlan=28
1554: PASS sky same roof -> real Damage: weapon=4381 firstDamage=0 plan=False
1555: PASS native sky input/birth: weapon=2750 representativeTick=32 cursor=1166.455,1020 births=1
1556: NATIVE-SKY-RESULT weapon=2750 seeded firstDamage=29 meanPlan=32
1557: PASS native sky input/birth: weapon=65 representativeTick=28 cursor=1155.15,1020 births=1
1558: NATIVE-SKY-RESULT weapon=65 seeded firstDamage=28 meanPlan=28
1559: PASS native sky input/birth: weapon=3065 representativeTick=59 cursor=1204.606,1020 births=3
1560: NATIVE-SKY-RESULT weapon=3065 seeded firstDamage=0 meanPlan=59
1561: PASS native sky input/birth: weapon=3570 representativeTick=25 cursor=1152.641,1020 births=3
1562: NATIVE-SKY-RESULT weapon=3570 seeded firstDamage=23 meanPlan=25
1563: PASS sky same roof -> real Damage: weapon=3570 firstDamage=23 plan=True
1564: SCATTER Damage window: type=14 key=spawner:0, index:2, gen:6 tick=20 old=10000 new=9970
1565: SCATTER actual result: weapon=534 seed=7123 births=5 representative=19 firstDamage=20 target={X:1120 Y:646} life=9970
1566: SCATTER Damage window: type=14 key=spawner:0, index:0, gen:166 tick=20 old=10000 new=9985
1567: SCATTER actual result: weapon=964 seed=7123 births=4 representative=22 firstDamage=20 target={X:1120 Y:646} life=9985
1568: SCATTER Damage window: type=14 key=spawner:0, index:0, gen:167 tick=19 old=10000 new=9982
1569: SCATTER actual result: weapon=4703 seed=7123 births=8 representative=19 firstDamage=19 target={X:1119 Y:646} life=9982
1570: SCATTER Damage window: type=661 key=spawner:0, index:4, gen:3 tick=14 old=10000 new=9939
1571: SCATTER actual result: weapon=3788 seed=7123 births=5 representative=19 firstDamage=14 target={X:1114 Y:646} life=9939
1572: SCATTER Damage window: type=1 key=spawner:0, index:2, gen:10 tick=29 old=10000 new=9939
1573: SCATTER actual result: weapon=2624 seed=7123 births=5 representative=29 firstDamage=29 target={X:1129 Y:646} life=9885
1574: SCATTER Damage window: type=278 key=spawner:0, index:1, gen:13 tick=13 old=10000 new=9942
1575: SCATTER actual result: weapon=2624 seed=7123 births=5 representative=13 firstDamage=13 target={X:1113 Y:646} life=9803
1576: SCATTER Damage window: type=1 key=spawner:0, index:1, gen:14 tick=27 old=10000 new=9968
1577: SCATTER actual result: weapon=1229 seed=7123 births=2 representative=29 firstDamage=27 target={X:1127 Y:646} life=9968
1595: PASS actual ItemCheck_Shoot -> Projectile.Update/Damage: weapon=95 targetVx=0 aim=10407.18,658.4001 expected=780,429.7837 tick=19
1596: PASS actual ItemCheck_Shoot -> Projectile.Update/Damage: weapon=95 targetVx=1 aim=10407.46,646.5065 expected=800,429.7943 tick=20
1597: PASS actual ItemCheck_Shoot -> Projectile.Update/Damage: weapon=39 targetVx=0 aim=9483.84,-268.5161 expected=782.028,430.5299 tick=41
1598: PASS actual ItemCheck_Shoot -> Projectile.Update/Damage: weapon=39 targetVx=1 aim=9462.056,-511.8942 expected=826.3949,430.887 tick=46
1599: PASS first natural click/release: Path=False Marker=false, one native shot, no idle virtual plan.
1600: PASS first natural click/release: Path=True Marker=false, one native shot, no idle virtual plan.
1601: PASS shared action/completed phases: one actual player move, native first NPC step, distinct same-tick path publication and matching red ownership.
1602: PASS natural Player.Update moving shooter: jumping=False originDelta={X:3.799988 Y:0} shot={X:9.999976 Y:0.02200171}
1603: PASS natural Player.Update moving shooter: jumping=True originDelta={X:3.900024 Y:-4.900024} shot={X:9.99913 Y:0.1319004}
1604: PASS retained tiny-target negative: all discrete subupdate radii are outside integer damage contact region; minimum gap=2 updates=2; positive shifts birth X by three pixels.
1605: TIMING born={X:710 Y:667} velocity={X:9.998468 Y:-0.1750082} planTick=9 aim=10708.46,491.5884 friendly=True damage=20 immune=0
1606: TIMING predicted frame actualShot={X:887 Y:661 Width:4 Height:4} npc={X:880 Y:663 Width:2 Height:2} immune=0 active=False damage=20 life=9983
1607: PASS completed world T -> Player/Shoot -> moving native NPC -> Projectile/Damage: firstContact=9 timelineIndex=9 sample=15809
1608: PASS preparation isolation: native up/down, read-only terrain, exception-local path survival, finite latch/recovery.
1609: PASS cursor receipts: successor identity, repeated finalizer, independent coordinate ownership.
```

## effects-r10.log

Origin: `.local/aim-comprehensive-repair-20261010/effects-r10.log`; SHA-256 `3BEABAD066963839058EBBB30D480F4100CA3AA33FACCB94DA96BA0E723FBA98`; encoding UTF-8; candidate **r10**.

exit=0; scope=CombatEffectsCpu; seconds=31.7217386

```text
159: EFFECT wall explosion: far=True expected= first=0 active=False body=22x22 center={X:888.8322 Y:643.4042}
160: EFFECT bounce: closed=False expected=9 first=9 bounce=True remaining=0 center={X:859.6711 Y:639.4497}
161: EFFECT bounce: closed=True expected= first=0 bounce=True remaining=1 center={X:813.5 Y:656.669}
162: EFFECT homing boundary: scene=0 contact= first=0 nativeTarget=0 chaseable=True dummyHits=0 blockerLife=0
163: EFFECT homing boundary: scene=1 contact= first=0 nativeTarget=4 chaseable=True dummyHits=0 blockerLife=9979
164: EFFECT homing boundary: scene=2 contact=12 first=12 nativeTarget=0 chaseable=False dummyHits=1 blockerLife=9979
165: OBSTACLE body-first: contact= selectedFirst=0 blockerLife=9980 active=False
166: OBSTACLE slot: type=14 target=7 owner=0 local=0 model=False selected=False other=True remaining=0
167: OBSTACLE slot: type=14 target=3 owner=0 local=0 model=True selected=True other=False remaining=0
168: OBSTACLE slot: type=36 target=7 owner=0 local=0 model=True selected=True other=True remaining=0
169: OBSTACLE slot: type=14 target=7 owner=10 local=0 model=False selected=False other=True remaining=0
170: OBSTACLE slot: type=36 target=7 owner=10 local=0 model=True selected=True other=False remaining=1
171: OBSTACLE slot: type=36 target=7 owner=0 local=-1 model=True selected=True other=False remaining=1
172: OBSTACLE slot: type=36 target=7 owner=0 local=1 model=True selected=True other=True remaining=0
173: OBSTACLE slot: type=36 target=7 owner=0 local=2 model=True selected=True other=False remaining=1
174: PASS changed receiver qualification: kind=0 otherLife=9980 selectedLife=10000
175: PASS changed receiver qualification: kind=1 otherLife=9981 selectedLife=10000
176: PASS changed receiver qualification: kind=2 otherLife=9986 selectedLife=10000
```

## mechanics-r10.log

Origin: `.local/aim-comprehensive-repair-20261010/mechanics-r10.log`; SHA-256 `6A3878428F3BC49CBC49BDA8EBA570DF619FF8A677D75D0CCD5FBA0A05B0C11C`; encoding UTF-8; candidate **r10**.

exit=1; scope=CombatMechanicsCpu; seconds=18.058028

```text
27: SWING direct finite preview=JueMingR.Features.Combat.AttackContact
28: SWING idle: player={X:700 Y:646} direction=1 frame={X:0 Y:0 Width:24 Height:24} visual=4 animation=0 target={X:745 Y:646 Width:18 Height:40} alive=True canReceive=True demand=True future=121 stop=None age=0 preview=null
29: SWING direct finite preview=
30: SWING idle: player={X:700 Y:646} direction=1 frame={X:0 Y:0 Width:24 Height:24} visual=4 animation=0 target={X:746 Y:645 Width:18 Height:40} alive=True canReceive=True demand=True future=121 stop=None age=0 preview=null
31: SWING direct finite preview=
32: SWING idle: player={X:701 Y:646} direction=1 frame={X:0 Y:0 Width:24 Height:24} visual=4 animation=0 target={X:780 Y:648 Width:18 Height:40} alive=True canReceive=True demand=True future=121 stop=None age=0 preview=null
33: SWING direct finite preview=
34: System.InvalidOperationException: idle native held frame supports future ordinary swing without Aim permission
35:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
36:    at NativeWorldTextProbe.NativeCombatSwingChecks.Idle(Object context) in /_/NativeWorldTextProbe/NativeCombatSwingChecks.cs:line 39
37:    at NativeWorldTextProbe.NativeCombatSwingChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatSwingChecks.cs:line 17
38:    at NativeWorldTextProbe.NativeCombatPhaseChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatPhaseChecks.cs:line 29
39:    at NativeWorldTextProbe.NativeCombatMeleeChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatMeleeChecks.cs:line 23
40:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 179
41:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
42:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 179
43:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
44:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## navigation-r04.log

Origin: `.local/aim-comprehensive-repair-20261010/navigation-r04.log`; SHA-256 `54D929A6346C4332C59AEB164A977D624AB2B88E55D11A956F4E21B6A319A08F`; encoding UTF-8; candidate **r04**.

exit=0; scope=CombatNavigationCpu; seconds=24.3904534

```text
51: NARROW horizontal offset=0 tick=0 center={X:710 Y:667} velocity={X:14.95654 Y:-5.683484} point={X:760 Y:648} plan=542
52: NARROW horizontal offset=0 tick=10 center={X:760 Y:632.1101} velocity={X:0 Y:-1.373721} point={X:808 Y:648} plan=542
53: NARROW horizontal offset=0 tick=20 center={X:760 Y:636.8033} velocity={X:0 Y:1.40346} point={X:808 Y:648} plan=542
54: NARROW horizontal offset=0 tick=30 center={X:762.7424 Y:648} velocity={X:1.795013 Y:0} point={X:808 Y:648} plan=542
55: NARROW horizontal offset=0 tick=40 center={X:810.8837 Y:648.5272} velocity={X:6.255307 Y:0.527148} point={X:833 Y:667} plan=542
56: NARROW RESULT horizontal offset=0 entered=True first=542 expected=542 center={X:829.6249 Y:653.5753}
57: NARROW horizontal offset=7.25 tick=0 center={X:717.25 Y:667} velocity={X:14.62098 Y:-6.498215} point={X:760 Y:648} plan=664
58: NARROW horizontal offset=7.25 tick=10 center={X:760 Y:628.497} velocity={X:0 Y:-1.354728} point={X:808 Y:648} plan=664
59: NARROW horizontal offset=7.25 tick=20 center={X:760 Y:635.3354} velocity={X:0 Y:1.686131} point={X:808 Y:648} plan=664
60: NARROW horizontal offset=7.25 tick=30 center={X:765.2953 Y:648} velocity={X:2.553271 Y:0} point={X:808 Y:648} plan=664
61: NARROW horizontal offset=7.25 tick=40 center={X:817.5281 Y:649.2762} velocity={X:6.503348 Y:0.8555719} point={X:841 Y:666} plan=664
62: NARROW RESULT horizontal offset=7.25 entered=True first=664 expected=664 center={X:830.7147 Y:652.4069}
63: NARROW horizontal offset=15 tick=0 center={X:725 Y:667} velocity={X:14.06166 Y:-7.63347} point={X:760 Y:648} plan=786
64: NARROW horizontal offset=15 tick=10 center={X:760 Y:623.7724} velocity={X:0 Y:-1.331247} point={X:808 Y:648} plan=786
65: NARROW horizontal offset=15 tick=20 center={X:760 Y:633.2327} velocity={X:0 Y:2.040786} point={X:808 Y:648} plan=786
66: NARROW horizontal offset=15 tick=30 center={X:765.2953 Y:648} velocity={X:2.553301 Y:0} point={X:808 Y:648} plan=786
67: NARROW horizontal offset=15 tick=40 center={X:817.5283 Y:649.2762} velocity={X:6.503348 Y:0.8555756} point={X:841 Y:666} plan=786
68: NARROW RESULT horizontal offset=15 entered=True first=786 expected=786 center={X:830.7149 Y:652.4069}
69: NARROW vertical offset=0 tick=0 center={X:710 Y:667} velocity={X:13.10771 Y:-9.175398} point={X:760 Y:632} plan=880
70: NARROW vertical offset=0 tick=10 center={X:761.5007 Y:590.607} velocity={X:1.016885 Y:-7.485856} point={X:777 Y:577} plan=880
71: NARROW RESULT vertical offset=0 entered=True first=880 expected=880 center={X:770.649 Y:565.1467}
72: NARROW vertical offset=7.25 tick=0 center={X:717.25 Y:667} velocity={X:12.38009 Y:-10.13575} point={X:760 Y:632} plan=974
73: NARROW vertical offset=7.25 tick=10 center={X:761.4535 Y:584.8428} velocity={X:0.9747205 Y:-7.891845} point={X:785 Y:558} plan=974
74: NARROW RESULT vertical offset=7.25 entered=True first=974 expected=974 center={X:770.9599 Y:555.5352}
75: NARROW vertical offset=15 tick=0 center={X:725 Y:667} velocity={X:11.31371 Y:-11.31371} point={X:760 Y:632} plan=1067
76: NARROW vertical offset=15 tick=10 center={X:762.9403 Y:576.2536} velocity={X:1.497381 Y:-8.196158} point={X:785 Y:558} plan=1067
77: NARROW RESULT vertical offset=15 entered=True first=1067 expected=1067 center={X:771.024 Y:553.8906}
78: NARROW corner offset=0 tick=0 center={X:710 Y:667} velocity={X:14.95654 Y:-5.683484} point={X:760 Y:648} plan=1213
79: NARROW corner offset=0 tick=10 center={X:760 Y:632.1101} velocity={X:0 Y:-1.373721} point={X:808 Y:648} plan=1213
80: NARROW corner offset=0 tick=20 center={X:760 Y:640.6743} velocity={X:0 Y:1.38341} point={X:808 Y:648} plan=1213
81: NARROW corner offset=0 tick=30 center={X:772.3663 Y:648} velocity={X:3.839062 Y:0} point={X:808 Y:648} plan=1213
82: NARROW corner offset=0 tick=40 center={X:820.4705 Y:639.5583} velocity={X:3.244191 Y:-3.195863} point={X:808 Y:600} plan=1213
83: NARROW corner offset=0 tick=50 center={X:821.6607 Y:632.01} velocity={X:-1.751814 Y:-9.765849E-06} point={X:808 Y:600} plan=1213
84: NARROW corner offset=0 tick=60 center={X:808 Y:623.4855} velocity={X:0 Y:-3.23133} point={X:808 Y:600} plan=1213
85: NARROW RESULT corner offset=0 entered=True first=1213 expected=1213 center={X:808.0526 Y:595.6707}
86: NARROW corner offset=7.25 tick=0 center={X:717.25 Y:667} velocity={X:14.62098 Y:-6.498215} point={X:760 Y:648} plan=1357
87: NARROW corner offset=7.25 tick=10 center={X:760 Y:632.3094} velocity={X:0 Y:0.2994068} point={X:808 Y:648} plan=1357
88: NARROW corner offset=7.25 tick=20 center={X:760 Y:643.8069} velocity={X:0 Y:1.340758} point={X:808 Y:648} plan=1357
89: NARROW corner offset=7.25 tick=30 center={X:781.6175 Y:648} velocity={X:4.868443 Y:0} point={X:808 Y:648} plan=1357
90: NARROW corner offset=7.25 tick=40 center={X:825.063 Y:632.01} velocity={X:1.974272 Y:-3.785288} point={X:808 Y:600} plan=1357
91: NARROW corner offset=7.25 tick=50 center={X:817.6605 Y:632.01} velocity={X:-2.060916 Y:-9.765849E-06} point={X:808 Y:600} plan=1357
92: NARROW corner offset=7.25 tick=60 center={X:808 Y:615.265} velocity={X:0 Y:-4.381868} point={X:808 Y:600} plan=1357
93: NARROW RESULT corner offset=7.25 entered=True first=1357 expected=1357 center={X:808.0526 Y:595.6707}
94: NARROW corner offset=15 tick=0 center={X:725 Y:667} velocity={X:14.06166 Y:-7.63347} point={X:760 Y:648} plan=1500
95: NARROW corner offset=15 tick=10 center={X:760 Y:632.8716} velocity={X:0 Y:0.5622457} point={X:808 Y:648} plan=1500
96: NARROW corner offset=15 tick=20 center={X:760 Y:645.089} velocity={X:0 Y:1.282069} point={X:808 Y:648} plan=1500
97: NARROW corner offset=15 tick=30 center={X:786.9208 Y:648} velocity={X:5.303344 Y:0} point={X:808 Y:648} plan=1500
98: NARROW corner offset=15 tick=40 center={X:826.3838 Y:632.01} velocity={X:1.320817 Y:-9.765849E-06} point={X:808 Y:600} plan=1500
99: NARROW corner offset=15 tick=50 center={X:815.5428 Y:632.01} velocity={X:-2.117697 Y:-9.765849E-06} point={X:808 Y:600} plan=1500
100: NARROW corner offset=15 tick=60 center={X:808 Y:610.541} velocity={X:0 Y:-4.724041} point={X:808 Y:600} plan=1500
101: NARROW RESULT corner offset=15 entered=True first=1500 expected=1500 center={X:808.0526 Y:595.6707}
102: NARROW platform offset=0 tick=0 center={X:710 Y:667} velocity={X:14.95654 Y:-5.683484} point={X:760 Y:648} plan=1623
103: NARROW platform offset=0 tick=10 center={X:760 Y:632.1101} velocity={X:0 Y:-1.373721} point={X:808 Y:648} plan=1623
104: NARROW platform offset=0 tick=20 center={X:760 Y:636.8033} velocity={X:0 Y:1.40346} point={X:808 Y:648} plan=1623
105: NARROW platform offset=0 tick=30 center={X:762.7424 Y:648} velocity={X:1.795013 Y:0} point={X:808 Y:648} plan=1623
106: NARROW platform offset=0 tick=40 center={X:810.8837 Y:648.5272} velocity={X:6.255307 Y:0.527148} point={X:833 Y:667} plan=1623
107: NARROW RESULT platform offset=0 entered=True first=1623 expected=1623 center={X:829.6249 Y:653.5753}
108: NARROW platform offset=7.25 tick=0 center={X:717.25 Y:667} velocity={X:14.62098 Y:-6.498215} point={X:760 Y:648} plan=1745
109: NARROW platform offset=7.25 tick=10 center={X:760 Y:628.497} velocity={X:0 Y:-1.354728} point={X:808 Y:648} plan=1745
110: NARROW platform offset=7.25 tick=20 center={X:760 Y:635.3354} velocity={X:0 Y:1.686131} point={X:808 Y:648} plan=1745
111: NARROW platform offset=7.25 tick=30 center={X:765.2953 Y:648} velocity={X:2.553271 Y:0} point={X:808 Y:648} plan=1745
112: NARROW platform offset=7.25 tick=40 center={X:817.5281 Y:649.2762} velocity={X:6.503348 Y:0.8555719} point={X:841 Y:666} plan=1745
113: NARROW RESULT platform offset=7.25 entered=True first=1745 expected=1745 center={X:830.7147 Y:652.4069}
114: NARROW platform offset=15 tick=0 center={X:725 Y:667} velocity={X:14.06166 Y:-7.63347} point={X:760 Y:648} plan=1867
115: NARROW platform offset=15 tick=10 center={X:760 Y:623.7724} velocity={X:0 Y:-1.331247} point={X:808 Y:648} plan=1867
116: NARROW platform offset=15 tick=20 center={X:760 Y:633.2327} velocity={X:0 Y:2.040786} point={X:808 Y:648} plan=1867
117: NARROW platform offset=15 tick=30 center={X:765.2953 Y:648} velocity={X:2.553301 Y:0} point={X:808 Y:648} plan=1867
118: NARROW platform offset=15 tick=40 center={X:817.5283 Y:649.2762} velocity={X:6.503348 Y:0.8555756} point={X:841 Y:666} plan=1867
119: NARROW RESULT platform offset=15 entered=True first=1867 expected=1867 center={X:830.7149 Y:652.4069}
120: NARROW slope offset=0 tick=0 center={X:710 Y:667} velocity={X:14.95654 Y:-5.683484} point={X:760 Y:648} plan=1975
121: NARROW slope offset=0 tick=10 center={X:776 Y:648.0065} velocity={X:0 Y:-0.001048681} point={X:808 Y:648} plan=1975
122: NARROW slope offset=0 tick=20 center={X:788.367 Y:648.0327} velocity={X:3.839174 Y:0.003252177} point={X:808 Y:648} plan=1975
123: NARROW RESULT slope offset=0 entered=True first=1975 expected=1975 center={X:829.1289 Y:655.6371}
124: NARROW slope offset=7.25 tick=0 center={X:717.25 Y:667} velocity={X:14.62098 Y:-6.498215} point={X:760 Y:648} plan=2065
125: NARROW RESULT slope offset=7.25 entered=True first=2065 expected=2065 center={X:828.1708 Y:650.4521}
126: NARROW slope offset=15 tick=0 center={X:725 Y:667} velocity={X:14.06166 Y:-7.63347} point={X:760 Y:648} plan=2172
127: NARROW slope offset=15 tick=10 center={X:776 Y:648.0054} velocity={X:0 Y:-0.001127617} point={X:808 Y:648} plan=2172
128: NARROW slope offset=15 tick=20 center={X:792.7495 Y:648.034} velocity={X:4.382418 Y:0.001334171} point={X:808 Y:648} plan=2172
129: NARROW RESULT slope offset=15 entered=True first=2172 expected=2172 center={X:830.3295 Y:654.0735}
143: PASS natural valid-route progress>14 retirement and +5 recovery; native low-movement braking, not an obstacle deadlock claim
```

## costs-r03.log

Origin: `.local/aim-comprehensive-repair-20261010/costs-r03.log`; SHA-256 `E036F2F5A9A8EA7DC09ACEB9225CDD9D232DDD8D7B4389F3B5834768391A4D43`; encoding UTF-8; candidate **r03**.

exit=1; scope=CombatAttackCosts; seconds=26.7341234

```text
2: ATTACK COST yoyo-first-natural-opening [HostAttackAim.ResolveSelection, 1];[PredictionTerrain.Read, 163];[HostAttackAim.PrepareCore, 1];[HostAttackControl.Clear, 2];[AttackAmmoSnapshot.CaptureCore, 2];[HostYoyoNavigation.OpeningPoint, 1];[HostYoyoNavigation.Find, 2];[PredictionTerrain.ProjectilePassage, 30];[PredictionTerrain.get_Unchanged, 2];[HostYoyoNavigation.Current, 1];[AttackAmmoSnapshot.MembersMatch, 1];[HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1]
4: ATTACK COST yoyo-continuous-hand-wall8 [HostAttackAim.ResolveSelection, 8];[PredictionTerrain.Read, 1129];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 16];[HostYoyoNavigation.Prepare, 8];[HostYoyoNavigation.Remaining, 8];[PredictionTerrain.get_Unchanged, 24];[HostYoyoNavigation.Find, 1];[PredictionTerrain.ProjectilePassage, 4];[AttackAmmoSnapshot.CaptureCore, 8];[HostYoyoNavigation.Current, 8]
6: ATTACK COST yoyo-native-recall-reopen [HostAttackAim.ResolveSelection, 1];[PredictionTerrain.Read, 171];[HostAttackAim.PrepareCore, 1];[HostAttackControl.Clear, 2];[AttackAmmoSnapshot.CaptureCore, 2];[HostYoyoNavigation.OpeningPoint, 1];[HostYoyoNavigation.Find, 2];[PredictionTerrain.ProjectilePassage, 30];[PredictionTerrain.get_Unchanged, 2];[HostYoyoNavigation.Current, 1];[AttackAmmoSnapshot.MembersMatch, 1];[HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1]
7: ATTACK COST yoyo-magic-string-natural32 [HostAttackAim.ResolveSelection, 64];[PredictionTerrain.Read, 6148];[HostAttackAim.PrepareCore, 64];[HostAttackControl.Clear, 128];[AttackAmmoSnapshot.CaptureCore, 104];[HostYoyoNavigation.OpeningPoint, 16];[HostYoyoNavigation.Find, 24];[PredictionTerrain.ProjectilePassage, 312];[PredictionTerrain.get_Unchanged, 87];[HostYoyoNavigation.Current, 16];[AttackAmmoSnapshot.MembersMatch, 8];[HostYoyoNavigation.Prepare, 24];[HostYoyoNavigation.Remaining, 24]
9: ATTACK COST solar-preview [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[HostMeleeAttack.Solve, 2];[PredictionTerrain.Read, 248];[HostMeleeAttack.SolarPose, 12];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4];[AttackAmmoSnapshot.MembersMatch, 6];[PredictionTerrain.get_Unchanged, 6]
10: ATTACK COST solar-attack [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 12];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 372];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 8];[HostMeleeAttack.Solve, 9];[HostMeleeAttack.SolarPose, 54]
11: ATTACK COST solar-unreachable [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 279];[HostMeleeAttack.Solve, 2];[HostMeleeAttack.SolarPose, 58];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
12: ATTACK COST solar-no-target [HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
13: ATTACK COST solar-switch-away [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 279];[HostMeleeAttack.Solve, 2];[HostMeleeAttack.SolarPose, 12];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
15: ATTACK COST idle [HostAttackAim.ResolveSelection, 8];[AttackAmmoSnapshot.CaptureCore, 23];[PredictionTerrain.Read, 925];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[HostAttackObstacles.Eligible, 1791];[AttackIntercept.Solve, 2];[HostAttackAim.Valid, 8];[PredictionTerrain.get_Unchanged, 15];[AttackAmmoSnapshot.MembersMatch, 7];[HostAttackObstacles.get_Unchanged, 7]
16: System.InvalidOperationException: held-weapon idle preparation has a useful read-only preview without an execution capability
```

## costs-r07.log

Origin: `.local/aim-comprehensive-repair-20261010/costs-r07.log`; SHA-256 `F3B65B238ECFC68D610F0483E65D6AB8050AD899D147D4F91CBAFC5FD9BB5DFD`; encoding UTF-8; candidate **r07**.

exit=0; scope=CombatAttackCosts; seconds=16.6486761

```text
2: ATTACK COST yoyo-first-natural-opening [HostAttackAim.ResolveSelection, 1];[PredictionTerrain.Read, 163];[HostAttackAim.PrepareCore, 1];[HostAttackControl.Clear, 2];[AttackAmmoSnapshot.CaptureCore, 2];[HostYoyoNavigation.OpeningPoint, 1];[HostYoyoNavigation.Find, 2];[PredictionTerrain.ProjectilePassage, 30];[PredictionTerrain.get_Unchanged, 2];[HostYoyoNavigation.Current, 1];[AttackAmmoSnapshot.MembersMatch, 1];[HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1]
4: ATTACK COST yoyo-continuous-hand-wall8 [HostAttackAim.ResolveSelection, 8];[PredictionTerrain.Read, 1129];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 16];[HostYoyoNavigation.Prepare, 8];[HostYoyoNavigation.Remaining, 8];[PredictionTerrain.get_Unchanged, 24];[HostYoyoNavigation.Find, 1];[PredictionTerrain.ProjectilePassage, 4];[AttackAmmoSnapshot.CaptureCore, 8];[HostYoyoNavigation.Current, 8]
6: ATTACK COST yoyo-native-recall-reopen [HostAttackAim.ResolveSelection, 1];[PredictionTerrain.Read, 171];[HostAttackAim.PrepareCore, 1];[HostAttackControl.Clear, 2];[AttackAmmoSnapshot.CaptureCore, 2];[HostYoyoNavigation.OpeningPoint, 1];[HostYoyoNavigation.Find, 2];[PredictionTerrain.ProjectilePassage, 30];[PredictionTerrain.get_Unchanged, 2];[HostYoyoNavigation.Current, 1];[AttackAmmoSnapshot.MembersMatch, 1];[HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1]
7: ATTACK COST yoyo-magic-string-natural32 [HostAttackAim.ResolveSelection, 64];[PredictionTerrain.Read, 6148];[HostAttackAim.PrepareCore, 64];[HostAttackControl.Clear, 128];[AttackAmmoSnapshot.CaptureCore, 104];[HostYoyoNavigation.OpeningPoint, 16];[HostYoyoNavigation.Find, 24];[PredictionTerrain.ProjectilePassage, 312];[PredictionTerrain.get_Unchanged, 87];[HostYoyoNavigation.Current, 16];[AttackAmmoSnapshot.MembersMatch, 8];[HostYoyoNavigation.Prepare, 24];[HostYoyoNavigation.Remaining, 24]
9: ATTACK COST solar-preview [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[HostMeleeAttack.Solve, 2];[PredictionTerrain.Read, 248];[HostMeleeAttack.SolarPose, 12];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4];[AttackAmmoSnapshot.MembersMatch, 6];[PredictionTerrain.get_Unchanged, 6]
10: ATTACK COST solar-attack [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 12];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 372];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 8];[HostMeleeAttack.Solve, 9];[HostMeleeAttack.SolarPose, 54]
11: ATTACK COST solar-unreachable [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 279];[HostMeleeAttack.Solve, 2];[HostMeleeAttack.SolarPose, 58];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
12: ATTACK COST solar-no-target [HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
13: ATTACK COST solar-switch-away [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 279];[HostMeleeAttack.Solve, 2];[HostMeleeAttack.SolarPose, 12];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
14: ATTACK COST natural-idle-controller-3475 [HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.CaptureCore, 8];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 416];[HostAttackObstacles.get_Unchanged, 8];[HostAttackObstacles.Eligible, 1592]
15: ATTACK COST natural-idle-controller-2797 [HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.CaptureCore, 8];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 272];[HostAttackObstacles.get_Unchanged, 8];[HostAttackObstacles.Eligible, 1592]
16: ATTACK COST natural-idle-controller-3541 [HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.CaptureCore, 8];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 8672]
17: ATTACK COST solar-natural-player-npc-projectile45 [HostAttackAim.ResolveSelection, 90];[PredictionTerrain.get_Unchanged, 47];[HostAttackAim.PrepareCore, 90];[HostAttackControl.Clear, 180];[AttackAmmoSnapshot.CaptureCore, 94];[HostMeleeAttack.Solve, 137];[HostMeleeAttack.SolarPose, 1057];[PredictionTerrain.Read, 4247];[HostAttackAim.Valid, 2];[AttackAmmoSnapshot.MembersMatch, 4];[HostMeleeAttack.KnownSolar, 43]
18: SOLAR natural actions=45 live=43 actualDamage=6 preview=36 poses=1057 modelCalls=180; component workload, not FPS
20: ATTACK COST idle [HostAttackAim.ResolveSelection, 8];[AttackAmmoSnapshot.CaptureCore, 23];[PredictionTerrain.Read, 925];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[HostAttackObstacles.Eligible, 1791];[AttackIntercept.Solve, 2];[HostAttackAim.Valid, 8];[PredictionTerrain.get_Unchanged, 15];[AttackAmmoSnapshot.MembersMatch, 7];[HostAttackObstacles.get_Unchanged, 7]
33: ATTACK COST navigation-prepare [HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1];[HostYoyoNavigation.Find, 1];[PredictionTerrain.ProjectilePassage, 10];[PredictionTerrain.Read, 55]
35: ATTACK COST charged-beam-wall24x6 [HostBeamAttack.Solve, 1];[HostBeamAttack.Length, 1];[PredictionTerrain.ProjectilePassage, 32];[PredictionTerrain.Read, 73]
36: ATTACK COST charged-beam-open-change [HostBeamAttack.Solve, 1];[HostBeamAttack.Length, 1];[PredictionTerrain.ProjectilePassage, 149];[PredictionTerrain.Read, 228]
37: ATTACK COST partial-beam-distinct-rays [HostBeamAttack.Solve, 1];[HostBeamAttack.Length, 355];[PredictionTerrain.ProjectilePassage, 9905];[PredictionTerrain.Read, 119]
38: PASS attack costs: real finite preparation/qualification/retirement/navigation and beam scans; component calls, not game FPS.
```

## attack-costs-r19.log

Origin: `.local/aim-comprehensive-repair-20261010/attack-costs-r19.log`; SHA-256 `86F604E00F6D3DC4E5E8722776F5A8A119B7226F73351EFDDE8EEDB5F400382C`; encoding UTF-8; candidate **r19**.

exit=0; scope=CombatAttackCosts; seconds=15.7930081

```text
2: ATTACK COST yoyo-first-natural-opening [HostAttackAim.ResolveSelection, 1];[PredictionTerrain.Read, 163];[HostAttackAim.PrepareCore, 1];[HostAttackControl.Clear, 2];[AttackAmmoSnapshot.CaptureCore, 2];[HostYoyoNavigation.OpeningPoint, 1];[HostYoyoNavigation.Find, 2];[PredictionTerrain.ProjectilePassage, 30];[PredictionTerrain.get_Unchanged, 2];[HostYoyoNavigation.Current, 1];[AttackAmmoSnapshot.MembersMatch, 1];[HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1]
4: ATTACK COST yoyo-continuous-hand-wall8 [HostAttackAim.ResolveSelection, 8];[PredictionTerrain.Read, 1129];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 16];[HostYoyoNavigation.Prepare, 8];[HostYoyoNavigation.Remaining, 8];[PredictionTerrain.get_Unchanged, 24];[HostYoyoNavigation.Find, 1];[PredictionTerrain.ProjectilePassage, 4];[AttackAmmoSnapshot.CaptureCore, 8];[HostYoyoNavigation.Current, 8]
6: ATTACK COST yoyo-native-recall-reopen [HostAttackAim.ResolveSelection, 1];[PredictionTerrain.Read, 171];[HostAttackAim.PrepareCore, 1];[HostAttackControl.Clear, 2];[AttackAmmoSnapshot.CaptureCore, 2];[HostYoyoNavigation.OpeningPoint, 1];[HostYoyoNavigation.Find, 2];[PredictionTerrain.ProjectilePassage, 30];[PredictionTerrain.get_Unchanged, 2];[HostYoyoNavigation.Current, 1];[AttackAmmoSnapshot.MembersMatch, 1];[HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1]
7: ATTACK COST yoyo-magic-string-natural32 [HostAttackAim.ResolveSelection, 64];[PredictionTerrain.Read, 6148];[HostAttackAim.PrepareCore, 64];[HostAttackControl.Clear, 128];[AttackAmmoSnapshot.CaptureCore, 104];[HostYoyoNavigation.OpeningPoint, 16];[HostYoyoNavigation.Find, 24];[PredictionTerrain.ProjectilePassage, 312];[PredictionTerrain.get_Unchanged, 87];[HostYoyoNavigation.Current, 16];[AttackAmmoSnapshot.MembersMatch, 8];[HostYoyoNavigation.Prepare, 24];[HostYoyoNavigation.Remaining, 24]
9: ATTACK COST solar-preview [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[HostMeleeAttack.Solve, 2];[PredictionTerrain.Read, 248];[HostMeleeAttack.SolarPose, 12];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4];[AttackAmmoSnapshot.MembersMatch, 6];[PredictionTerrain.get_Unchanged, 6]
10: ATTACK COST solar-attack [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 12];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 403];[HostMeleeAttack.Solve, 10];[HostMeleeAttack.SolarPose, 60];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 8]
11: ATTACK COST solar-unreachable [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 279];[HostMeleeAttack.Solve, 2];[HostMeleeAttack.SolarPose, 58];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
12: ATTACK COST solar-no-target [HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
13: ATTACK COST solar-switch-away [HostAttackAim.ResolveSelection, 4];[AttackAmmoSnapshot.CaptureCore, 8];[AttackAmmoSnapshot.MembersMatch, 7];[PredictionTerrain.get_Unchanged, 7];[PredictionTerrain.Read, 279];[HostMeleeAttack.Solve, 2];[HostMeleeAttack.SolarPose, 12];[HostAttackAim.PrepareCore, 4];[HostAttackControl.Clear, 4]
14: ATTACK COST natural-idle-controller-3475 [HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.CaptureCore, 8];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 440];[HostAttackObstacles.get_Unchanged, 8];[HostAttackObstacles.Eligible, 1592]
15: ATTACK COST natural-idle-controller-2797 [HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.CaptureCore, 8];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 296];[HostAttackObstacles.get_Unchanged, 8];[HostAttackObstacles.Eligible, 1592]
16: ATTACK COST natural-idle-controller-3541 [HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.CaptureCore, 8];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 8672]
17: ATTACK COST solar-natural-player-npc-projectile45 [HostAttackAim.ResolveSelection, 90];[PredictionTerrain.get_Unchanged, 47];[HostAttackAim.PrepareCore, 90];[HostAttackControl.Clear, 180];[AttackAmmoSnapshot.CaptureCore, 94];[HostMeleeAttack.Solve, 137];[HostMeleeAttack.SolarPose, 1057];[PredictionTerrain.Read, 4247];[HostAttackAim.Valid, 2];[AttackAmmoSnapshot.MembersMatch, 6];[HostMeleeAttack.KnownSolar, 43]
18: SOLAR natural actions=45 live=43 actualDamage=6 preview=36 poses=1057 modelCalls=180; component workload, not FPS
19: ATTACK COST harpy-natural-idle8 [AttackAmmoSnapshot.CaptureCore, 32];[HostAttackAim.ResolveSelection, 8];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[AttackAmmoSnapshot.MembersMatch, 24];[State.get_MembersCurrent, 16];[PredictionTerrain.get_Unchanged, 8];[PredictionTerrain.Read, 568];[HostAttackObstacles.get_Unchanged, 16];[HostAttackObstacles.Eligible, 3184];[State.get_Current, 8]
20: ATTACK COST harpy-presentation20 [AttackAmmoSnapshot.MembersMatch, 40];[State.get_MembersCurrent, 20]
21: ATTACK COST harpy-natural-first-use40 [AttackAmmoSnapshot.CaptureCore, 242];[HostAttackAim.ResolveSelection, 80];[PredictionTerrain.get_Unchanged, 50];[HostAttackAim.PrepareCore, 80];[HostAttackControl.Clear, 81];[HostAttackObstacles.Eligible, 16119];[HostHarpyAttack.Solve, 32];[PredictionTerrain.Read, 2982];[HostAttackAim.Valid, 1];[AttackAmmoSnapshot.MembersMatch, 62];[HostAttackObstacles.get_Unchanged, 49];[State.get_Current, 40];[State.get_MembersCurrent, 50]
22: HARPY natural updates=40 actualDamage=1 preview=40 finiteSolves=32; component work, not FPS
24: ATTACK COST idle [HostAttackAim.ResolveSelection, 8];[AttackAmmoSnapshot.CaptureCore, 23];[PredictionTerrain.Read, 925];[HostAttackAim.PrepareCore, 8];[HostAttackControl.Clear, 8];[HostAttackObstacles.Eligible, 1791];[AttackIntercept.Solve, 2];[HostAttackAim.Valid, 8];[PredictionTerrain.get_Unchanged, 15];[AttackAmmoSnapshot.MembersMatch, 7];[HostAttackObstacles.get_Unchanged, 7]
37: ATTACK COST navigation-prepare [HostYoyoNavigation.Prepare, 1];[HostYoyoNavigation.Remaining, 1];[HostYoyoNavigation.Find, 1];[PredictionTerrain.ProjectilePassage, 10];[PredictionTerrain.Read, 55]
39: ATTACK COST charged-beam-wall24x6 [HostBeamAttack.Solve, 1];[HostBeamAttack.Length, 1];[PredictionTerrain.ProjectilePassage, 32];[PredictionTerrain.Read, 73]
40: ATTACK COST charged-beam-open-change [HostBeamAttack.Solve, 1];[HostBeamAttack.Length, 1];[PredictionTerrain.ProjectilePassage, 149];[PredictionTerrain.Read, 228]
41: ATTACK COST partial-beam-distinct-rays [HostBeamAttack.Solve, 1];[HostBeamAttack.Length, 355];[PredictionTerrain.ProjectilePassage, 9905];[PredictionTerrain.Read, 119]
42: PASS attack costs: real finite preparation/qualification/retirement/navigation and beam scans; component calls, not game FPS.
```

## control-r08.log

Origin: `.local/aim-comprehensive-repair-20261010/control-r08.log`; SHA-256 `A8A8E66EC0541ED56B0594A611CEC863392C376B0E748F1C42FAA25C3B2B19D9`; encoding UTF-8; candidate **r08**.

exit=1; scope=CombatControlCpu; seconds=27.2727426

```text
172: PASS actual guided contact: weapon=113 firstDamage=12 impact=1112,666.0341
173: PASS actual guided AI/release: weapon=218 released=-1
174: PASS actual guided contact: weapon=218 firstDamage=12 impact=1112,666.0341
175: PASS actual guided AI/release: weapon=495 released=-1
176: PASS actual guided contact: weapon=495 firstDamage=12 impact=1112,666.0341
177: PASS natural bubble Kill after release/ammo change: child=14 velocity={X:15.99995 Y:-0.04090214}
178: PASS bubble permission exits: OFF and same-type new weapon retain native death and existing child velocity.
179: PASS original bubble AI78 collision/angle movement substeps=1392 reflections=30
180: System.InvalidOperationException: natural Kill child birth uses the predicted reflected origin
181:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
182:    at NativeWorldTextProbe.NativeCombatBubbleChecks.Natural(Object context, Object host, Object combat, Object input, Object attack, Object terrain, MethodInfo advance) in /_/NativeWorldTextProbe/NativeCombatBubbleChecks.cs:line 86
183:    at NativeWorldTextProbe.NativeCombatBubbleChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatBubbleChecks.cs:line 53
184:    at NativeWorldTextProbe.NativeCombatControlChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatControlChecks.cs:line 49
185:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 179
186:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
187:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 179
188:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
189:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## bubble-r12.log

Origin: `.local/aim-comprehensive-repair-20261010/bubble-r12.log`; SHA-256 `A22A46C09E2F04D6519EBC6FA650655AA0FD2E9FCDEB1EC1B3838B01000BC195`; encoding UTF-8; candidate **r12**.

exit=0; scope=CombatControlCpu; seconds=19.2636314

```text
1: COMPOSITION initialization-ms=6720.8208 code-prepare-ms=153.4593 exact-comparison=False
2: PASS original bubble AI78 collision/angle movement substeps=1392 reflections=30
3: BUBBLE original birth sample pos=660.9337,652.2706 velocity=-2.26791739,-1.57468414 angle=-2.97939372 left=58 extra=1
4: BUBBLE Kill entry active=True type=444 left=0 center=708.5279,626.0792 movements=58
5: PASS natural bubble wall bounce -> lifetime Kill -> sealed child -> actual Damage; visible=28 damage=1 origin={X:708.5279 Y:626.0792}
```

## cold-bolt-r18b.log

Origin: `.local/aim-comprehensive-repair-20261010/cold-bolt-r18b.log`; SHA-256 `0E072F027E7F8B9DCDAEA2C367D1DB0AC60E1A0D9074D66ACF02F2293DDEA2B6`; encoding UTF-8; candidate **r18b**.

exit=0; scope=CombatEffectsCpu; seconds=21.7311515

```text
1: COMPOSITION initialization-ms=7291.1638 code-prepare-ms=256.6157 exact-comparison=False
2: COLD BOLT item=676 scene=0 native=26 preview=26
3: COLD BOLT item=676 scene=1 native=43 preview=43
4: COLD BOLT item=676 scene=2 native=77 preview=77
5: COLD BOLT item=676 scene=3 native=-1 preview=
6: COLD BOLT item=676 scene=4 native=26 preview=26
7: COLD BOLT item=676 scene=5 native=-1 preview=
8: COLD BOLT item=724 scene=0 native=32 preview=32
9: COLD BOLT item=724 scene=1 native=54 preview=54
10: COLD BOLT item=724 scene=2 native=98 preview=98
11: COLD BOLT item=724 scene=3 native=-1 preview=
12: COLD BOLT item=724 scene=4 native=-1 preview=
13: COLD BOLT item=724 scene=5 native=32 preview=32
14: COLD BOLT item=726 scene=0 native=19 preview=19
15: COLD BOLT item=726 scene=1 native=32 preview=32
16: COLD BOLT item=726 scene=2 native=58 preview=58
17: COLD BOLT item=726 scene=3 native=-1 preview=
18: COLD BOLT item=726 scene=4 native=19 preview=19
19: COLD BOLT item=726 scene=5 native=19 preview=19
20: PASS cold bolt original first-use cases=18 damage=13 negative=5
```

## cold-bolt-r19.log

Origin: `.local/aim-comprehensive-repair-20261010/cold-bolt-r19.log`; SHA-256 `066443DB4B2D89F239F603090C4FA0B29408E2B3E46913EFC84CA4DC16F2C180`; encoding UTF-8; candidate **r19**.

exit=0; scope=CombatEffectsCpu; seconds=16.917308

```text
1: COMPOSITION initialization-ms=5496.893 code-prepare-ms=187.2255 exact-comparison=False
2: COLD BOLT item=676 scene=0 native=26 preview=26
3: COLD BOLT item=676 scene=1 native=43 preview=43
4: COLD BOLT item=676 scene=2 native=77 preview=77
5: COLD BOLT item=676 scene=3 native=-1 preview=
6: COLD BOLT item=676 scene=4 native=26 preview=26
7: COLD BOLT item=676 scene=5 native=-1 preview=
8: COLD BOLT item=724 scene=0 native=32 preview=32
9: COLD BOLT item=724 scene=1 native=54 preview=54
10: COLD BOLT item=724 scene=2 native=98 preview=98
11: COLD BOLT item=724 scene=3 native=-1 preview=
12: COLD BOLT item=724 scene=4 native=-1 preview=
13: COLD BOLT item=724 scene=5 native=32 preview=32
14: COLD BOLT item=726 scene=0 native=19 preview=19
15: COLD BOLT item=726 scene=1 native=32 preview=32
16: COLD BOLT item=726 scene=2 native=58 preview=58
17: COLD BOLT item=726 scene=3 native=-1 preview=
18: COLD BOLT item=726 scene=4 native=19 preview=19
19: COLD BOLT item=726 scene=5 native=19 preview=19
20: PASS cold bolt original first-use cases=18 damage=13 negative=5
```

## attached-r20.log

Origin: `.local/aim-comprehensive-repair-20261010/attached-r20.log`; SHA-256 `5DFF39F377BA5C6982A8BDEBBD1ED08A2F79620B7F4F6F2DC3B7B377D216AE8E`; encoding UTF-8; candidate **r20**.

exit=1; scope=NpcAttachedWeapon; seconds=283.1541152

```text
21:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
22:    at NativeWorldTextProbe.NativeCombatAttachedWeaponChecks.Continuous(Object context, NpcPredictionCache cache, Action step, String output) in /_/NativeWorldTextProbe/NativeCombatAttachedWeaponChecks.cs:line 176
23: FAIL natural attached component=395 mode=1 System.InvalidOperationException: Natural complete component publishes curved long forecasts family=395 mode=1 members=395/393/394/392 long=22037 published=1799 actions=10799
24:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
25:    at NativeWorldTextProbe.NativeCombatAttachedWeaponChecks.Continuous(Object context, NpcPredictionCache cache, Action step, String output) in /_/NativeWorldTextProbe/NativeCombatAttachedWeaponChecks.cs:line 176
26: PASS natural AI75 component=491 mode=0 matched=31848 long=22037 fullActions=8999 members=491/492 maxError=0
27: PASS natural AI75 component=491 mode=1 matched=31848 long=22037 fullActions=8999 members=491/492 maxError=0
28: SESSION rolling default; native owner absent, helper requests/history/background=0
29: System.InvalidOperationException: Complete natural attached components: 395-0:Natural complete component publishes curved long forecasts family=395 mode=0 members=395/393/394/392 long=22037 published=1799 actions=10799 | 395-1:Natural complete component publishes curved long forecasts family=395 mode=1 members=395/393/394/392 long=22037 published=1799 actions=10799
30:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
31:    at NativeWorldTextProbe.NativeCombatAttachedWeaponChecks.Continuous(Object context, NpcPredictionCache cache, Action step, String output) in /_/NativeWorldTextProbe/NativeCombatAttachedWeaponChecks.cs:line 185
32:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.Run(Object context, String output, String content) in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 296
33:    at NativeWorldTextProbe.NativeCombatWorkerChecks.<>c__DisplayClass5_0.<Run>b__0(Object context) in /_/NativeWorldTextProbe/NativeCombatWorkerChecks.cs:line 100
34:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
35:    at NativeWorldTextProbe.NativeCombatWorkerChecks.Run(String output, Boolean catalogue, Boolean linkedOnly, Boolean lifetimeOnly, Boolean assetsOnly, Boolean randomOnly, Boolean playerOnly, Boolean entityOnly, Boolean birthOnly, Boolean contextOnly, Boolean immunityOnly, Boolean lifecycleOnly, Boolean fieldsOnly, Boolean transportOnly, Boolean productionOnly, String content, Boolean preparationOnly, Boolean menuOnly, Boolean snapshotOnly, Boolean longCoverageOnly, Boolean legalOnly) in /_/NativeWorldTextProbe/NativeCombatWorkerChecks.cs:line 100
36:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 126
37:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
38:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## attached-r21.log

Origin: `.local/aim-comprehensive-repair-20261010/attached-r21.log`; SHA-256 `D9C48983E308F5EB6505A569E9DD15074FFE63AD1C06A88D081A08059B9F5CCE`; encoding UTF-8; candidate **r21**.

exit=0; scope=NpcAttachedWeapon; seconds=290.0028848

```text
8: PASS production codec refusal-page / raw identity bits / invalid geometry
9: OFF ENTRY before user=C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\attached-r21\u-ec2e6e54e399 loaded=True path=False worker=rolling requests=0
10: OFF ENTRY after loaded=True path=False worker=rolling requests=0
11: PASS AI75 exact families / required component / XNA matrix8192
12: PASS AI75/76/93 native scalar=122 retirement/form boundaries=6 original line edges=18
13: PASS initialized 516 native scalar256
14: PASS original attached orphan server/client transformations=6 isolatedOriginalNetworkOutlets=4
15: PASS original AI76 unsafe raw tile edge counterexamples=3 and fresh native candidate recovery; no forecast crosses the original exception
16: PASS natural AI75 component=390 mode=0 matched=31848 long=22037 fullActions=3599 members=390/391 velocityCurves=1338 positionCurves=1585 maxError=0
17: PASS natural AI75 component=390 mode=1 matched=31848 long=22037 fullActions=3599 members=390/391 velocityCurves=1375 positionCurves=1624 maxError=0
18: PASS natural AI75 component=416 mode=0 matched=24484 long=15367 fullActions=4732 members=416/415/516 velocityCurves=771 positionCurves=1147 maxError=0
19: PASS natural AI75 component=416 mode=1 matched=15698 long=10782 fullActions=2151 members=416/415/516 velocityCurves=272 positionCurves=416 maxError=0
20: PASS natural AI75 component=395 mode=0 matched=31848 long=22037 fullActions=10799 members=395/393/394/392 velocityCurves=0 positionCurves=1131 maxError=0
21: PASS natural AI75 component=395 mode=1 matched=31848 long=22037 fullActions=10799 members=395/393/394/392 velocityCurves=0 positionCurves=1131 maxError=0
22: PASS natural AI75 component=491 mode=0 matched=31848 long=22037 fullActions=8999 members=491/492 velocityCurves=1257 positionCurves=1289 maxError=0
23: PASS natural AI75 component=491 mode=1 matched=31848 long=22037 fullActions=8999 members=491/492 velocityCurves=1295 positionCurves=1345 maxError=0
24: PASS AI75 eight complete natural components matched=231270 long=158371
25: SESSION rolling default; native owner absent, helper requests/history/background=0
```

## queen-bee-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/queen-bee-r22.log`; SHA-256 `0FD0254A82BC872D4C25FD815909FE1328FD0418A37E9A774E043E1F22C6434C`; encoding UTF-8; candidate **r22**.

exit=0; scope=NpcQueenBee; seconds=430.0480582

```text
1: BUILD prediction-configuration=Debug
2: RUNTIME JueMingR.TerrariaHost mvid=e29edbbb-b27b-473b-9489-c0fcee506ba9 optimized=False
3: RUNTIME JueMingR.Platform mvid=89bc5079-7430-4500-9c32-b5780e4909de optimized=False
4: RUNTIME JueMingR.Features mvid=7012f410-1bd0-45ea-95dc-a4cc95ab483a optimized=False
5: RUNTIME JueMingR.Infrastructure mvid=b559a70d-2158-4026-a779-aaaafaa1f502 optimized=False
6: Explicit historical candidate: C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\queen-bee-r22\worker-layout-63d2342954b848fd93683e4243644d40\JueMingR.TerrariaHost.dll MVID=e29edbbb-b27b-473b-9489-c0fcee506ba9
7: COMPOSITION initialization-ms=6473.0186 code-prepare-ms=244.687 exact-comparison=False
8: PASS production codec refusal-page / raw identity bits / invalid geometry
9: OFF ENTRY before user=C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\queen-bee-r22\u-53f8ffad3ecb loaded=True path=False worker=rolling requests=0
10: OFF ENTRY after loaded=True path=False worker=rolling requests=0
11: PASS QueenBee native8192roots/server-client/20-origin proof/ghost/dead/multiplayer boundaries
12: QUEEN-BEE scene=0 actions=3194 matched=66516 long=48594 cross=54565 random=6 restored=6 far=0 dead=0 retired=0 error=0
13: QUEEN-BEE scene=1 actions=3192 matched=64944 long=47138 cross=54881 random=8 restored=7 far=0 dead=0 retired=0 error=0
14: QUEEN-BEE scene=2 actions=3186 matched=53054 long=36145 cross=44887 random=14 restored=14 far=0 dead=0 retired=0 error=0
15: QUEEN-BEE scene=3 actions=3184 matched=53103 long=36198 cross=45391 random=16 restored=16 far=0 dead=0 retired=0 error=0
16: QUEEN-BEE scene=4 actions=3181 matched=50529 long=33707 cross=39066 random=19 restored=19 far=0 dead=0 retired=0 error=0
17: QUEEN-BEE scene=5 actions=3191 matched=61529 long=44037 cross=49088 random=9 restored=9 far=200 dead=0 retired=0 error=0
18: QUEEN-BEE scene=6 actions=1454 matched=22944 long=16343 cross=19545 random=3 restored=3 far=0 dead=256 retired=1 error=0
19: QUEEN-BEE scene=7 actions=3191 matched=30728 long=20675 cross=26469 random=9 restored=9 far=0 dead=0 retired=0 error=0
20: PASS QueenBee natural scenes=8 matched=403347 actions=23773 random=84 restored=83
21: SESSION rolling default; native owner absent, helper requests/history/background=0
```

## brain-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/brain-r22.log`; SHA-256 `73F3717D5A3B1D51FE362FFEA408A57C25AF1DE516385B4CA796CA4CEE2B2AF2`; encoding UTF-8; candidate **r22**.

exit=1; scope=NpcBrainSwarm; seconds=13.5345073

```text
1: BUILD prediction-configuration=Debug
2: RUNTIME JueMingR.TerrariaHost mvid=e29edbbb-b27b-473b-9489-c0fcee506ba9 optimized=False
3: RUNTIME JueMingR.Platform mvid=89bc5079-7430-4500-9c32-b5780e4909de optimized=False
4: RUNTIME JueMingR.Features mvid=7012f410-1bd0-45ea-95dc-a4cc95ab483a optimized=False
5: RUNTIME JueMingR.Infrastructure mvid=b559a70d-2158-4026-a779-aaaafaa1f502 optimized=False
6: Explicit historical candidate: C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\brain-r22\worker-layout-66a90fb6766547ba86d0242a7b8aa8c1\JueMingR.TerrariaHost.dll MVID=e29edbbb-b27b-473b-9489-c0fcee506ba9
7: COMPOSITION initialization-ms=6378.4282 code-prepare-ms=274.0271 exact-comparison=False
8: PASS production codec refusal-page / raw identity bits / invalid geometry
9: OFF ENTRY before user=C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\brain-r22\u-f07cf087d8fd loaded=True path=False worker=rolling requests=0
10: OFF ENTRY after loaded=True path=False worker=rolling requests=0
11: SESSION rolling default; native owner absent, helper requests/history/background=0
12: System.InvalidOperationException: Original XNA Creeper inertia/division field stores sample=2 good=False
13:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
14:    at NativeWorldTextProbe.NativeCombatBrainSwarmChecks.Boundaries() in /_/NativeWorldTextProbe/NativeCombatBrainSwarmChecks.cs:line 48
15:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.Run(Object context, String output, String content) in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 298
16:    at NativeWorldTextProbe.NativeCombatWorkerChecks.<>c__DisplayClass5_0.<Run>b__0(Object context) in /_/NativeWorldTextProbe/NativeCombatWorkerChecks.cs:line 100
17:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
18:    at NativeWorldTextProbe.NativeCombatWorkerChecks.Run(String output, Boolean catalogue, Boolean linkedOnly, Boolean lifetimeOnly, Boolean assetsOnly, Boolean randomOnly, Boolean playerOnly, Boolean entityOnly, Boolean birthOnly, Boolean contextOnly, Boolean immunityOnly, Boolean lifecycleOnly, Boolean fieldsOnly, Boolean transportOnly, Boolean productionOnly, String content, Boolean preparationOnly, Boolean menuOnly, Boolean snapshotOnly, Boolean longCoverageOnly, Boolean legalOnly) in /_/NativeWorldTextProbe/NativeCombatWorkerChecks.cs:line 100
19:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 127
20:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
21:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## wall-boss-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/wall-boss-r22.log`; SHA-256 `6380DBB7E353A6AB9069231C6D20B5702464ACD32FDA8C787957AC34E298287F`; encoding UTF-8; candidate **r22**.

exit=1; scope=NpcWallBoss; seconds=105.4864998

```text
111:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.Step(Object context, List`1 samples, List`1 prepares, Boolean right, Action sampleIntent, Boolean prepareHost) in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 538
112:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.<>c__DisplayClass21_0.<Run>b__23() in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 114
113:    at NativeWorldTextProbe.NativeCombatWallBossChecks.Continuous(Object context, NpcPredictionCache cache, Action step, String output) in /_/NativeWorldTextProbe/NativeCombatWallBossChecks.cs:line 185
114: WALL scene=11 body=286 eyes=574 hungry=3157 pc=106 tongue=25 gross=106 snap=0 restored=0 matched=1071 long=555 cross=0 selected=106 published=105 retired=1 error=0
115: WALL FAIL scene=12: System.InvalidOperationException: Actual canonical Wall health continuation matches original UpdateLifeRegen.
116:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
117:    at NativeWorldTextProbe.NativeCombatWallBossChecks.<>c__DisplayClass20_2.<Continuous>b__3(Player p) in /_/NativeWorldTextProbe/NativeCombatWallBossChecks.cs:line 171
118:    at NativeWorldTextProbe.NativeCombatWallBossChecks.PlayerAfter(Player __instance) in /_/NativeWorldTextProbe/NativeCombatWallBossChecks.cs:line 25
119:    at Terraria.Player.Update_Patch1(Player this, Int32 i)
120:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.RollingStep(Object context, List`1 samples, List`1 prepares, Boolean right, Action sampleIntent, Boolean prepareHost) in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 525
121:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.Step(Object context, List`1 samples, List`1 prepares, Boolean right, Action sampleIntent, Boolean prepareHost) in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 538
122:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.<>c__DisplayClass21_0.<Run>b__23() in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 114
123:    at NativeWorldTextProbe.NativeCombatWallBossChecks.Continuous(Object context, NpcPredictionCache cache, Action step, String output) in /_/NativeWorldTextProbe/NativeCombatWallBossChecks.cs:line 185
124: SESSION rolling default; native owner absent, helper requests/history/background=0
125: System.InvalidOperationException: Wall all original natural scenes remain required: 0: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 1: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 2: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 3: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 4: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 5: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 6: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 7: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 8: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 9: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 10: Actual canonical Wall health continuation matches original UpdateLifeRegen. | 12: Actual canonical Wall health continuation matches original UpdateLifeRegen.
126:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
127:    at NativeWorldTextProbe.NativeCombatWallBossChecks.Continuous(Object context, NpcPredictionCache cache, Action step, String output) in /_/NativeWorldTextProbe/NativeCombatWallBossChecks.cs:line 209
128:    at NativeWorldTextProbe.NativeCombatProductionPredictionChecks.Run(Object context, String output, String content) in /_/NativeWorldTextProbe/NativeCombatProductionPredictionChecks.cs:line 298
129:    at NativeWorldTextProbe.NativeCombatWorkerChecks.<>c__DisplayClass5_0.<Run>b__0(Object context) in /_/NativeWorldTextProbe/NativeCombatWorkerChecks.cs:line 100
130:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
131:    at NativeWorldTextProbe.NativeCombatWorkerChecks.Run(String output, Boolean catalogue, Boolean linkedOnly, Boolean lifetimeOnly, Boolean assetsOnly, Boolean randomOnly, Boolean playerOnly, Boolean entityOnly, Boolean birthOnly, Boolean contextOnly, Boolean immunityOnly, Boolean lifecycleOnly, Boolean fieldsOnly, Boolean transportOnly, Boolean productionOnly, String content, Boolean preparationOnly, Boolean menuOnly, Boolean snapshotOnly, Boolean longCoverageOnly, Boolean legalOnly) in /_/NativeWorldTextProbe/NativeCombatWorkerChecks.cs:line 100
132:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 127
133:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
134:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## gem-motor-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/gem-motor-r22.log`; SHA-256 `6C6EDBD25A376BB5262D2BCBDD76A8AFA440B33B6C0A7AD2E8C7E798800782E8`; encoding UTF-8; candidate **r22**.

exit=1; scope=CombatEffectsCpu; seconds=13.4996151

```text
1: COMPOSITION initialization-ms=7288.5163 code-prepare-ms=289.0259 exact-comparison=False
2: System.InvalidOperationException: gem trail shifts at original post-Damage boundary 739/0/0/54 differences=0:{X:1541.966 Y:402.9966}/{X:1541.966 Y:402.9966}
3:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
4:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Motor(Object context, Assembly a) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 106
5:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 41
6:    at NativeWorldTextProbe.NativeCombatEffectChecks.RunCore(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 38
7:    at NativeWorldTextProbe.NativeCombatEffectChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 24
8:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 197
9:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
10:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 197
11:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
12:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## gem-collision-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/gem-collision-r22.log`; SHA-256 `5B6106B0372F0B5F4C215B25668F24001429089EB34CC1D236B89288F98C39B0`; encoding UTF-8; candidate **r22**.

exit=0; scope=CombatEffectsCpu; seconds=69.7192323

```text
1: COMPOSITION initialization-ms=7385.8512 code-prepare-ms=276.3296 exact-comparison=False
2: PASS original gem colliders=560 outsideBody=140 bursts=250
```

## gem-natural-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/gem-natural-r22.log`; SHA-256 `2C7446F0D30BA22253D64502EAD11330D655CE1A9A82970A6BE7C699FA54E62E`; encoding UTF-8; candidate **r22**.

exit=1; scope=CombatEffectsCpu; seconds=195.4220174

```text
37:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 142
38: GEM NATURAL FAIL System.InvalidOperationException: native first gem Damage agrees with unpressed preview 744/1283/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/1.008935E-43/0 received=126/{X:958.998 Y:662.8516}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646}
39:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
40:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 142
41: GEM NATURAL FAIL System.InvalidOperationException: native first gem Damage agrees with unpressed preview 744/1284/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/1.121039E-43/0;126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/1.121039E-43/1 received=126/{X:959.0079 Y:659.3249}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646}
42:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
43:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 142
44: GEM NATURAL FAIL System.InvalidOperationException: native first gem Damage agrees with unpressed preview 744/1287/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/4.484155E-43/0 received=126/{X:958.998 Y:662.8516}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646}
45:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
46:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 142
47: GEM NATURAL FAIL System.InvalidOperationException: native first gem Damage agrees with unpressed preview 3377/1287/False actual=27 preview=28 sampleAge=0 aim=10659.96,695.7355 birth=597/{X:655 Y:662}/{X:11.24996 Y:0.03150303}/9.52883E-44/0 received=597/{X:958.7484 Y:662.8503}/{X:11.24996 Y:0.03150303}/10x10 player={X:650 Y:646}
48:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
49:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 142
50: System.InvalidOperationException: all natural gem/robe members: 740/0/False: native first gem Damage agrees with unpressed preview 740/0/False actual=51 preview=52 sampleAge=0 aim=10659.96,695.7355 birth=122/{X:655 Y:662}/{X:6.499975 Y:0.01820175}/1.121039E-44/0 received=122/{X:986.5 Y:662.9276}/{X:6.499975 Y:0.01820175}/10x10 player={X:650 Y:646} | 740/1282/False: native first gem Damage agrees with unpressed preview 740/1282/False actual=51 preview=52 sampleAge=0 aim=10659.96,695.7355 birth=122/{X:655 Y:662}/{X:6.499975 Y:0.01820175}/1.261169E-44/0 received=122/{X:986.5 Y:662.9276}/{X:6.499975 Y:0.01820175}/10x10 player={X:650 Y:646} | 740/1283/False: native first gem Damage agrees with unpressed preview 740/1283/False actual=51 preview=52 sampleAge=0 aim=10659.96,695.7355 birth=122/{X:655 Y:662}/{X:6.499975 Y:0.01820175}/3.699428E-43/0 received=122/{X:986.5 Y:662.9276}/{X:6.499975 Y:0.01820175}/10x10 player={X:650 Y:646} | 740/1284/False: native first gem Damage agrees with unpressed preview 740/1284/False actual=51 preview=52 sampleAge=0 aim=10659.96,695.7355 birth=122/{X:655 Y:662}/{X:6.499975 Y:0.01820175}/3.363116E-44/0;122/{X:655 Y:662}/{X:6.499975 Y:0.01820175}/3.363116E-44/1 received=122/{X:986.5052 Y:661.0735}/{X:6.499975 Y:0.01820175}/10x10 player={X:650 Y:646} | 740/4256/False: native first gem Damage agrees with unpressed preview 740/4256/False actual=41 preview=42 sampleAge=0 aim=10659.96,695.7355 birth=122/{X:655 Y:662}/{X:8.124969 Y:0.02275219}/1.681558E-44/0 received=122/{X:988.1225 Y:662.9334}/{X:8.124969 Y:0.02275219}/10x10 player={X:650 Y:646} | 741/0/True: native first gem Damage agrees with unpressed preview 741/0/True actual=45 preview=45 sampleAge=0 aim=10659.96,695.7355 birth=123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/2.242078E-44/0;123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/2.242078E-44/1 received=123/{X:992.4832 Y:668.9448}/{X:7.49997 Y:0.02100202}/10x10 player={X:650 Y:646} | 741/1282/True: native first gem Damage agrees with unpressed preview 741/1282/True actual=45 preview=45 sampleAge=0 aim=10659.96,695.7355 birth=123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/2.382207E-44/0;123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/2.382207E-44/1 received=123/{X:992.4832 Y:668.9448}/{X:7.49997 Y:0.02100202}/10x10 player={X:650 Y:646} | 741/1283/True: native first gem Damage agrees with unpressed preview 741/1283/True actual=45 preview=45 sampleAge=0 aim=10659.96,695.7355 birth=123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/3.363116E-44/0;123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/3.363116E-44/1 received=123/{X:992.4832 Y:668.9448}/{X:7.49997 Y:0.02100202}/10x10 player={X:650 Y:646} | 741/1284/True: native first gem Damage agrees with unpressed preview 741/1284/True actual=45 preview=45 sampleAge=0 aim=10659.96,695.7355 birth=123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/3.811532E-43/0;123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/3.811532E-43/1 received=123/{X:992.4832 Y:668.9448}/{X:7.49997 Y:0.02100202}/10x10 player={X:650 Y:646} | 741/1287/True: native first gem Damage agrees with unpressed preview 741/1287/True actual=41 preview=41 sampleAge=0 aim=10659.96,695.7355 birth=123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/1.121039E-43/0;123/{X:655 Y:662}/{X:7.49997 Y:0.02100202}/1.121039E-43/1 received=123/{X:962.4948 Y:664.7149}/{X:7.49997 Y:0.02100202}/10x10 player={X:650 Y:646} | 744/0/False: native first gem Damage agrees with unpressed preview 744/0/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/8.96831E-44/0 received=126/{X:958.998 Y:662.8516}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646} | 744/1282/False: native first gem Damage agrees with unpressed preview 744/1282/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/9.10844E-44/0 received=126/{X:958.998 Y:662.8516}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646} | 744/1283/False: native first gem Damage agrees with unpressed preview 744/1283/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/1.008935E-43/0 received=126/{X:958.998 Y:662.8516}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646} | 744/1284/False: native first gem Damage agrees with unpressed preview 744/1284/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/1.121039E-43/0;126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/1.121039E-43/1 received=126/{X:959.0079 Y:659.3249}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646} | 744/1287/False: native first gem Damage agrees with unpressed preview 744/1287/False actual=32 preview=33 sampleAge=0 aim=10659.96,695.7355 birth=126/{X:655 Y:662}/{X:9.499963 Y:0.02660256}/4.484155E-43/0 received=126/{X:958.998 Y:662.8516}/{X:9.499963 Y:0.02660256}/10x10 player={X:650 Y:646} | 3377/1287/False: native first gem Damage agrees with unpressed preview 3377/1287/False actual=27 preview=28 sampleAge=0 aim=10659.96,695.7355 birth=597/{X:655 Y:662}/{X:11.24996 Y:0.03150303}/9.52883E-44/0 received=597/{X:958.7484 Y:662.8503}/{X:11.24996 Y:0.03150303}/10x10 player={X:650 Y:646}
51:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
52:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 147
53:    at NativeWorldTextProbe.NativeCombatGemStaffChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs:line 43
54:    at NativeWorldTextProbe.NativeCombatEffectChecks.RunCore(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 38
55:    at NativeWorldTextProbe.NativeCombatEffectChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 24
56:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 197
57:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
58:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 197
59:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
60:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## common-boomerang-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/common-boomerang-r22.log`; SHA-256 `9434C072349A2661C448429A585DF538E52FFC2C1D06F2A822D944109D9D974D`; encoding UTF-8; candidate **r22**.

exit=1; scope=CombatMechanicsCpu; seconds=28.1460192

```text
1: COMPOSITION initialization-ms=7065.0520000000006 code-prepare-ms=294.4447 exact-comparison=False
2: COMMON BOOMERANG actual receives=52 subsequentMotor=1934
3: COMMON BOOMERANG actual outbound/return world-edge cases=104
4: System.InvalidOperationException: actual native selected Damage or causal front-interception negative agrees: 52/False first=6 other=0
5:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
6:    at NativeWorldTextProbe.NativeCombatBoomerangCommonChecks.Contacts(Object context, Assembly a) in /_/NativeWorldTextProbe/NativeCombatBoomerangCommonChecks.cs:line 92
7:    at NativeWorldTextProbe.NativeCombatBoomerangCommonChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatBoomerangCommonChecks.cs:line 36
8:    at NativeWorldTextProbe.NativeCombatMeleeChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatMeleeChecks.cs:line 20
9:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 197
10:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
11:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 197
12:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
13:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## phaseblade-integration-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/phaseblade-integration-r22.log`; SHA-256 `2CD65558F2309C2ACDED300B2A79A35A949402B3B2093ABC9A16203BA0C3042F`; encoding UTF-8; candidate **r22**.

exit=0; scope=CombatEffectsCpu; seconds=15.1956441

```text
47: PHASEBLADE RELEASEPLAN item=5671 animation=15 time=0 use=False owner={X:700 Y:646} phaseBefore=True first=1 delay=0 contact=10 aim=10709.83,725.8225
48: PHASEBLADE SHOTENTRY animation=14 time=0 use=False selected= current=10 aim=10709.83,725.8225
49: PHASEBLADE SHOTCURSOR borrowed=True
50: PHASEBLADE BORN type=1102 slot=0 center={X:710 Y:667} velocity={X:13.99976 Y:0.08120675} ownerHitCheck=False animation=14 mouse={X:10709 Y:725}
51: PHASEBLADE RELEASEAFTER owner={X:700 Y:646} target={X:871 Y:648 Width:18 Height:40} body={X:700 Y:644 Width:46 Height:46} velocity={X:13.99976 Y:0.08120675} ai=0,1 life=1000 cooldown=-1 local=0 ownerImmune=0
52: PHASEBLADE RELEASEFLIGHT tick=1 center={X:737.9995 Y:667.1624} velocity={X:13.99976 Y:0.08120675} ai=0,2 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
53: PHASEBLADE RELEASEFLIGHT tick=2 center={X:751.9993 Y:667.2435} velocity={X:13.99976 Y:0.08120675} ai=0,3 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
54: PHASEBLADE RELEASEFLIGHT tick=3 center={X:765.999 Y:667.3247} velocity={X:13.99976 Y:0.08120675} ai=0,4 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
55: PHASEBLADE RELEASEFLIGHT tick=4 center={X:779.9988 Y:667.4059} velocity={X:13.99976 Y:0.08120675} ai=0,5 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
56: PHASEBLADE RELEASEFLIGHT tick=5 center={X:793.9985 Y:667.4871} velocity={X:13.99976 Y:0.08120675} ai=0,6 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
57: PHASEBLADE RELEASEFLIGHT tick=6 center={X:807.9983 Y:667.5682} velocity={X:13.99976 Y:0.08120675} ai=0,7 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
58: PHASEBLADE RELEASEFLIGHT tick=7 center={X:821.998 Y:667.6494} velocity={X:13.99976 Y:0.08120675} ai=0,8 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
59: PHASEBLADE RELEASEFLIGHT tick=8 center={X:835.9978 Y:667.7306} velocity={X:13.99976 Y:0.08120675} ai=0,9 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=0
60: PHASEBLADE RELEASEFLIGHT tick=9 center={X:849.9976 Y:667.8118} velocity={X:13.99976 Y:0.08120675} ai=0,10 active=True target={X:871 Y:648 Width:18 Height:40} life=1000 local=10
61: PHASEBLADE INTEGRATION weapon=5671 releaseDamageTick=10 plan=10 actualPositiveDamage=55 strikes=1
```

## reflection-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/reflection-r22.log`; SHA-256 `867252B20CFB27DC584F3FCBB30B234A2AA69A9A995CD134D079869682F58BF6`; encoding UTF-8; candidate **r22**.

exit=0; scope=CombatEffectsCpu; seconds=17.6976989

```text
1: COMPOSITION initialization-ms=6528.8974000000007 code-prepare-ms=268.1613 exact-comparison=False
2: PASS original projectile reflection receive cases=40 reflected=16 actualDamage=24
3: PASS derived receive leases=4 repeatedCandidates=16 perPolicy=1
```

## poison-cloud-r22.log

Origin: `.local/aim-comprehensive-repair-20261010/poison-cloud-r22.log`; SHA-256 `952CB2A8CE3E05047D2B4101583C572F3A96D14CEC56A308B0B337982F2C9931`; encoding UTF-8; candidate **r22**.

exit=0; scope=CombatEffectsCpu; seconds=22.4938948

```text
1: COMPOSITION initialization-ms=7096.4893 code-prepare-ms=283.1284 exact-comparison=False
2: PASS POISON point-solid native matrix=364
3: PASS POISON cloud motors=48 actions=8379 all-owner scenes
4: PASS POISON bottle motors=10 actions=233
5: PASS POISON native ambient/source/static and same-Damage higher-slot receivers=5
6: POISON FAMILY seed=0 children=24 first=12 model=12
7: POISON FAMILY seed=1 children=29 first=12 model=12
8: POISON FAMILY seed=2 children=23 first=13 model=13
9: POISON FAMILY seed=3 children=28 first=14 model=14
10: POISON FAMILY seed=4 children=22 first=10 model=10
11: POISON FAMILY seed=5 children=27 first=11 model=11
12: POISON FAMILY seed=6 children=21 first=13 model=13
13: POISON FAMILY seed=7 children=26 first=17 model=17
14: PASS POISON actual parent deaths=8 useful=8
15: PASS POISON workload captures=1 bounded120; transform and owner retirement lease negatives
```

## build-debug-r23.log

Origin: `.local/aim-comprehensive-repair-20261010/build-debug-r23.log`; SHA-256 `01AE0298362695F1024F8700DC397487B682821F9B023486286DB2AF2403D7F0`; encoding UTF-8; candidate **r23**.

No normal final receipt; see pause cancellation record or source-only status.

```text
19: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\HostHarpyAttack.cs(88,65): error CS1061: “Projectile”未包含“summon”的定义，并且找不到可接受第一个“Projectile”类型参数的可访问扩展方法“summon”(是否缺少 using 指令或程序集引用?) [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
20: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\HostHarpyAttack.cs(88,74): error CS1061: “Projectile”未包含“summon”的定义，并且找不到可接受第一个“Projectile”类型参数的可访问扩展方法“summon”(是否缺少 using 指令或程序集引用?) [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
21: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(360,26): error CS0136: 无法在此范围中声明名为“target”的局部变量或参数，因为该名称在封闭局部范围中用于定义局部变量或参数 [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
22: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(372,29): error CS0136: 无法在此范围中声明名为“lifeOwner”的局部变量或参数，因为该名称在封闭局部范围中用于定义局部变量或参数 [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
23: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(512,25): error CS0136: 无法在此范围中声明名为“target”的局部变量或参数，因为该名称在封闭局部范围中用于定义局部变量或参数 [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
24: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(702,89): error CS1061: “Projectile”未包含“identity”的定义，并且找不到可接受第一个“Projectile”类型参数的可访问扩展方法“identity”(是否缺少 using 指令或程序集引用?) [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
26: 生成失败。
28: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\HostHarpyAttack.cs(88,65): error CS1061: “Projectile”未包含“summon”的定义，并且找不到可接受第一个“Projectile”类型参数的可访问扩展方法“summon”(是否缺少 using 指令或程序集引用?) [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
29: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\HostHarpyAttack.cs(88,74): error CS1061: “Projectile”未包含“summon”的定义，并且找不到可接受第一个“Projectile”类型参数的可访问扩展方法“summon”(是否缺少 using 指令或程序集引用?) [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
30: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(360,26): error CS0136: 无法在此范围中声明名为“target”的局部变量或参数，因为该名称在封闭局部范围中用于定义局部变量或参数 [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
31: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(372,29): error CS0136: 无法在此范围中声明名为“lifeOwner”的局部变量或参数，因为该名称在封闭局部范围中用于定义局部变量或参数 [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
32: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(512,25): error CS0136: 无法在此范围中声明名为“target”的局部变量或参数，因为该名称在封闭局部范围中用于定义局部变量或参数 [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
33: C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\Combat\NpcPredictionSource.cs(702,89): error CS1061: “Projectile”未包含“identity”的定义，并且找不到可接受第一个“Projectile”类型参数的可访问扩展方法“identity”(是否缺少 using 指令或程序集引用?) [C:\Users\kongd\Desktop\JueMingR\src\JueMingR.TerrariaHost\JueMingR.TerrariaHost.csproj]
35:     6 个错误
```

## build-debug-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/build-debug-r23b.log`; SHA-256 `E3EC793189EED7A6C1F9425245031B2FEE4C047654C2114FFBAAF269EA59F536`; encoding UTF-8; candidate **r23b**.

No normal final receipt; see pause cancellation record or source-only status.

```text
21: 已成功生成。
22:     0 个警告
23:     0 个错误
24: 
25: 已用时间 00:00:04.02
26: Selected checks: failures=0
27: PREPARED: Debug compilation + structure only; product checks NOT_EXECUTED; no delivery qualification.
```

## gem-motor-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/gem-motor-r23b.log`; SHA-256 `0C3D700536F013A6D644AB19F8346C796E8AA3575EBDCE43CF8E086F962030FB`; encoding UTF-8; candidate **r23b**.

exit=0; scope=CombatEffectsCpu; seconds=85.3432248

```text
1: COMPOSITION initialization-ms=8214.9652 code-prepare-ms=313.807 exact-comparison=False
2: PASS original gem motors=448 actions=28270 retired=224 bounces=596 settled=1344
```

## reflection-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/reflection-r23b.log`; SHA-256 `916E890602F651AA105E072DF2CF5C856831D83ADA1EA55DE565CC963A55D379`; encoding UTF-8; candidate **r23b**.

exit=1; scope=CombatEffectsCpu; seconds=41.2643013

```text
1: COMPOSITION initialization-ms=8154.7554 code-prepare-ms=309.7354 exact-comparison=False
2: PASS original projectile reflection receive cases=40 reflected=16 actualDamage=24
3: PASS derived receive leases=4 repeatedCandidates=16 perPolicy=1
4: PASS original pre-NPC reflection cases=128 reflectedBeforeDamage=24 postNpcBirths=64
5: System.InvalidOperationException: damage-only reflection falsely survives an invulnerable guard into its later receive window
6:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
7:    at NativeWorldTextProbe.NativeCombatReflectionChecks.ReflectionBeforeFutureReceive(Object context, Assembly assembly, Type obstacleType) in /_/NativeWorldTextProbe/NativeCombatReflectionChecks.cs:line 109
8:    at NativeWorldTextProbe.NativeCombatReflectionChecks.BeforeNpcReflection(Object context, Assembly assembly, Type obstacleType) in /_/NativeWorldTextProbe/NativeCombatReflectionChecks.cs:line 88
9:    at NativeWorldTextProbe.NativeCombatReflectionChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatReflectionChecks.cs:line 49
10:    at NativeWorldTextProbe.NativeCombatEffectChecks.RunCore(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 42
11:    at NativeWorldTextProbe.NativeCombatEffectChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 24
12:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 199
13:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
14:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 199
15:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
16:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## flame-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/flame-r23b.log`; SHA-256 `754751EC28AEC2195AD69F1505DE4E23D287C5001FD2BEE644E14C67833956D3`; encoding UTF-8; candidate **r23b**.

exit=1; scope=CombatEffectsCpu; seconds=37.845338

```text
1: COMPOSITION initialization-ms=8124.4478000000008 code-prepare-ms=306.0475 exact-comparison=False
2: PASS native flame births=16 forwardMuzzles=9 obstructedMuzzles=7
3: PASS native flame motors=40 subupdates=1418 waterDeaths=12 wallSurvivals=124 harmlessTail=108
4: PASS native flame colliders=60 expandedPositives=15 wallRejections=5
5: PASS native flame last damaging subupdate53 / harmless54
6: System.InvalidOperationException: flame marker uses the same target path and receive epoch
7:    at NativeWorldTextProbe.NativeInformationChecks.Require(Boolean value, String reason) in /_/NativeWorldTextProbe/NativeInformationChecks.cs:line 80
8:    at NativeWorldTextProbe.NativeCombatFlameChecks.Natural(Object context, Object host, Object combat) in /_/NativeWorldTextProbe/NativeCombatFlameChecks.cs:line 99
9:    at NativeWorldTextProbe.NativeCombatFlameChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatFlameChecks.cs:line 26
10:    at NativeWorldTextProbe.NativeCombatEffectChecks.RunCore(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 40
11:    at NativeWorldTextProbe.NativeCombatEffectChecks.Run(Object context) in /_/NativeWorldTextProbe/NativeCombatEffectChecks.cs:line 24
12:    at NativeWorldTextProbe.NativeChecks.<>c__DisplayClass0_0.<Run>b__12(Object context) in /_/NativeWorldTextProbe/NativeChecks.cs:line 199
13:    at NativeWorldTextProbe.NativeQuickItemChecks.Run(Action`1 visual, Boolean coins, Boolean about, Boolean recovery, Boolean processing, Boolean shortFeedback, String candidateAssembly, String predictionHostHash, String candidatePackageId, Boolean exactPredictionComparison) in /_/NativeWorldTextProbe/NativeQuickItemChecks.cs:line 88
14:    at NativeWorldTextProbe.NativeChecks.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/NativeChecks.cs:line 199
15:    at NativeWorldTextProbe.Program.Run(String content, String output, String scope) in /_/NativeWorldTextProbe/Program.cs:line 91
16:    at NativeWorldTextProbe.Program.Main(String[] args) in /_/NativeWorldTextProbe/Program.cs:line 47
```

## gem-collision-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/gem-collision-r23b.log`; SHA-256 `1B0805D933855B2294BB6ED25BECB590909BCB5E391C563514B59EB76B9ABAE7`; encoding UTF-8; candidate **r23b**.

No normal final receipt; see pause cancellation record or source-only status.

```text
1: COMPOSITION initialization-ms=8252.2592 code-prepare-ms=297.2523 exact-comparison=False
2: PASS original gem colliders=784 outsideBody=264 bursts=362 damageMarginControls=110
```

## gem-natural-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/gem-natural-r23b.log`; SHA-256 `5072834D6D1F0217892EE957BBF7A33133BCE0CD5B1F789C067CF6461C472054`; encoding UTF-8; candidate **r23b**.

No normal final receipt; see pause cancellation record or source-only status.

```text
1: COMPOSITION initialization-ms=8084.6091000000006 code-prepare-ms=313.6677 exact-comparison=False
```

## tree-r23b.log

Origin: `.local/aim-comprehensive-repair-20261010/tree-r23b.log`; SHA-256 `716F63B1D21AE7570686D17A86BD5A47672BF34B5C2F62D8256E99C2EF6359E1`; encoding UTF-8; candidate **r23b**.

No normal final receipt; see pause cancellation record or source-only status.

```text
1: BUILD prediction-configuration=Debug
2: RUNTIME JueMingR.TerrariaHost mvid=748024cb-a815-402a-84be-fa9abfa42174 optimized=False
3: RUNTIME JueMingR.Platform mvid=7db2288f-6755-4255-9c33-7075b145e03d optimized=False
4: RUNTIME JueMingR.Features mvid=d0e30a97-9d28-4c1b-be94-4e1eb60a0465 optimized=False
5: RUNTIME JueMingR.Infrastructure mvid=b3c1e687-c122-4d88-8f5a-2e57aa1dfba2 optimized=False
6: Explicit historical candidate: C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\tree-r23b\worker-layout-ef6e4e850b724afeb4d15f8c45dbf048\JueMingR.TerrariaHost.dll MVID=748024cb-a815-402a-84be-fa9abfa42174
7: COMPOSITION initialization-ms=8075.6791 code-prepare-ms=306.5631 exact-comparison=False
8: PASS production codec refusal-page / raw identity bits / invalid geometry
9: OFF ENTRY before user=C:\Users\kongd\Desktop\JueMingR\.local\aim-comprehensive-repair-20261010\tree-r23b\u-dcc2a258a572 loaded=True path=False worker=rolling requests=0
10: OFF ENTRY after loaded=True path=False worker=rolling requests=0
11: PASS exact AI57 two-member and server random-consumer boundaries
12: PASS original AI57 scalar chosen-phase/life/terrain/day/client/target matrix cases=200
13: PASS original AI57 natural scene=0 matched=47080 long=31165 cross=4652 maxError=0
```

## npc/tree-brain-r23-checkpoint.txt

Origin: `.local/aim-comprehensive-repair-20261010/npc/tree-brain-r23-checkpoint.txt`; SHA-256 `D603BF3C12A398FF2FF9BCA6F381A957577374301378C4560AA7657151162899`; encoding UTF-8; candidate **r23**.

No normal final receipt; see pause cancellation record or source-only status.

```text
1: R23 source checkpoint; product/fixture execution pending root fixed build.
2: New AI57 exact members: 325/344, locked NPC.cs33003-33315 (1.4.5.8).
3: New NpcTreeBossMotion + NativeCombatTreeBossChecks. Shared source preheat, NpcMotion Known/Step/Needs/PlayerPremise, Rolling SamePhase, actual catalogue reflection wired.
4: No new DTO/group closure; normal existing player premise and foot Solid controller.
5: Observed all phase0..4 motor branches computed. Server true phase-choice clock300 returns RandomDecision; client retains clock and motor. Shot/dust RNG omitted only because no own-motor feedback.
6: Lifecycle 400 scalar cases; Continuous eight natural birth cases, actual health hits, true random chosen phases and sample recovery, turning/terrain/day near/far. Unexecuted, no PASS claim.
7: Brain R22 root formal Boundaries FAIL sample2 preserved. Previous PowerShell x64 arithmetic does not certify x86.
8: Private x86 diagnostic verifies fixedR22 Features SHA558F33E7, MVID7012f410; Platform MVID89bc5079; actual GAC32 XNA MVID34d977ae.
9: R22 model actual x86 16384 cases/8398 mismatches; scalar stage sample2 NormalizedX .796162248 vs .7961622 and DividedX -3.38710833 vs -3.38710856.
10: Private x86 wider reciprocal + actual value-return field boundaries 16384 cases/0 mismatches. Product Brain two helpers now match this arithmetic; formal x86 Boundary adds R input/delta/aim/result plus load identity, unchanged equality.
11: No public outputs/build/Git mutation/game/userdata modifications. AI58(327)/60(345) and other catalog families remain separate incomplete mechanisms; Wall canonical-profile/state failures remain sibling work. Tree future new-hit/terrain-effect premise remains declared original prediction contract.
```

## npc/r23-relations-checkpoint.md

Origin: `.local/aim-comprehensive-repair-20261010/npc/r23-relations-checkpoint.md`; SHA-256 `68D80DE605BF29446232471E5B803FF0CC5F6CB8526966D4619C29529AB32549`; encoding UTF-8; candidate **source checkpoint / unexecuted plan**.

No normal final receipt; see pause cancellation record or source-only status.

```text
1: # R23 relations frozen checkpoint
2: 
3: - Sole implementation writer: npc_relations (unchanged Sol/high).
4: - Frozen production: src/JueMingR.Features/Combat/NpcCelestialTowerMotion.cs.
5: - Frozen tests: tests/NativeWorldTextProbe/NativeCombatCelestialTowerChecks.cs and NativeCombatWallBossChecks.cs.
6: - Root started R23 build session42587; no child build/test/Git or public-output writes.
7: - Tower kernel: exact AI94 height/sine/death motor, already observed629 complete subupdate/counter consumption, earliest known-new-consumer boundary. Shared Source/DTO/Rolling are npc_prediction-owned.
8: - Tower checks: 8192 original XNA vectors, 35 original raw-solid AI queries, 20 genuine minion-death629 variants, all4 death180 lifecycles, unrelated positiveSolar/healthy412 vs zeroStardust Source long-window counterexample, 8 natural1800f scenes. Authored, not executed or PASS.
9: - Explicit remaining: healthySolar link survival only earliestconsumer2; arbitrary extraUpdates>16 not certified; malformed server629 AI0=-1 is not invented safe retirement.
10: - Wall: original13 natural expectations unchanged. R23 adds genuine UpdateLifeRegen prefix/postfix/current-consumer pure comparison; wait fixed result for cause rather than guess.
11: - Root R23 first compile stopped in other shared files. Root authorized narrow fixture correction: Snapshot identity uses locked (int)p.key; actual629 outworld TrySendData29 records only matching native slot/key. Re-frozen before retry, no mechanism or expectation change.
12: - Root next request: prioritize actual Wall health firstdifference, then read-only propose a remaining AI73/78/79/83/84 family distinct from prediction Pumpking/IceQueen. No new family implementation before batch opening.
13: 
14: - Read-only R24 candidate: AI73 type387 MartianTurret; exact intro120 receiving transition, X damping, justHit clock, real gravity/collision tail; future projectile-only RNG does not stop own motor. Proposed new NpcMartianTurretMotion.cs / NativeCombatMartianTurretChecks.cs; no new NPC relation DTO. Await round opening; Wall true health firstdifference takes priority.
15: 
```

## weapons/r24-foreign-bee-owner-gate-plan.txt

Origin: `.local/aim-comprehensive-repair-20261010/weapons/r24-foreign-bee-owner-gate-plan.txt`; SHA-256 `0E2CE2220BD0EDB01344AF65AB9E8387F33ACFA6A9F0990FC83A3729473C0CA4`; encoding UTF-8; candidate **source checkpoint / unexecuted plan**.

No normal final receipt; see pause cancellation record or source-only status.

```text
1: R24 foreign Bee owner-gate plan (read-only R23 source freeze, 2026-10-11).
2: Original locked Terraria1.4.5.8 Projectile.cs12502..12532:
3: Damage() only enters Damage_PVE/PVP in owner==Main.myPlayer branch. Update17191 calls Damage() after movement, but owner254 AI036 does not write static181 or any local owner's NPC immunity here. 17317 guard is later networking tail and must not be used as causal Damage gate.
4: Therefore R23 ForeignBeeCompetition rejects too broadly until actual original oracle proves the role boundary. Root accepted R24 remove that refusal if confirmed; do not create foreign world or foreign owner writes.
5: 
6: Native independent matrix draft, no test/build run:
7: -181 and566 each with foreign owner254 and real Main.player[254].active=true (save/restore exact flag), default nonhostile/static181 settings, normal native NewProjectile in netMode0 supported receiver (no changing product/world network mode).
8: -Selected normal NPC3 damageable, aiStyle-1/frozen, defense0, buff arrays cleared; both immunity slots separate. selected.localowner immune12, selected.foreignowner immune37 or0. static181=0.
9: -Foreign direct native Damage with body overlap: selected life unchanged, static181 unchanged, both owner immune fields unchanged. No geometry miss/immunity confusion: duplicate exact sample with local owner is independent positive.
10: -Foreign real Update same body: alpha decreases, lifetime decreases/entry extra subcycle runs, active true (foreign player active; avoid owner-inactive Kill confound), life/static/owner immune unchanged.
11: -Then clear only local owner12 to0 (observed native permission) and use a freshly born local181/566 same body; Update must actual Damage, one penetration spent, sharedstatic181 deadline=GUC+idStaticNPCHitCooldown, foreign owner immune unchanged. Need record life before q.Update, not NPC.Update DOT; capture source key.
12: -Reverse local first/foreign second: real local static deadline persists unchanged through foreign Update/Damage; foreign cannot consume/clear local owner history. Counterexamples include native source maxPen defaults and owner indices untouched.
13: -Add ordinary HostHarpy.Solve with active foreign bee alongside local source, compare exact local source first Damage to no-foreign control (future other attacks/network receives remain existing Conditional premise). No Unsupported for foreign nonreceiving Bee; foreign occupied slot remains in captured scene/pool, source flag never foreign.
14: 
15: Minimum proposed R24 product diff:
16: Remove State.ForeignBeeCompetition whole-family early rejection once native matrix passes. Preserve full captured occupied pool and cheap presentation lease. Foreign PVE writes remain zero by original owner gate. Do not call local-owner Fork/Pass for foreign. Root said no foreign world replay; external asynchronous packet arrivals remain Conditional/unmodeled, not invented receives.
17: Before deletion, replace current Harpy BeeWorkload foreign unsupported assertion with owner-gate/active-foreign local useful positive. Can preserve property purely diagnostic if useful, but root preference is minimum deletion.
18: 
19: R23 frozen artifacts/checkpoint remain untouched. Existing .local/r23-frozen-handoff contains the conservative R23 behavior and explicitly pending R24; this newer plan records precise causal correction, not R23 native PASS.
```
