using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // The directory is an observed population premise, not extra simulated
    // actors. Motion/AI/state reads require the actor's full dependency page.
    // This boundary guards native instance access, not the remaining static
    // world/complex-state closure; it must not be described as that proof.
    internal static class NativeEntityDirectory
    {
        private static readonly NativeValueSnapshot Npcs=new NativeValueSnapshot(typeof(NPC),fieldNames:new[]{"active","type","whoAmI","aiStyle","friendly","CanBeReplacedByOtherNPCs","<generation>k__BackingField"});
        private static readonly NativeValueSnapshot Projectiles=new NativeValueSnapshot(typeof(Projectile),fieldNames:new[]{"active","type","whoAmI","key","owner","netImportant","timeLeft","minion"});
        private static readonly bool[] minionOwnersChecked=new bool[Main.maxPlayers+1];
        private static HashSet<int> NpcReads,ProjectileReads,NpcFields,ProjectileFields;
        private static FieldPermissions NpcPermissions,ProjectilePermissions;
        private static FieldPermissions NpcDirectoryPermissions,ProjectileDirectoryPermissions;
        private sealed class FieldPermissions
        {
            private readonly int first;
            private readonly bool[] allowed;
            internal FieldPermissions(HashSet<int> fields)
            {
                first=fields.Min();long span=(long)fields.Max()-first+1;
                if(span<1 || span>65536)throw new InvalidDataException("Entity field permission extent.");
                allowed=new bool[(int)span];foreach(int field in fields)allowed[field-first]=true;
            }
            internal bool Contains(int field)
            {uint offset=unchecked((uint)(field-first));return offset<(uint)allowed.Length && allowed[offset];}
        }
        private static Dictionary<int,string> FieldIdentities;
        private static readonly NPC[] OpaqueNpcs=new NPC[Main.maxNPCs+1];
        private static readonly Projectile[] OpaqueProjectiles=new Projectile[Main.maxProjectiles+1];
        private static readonly NPC[] CapturedNpcs=new NPC[Main.maxNPCs+1];
        private static readonly Projectile[] CapturedProjectiles=new Projectile[Main.maxProjectiles+1];
        private struct Entry {internal int Kind,Slot;}
        private sealed class IdentityComparer : IEqualityComparer<object>
        {public new bool Equals(object a,object b){return ReferenceEquals(a,b);}public int GetHashCode(object value){return RuntimeHelpers.GetHashCode(value);}}
        private static readonly Dictionary<object,Entry> Opaque=new Dictionary<object,Entry>(new IdentityComparer());
        private static readonly Dictionary<object,Entry> Captured=new Dictionary<object,Entry>(new IdentityComparer());
        private static readonly FieldInfo KeyIndex=typeof(Projectile).GetField("keyToIndex",BindingFlags.Static|BindingFlags.NonPublic);
        private static FieldInfo handler,columnHandler;
        internal static int MissingKind {get;private set;}
        internal static int MissingSlot {get;private set;}
        internal static int MissingField {get;private set;}
        internal static void Write(BinaryWriter writer)
        {
            Npcs.WriteHeader(writer);for(int i=0;i<=Main.maxNPCs;i++)Npcs.WriteFields(writer,Main.npc[i]);
            var index=(int[,])KeyIndex.GetValue(null);
            Projectiles.WriteHeader(writer);
            for(int i=0;i<=Main.maxProjectiles;i++)
            {
                var p=Main.projectile[i];
                if(p.key.Index>1000)throw new InvalidDataException("Projectile directory key.");
                Projectiles.WriteFields(writer,p);writer.Write(index[p.key.Spawner,p.key.Index]);
            }
        }
        internal static void Read(BinaryReader reader)
        {
            var cells=new Dictionary<int,int>();var index=(int[,])KeyIndex.GetValue(null);Array.Clear(index,0,index.Length);
            Npcs.ReadHeader(reader);
            for(int i=0;i<=Main.maxNPCs;i++)
            {
                NPC npc=Main.npc[i];Npcs.ReadFields(reader,npc);
                if(npc.active && (npc.whoAmI!=i || npc.type<=0 || npc.type>=Terraria.ID.NPCID.Count))throw new InvalidDataException("NPC directory identity.");
                OpaqueNpcs[i]=npc;Opaque.Add(npc,new Entry{Kind=1,Slot=i});
            }
            Projectiles.ReadHeader(reader);for(int i=0;i<=Main.maxProjectiles;i++)
            {
                Projectile p=Main.projectile[i];Projectiles.ReadFields(reader,p);
                int lookup=reader.ReadInt32();
                if(p.key.Index>=1001 || p.type<0 || p.type>=Terraria.ID.ProjectileID.Count || p.active && (p.whoAmI!=i || p.type==0))throw new InvalidDataException("Projectile directory identity.");
                if(lookup<0 || lookup>Main.maxProjectiles)throw new InvalidDataException("Projectile directory lookup slot.");
                // TryLookup compares keys even for inactive/sentinel slots.
                // Preserve the actual index cell for every key that exists in
                // any slot, including stale links to a different key. Absent
                // keys cannot match any object, so other cells may be zero.
                // Generation is not part of the native index cell identity.
                int cell=p.key.Spawner*1001+p.key.Index,previous;
                if(cells.TryGetValue(cell,out previous) && previous!=lookup)throw new InvalidDataException("Conflicting projectile directory cell.");
                cells[cell]=lookup;index[p.key.Spawner,p.key.Index]=lookup;
                OpaqueProjectiles[i]=p;Opaque.Add(p,new Entry{Kind=2,Slot=i});
            }
        }
        internal static void KnowNpc(int slot){Know(OpaqueNpcs[slot]);OpaqueNpcs[slot]=null;}
        internal static void KnowProjectile(int slot){Know(OpaqueProjectiles[slot]);OpaqueProjectiles[slot]=null;}
        private static void Know(object value)
        {Entry entry=Opaque[value];Opaque.Remove(value);Captured.Add(value,entry);if(entry.Kind==1)CapturedNpcs[entry.Slot]=(NPC)value;else CapturedProjectiles[entry.Slot]=(Projectile)value;}
        internal static bool CanAdvance(NPC npc)
        {
#if JMR_CONDITIONAL_RESEARCH
            if(ConditionalNpcQuery.IsQuery(npc))return false;
#endif
            return !Opaque.ContainsKey(npc);
        }
        internal static bool CanAdvance(Projectile projectile){return !Opaque.ContainsKey(projectile);}
        internal static bool IsOpaque(NPC npc){return Opaque.ContainsKey(npc);}
        internal static void RequireMinionContext(Projectile projectile)
        {
            if(!projectile.minion || minionOwnersChecked[projectile.owner])return;
            for(int i=0;i<Main.maxProjectiles;i++)
            {var p=Main.projectile[i];if(p.active && p.minion && p.owner==projectile.owner)RequireKnown(p);}
            minionOwnersChecked[projectile.owner]=true;
        }
        internal static void RequireKnown(object value)
        {
#if JMR_CONDITIONAL_RESEARCH
            var query=value as NPC;
            if(query!=null && ConditionalNpcQuery.IsQuery(query))
            {if(MissingKind==0){MissingKind=5;MissingSlot=query.whoAmI;MissingField=0;}throw new InvalidDataException("Research query mutation requires exact actor.");}
#endif
            Entry entry;if(!Opaque.TryGetValue(value,out entry))return;
            if(MissingKind==0){MissingKind=entry.Kind;MissingSlot=entry.Slot;MissingField=0;}
            throw new InvalidDataException("Unobserved entity for immunity reset: kind="+entry.Kind+" slot="+entry.Slot);
        }
        internal static void MissingNpcColumn(int slot)
        {
            if(MissingKind==0){MissingKind=1;MissingSlot=slot;MissingField=0;}
            throw new InvalidDataException("Unobserved NPC immunity column: slot="+slot);
        }
        internal static void Reset()
        {
            NativeNpcEligibility.Reset();
            Array.Clear(minionOwnersChecked,0,minionOwnersChecked.Length);
#if JMR_CONDITIONAL_RESEARCH
            ConditionalNpcQuery.Reset();
#endif
            End();MissingKind=0;MissingSlot=-1;MissingField=0;Opaque.Clear();Captured.Clear();Array.Clear(OpaqueNpcs,0,OpaqueNpcs.Length);Array.Clear(OpaqueProjectiles,0,OpaqueProjectiles.Length);Array.Clear(CapturedNpcs,0,CapturedNpcs.Length);Array.Clear(CapturedProjectiles,0,CapturedProjectiles.Length);
        }
        internal static void ClearWorld()
        {Reset();var index=(int[,])KeyIndex.GetValue(null);Array.Clear(index,0,index.Length);}
        internal static void Begin()
        {
            if(handler==null)
            {
                Type guard=typeof(Main).Assembly.GetType(NativeEntityImage.GuardName,true);handler=guard.GetField("Handler",BindingFlags.Public|BindingFlags.Static);
                columnHandler=guard.GetField("CanClearColumn",BindingFlags.Public|BindingFlags.Static);
                FieldIdentities=((string)guard.GetField("OriginalFields").GetRawConstantValue()).Split('\n').Select(line=>line.Split('|')).ToDictionary(p=>int.Parse(p[0],System.Globalization.CultureInfo.InvariantCulture),p=>p[1]);
                var names=FieldIdentities.ToDictionary(p=>p.Value,p=>p.Key);
#if JMR_CONDITIONAL_RESEARCH
                ConditionalNpcQuery.Prepare(names);
#endif
                NpcReads=new HashSet<int>(Npcs.FieldIdentities.Select(n=>names[n]));ProjectileReads=new HashSet<int>(Projectiles.FieldIdentities.Select(n=>names[n]));
                NpcFields=new HashSet<int>(PredictionWire.Npcs.FieldIdentities.Where(names.ContainsKey).Select(n=>names[n]));
                ProjectileFields=new HashSet<int>(new NativeValueSnapshot(typeof(Projectile)).FieldIdentities.Where(names.ContainsKey).Select(n=>names[n]));
                ProjectileFields.Add(names["Terraria.Projectile.hostileDamageScaling"]);
                foreach(string field in NativeEntityContext.NpcFields)NpcFields.Add(names[field]);
                foreach(string field in NativeEntityContext.ProjectileFields)ProjectileFields.Add(names[field]);
                // Build from authenticated original tokens, never the private
                // image's renumbered FieldDefs or token RIDs stripped of their
                // table identity. Repeated array reads in native immunity loops
                // still check every receiver/field, without hashing each read.
                NpcPermissions=new FieldPermissions(NpcFields);ProjectilePermissions=new FieldPermissions(ProjectileFields);
                NpcDirectoryPermissions=new FieldPermissions(NpcReads);ProjectileDirectoryPermissions=new FieldPermissions(ProjectileReads);
            }
            handler.SetValue(null,(Action<object,int,int>)Check);
            columnHandler.SetValue(null,(Func<object,bool>)(value=>!Opaque.ContainsKey(value)));
        }
        internal static void End(){if(handler!=null)handler.SetValue(null,null);if(columnHandler!=null)columnHandler.SetValue(null,null);}
        private delegate ref int SlotAddress(Entity entity);
        internal static void VerifyIntrinsics()
        {
            // Test the private CLR image itself, including an address of a
            // directory-readable field: addresses must still require a page.
            var sample=new NPC{whoAmI=3,active=true,width=20,height=30};
            OpaqueNpcs[3]=sample;Opaque.Add(sample,new Entry{Kind=1,Slot=3});Begin();
            try
            {
                bool refused=false;try{var ignored=sample.Center;}catch(InvalidDataException){refused=true;}
                if(!refused || MissingKind!=1 || MissingSlot!=3)throw new InvalidDataException("Entity read guard self-test.");
                refused=false;try{sample.Size=new Microsoft.Xna.Framework.Vector2(300,400);}catch(InvalidDataException){refused=true;}
                if(!refused || sample.width!=20 || sample.height!=30)throw new InvalidDataException("Entity write guard self-test.");
                var method=typeof(Main).Assembly.GetType(NativeEntityImage.GuardName,true).GetMethod("ProbeSlotAddress",BindingFlags.Static|BindingFlags.NonPublic);
                var address=(SlotAddress)Delegate.CreateDelegate(typeof(SlotAddress),method);
                refused=false;try{address(sample)=9;}catch(InvalidDataException){refused=true;}
                if(!refused || sample.whoAmI!=3)throw new InvalidDataException("Entity address guard self-test.");
                KnowNpc(3);ref int alias=ref address(sample);alias=4;
                if(sample.whoAmI!=4)throw new InvalidDataException("Entity address alias changed.");
            }
            finally{Reset();}
            // Native TryLookup retains a killed projectile's canonical key.
            // Verify directory roundtrip preserves lookup and then refuses an
            // unsampled physical read, including an inactive object's page.
            Projectile previous=Main.projectile[5];var index=(int[,])KeyIndex.GetValue(null);var oldIndex=(int[,])index.Clone();
            try
            {
                var key=new Terraria.DataStructures.ProjectileKey(0,5,17);
                Main.projectile[5]=new Projectile{whoAmI=5,type=1,active=false,key=key};index[key.Spawner,key.Index]=5;
                Projectile found;if(!Projectile.TryLookup(key,out found) || !ReferenceEquals(found,Main.projectile[5]))throw new InvalidDataException("Inactive key fixture.");
                byte[] bytes;using(var output=new MemoryStream())using(var writer=new BinaryWriter(output)){Write(writer);writer.Flush();bytes=output.ToArray();}
                Reset();using(var input=new BinaryReader(new MemoryStream(bytes,false)))Read(input);Begin();
                if(!Projectile.TryLookup(key,out found) || !ReferenceEquals(found,Main.projectile[5]))throw new InvalidDataException("Inactive key directory roundtrip.");
                bool refused=false;try{var ignored=found.Center;}catch(InvalidDataException){refused=true;}
                if(!refused || MissingKind!=2 || MissingSlot!=5)throw new InvalidDataException("Inactive key physical read escaped directory.");
            }
            finally{Reset();Main.projectile[5]=previous;Array.Copy(oldIndex,index,oldIndex.Length);}
            VerifyColumnReset();
            VerifyFullPageGaps();
        }
        private static void VerifyFullPageGaps()
        {
            var sample=new NPC{whoAmI=3,active=true};
            OpaqueNpcs[3]=sample;Opaque.Add(sample,new Entry{Kind=1,Slot=3});KnowNpc(3);Begin();
            try
            {
                // This original method reads the unsampled network array
                // before it could inspect a socket or send anything.
                bool refused=false;
                try{typeof(NPC).GetMethod("RecheckSectionsForSkippedUpdates",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sample,null);}
                catch(TargetInvocationException error){refused=error.InnerException is InvalidDataException;}
                if(!refused || MissingKind!=3 || MissingSlot!=3 || FieldIdentities[MissingField]!="Terraria.NPC.playerNetSyncState")throw new InvalidDataException("Full NPC page silently read uncaptured context.");
                int slotField=FieldIdentities.Single(p=>p.Value=="Terraria.Entity.whoAmI").Key;
                // Model a schema that omitted an addressable scalar, using the
                // existing real CLR byref probe. Restore the permitted schema
                // before any request; this is not a product policy override.
                FieldPermissions completePermissions=NpcPermissions;
                NpcFields.Remove(slotField);NpcPermissions=new FieldPermissions(NpcFields);MissingKind=0;MissingSlot=-1;MissingField=0;
                try
                {
                    var address=(SlotAddress)Delegate.CreateDelegate(typeof(SlotAddress),typeof(Main).Assembly.GetType(NativeEntityImage.GuardName,true).GetMethod("ProbeSlotAddress",BindingFlags.Static|BindingFlags.NonPublic));
                    refused=false;try{address(sample)=7;}catch(InvalidDataException){refused=true;}
                    if(!refused || sample.whoAmI!=3 || MissingKind!=3 || MissingSlot!=3 || MissingField!=slotField)throw new InvalidDataException("Full-page missing scalar escaped byref guard.");
                }
                finally{NpcFields.Add(slotField);NpcPermissions=completePermissions;}
            }
            finally{Reset();}
            // Exercise the actual guard dispatch for byref and assignment:
            // a whole-field write is allowed but never certifies its graph.
            var projectile=new Projectile{whoAmI=5,active=true,owner=0};
            Projectile previous=Main.projectile[5];Main.projectile[5]=projectile;
            OpaqueProjectiles[5]=projectile;Opaque.Add(projectile,new Entry{Kind=2,Slot=5});KnowProjectile(5);Begin();
            try
            {
                int field=FieldIdentities.Single(p=>p.Value=="Terraria.Projectile.MinionSpawnInfo").Key;
                var dispatch=(Action<object,int,int>)Delegate.CreateDelegate(typeof(Action<object,int,int>),typeof(Main).Assembly.GetType(NativeEntityImage.GuardName,true).GetMethod("Check",BindingFlags.Static|BindingFlags.NonPublic));
                // Actual native SetDefaults writes MinionSpawnInfo=null. That
                // complete stfld must not make its later read appear sampled.
                projectile.SetDefaults(1);projectile.owner=0;
                bool nativeRefused=false;
                try{new Terraria.DataStructures.MinionRespawner().CollectMinionsFor(new Player{whoAmI=0});}catch(InvalidDataException){nativeRefused=true;}
                if(!nativeRefused || MissingKind!=4 || MissingSlot!=5 || MissingField!=field)throw new InvalidDataException("Full projectile native read escaped its context gap.");
                foreach(int mode in new[]{0,1})
                {
                    bool refused=false;try{dispatch(projectile,field,mode);}catch(InvalidDataException){refused=true;}
                    if(!refused || MissingKind!=4 || MissingSlot!=5 || MissingField!=field)throw new InvalidDataException("Full projectile context was promoted by a write.");
                }
            }
            finally{Reset();Main.projectile[5]=previous;}
            Console.Error.WriteLine("ENTITY full-page unknown NPC native read / projectile read-address / write does not promote verified");
        }
        private static void VerifyColumnReset()
        {
            // Exercise the rewritten original store and a later original read.
            // Host reflection alone would bypass the image guard. A skipped
            // opaque column is never observed: even after this write it must
            // still request a page, which is restored before a fresh replay.
            const int column=7,adjacent=8;var matrix=Projectile.perIDStaticNPCImmunity;
            var oldColumn=new uint[matrix.GetLength(0)];var oldAdjacent=new uint[oldColumn.Length];
            byte[] directory;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){Write(writer);writer.Flush();directory=stream.ToArray();}
            int knownOld=Main.projectile[0].localNPCImmunity[column],unknownOld=Main.projectile[5].localNPCImmunity[column];
            int adjacentOld=Main.projectile[0].localNPCImmunity[adjacent];
            for(int i=0;i<oldColumn.Length;i++){oldColumn[i]=matrix[i,column];oldAdjacent[i]=matrix[i,adjacent];matrix[i,column]=13;matrix[i,adjacent]=17;}
            try
            {
                Reset();using(var reader=new BinaryReader(new MemoryStream(directory,false)))Read(reader);KnowProjectile(0);
                Main.projectile[0].localNPCImmunity[column]=11;Main.projectile[0].localNPCImmunity[adjacent]=19;Main.projectile[5].localNPCImmunity[column]=23;
                Begin();Projectile.ResetNPCSlotData(column);
                if(Main.projectile[0].localNPCImmunity[column]!=0 || Main.projectile[0].localNPCImmunity[adjacent]!=19 || Main.projectile[5].localNPCImmunity[column]!=23 || CanAdvance(Main.projectile[5]))throw new InvalidDataException("Native immunity column scope self-test.");
                for(int i=0;i<oldColumn.Length;i++)if(matrix[i,column]!=0 || matrix[i,adjacent]!=17)throw new InvalidDataException("Native static immunity column changed.");
                bool matchingRefused=false;try{Main.projectile[5].ResetStaticImmunityMatching(7);}catch(InvalidDataException){matchingRefused=true;}
                if(!matchingRefused || MissingKind!=2 || MissingSlot!=5)throw new InvalidDataException("Matching immunity reset escaped opaque receiver scope.");
                bool refused=false;try{Main.projectile[5].ResetLocalNPCHitImmunity();}catch(InvalidDataException){refused=true;}
                if(!refused || MissingKind!=2 || MissingSlot!=5)throw new InvalidDataException("Skipped immunity column escaped opaque scope.");
                // This intrinsic test restores its column premise and changes
                // the known set before replay. End-to-end page recapture is a
                // separate request test, not proved by these manual values.
                Reset();using(var reader=new BinaryReader(new MemoryStream(directory,false)))Read(reader);KnowProjectile(0);KnowProjectile(5);
                Main.projectile[0].localNPCImmunity[column]=11;Main.projectile[5].localNPCImmunity[column]=23;
                Begin();Projectile.ResetNPCSlotData(column);
                if(Main.projectile[0].localNPCImmunity[column]!=0 || Main.projectile[5].localNPCImmunity[column]!=0)throw new InvalidDataException("Native immunity full-page replay self-test.");
            }
            finally
            {
                Reset();Main.projectile[0].localNPCImmunity[column]=knownOld;Main.projectile[5].localNPCImmunity[column]=unknownOld;Main.projectile[0].localNPCImmunity[adjacent]=adjacentOld;
                for(int i=0;i<oldColumn.Length;i++){matrix[i,column]=oldColumn[i];matrix[i,adjacent]=oldAdjacent[i];}
            }
            Console.Error.WriteLine("ENTITY immunity-column known clear / opaque refusal / restored column replay verified");
        }
        private static void Check(object value,int field,int mode)
        {
            Entry entry;if(value==null || !(value is NPC) && !(value is Projectile))return;
#if JMR_CONDITIONAL_RESEARCH
            var query=value as NPC;
            if(query!=null && ConditionalNpcQuery.IsQuery(query) && !ConditionalNpcQuery.ReadOnly(query,field,mode))
            {
                if(MissingKind==0){MissingKind=5;MissingSlot=query.whoAmI;MissingField=field;}
                throw new InvalidDataException("Research query address/reference/mutation requires exact actor field="+FieldIdentities[field]);
            }
#endif
            // Frequent native scalar access can prove membership by both slot
            // and reference, before paying two object dictionaries. Slot alone
            // is never authority: replaced objects and mutated whoAmI fall
            // through to the original identity-based rules, including unknown
            // fields and managed-address refusal. Reset clears both indexes.
            var npc=value as NPC;
            if(npc!=null && (uint)npc.whoAmI<(uint)CapturedNpcs.Length && ReferenceEquals(CapturedNpcs[npc.whoAmI],npc) && NpcPermissions.Contains(field))
            {NativePredictionPurpose.Access(npc,mode,NpcDirectoryPermissions.Contains(field),field);return;}
            var projectile=value as Projectile;
            if(projectile!=null && (uint)projectile.whoAmI<(uint)CapturedProjectiles.Length && ReferenceEquals(CapturedProjectiles[projectile.whoAmI],projectile) && ProjectilePermissions.Contains(field))
            {NativePredictionPurpose.Access(projectile,mode,ProjectileDirectoryPermissions.Contains(field),field);return;}
            if(!Opaque.TryGetValue(value,out entry))
            {
                // A restored primitive page is not a restored object graph.
                // Native future births are initialized by the original code;
                // only captured objects carry missing historical field state.
                if(!Captured.TryGetValue(value,out entry) || (entry.Kind==1?NpcFields:ProjectileFields).Contains(field))
                {NativePredictionPurpose.Access(value,mode,npc!=null?NpcDirectoryPermissions.Contains(field):ProjectileDirectoryPermissions.Contains(field),field);return;}
                // Preserve a complete original assignment, but never promote
                // an unknown nested graph merely because its outer ref changed.
                // A later read or managed address still requires its codec.
                if(mode==2){NativePredictionPurpose.Access(value,mode,false,field);return;}
                if(MissingKind==0){MissingKind=entry.Kind+2;MissingSlot=entry.Slot;MissingField=field;}
                throw new InvalidDataException("Uncaptured full-page field kind="+entry.Kind+" slot="+entry.Slot+" field="+FieldIdentities[field]+" access="+mode);
            }
            int kind=entry.Kind,slot=entry.Slot;
            if(mode==0 && (kind==1?NpcReads:ProjectileReads).Contains(field) && !(kind==1 && NativeNpcEligibility.AllocationNeedsPage(npc)))return;
            if(MissingKind==0){MissingKind=kind;MissingSlot=slot;MissingField=field;}
            throw new InvalidDataException("Unobserved entity field kind="+kind+" slot="+slot+" token="+field.ToString("X8")+" field="+FieldIdentities[field]+" access="+mode);
        }
    }
}
