"""Resolve mechanism claims to preserved raw artifacts; never infer runtime PASS.

This is a review index, not a test runner. Owner statuses retain their fixed
identity/layer. A matching filename or a PASS line cannot promote a component
claim to ordinary quality/cost or all-family coverage.
"""
from pathlib import Path
import argparse, csv, hashlib, json, re

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--production',required=True,help='Explicit sealed candidate identity; never infer from floating source')
args=parser.parse_args()

ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
LOCAL=ROOT/'.local/aim-algorithm-rework-20261015'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def rows(path):
    with path.open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))

artifacts={}
claims=[]
unresolved=[]
for owner in ('A','B','C'):
    table=OUT/f'weapon-mechanisms-{owner}.csv'
    for row in rows(table):
        evidence=row.get('evidence','')
        refs=[]
        evidence_columns={column:row.get(column,'') for column in ('evidence','kernelEvidence','phaseEvidence','runtimeEvidence','offWorkEvidence','currentFunctionalEvidence','terminalEvidence','rangeEvidence')}
        column_refs={}
        for column,value in evidence_columns.items():
            column_refs[column]=[]
            for match in re.finditer(r'(?:\.local/aim-algorithm-rework-20261015/)?[A-Za-z0-9_./-]+\.log',value):
                name=match.group(0)
                path=ROOT/name if name.startswith('.local/') else LOCAL/name
                if not path.is_file():
                    unresolved.append({'owner':owner,'projectile':row.get('projectile',row.get('resolvedProjectile')),'column':column,'reference':name})
                    continue
                relative=path.relative_to(ROOT).as_posix()
                refs.append(relative);column_refs[column].append(relative)
                if relative not in artifacts:
                    artifacts[relative]={'path':relative,'sha256':sha(path),'bytes':path.stat().st_size,
                        'verification':'BYTES_RESOLVED; execution/result/layer remain owner claim and final scope ledger'}
        claims.append({'owner':owner,'projectile':row.get('projectile',row.get('resolvedProjectile')),
            'helper':row['helper'],'status':row['status'],'evidenceText':evidence,
            'rawArtifactRefs':list(dict.fromkeys(refs)),
            'rawArtifactsByEvidenceColumn':{column:list(dict.fromkeys(values)) for column,values in column_refs.items()},
            'kernelEvidence':row.get('kernelEvidence',''),
            'phaseEvidence':row.get('phaseEvidence',''),
            'runtimeEvidence':row.get('runtimeEvidence',''),
            'costStatus':row.get('costStatus','SEPARATE_07; component is not ordinary cost proof')})

result={'schema':'mechanism-evidence-index-1','taskBase':'97ceecf7c8706779bc2cf9acfff795830eb449da',
    'currentProduction':args.production,'fullCandidate':'NOT_ADMITTED; byte index only. Current author completion and independent-review state are in 07-complete-candidate.md',
    'boundary':'Source/caller classification is exhaustive inventory, not per-combination runtime PASS. Original fixed component identities retain scope; shared input/phase and ordinary cost need their own evidence.',
    'tables':[{'path':f'weapon-mechanisms-{owner}.csv','sha256':sha(OUT/f'weapon-mechanisms-{owner}.csv')} for owner in ('A','B','C')],
    'artifacts':list(artifacts.values()),'claims':claims,'unresolvedReferences':unresolved}
(OUT/'mechanism-evidence-index.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'claims':len(claims),'rawArtifacts':len(artifacts),'unresolved':unresolved},ensure_ascii=False,indent=2))
