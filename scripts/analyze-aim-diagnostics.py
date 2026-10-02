# Offline parser for the temporary aim diagnostics format. No game execution,
# replay AI, save reads or new product state. Production frame inputs remain
# authoritative; field tapes explain the exact hash key only after it differs.
import base64, collections, csv, gzip, hashlib, io, json, math, re, struct, sys
from pathlib import Path

class Reader:
    def __init__(self,data): self.data=io.BytesIO(data)
    def read(self,n):
        if n<0 or n>32*1024*1024:raise ValueError('invalid primitive extent')
        value=self.data.read(n)
        if len(value)!=n:raise ValueError('truncated primitive')
        return value
    def value(self,fmt):return struct.unpack('<'+fmt,self.read(struct.calcsize('<'+fmt)))[0]
    def string(self):
        size=shift=0
        while True:
            byte=self.value('B');size|=(byte&127)<<shift
            if byte<128:break
            shift+=7
            if shift>28:raise ValueError('string length overflow')
        return self.read(size).decode('utf-8')
    def count(self,maximum):
        n=self.value('i')
        if n<0 or n>maximum:raise ValueError('invalid array extent')
        return n
    def vector(self):return [self.value('f'),self.value('f')]

def properties(row):
    detail=row['detail'].split(';prior=',1)[0] if row.get('kind')=='selection' else row['detail']
    return dict(item.split('=',1) for item in detail.split(';') if '=' in item)
def payload(row):
    path=Path(row['_directory'])/row['file']
    if not path.exists():return None
    with path.open('rb') as stream:stream.seek(int(row['offset']));data=stream.read(int(row['length']))
    if len(data)!=int(row['length']):raise ValueError('truncated journal '+str(path))
    return gzip.decompress(data) if row['encoding']=='gzip' else data

def fields(data):
    r=Reader(data)
    if r.value('i')!=0x41494D54:raise ValueError('unknown field tape')
    names=[(r.string(),r.string()) for _ in range(r.count(100000))];values=[];hash_value=14695981039346656037
    for _ in range(r.count(1000000)):
        field,index,kind,bits=r.value('i'),r.value('i'),r.value('B'),r.value('Q');name,typ=names[field]
        entry={'field':name,'type':typ,'wirePrimitiveIndex':index,'primitiveBytes':kind,'bits':f'{bits:016X}'}
        primitive=typ.removeprefix('System.');logical=index
        if primitive.endswith('[]'):
            primitive=primitive[:-2]
            if index==0:entry['decodedValue']=struct.unpack('<i',struct.pack('<I',bits))[0];entry['component']='arrayLength';primitive='__length'
            else:logical=index-1;entry['arrayElementIndex']=logical
        if primitive in ('Microsoft.Xna.Framework.Vector2','Vector2','Microsoft.Xna.Framework.Rectangle','Rectangle'):
            components=['X','Y'] if primitive.endswith('Vector2') else ['X','Y','Width','Height']
            entry['component']=components[logical%len(components)]
            if typ.endswith('[]'):entry['arrayElementIndex']=logical//len(components)
            primitive='Single' if primitive.endswith('Vector2') else 'Int32'
        if kind==4 and primitive in ('Single','Int32','UInt32'):
            entry['decodedValue']=struct.unpack('<'+{'Single':'f','Int32':'i','UInt32':'I'}[primitive],struct.pack('<I',bits))[0]
        elif primitive=='Boolean' and kind in (0,1):entry['decodedValue']=bool(bits)
        elif primitive in ('Int16','UInt16','Byte','SByte') and kind=={'Int16':2,'UInt16':2,'Byte':1,'SByte':1}[primitive]:entry['decodedValue']=struct.unpack('<'+{'Int16':'h','UInt16':'H','Byte':'B','SByte':'b'}[primitive],bits.to_bytes(8,'little')[:kind])[0]
        elif primitive in ('UInt64','Int64','Double'):
            entry['component']='lowUInt32' if logical%2==0 else 'highUInt32'
            entry['decodedValueUnavailable']='64-bit value is represented by two wire primitives; raw word bits retained'
        elif primitive!='__length':entry['decodedValueUnavailable']='type/component layout not reliably identified; raw primitive bits only'
        values.append(entry)
        for byte in bits.to_bytes(8,'little')[:kind or 1]:hash_value=((hash_value^byte)*1099511628211)&0xffffffffffffffff
    return hash_value,values

def frame(r):
    result={'tick':r.value('q'),'hasState':r.value('?')}
    if result['hasState']:
        result['world']=r.value('Q')
        for prefix,maximum in [('npc',201),('projectile',1001)]:
            entries=[(r.value('i'),r.value('Q')) for _ in range(r.count(maximum))]
            result[prefix+'Slots']=[x[0] for x in entries];result[prefix+'State']=[x[1] for x in entries]
        ns,ps=result['npcSlots'],result['projectileSlots'];result['npcShootClock']=[r.value('f') for _ in ns]
        result['npcIdentity']=[r.value('Q') for _ in ns];result['projectileIdentity']=[r.value('Q') for _ in ps]
        for prefix,slots in [('npc',ns),('projectile',ps)]:
            motion=[(r.vector(),r.vector()) for _ in slots]
            result[prefix+'Velocity']=[x[0] for x in motion];result[prefix+'OldVelocity']=[x[1] for x in motion]
        for prefix,slots in [('npc',ns),('projectile',ps)]:result[prefix+'Positions']=[[r.vector() for _ in range(r.count(4098))] for _ in slots]
        players=[]
        for _ in range(r.count(255)):players.append({'slot':r.value('i'),'premise':r.value('Q'),'conditional':r.value('?'),'position':r.vector(),'velocity':r.vector()})
        result['players']=players
    result.update(netOffset=r.vector(),canReceive=r.value('?'),canHarm=r.value('?'),newSegment=r.value('?'))
    return result

def response(data):
    r=Reader(data)
    if r.value('i')!=0x4A525058 or r.value('i')!=1 or not r.value('?'):raise ValueError('response envelope')
    sequence=r.value('q')
    if r.value('i')!=1:return {'sequence':sequence,'clearWorld':True}
    asset=r.value('i');core=r.read(r.count(4*1024*1024));proof=r.read(r.count(4*1024*1024))
    c=Reader(core);protocol=c.value('i')
    if protocol<0:return {'sequence':sequence,'missingAsset':asset,'error':c.string()+': '+c.string(),'tileX':c.value('i'),'tileY':c.value('i'),'kind':c.value('i'),'slot':c.value('i'),'field':c.value('i')}
    p=Reader(proof);frames=[frame(p) for _ in range(p.count(181))];p.value('q');usage={}
    for _ in range(p.count(128)):usage[p.value('i')]=p.read(128).hex()
    first=frames[0];nr=[p.value('?') for _ in first['npcSlots']];pr=[p.value('?') for _ in first['projectileSlots']]
    return {'sequence':sequence,'missingAsset':asset,'frames':frames,'npcRequired':nr,'projectileRequired':pr,'terrainUsage':usage}

def near(a,b,velocity=False):
    for x,y in zip(a,b):
        if not math.isfinite(x) or not math.isfinite(y):return False
        if velocity and ((x==0)!=(y==0) or (x<0)!=(y<0)):return False
        # XNA Near subtracts Single values before comparison; velocity uses Double.
        delta=x-y if velocity else struct.unpack('<f',struct.pack('<f',x-y))[0]
        if abs(delta)>(.002 if velocity else .0020000000949949026):return False
    return True

def difference(e,a,reply,index):
    # The order and masks mirror NativePredictionAlignment.Difference. A report
    # marks reconstruction confirmed only when its key equals the logged result.
    def changed(reason,key,expected,actual,subject=None):return {'key':reason,'field':key,'expected':expected,'actual':actual,'tapeSubject':subject}
    if e['tick']!=a['tick']:return changed('tick','tick',e['tick'],a['tick'])
    if not e['hasState'] or not a['hasState']:return changed('missing state proof','hasState',e['hasState'],a['hasState'])
    if e['world']!=a['world']:return changed('world premise','world',e['world'],a['world'],'world')
    def pairs(prefix,suffix,label,mask=None):
        slots,actual=e[prefix+'Slots'],a[prefix+'Slots'];ev,av=e[prefix+suffix],a[prefix+suffix]
        if len(slots)!=len(actual):return changed(label+' count',prefix+'Slots',slots,actual)
        for i,slot in enumerate(slots):
            if slot!=actual[i] or (mask is None or mask[i]) and ev[i]!=av[i]:
                subject=prefix+'-'+('identity' if suffix=='Identity' else 'state')+' slot='+str(slot)
                return changed(label+' slot='+str(slot),prefix+suffix,ev[i],av[i],subject)
    for prefix,label in [('npc','NPC identity'),('projectile','Projectile identity')]:
        found=pairs(prefix,'Identity',label)
        if found:return found
    nr=None if index==0 else reply['npcRequired'];pr=None if index==0 else reply['projectileRequired']
    found=pairs('npc','State','NPC',nr)
    if found:return found
    if index==0:
        for i,(x,y) in enumerate(zip(e['npcShootClock'],a['npcShootClock'])):
            if struct.pack('<f',x)!=struct.pack('<f',y):return changed('NPC sampled shooting clock slot='+str(e['npcSlots'][i]),'npcShootClock',x,y)
    found=pairs('projectile','State','Projectile',pr)
    if found:return found
    for prefix,label,mask in [('npc','NPC',nr),('projectile','Projectile',pr)]:
        for suffix in ['Velocity','OldVelocity']:
            ev,av=e[prefix+suffix],a[prefix+suffix]
            for i,(x,y) in enumerate(zip(ev,av)):
                if (mask is None or mask[i]) and not near(x,y,True):return changed(label+' velocity',prefix+suffix+' slot='+str(e[prefix+'Slots'][i]),x,y)
    for prefix,label,mask in [('npc','NPC',nr),('projectile','Projectile',pr)]:
        for i,(x,y) in enumerate(zip(e[prefix+'Positions'],a[prefix+'Positions'])):
            if mask is not None and not mask[i]:continue
            if len(x)!=len(y):return changed(label+' position',prefix+'Positions slot='+str(e[prefix+'Slots'][i]),x,y)
            for j,(xv,yv) in enumerate(zip(x,y)):
                if not near(xv,yv):return changed(label+' position',prefix+'Positions slot='+str(e[prefix+'Slots'][i])+' trailIndex='+str(j),xv,yv)
    ep,ap=e['players'],a['players']
    if len(ep)!=len(ap):return changed('Player premise count','players',ep,ap)
    for x,y in zip(ep,ap):
        if x['slot']!=y['slot'] or x['premise']!=y['premise']:return changed('Player premise slot='+str(x['slot']),'playerPremise',x['premise'],y['premise'],'player-premise slot='+str(x['slot']))
    for x,y in zip(ep,ap):
        if x['conditional']!=y['conditional']:return changed('Player mechanism '+str(x['slot']),'playerConditional',x['conditional'],y['conditional'])
        if not x['conditional'] and (not near(x['position'],y['position']) or not near(x['velocity'],y['velocity'])):return changed('Player motion '+str(x['slot']),'playerMotion',x,y)
    return None

def percentiles(values):
    values=sorted(values)
    return {'count':len(values),'mean':sum(values)/len(values),'p50':values[len(values)//2],'p95':values[min(len(values)-1,int(len(values)*.95))],'max':values[-1]} if values else {'count':0}

def analyze(session,output):
    if output.exists():raise ValueError('new output directory required')
    output.mkdir(parents=True);rows=[];integrity=[];completion={};rolled={};manifests={}
    directories={path.parent for name in ('timeline.tsv','manifest.tsv','session.tsv','completion.tsv') for path in session.rglob(name)}
    directories.add(session/'host');directories.update(path for path in session.glob('worker-*') if path.is_dir())
    for directory in sorted(directories):
        timeline=directory/'timeline.tsv'
        if not timeline.exists():integrity.append('process timeline missing '+str(timeline))
        else:
            with timeline.open(encoding='utf-8-sig',newline='') as stream:
                for row in csv.DictReader(stream,delimiter='\t'):
                    if not row.get('kind'):continue
                    if any(value is None for value in row.values()) or None in row:integrity.append('truncated timeline row '+str(timeline));continue
                    try:int(row['monotonic']);int(row['tick']);int(row['queueBytes'])
                    except (ValueError,KeyError):integrity.append('invalid timeline row '+str(timeline));continue
                    row['_directory']=str(directory);rows.append(row)
        index=directory/'files.tsv'
        entries=[]
        if index.exists():
            with index.open(encoding='utf-8-sig',newline='') as stream:entries=list(csv.DictReader(stream,delimiter='\t'))
        valid_entries=[]
        for entry in entries:
            if any(value is None for value in entry.values()) or None in entry:integrity.append('truncated file index row '+str(index))
            else:valid_entries.append(entry)
        entries=valid_entries
        rolled[directory]={entry['file'] for entry in entries if entry.get('state')=='rolled-out'}
        streams={}
        try:
            for entry in entries:
                if entry.get('state')!='present' or entry['file'] in rolled[directory]:continue
                path=directory/entry['file']
                try:
                    if path not in streams:streams[path]=path.open('rb')
                    stream=streams[path];stream.seek(int(entry['offset']));data=stream.read(int(entry['length']))
                    good=len(data)==int(entry['length']) and hashlib.sha256(data).hexdigest().upper()==entry['sha256']
                except OSError:good=False
                if not good:integrity.append(str(path)+': hash/length failure')
        finally:
            for stream in streams.values():stream.close()
        manifest=directory/'manifest.tsv';manifests[str(directory.relative_to(session))]=manifest.exists()
        if manifest.exists():
            with manifest.open(encoding='utf-8-sig',newline='') as stream:
                items=list(csv.DictReader(stream,delimiter='\t'))
                names=[item.get('file') for item in items]
                if any(value is None for item in items for value in item.values()) or len(names)!=len(set(names)) or not {'timeline.tsv','files.tsv','session.tsv','completion.tsv'}.issubset(names):
                    integrity.append('manifest mandatory unique stream set incomplete '+str(manifest));manifests[str(directory.relative_to(session))]=False
                for item in items:
                    if any(value is None for value in item.values()) or not item.get('file'):continue
                    path=directory/item['file']
                    if path.parent!=directory or not path.is_file():integrity.append('manifest missing file '+str(path));continue
                    digest=hashlib.sha256()
                    with path.open('rb') as content:
                        for block in iter(lambda:content.read(1024*1024),b''):digest.update(block)
                    if path.stat().st_size!=int(item['length']) or digest.hexdigest().upper()!=item['sha256']:integrity.append('manifest hash/length failure '+str(path))
        end=directory/'completion.tsv'
        completion[str(directory.relative_to(session))]=dict(line.split('\t',1) for line in end.read_text(encoding='utf-8-sig').splitlines() if '\t' in line) if end.exists() else {'reason':'unconfirmed abnormal exit','failure':'completion missing'}
    rows.sort(key=lambda row:int(row['monotonic']))
    if not rows:integrity.append('no timeline records')
    host=[row for row in rows if Path(row['_directory']).name=='host'];context={};first_gap=None;last_display=None;had=False;prepared_gap=None
    presentations={};host_draws=collections.Counter();chain=collections.Counter(row['kind'] for row in host);distribution=collections.Counter();scene_groups={};instance_groups={};transitions=[];prior_scene=None
    for row in host:
        kind=row['kind'];p=properties(row);tick=row['tick']
        if kind in ('update','selection','session-prepare','acceptance','capture-failed','host-draw-entry','draw','cache-invalidated','request-retired'):context[kind]=row
        if kind=='host-draw-entry':host_draws[tick]+=1
        if kind.startswith('presentation-') or kind in ('draw','draw-completed','text-draw'):
            entry=presentations.setdefault((tick,p.get('presentation','0')),{'tick':tick,'presentation':p.get('presentation'),'drawCalls':0,'textDrawn':False,'pathDrawn':False,'events':[]});entry['events'].append(row)
            if kind=='draw':entry['drawCalls']+=1;entry['gates']=p
            if kind=='draw-completed':entry['pathDrawn']=int(p.get('pathStrokes','0'))>0
            if kind=='text-draw' and p.get('path')=='True':entry['textDrawn']=p.get('drawn')=='True'
        if kind=='presentation-cache':
            if p.get('cache')=='True':had=True
            elif had and prepared_gap is None:prepared_gap={'tick':tick,'context':dict(context),'event':row}
        if kind=='acceptance':distribution[p.get('outcome','unknown')]+=1
        if kind=='update':
            scene=tuple(p.get(key,'unknown') for key in ('session','worldId','worldName','playerName','netMode','zone'))
            key=' | '.join(scene);entry=scene_groups.setdefault(key,{'updates':0,'acceptance':collections.Counter(),'intervalMs':[]});entry['updates']+=1
            try:entry['intervalMs'].append(float(p.get('intervalMs',0)))
            except ValueError:pass
            if scene!=prior_scene:transitions.append({'tick':tick,'scene':key,'previous':None if prior_scene is None else ' | '.join(prior_scene),'context':dict(context)});prior_scene=scene
        if kind=='acceptance' and prior_scene:scene_groups[' | '.join(prior_scene)]['acceptance'][p.get('outcome','unknown')]+=1
        if kind=='selection' and p.get('selected')=='True':
            identity=' | '.join(p.get(key,'unknown') for key in ('session','slot','generation','type','netId','token'))
            instance_groups.setdefault(identity,{'selectedTicks':0,'firstTick':tick,'lastTick':tick})['selectedTicks']+=1;instance_groups[identity]['lastTick']=tick
    request_instances={row['request']:' | '.join(properties(row).get(key,'unknown') for key in ('session','slot','generation','type','netId','token')) for row in host if row['kind']=='capture-attempt'}
    request_chain={'capture-attempt','capture-completed','capture-failed','capture-partial','request-raw','terrain-pages','sent','response-envelope','mailbox-take','receive','acceptance','history-frame','history-compare','cache-proof-compare','cache-invalidated','request-retired','alternate-retired-discard'}
    current_instance=None;current_update=None
    for row in host:
        p=properties(row)
        if row['kind']=='update':current_update=p
        if row['kind']=='selection':
            current_instance=' | '.join(p.get(key,'unknown') for key in ('session','slot','generation','type','netId','token')) if p.get('selected')=='True' else None
        linked_instance=request_instances.get(row['request']) if row['kind'] in request_chain else current_instance
        if linked_instance:
            entry=instance_groups.setdefault(linked_instance,{'selectedTicks':0,'firstTick':row['tick'],'lastTick':row['tick']})
            entry.setdefault('events',collections.Counter())[row['kind']]+=1
            if row['kind']=='acceptance':entry.setdefault('acceptance',collections.Counter())[p.get('outcome','unknown')]+=1
            if row['kind'] in ('acceptance','request-retired','cache-invalidated','capture-failed','draw','presentation-ready'):
                entry.setdefault('chainSamples',[]).append({'event':row,'playerAndScene':current_update})
    for entry in instance_groups.values():
        for key in ('events','acceptance'):
            if key in entry:entry[key]=dict(entry[key])
    shown=False
    for entry in presentations.values():
        if entry['pathDrawn'] and entry['textDrawn']:shown=True;last_display=entry
        elif shown and not entry['pathDrawn'] and not entry['textDrawn'] and first_gap is None and (entry['drawCalls'] or any(int(other['tick'])>int(entry['tick']) for other in presentations.values())):
            entry['actualHostDrawCalls']=host_draws[entry['tick']];entry['classification']='Draw invoked; neither path strokes nor path text completed' if entry['drawCalls'] else 'Prepared update has no layer Draw call before next preparation'
            first_gap={'firstObservedDisplayGap':entry,'previousDisplay':last_display,'nearbyChain':[row for row in host if abs(int(row['tick'])-int(entry['tick']))<=2 and row['kind'] not in ('alignment-fields','alignment-frame','pool')]}
    sent={(row['generation'],row['sequence']):row for row in host if row['kind']=='sent' and 'action=prediction' in row['detail']};replies={};actual_frames={};actual_tapes={};expected_tapes={};comparisons=[]
    for row in rows:
        p=properties(row);worker=Path(row['_directory']).name.startswith('worker-')
        if row['kind']=='response-envelope':
            data=payload(row)
            if data:
                try:replies[(row['generation'],row['sequence'])]=response(data)
                except Exception as error:integrity.append('response decode: '+str(error))
        if row['kind']=='alignment-frame' and not worker:actual_frames[p.get('observation')]=row
        if row['kind']=='alignment-fields':
            subject=row['detail'].split(';observation=')[0]
            if worker:expected_tapes[(row['generation'],row['sequence'],row['tick'],subject)]=row
            else:actual_tapes[(p.get('observation'),subject)]=row
    request_replies={sent[key]['request']:(key,replies[key]) for key in sent if key in replies and 'frames' in replies[key]}
    for row in host:
        if row['kind'] not in ('history-compare','cache-proof-compare'):continue
        p=properties(row);index=int(p.get('historyIndex',p.get('frameIndex','-1')));observed=actual_frames.get(p.get('observation'));match=request_replies.get(row['request']);outcome=p.get('outcome','');entry={'event':row,'outcome':outcome,'observation':p.get('observation'),'frameIndex':index}
        if not match or observed is None or index<0 or index>=len(match[1]['frames']):entry['status']='unknown: original reply or observed frame was rolled out/missing; no causal attribution'
        elif payload(observed) is None:entry['status']='unknown: actual detail rolled out'
        else:
            try:
                key,reply=match;e,a=reply['frames'][index],frame(Reader(payload(observed)));found=difference(e,a,reply,index);entry.update(causalKey=found,finalNpcRequired=reply['npcRequired'],finalProjectileRequired=reply['projectileRequired'],isSample=index==0)
                entry['status']='confirmed original Difference key' if (found['key'] if found else '')==outcome else 'unknown: reconstructed key disagrees with logged original outcome'
                if found and found.get('tapeSubject'):
                    subject=found['tapeSubject'];erow=expected_tapes.get((key[0],key[1],str(e['tick']),subject));arow=actual_tapes.get((p.get('observation'),subject))
                    if erow and arow and payload(erow) is not None and payload(arow) is not None:
                        eh,ev=fields(payload(erow));ah,av=fields(payload(arow))
                        if eh==found['expected'] and ah==found['actual']:
                            found['firstAcceptanceFieldDifference']=next(({'expected':x,'actual':y} for x,y in zip(ev,av) if x!=y),{'countExpected':len(ev),'countActual':len(av)} if len(ev)!=len(av) else None)
                            found['fieldTapeHashVerified']=True
                        else:found['fieldTapeHashVerified']=False
                    else:found['fieldDetail']='unknown: matching worker/actual field tape unavailable'
            except Exception as error:entry['status']='unknown: frame decode '+str(error);integrity.append(entry['status'])
        if outcome or entry.get('causalKey') or entry['status'].startswith('unknown'):comparisons.append(entry)
    terrain=[]
    for row in host:
        if row['kind']!='terrain-difference':continue
        p=properties(row);x,y=int(p['x']),int(p['y']);reply_match=request_replies.get(row['request']);used=None
        if reply_match:
            bits=reply_match[1]['terrainUsage'].get(x//32*128+y//32)
            if bits:bits=bytes.fromhex(bits);at=x%32*32+y%32;used=bool(bits[at//8]&(1<<(at%8)))
            else:used=False
        baseline=base64.b64decode(p['baseline']);live=base64.b64decode(p['live']) if p['live']!='null' else None
        names=['type','wall','liquid','sTileHeader','bTileHeader','bTileHeader2','bTileHeader3','frameX','frameY'];formats=['H','H','B','H','B','B','B','h','h'];offset=0;changes=[]
        for name,fmt in zip(names,formats):
            size=struct.calcsize('<'+fmt);before=struct.unpack_from('<'+fmt,baseline,offset)[0];after=None if live is None else struct.unpack_from('<'+fmt,live,offset)[0]
            if before!=after:changes.append({'field':name,'baseline':before,'live':after,'xor':None if after is None else before^after})
            offset+=size
        terrain.append({'event':row,'changes':changes,'finalWorkerUsageIntersects':used,'pendingChange':p.get('pendingChange'),'meaning':'late final usage intersects changed cell' if used else 'changed cell was not in final usage' if used is False else 'unknown: final usage unavailable'})
    for entry in scene_groups.values():entry['intervalMs']=percentiles(entry['intervalMs']);entry['acceptance']=dict(entry['acceptance'])
    costs={}
    for key in ['copyMs','tapeEncodeMs','observeTotalMs','captureMs','encodeMs','encodeExchangeMs','elapsedMs','wallAgeMs']:
        values=[]
        for row in host:
            try:
                value=properties(row).get(key)
                if value is not None:values.append(float(value))
            except ValueError:pass
        costs[key]=percentiles(values)
    costs['completedWriterBatchMs']=percentiles([float(row.get('previousWriteMs',row.get('writerMs',0))) for row in rows]);costs['queueHighWater']=max([int(row['queueBytes']) for row in rows]+[int(end.get('queueHighWater',0)) for end in completion.values()]);costs['retainedBytes']=sum(file.stat().st_size for file in session.rglob('*') if file.is_file() and output not in file.parents)
    phase_costs=collections.defaultdict(list);phase_missing=collections.Counter()
    for process_rows in (host,[row for row in rows if Path(row['_directory']).name.startswith('worker-')]):
        grouped=collections.defaultdict(dict)
        for row in process_rows:grouped[(row['generation'],row['sequence'])][row['kind']]=row
        for stages in grouped.values():
            for label,start,finish in [('workerProcessingWithDiagnostics','worker-receive','worker-completed'),('workerExceptionWithDiagnostics','worker-receive','worker-exception'),('workerReturnPreparation','worker-completed','worker-return'),('hostSentToResponse','sent','response-envelope'),('hostResponseToTake','response-envelope','mailbox-take'),('hostTakeToAcceptance','mailbox-take','acceptance')]:
                if start not in stages:continue
                if finish not in stages:phase_missing[label]+=1;continue
                begin,end=stages[start],stages[finish]
                meta=Path(begin['_directory'])/'session.tsv'
                frequency=dict(line.split('\t',1) for line in meta.read_text(encoding='utf-8-sig').splitlines() if '\t' in line).get('frequency') if meta.exists() else None
                if frequency:phase_costs[label].append((int(end['monotonic'])-int(begin['monotonic']))*1000/float(frequency))
                else:phase_missing[label]+=1
    costs['observedPhaseWallMs']={key:percentiles(value) for key,value in phase_costs.items()};costs['phaseMissingDenominators']=dict(phase_missing)
    costs['recordOwnershipTotalMsByProcess']={key:float(end.get('recordOwnershipTotalMs',0)) for key,end in completion.items()}
    costs['measurementScope']='Original processing plus diagnostic overhead; not pure additional overhead or FPS. Writer batch timings exclude final manifest hashing.'
    worker_gens={row['generation'] for row in host if row['kind']=='sent'};recorded_gens={Path(row['_directory']).name[7:] for row in rows if Path(row['_directory']).name.startswith('worker-')};missing_workers=sorted(worker_gens-recorded_gens)
    complete=not list(session.glob('initialization-failure-*.tsv')) and not integrity and not missing_workers and all(manifests.values()) and all(end.get('dropped')=='0' and end.get('detailStopped')=='False' and not end.get('failure') for end in completion.values())
    report={'scope':'collected observations; fixture runs do not establish owner field cause','complete':complete,'completion':completion,'finalStreamManifests':manifests,'rolledDetailSegments':{str(path.relative_to(session)):len(parts) for path,parts in rolled.items()},'detailedRetention':'approximately last 180 seconds plus permanently pinned trigger windows; rolled-out detail is intentionally unavailable','missingWorkerGenerations':missing_workers,'integrityErrors':integrity,'initializationFailures':[str(path.relative_to(session)) for path in session.glob('initialization-failure-*.tsv')],'firstPathAndTextDisplayGap':first_gap,'firstPreparedCacheGap':prepared_gap,'chainCounts':dict(chain),'acceptanceDistribution':dict(distribution),'causalComparisons':comparisons,'terrainChanges':terrain,'instances':instance_groups,'scenes':scene_groups,'sceneTransitions':transitions,'costs':costs,'resetAndFailureEvents':[row for row in host if row['kind'] in ('world-detach','target-clear','transport-reset','transport-stop','transport-fail','request-retired','cache-invalidated','diagnostic-loss')],'unknown':['No first display gap is reported unless path strokes and path text were both previously observed drawn. No Draw during a prepared frame is classified separately from an invoked Draw returning early.','Field wirePrimitiveIndex includes array length, vector/rectangle components and low/high UInt32 pieces; it is not automatically a logical element index.','A killed worker has no confirmed final drain; completion missing makes complete false even when persisted rows show zero dropped.','Normalized acceptance fields and final required masks are used for attribution; raw pool state changes alone are descriptive.']}
    (output/'analysis.json').write_text(json.dumps(report,ensure_ascii=False,indent=2,allow_nan=True),encoding='utf-8')
    with (output/'timeline.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(rows[0]) if rows else ['kind']);writer.writeheader();writer.writerows(rows)
    summary='完整性: '+('时间线和已保留文件校验通过' if complete else '缺测或未确认')+'\n首次路径和文字显示空窗: '+str(first_gap and first_gap['firstObservedDisplayGap']['tick'])+'\n首次准备缓存空窗: '+str(prepared_gap and prepared_gap['tick'])+'\n接受原因分布: '+json.dumps(dict(distribution),ensure_ascii=False)+'\n比较拒收/缺测数: '+str(len(comparisons))+'\n场景/实例数: '+str(len(scene_groups))+'/'+str(len(instance_groups))+'\n磁盘保留字节: '+str(costs['retainedBytes'])+'\n完整报告见 analysis.json；所有未知项均保留其证据缺口。\n'
    (output/'summary.txt').write_text(summary,encoding='utf-8');print(output/'analysis.json');return 1 if integrity else 0

if __name__=='__main__':
    try:sys.exit(analyze(Path(sys.argv[1]).resolve(),Path(sys.argv[2]).resolve()))
    except Exception as error:print(type(error).__name__+': '+str(error),file=sys.stderr);sys.exit(1)
