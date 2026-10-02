# Local evidence checks for the temporary offline parser. No Terraria execution.
import copy, csv, hashlib, importlib.util, json, struct, sys, tempfile, unittest
from pathlib import Path
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('aim',Path(__file__).parents[1]/'analyze-aim-diagnostics.py')
aim=importlib.util.module_from_spec(spec);spec.loader.exec_module(aim)
HEADER='ordinal utc monotonic tick request generation sequence kind detail file offset length encoding queueBytes dropped previousWriteMs'.split()
class ParserChecks(unittest.TestCase):
    def fixture(self,root,name,events):
        directory=root/name;directory.mkdir(parents=True)
        with (directory/'timeline.tsv').open('w',newline='',encoding='utf-8') as stream:
            writer=csv.DictWriter(stream,fieldnames=HEADER,delimiter='\t');writer.writeheader()
            for i,(tick,kind,detail,request) in enumerate(events):
                row=dict.fromkeys(HEADER,'0');row.update(ordinal=i+1,utc='2026-10-02',monotonic=1000+i,tick=tick,kind=kind,detail=detail,request=request,file='',encoding='raw');writer.writerow(row)
        (directory/'files.tsv').write_text('file\toffset\tlength\tsha256\tencoding\tstate\n',encoding='utf-8')
        (directory/'session.tsv').write_text('frequency\t1000\n',encoding='utf-8')
        (directory/'completion.tsv').write_text('reason\tmanual-stop\ndropped\t0\ndetailStopped\tFalse\nfailure\t\n',encoding='utf-8')
        manifest='file\tlength\tsha256\n'
        for path in sorted(directory.iterdir()):manifest+=f'{path.name}\t{path.stat().st_size}\t{hashlib.sha256(path.read_bytes()).hexdigest().upper()}\n'
        (directory/'manifest.tsv').write_text(manifest,encoding='utf-8')
        return directory
    def test_missing_host_entire_timeline_never_complete(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)/'session';host=self.fixture(root,'host',[(1,'update','zone=1',0)]);self.fixture(root,'worker-1',[(1,'worker-receive','',0)])
            (host/'timeline.tsv').unlink()
            output=Path(directory)/'analysis';self.assertEqual(aim.analyze(root,output),1)
            report=json.loads((output/'analysis.json').read_text());self.assertFalse(report['complete']);self.assertTrue(any('timeline missing' in x for x in report['integrityErrors']))
    def test_truncated_manifest_header_never_complete(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)/'session';host=self.fixture(root,'host',[(1,'update','',0)]);(host/'manifest.tsv').write_text('file\tlength\tsha256\n')
            out=Path(directory)/'analysis';self.assertEqual(aim.analyze(root,out),1);report=json.loads((out/'analysis.json').read_text());self.assertFalse(report['complete']);self.assertTrue(any('mandatory' in error for error in report['integrityErrors']))
    def test_tail_without_draw_is_unknown_and_old_reply_uses_capture_identity(self):
        events=[(1,'selection','selected=True;session=1;slot=2;generation=1;type=1;netId=1;token=1;prior=session=0;slot=99',0),
                (1,'capture-attempt','session=1;slot=2;generation=1;type=1;netId=1;token=1',7),
                (1,'draw','presentation=1',0),(1,'draw-completed','presentation=1;pathStrokes=2',0),(1,'text-draw','presentation=1;path=True;drawn=True',0),
                (2,'selection','selected=True;session=1;slot=3;generation=1;type=3;netId=3;token=2',7),(2,'acceptance','outcome=retired',7),(2,'presentation-begin','presentation=2',0)]
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)/'session';self.fixture(root,'host',events);out=Path(directory)/'one';self.assertEqual(aim.analyze(root,out),0)
            report=json.loads((out/'analysis.json').read_text());self.assertIsNone(report['firstPathAndTextDisplayGap']);self.assertEqual(report['instances']['1 | 2 | 1 | 1 | 1 | 1']['acceptance']['retired'],1);self.assertEqual(report['instances']['1 | 3 | 1 | 3 | 3 | 2']['events']['selection'],1);self.assertEqual(report['instances']['1 | 2 | 1 | 1 | 1 | 1']['events']['selection'],1)
            self.fixture(root,'worker-1',[])
            events.append((3,'presentation-begin','presentation=3',0));(root/'host/manifest.tsv').unlink()
            with (root/'host/timeline.tsv').open('a',newline='',encoding='utf-8') as stream:
                row=dict.fromkeys(HEADER,'0');row.update(ordinal=9,monotonic=1010,tick=3,kind='presentation-begin',detail='presentation=3',file='',encoding='raw');csv.DictWriter(stream,fieldnames=HEADER,delimiter='\t').writerow(row)
            out=Path(directory)/'two';aim.analyze(root,out);report=json.loads((out/'analysis.json').read_text());self.assertEqual(report['firstPathAndTextDisplayGap']['firstObservedDisplayGap']['tick'],'2');self.assertFalse(report['complete'])
    def test_bits_decode_and_wire_index_are_distinct(self):
        def string(text):data=text.encode();return bytes([len(data)])+data
        data=struct.pack('<ii',0x41494D54,1)+string('NPC.ai')+string('System.Single')+struct.pack('<i',1)+struct.pack('<iiBQ',0,1,4,0x3f800000)
        _,fields=aim.fields(data);self.assertEqual(fields[0]['decodedValue'],1.0);self.assertEqual(fields[0]['wirePrimitiveIndex'],1);self.assertEqual(fields[0]['bits'],'000000003F800000')
    def test_typed_array_length_and_vector_components(self):
        def string(text):data=text.encode();return bytes([len(data)])+data
        data=struct.pack('<ii',0x41494D54,1)+string('NPC.ai')+string('System.Single[]')+struct.pack('<i',2)+struct.pack('<iiBQ',0,0,4,1)+struct.pack('<iiBQ',0,1,4,0x3f800000)
        _,items=aim.fields(data);self.assertEqual(items[0]['component'],'arrayLength');self.assertEqual(items[1]['arrayElementIndex'],0);self.assertEqual(items[1]['decodedValue'],1.0)
    def test_final_required_mask_and_sample_precedence(self):
        frame=dict(tick=1,hasState=True,world=1,npcSlots=[0],npcIdentity=[1],npcState=[1],npcShootClock=[0.0],npcVelocity=[[0,0]],npcOldVelocity=[[0,0]],npcPositions=[[[0,0]]],projectileSlots=[],projectileIdentity=[],projectileState=[],projectileVelocity=[],projectileOldVelocity=[],projectilePositions=[],players=[])
        changed=copy.deepcopy(frame);changed['npcState']=[2];reply=dict(npcRequired=[False],projectileRequired=[])
        self.assertIsNone(aim.difference(frame,changed,reply,1));self.assertEqual(aim.difference(frame,changed,reply,0)['key'],'NPC slot=0')
        changed=copy.deepcopy(frame);changed['npcVelocity']=[[0.001,0]];self.assertEqual(aim.difference(frame,changed,dict(npcRequired=[True],projectileRequired=[]),1)['key'],'NPC velocity')
if __name__=='__main__':unittest.main()
