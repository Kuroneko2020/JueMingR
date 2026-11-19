using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Companions;
using JueMingR.Infrastructure.Companions;
using JueMingR.Platform.Companions;

namespace JueMingR.ArchitectureTests
{
    // Opaque payloads test storage/lifetime semantics only. They deliberately do
    // not stand in for native Player serialization, use, death or multiplayer.
    internal static class CompanionCoreChecks
    {
        internal static void Check(IList<string> failures)
        {
            string parent = Path.GetFullPath(Path.GetTempPath());
            string root = Path.GetFullPath(Path.Combine(parent, "JueMingR-companion-checks-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try { ArchiveRoundTrip(root); PreferenceRoundTrip(root); FailedCreation(root); CoalescedReset(root); CorruptIsolation(root); RetryRead(root); RetireOwner(root); MenuMigration(root); TerminalArchive(root); LegacyArchive(root); ContinuousJump(); RejectedTakeoff(); }
            catch (Exception exception) { failures.Add("companion core: " + exception); }
            finally
            {
                if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(root).IndexOf("JueMingR-companion-checks-", StringComparison.Ordinal) != 0)
                    throw new InvalidOperationException("Unsafe isolated test cleanup path.");
                Directory.Delete(root, true);
            }
        }
        private static void ArchiveRoundTrip(string root)
        {
            string game = Path.Combine(root, "roundtrip"); Guid owner, id = Guid.NewGuid();
            byte[] bytes = Enumerable.Range(0, 131072).Select(n => (byte)(n % 251)).ToArray();
            using (var archive = new FileCompanionArchive(game))
            {
                owner = archive.LoadOwner("local-file-before-display-rename").OwnerId;
                archive.LinkOwner("cloud-file-after-native-move", owner);
                var original = new CompanionProfile(id, owner, "旅伴", 1, 1, new CompanionBehavior(), bytes);
                archive.Save(original);
                bytes[0] = 255;
                var changed = original.Revise(new byte[] { 150 }, new CompanionBehavior { Follow = false });
                archive.Save(changed);
                bool stale = false;
                try { archive.Save(original); } catch (InvalidDataException) { stale = true; }
                Require(stale, "older resource revision is rejected");
            }
            using (var archive = new FileCompanionArchive(game))
            {
                var local = archive.LoadOwner("local-file-before-display-rename");
                var cloud = archive.LoadOwner("cloud-file-after-native-move");
                Require(local.OwnerId == owner && cloud.OwnerId == owner && local.Profiles.Count == 1 && cloud.Profiles.Count == 1, "explicit native-file migration preserves one owner and profile");
                Require(local.Profiles[0].Id == id && local.Profiles[0].NativeData.SequenceEqual(new byte[] { 150 }) && !local.Profiles[0].Behavior.Follow, "new archive instance loads the one latest resource revision with matching preferences");
                Require(archive.LoadOwner("different-real-character-file").Profiles.Count == 0, "a different file cannot claim companions by display name");
            }
        }
        private static void LegacyArchive(string root)
        {
            foreach(int version in new[]{1,2,3})
            {
            string game=Path.Combine(root,"v"+version);Guid owner,id=Guid.NewGuid();
            using(var archive=new FileCompanionArchive(game)){owner=archive.LoadOwner("legacy").OwnerId;}
            string folder=Path.Combine(game,"JueMingRData","companions","characters",owner.ToString("N"));Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,id.ToString("N")+".bin");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new System.Text.UTF8Encoding(false,true)))
            {
                // Independently encode the historical formats rather than use
                // the current encoder to pretend it emitted a legacy file.
                writer.Write("JueMingR.Companion");writer.Write(version);writer.Write(id.ToByteArray());writer.Write(owner.ToByteArray());
                writer.Write("旧人物");writer.Write(7L);writer.Write(3L);writer.Write(false);writer.Write(true);writer.Write(true);writer.Write(false);
                if(version>=2)writer.Write(version==2);
                if(version==3){writer.Write(false);writer.Write(48);writer.Write(39);writer.Write((byte)60);}
                writer.Write(2);writer.Write(new byte[]{44,55});writer.Flush();
                using(var sha=System.Security.Cryptography.SHA256.Create())writer.Write(sha.ComputeHash(stream.ToArray()));writer.Flush();File.WriteAllBytes(path,stream.ToArray());
            }
            using(var archive=new FileCompanionArchive(game))
            {
                byte[] original = File.ReadAllBytes(path);
                var old=archive.LoadOwner("legacy").Profiles.Single();
                var behavior = old.Behavior;
                Require(old.Id==id && old.OwnerId==owner && old.Revision==7 && old.DataEpoch==3 && old.PermanentlyDead==(version==2) &&
                    old.NativeData.SequenceEqual(new byte[]{44,55}) && !behavior.Follow && behavior.AttackEnabled && behavior.AutoRespawn==(version<3),
                    "legacy archive preserves native bytes, ownership, terminal identity, and retained behavior fields");
                Require(behavior.HealthRecoveryEnabled && behavior.HealthRecoveryMode==CompanionHealthRecoveryMode.Smart && behavior.ManaRecoveryEnabled &&
                    behavior.AllowNurse && behavior.ConsumablesEnabled && behavior.ExcludedHealingTypes.Length==0 && behavior.ExcludedManaTypes.Length==0 &&
                    behavior.AllowedGrowthTypes.Length==0 && behavior.AllowedBuffTypes.Length==0, "each legacy version receives the intended supply defaults");
                Require(behavior.MainWeapons.Length==(version==3?1:0),"only v3 contains a legacy manual selection");
                if(version==3) Require(behavior.MainWeapons[0].SlotHint==48&&behavior.MainWeapons[0].Type==39&&behavior.MainWeapons[0].Prefix==60,
                    "v3 manual selection migrates to one actual-slot preference");
                archive.Save(old);
                Require(original.SequenceEqual(File.ReadAllBytes(path)),"loading and reaccepting an identical old revision leaves the original format untouched");
                archive.Save(old.Revise(old.NativeData));
            }
            using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream))
            { Require(reader.ReadString()=="JueMingR.Companion"&&reader.ReadInt32()==4,"the next complete revision writes one v4 record"); }
            using(var archive=new FileCompanionArchive(game))Require(archive.LoadOwner("legacy").Profiles.Single().PermanentlyDead==(version==2),"normal next revision upgrades legacy without rewriting terminal semantics");
            }
        }
        private static void PreferenceRoundTrip(string root)
        {
            string game=Path.Combine(root,"preferences");Guid owner,id=Guid.NewGuid();
            var behavior=new CompanionBehavior
            {
                AutoRespawn=false,HealthRecoveryEnabled=false,HealthRecoveryMode=CompanionHealthRecoveryMode.Fast,
                ManaRecoveryEnabled=false,AllowNurse=false,ConsumablesEnabled=false,
                MainWeapons=new[]{new CompanionWeaponPreference(39,60,12),new CompanionWeaponPreference(3507,0,49)},
                ExcludedHealingTypes=new[]{28},ExcludedManaTypes=new[]{110},AllowedGrowthTypes=new[]{29,109},AllowedBuffTypes=new[]{292}
            };
            var clone=behavior.Clone();clone.Follow=false;
            behavior.MainWeapons[0]=default(CompanionWeaponPreference);behavior.ExcludedHealingTypes[0]=1;behavior.ExcludedManaTypes[0]=2;
            behavior.AllowedGrowthTypes[0]=3;behavior.AllowedBuffTypes[0]=4;
            Require(clone.MainWeapons[0].Type==39 && clone.ExcludedHealingTypes[0]==28 && clone.ExcludedManaTypes[0]==110 &&
                clone.AllowedGrowthTypes[0]==29 && clone.AllowedBuffTypes[0]==292,"each cloned preference array is independent");
            using(var archive=new FileCompanionArchive(game))
            {owner=archive.LoadOwner("creator").OwnerId;archive.Save(new CompanionProfile(id,owner,"偏好保存",1,1,clone,new byte[]{7,9},true));}
            using(var archive=new FileCompanionArchive(game))
            {
                var profile=archive.LoadOwner("creator").Profiles.Single();var saved=profile.Behavior;
                Require(profile.OwnerId==owner && profile.Id==id && profile.PermanentlyDead && profile.NativeData.SequenceEqual(new byte[]{7,9}) &&
                    !saved.Follow && !saved.AutoRespawn && saved.MainWeapons.Length==2 && saved.MainWeapons[0].Prefix==60 && saved.MainWeapons[1].SlotHint==49 &&
                    !saved.HealthRecoveryEnabled && saved.HealthRecoveryMode==CompanionHealthRecoveryMode.Fast && !saved.ManaRecoveryEnabled &&
                    !saved.AllowNurse && !saved.ConsumablesEnabled && saved.ExcludedHealingTypes.SequenceEqual(new[]{28}) && saved.ExcludedManaTypes.SequenceEqual(new[]{110}) &&
                    saved.AllowedGrowthTypes.SequenceEqual(new[]{29,109}) && saved.AllowedBuffTypes.SequenceEqual(new[]{292}),
                    "one v4 record reopens the full settings with native, owner and terminal data intact");
                saved.AutoRespawn=true;saved.MainWeapons[0]=default(CompanionWeaponPreference);saved.ExcludedHealingTypes[0]=1;
                Require(!profile.Behavior.AutoRespawn&&profile.Behavior.MainWeapons[0].Type==39&&profile.Behavior.ExcludedHealingTypes[0]==28,
                    "caller's edits cannot mutate the stored profile or its arrays");
            }
        }
        private static void TerminalArchive(string root)
        {
            string game = Path.Combine(root,"terminal"); Guid id = Guid.NewGuid();
            using (var archive = new FileCompanionArchive(game))
            using (var manager = new CompanionProfileManager(archive))
            {
                manager.SetOwner("creator"); Wait(manager,()=>manager.Ready);
                var profile = new CompanionProfile(id,manager.OwnerId,"硬核终局",1,1,new CompanionBehavior(),new byte[]{2});
                manager.Add(profile); Wait(manager,()=>manager.IsSaved(id)); long generation;
                Require(manager.TryReserve(id,out generation),"living hardcore archive can activate");
                profile=profile.Revise(new byte[]{0},permanentlyDead:true); manager.Update(profile); manager.Release(id,generation);
                Require(!manager.TryReserve(id,out generation),"terminal marker immediately blocks activation before disk completion");
                Wait(manager,()=>manager.IsSaved(id));
            }
            using(var archive = new FileCompanionArchive(game))
            {
                var profile=archive.LoadOwner("creator").Profiles.Single();
                Require(profile.PermanentlyDead && profile.NativeData[0]==0,"terminal marker and final resources survive a fresh archive reader");
                var behavior=profile.Revise(profile.NativeData,new CompanionBehavior());
                Require(behavior.PermanentlyDead,"behavior restoration cannot revive terminal character");
                var reset=behavior.Revise(new byte[]{100},reset:true);
                Require(!reset.PermanentlyDead && reset.DataEpoch==profile.DataEpoch+1,"only explicit reset creates a fresh lifecycle epoch");
                archive.Save(reset);
            }
        }
        private static void FailedCreation(string root)
        {
            using (var archive = new FailOnceArchive(new FileCompanionArchive(Path.Combine(root, "failure"))))
            using (var manager = new CompanionProfileManager(archive))
            {
                manager.SetOwner("creator"); Wait(manager, () => manager.Ready);
                var profile = new CompanionProfile(Guid.NewGuid(), manager.OwnerId, "重试", 1, 1, new CompanionBehavior(), new byte[] { 41, 42, 43 });
                manager.Add(profile);
                long generation;
                Require(!manager.TryReserve(profile.Id, out generation), "first commit must finish before activation");
                Wait(manager, () => manager.SaveFailed(profile.Id));
                Require(manager.Profiles.Count == 1 && manager.Find(profile.Id).NativeData.SequenceEqual(new byte[] { 41, 42, 43 }) && !manager.TryReserve(profile.Id, out generation), "failed first commit retains one retryable draft without an activatable body");
                manager.RetrySave(profile.Id); Wait(manager, () => manager.IsSaved(profile.Id));
                Require(!manager.SaveFailed(profile.Id) && manager.TryReserve(profile.Id, out generation), "same draft can commit and activate after explicit retry");
                long duplicate; Require(!manager.TryReserve(profile.Id, out duplicate), "same profile has one active body");
                manager.Release(profile.Id, generation); Require(manager.TryReserve(profile.Id, out duplicate) && duplicate != generation, "new activation has a new generation");
                manager.Release(profile.Id, generation);
                Require(manager.OwnsActivation(profile.Id, duplicate), "old release cannot revoke a successor");
                manager.Release(profile.Id, duplicate);
            }
        }
        private static void CoalescedReset(string root)
        {
            var gated = new GatedArchive(new FileCompanionArchive(Path.Combine(root, "coalesced")));
            using (var manager = new CompanionProfileManager(gated))
            {
                manager.SetOwner("creator"); Wait(manager, () => manager.Ready);
                var profile = new CompanionProfile(Guid.NewGuid(), manager.OwnerId, "合并写", 1, 1, new CompanionBehavior(), new byte[] { 200 });
                manager.Add(profile);
                Require(gated.Entered.Wait(5000), "worker reached the actual archive write");
                for (int n = 0; n < 100; n++) { profile = profile.Revise(new byte[] { (byte)(199 - n) }); manager.Update(profile); }
                profile = profile.Revise(new byte[] { 7 }, new CompanionBehavior(), reset: true); manager.Update(profile);
                gated.Allow.Set(); Wait(manager, () => manager.IsSaved(profile.Id));
                Require(gated.Writes == 2 && manager.Find(profile.Id).DataEpoch == 2 && manager.Find(profile.Id).NativeData[0] == 7, "one pending snapshot per character; older completion never overwrites reset data in memory");
            }
            using (var archive = new FileCompanionArchive(Path.Combine(root, "coalesced")))
            {
                var latest = archive.LoadOwner("creator").Profiles.Single();
                Require(latest.DataEpoch == 2 && latest.NativeData[0] == 7 && latest.Revision == 102, "ordered final commit survives reopening after reset");
            }
        }
        private static void CorruptIsolation(string root)
        {
            string game = Path.Combine(root, "corruption"); Guid owner, bad = Guid.NewGuid(), good = Guid.NewGuid();
            using (var archive = new FileCompanionArchive(game))
            {
                owner = archive.LoadOwner("creator").OwnerId;
                archive.Save(new CompanionProfile(bad, owner, "损坏", 1, 1, new CompanionBehavior(), new byte[] { 1 }));
                archive.Save(new CompanionProfile(good, owner, "可用", 1, 1, new CompanionBehavior(), new byte[] { 2 }));
            }
            string path = Path.Combine(game, "JueMingRData", "companions", "characters", owner.ToString("N"), bad.ToString("N") + ".bin");
            File.WriteAllBytes(path, new byte[] { 255, 255 });
            using (var archive = new FileCompanionArchive(game))
            {
                var loaded = archive.LoadOwner("creator");
                Require(loaded.Profiles.Count == 1 && loaded.Profiles[0].Id == good && loaded.Error != null, "one corrupt character reports its error without disabling another");
                Require(File.ReadAllBytes(path).SequenceEqual(new byte[] { 255, 255 }), "corrupt source is not silently replaced by a starter character");
            }
        }
        private static void ContinuousJump()
        {
            var brain = new CompanionBehaviorBrain();
            var observation = new CompanionMovementObservation { X = 0, Y = 0, TargetX = 200, TargetY = 0, Grounded = true, CanTakeOff = true, WalkSafe = false, Wall = true, JumpSafe = true, JumpHoldTicks = 15 };
            var start = brain.Decide(observation); Require(start.Jump && start.Move == 1, "reachable obstacle starts a jump");
            observation.Grounded = false; observation.Y = -4; observation.VelocityY = -4; observation.JumpSafe = false; observation.TargetX = -200;
            var movingOwner = brain.Decide(observation);
            Require(movingOwner.Jump && movingOwner.Move == 1, "owner turn and deferred planning preserve the launched jump prefix");
            for (int n = 0; n < 20; n++)
            {
                var airborne = brain.Decide(observation);
                Require(airborne.Move == 1, "exhausted jump hold does not retire an actually airborne movement prefix");
            }
            observation.Grounded = true; observation.VelocityY = 0; observation.Wall = false; observation.WalkSafe = true;
            Require(brain.Decide(observation).Move == -1, "actual landing retires the old jump and accepts the new target");
            observation.Dead = true; Require(!brain.Decide(observation).Jump, "death cancels old movement intent");
        }
        private static void RejectedTakeoff()
        {
            var observation = new CompanionMovementObservation { X = 0, Y = 0, TargetX = 200, Grounded = true, WalkSafe = false, Wall = true, JumpSafe = true, JumpHoldTicks = 3 };
            var brain = new CompanionBehaviorBrain();
            Require(!brain.Decide(observation).Jump, "route feasibility does not supply native takeoff admission");
            observation.CanTakeOff = true;
            Require(brain.Decide(observation).Jump, "native takeoff request is issued");
            Require(!brain.Decide(observation).Jump, "unchanged demand after rejected takeoff gets a real release frame");
            Require(brain.Decide(observation).Jump, "persistent obstacle retries after release and a legal observation");
            observation.Danger = true; observation.EscapeDirection = -1;
            var recovered = brain.Decide(observation);
            Require(!recovered.Jump && recovered.Move == -1 && recovered.Status == "避险中", "release frame still permits current danger movement");
        }
        private static void MenuMigration(string root)
        {
            const string local = "real-local-character-file", cloud = "real-cloud-character-file";
            string game = Path.Combine(root, "menu-migration"); Guid owner, good = Guid.NewGuid(), bad = Guid.NewGuid();
            using (var archive = new FileCompanionArchive(game))
            {
                owner = archive.LoadOwner(local).OwnerId;
                archive.Save(new CompanionProfile(good, owner, "跨菜单迁移", 1, 1, new CompanionBehavior(), new byte[] { 200 }));
                archive.Save(new CompanionProfile(bad, owner, "坏档", 1, 1, new CompanionBehavior(), new byte[] { 1 }));
            }
            string corrupt = Path.Combine(game, "JueMingRData", "companions", "characters", owner.ToString("N"), bad.ToString("N") + ".bin");
            File.WriteAllBytes(corrupt, new byte[] { 255, 255 });
            using (var manager = new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                // No SetOwner occurs before the native-menu migration completes.
                manager.MigrateOwner(local, cloud); WaitMigration(manager, cloud);
                manager.SetOwner(cloud);
                Require(manager.Ready && manager.OwnerId == owner && manager.Profiles.Count == 1 && manager.Profiles[0].Id == good && manager.Profiles[0].NativeData[0] == 200,
                    "fresh manager's completed menu migration publishes the existing character, not an empty owner alias");
                Require(manager.Error != null && manager.Error.Contains(bad.ToString("N")), "menu migration retains the specific corrupt-character feedback");
                manager.SetOwner(local);
                Require(manager.Ready && manager.OwnerId == owner && manager.Profiles.Single().Id == good && manager.Profiles[0].NativeData[0] == 200 && manager.Error != null,
                    "old and new aliases expose the same actual owner resources and load errors");
            }
            using (var archive = new PausedLoadArchive(new FileCompanionArchive(game)))
            using (var manager = new CompanionProfileManager(archive))
            {
                manager.SetOwner(local); Wait(manager, () => manager.Ready);
                archive.PauseNextLoad = true; manager.MigrateOwner(local, cloud);
                Require(archive.Loaded.Wait(5000), "migration obtained an actual older disk snapshot");
                var updated = manager.Find(good).Revise(new byte[] { 150 }); manager.Update(updated);
                archive.Continue.Set(); WaitMigration(manager, cloud); manager.SetOwner(cloud);
                Require(manager.Find(good).Revision == updated.Revision && manager.Find(good).NativeData[0] == 150,
                    "late migration completion cannot replace a newer in-memory resource revision");
                Wait(manager, () => manager.IsSaved(good));
                Require(manager.Error != null && manager.Error.Contains(bad.ToString("N")), "saving a healthy migrated profile cannot erase its owner's corrupt-profile feedback");
            }
            Require(File.ReadAllBytes(corrupt).SequenceEqual(new byte[] { 255, 255 }), "migration and healthy saves preserve the corrupt source bytes");
        }
        private static void RetryRead(string root)
        {
            const string key="retry-local-owner";
            string game=Path.Combine(root,"retry-read");
            string archiveRoot=Path.Combine(game,"JueMingRData","companions");
            Guid owner, healthy=Guid.NewGuid(), blocked=Guid.NewGuid();
            using(var archive=new FileCompanionArchive(game))
            {
                owner=archive.LoadOwner(key).OwnerId;
                archive.Save(new CompanionProfile(healthy,owner,"可用",1,1,new CompanionBehavior(),new byte[]{1}));
                archive.Save(new CompanionProfile(blocked,owner,"暂时占用",1,1,new CompanionBehavior(),new byte[]{2}));
            }
            string ownerFile=Directory.GetFiles(Path.Combine(archiveRoot,"owners"),"*.bin").Single();
            string memberFile=Path.Combine(archiveRoot,"characters",owner.ToString("N"),blocked.ToString("N")+".bin");
            byte[] memberBytes=File.ReadAllBytes(memberFile);
            using(var manager=new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                using(var locked=new FileStream(ownerFile+".lock",FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    manager.SetOwner(key);Wait(manager,()=>manager.LoadError!=null);
                    Require(!manager.Ready && !manager.Loading && manager.Profiles.Count==0,"unreadable owner is distinguishable from a loaded empty roster");
                }
                File.WriteAllBytes(memberFile,new byte[]{255});
                manager.RetryLoad();Wait(manager,()=>manager.Ready && !manager.Loading);
                Require(manager.OwnerId==owner && manager.Profiles.Single().Id==healthy && manager.LoadError.Contains(blocked.ToString("N")),
                    "same owner retry restores the healthy member and isolates corrupt contents");
                var newer=manager.Find(healthy).Revise(new byte[]{9});manager.Update(newer);Wait(manager,()=>manager.IsSaved(healthy));
                // A failed semantic read had acquired the document lease.
                // Repairing its source must become visible to explicit retry.
                File.WriteAllBytes(memberFile,memberBytes);
                using(var locked=new FileStream(memberFile+".lock",FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    manager.RetryLoad();Wait(manager,()=>!manager.Loading);
                    Require(manager.LoadError!=null && manager.Profiles.Single().Id==healthy,"still-busy member keeps exact error and healthy roster");
                }
                manager.RetryLoad();Wait(manager,()=>!manager.Loading && manager.LoadError==null);
                Require(manager.Profiles.Count==2 && manager.Find(blocked).NativeData[0]==2 &&
                    manager.Find(healthy).Revision==newer.Revision && manager.Find(healthy).NativeData[0]==9,
                    "same owner retry restores the complete roster while preserving the healthy newer save");
            }
        }
        private static void RetireOwner(string root)
        {
            var gated=new GatedArchive(new FileCompanionArchive(Path.Combine(root,"retire-owner")));
            using(var manager=new CompanionProfileManager(gated))
            {
                manager.SetOwner("first");Wait(manager,()=>manager.Ready);
                Guid owner=manager.OwnerId,id=Guid.NewGuid();
                manager.Add(new CompanionProfile(id,owner,"在途创建",1,1,new CompanionBehavior(),new byte[]{3}));
                Require(gated.Entered.Wait(5000),"worker obtained first creation before the player leaves");
                manager.SetOwner(null);
                Require(!manager.Ready && manager.Profiles.Count==0 && manager.Find(id)==null,"leaving removes even a previously accepted draft from visible ownership");
                manager.SetOwner("second");gated.Allow.Set();Wait(manager,()=>manager.Ready);
                Require(manager.OwnerId!=owner && manager.Profiles.Count==0 && manager.Find(id)==null && manager.IsSaved(id),
                    "first owner's committed creation never publishes into the second owner's roster");
                manager.SetOwner("first");
                Require(manager.Profiles.Single().Id==id,"returning owner gets the committed creation");
                Require(manager.TryReserve(id,out long generation),"returning owner reserves its committed profile");
                manager.SetOwner(null);
                var captured=manager.FindActivated(id,generation).Revise(new byte[]{4});
                manager.Update(captured,generation);Wait(manager,()=>manager.IsSaved(id));
                manager.Release(id,generation);
                Require(manager.Find(id)==null && manager.FindActivated(id,generation)==null,"retired activation can settle accepted bytes and releases narrow ownership");
                manager.SetOwner("first");
                Require(manager.Find(id).NativeData[0]==4,"next same-owner entry sees the latest departed body bytes");
            }
        }
        private static void WaitMigration(CompanionProfileManager manager, string key)
        {
            // Migration intentionally has no gameplay readiness until SetOwner.
            // Observe its existing completion marker only to control this menu
            // ordering; all identity/resource/error assertions use public state.
            var field = typeof(CompanionProfileManager).GetField("owners", BindingFlags.Instance | BindingFlags.NonPublic);
            Wait(manager, () => ((Dictionary<string, Guid>)field.GetValue(manager)).ContainsKey(key));
        }
        private static void Wait(CompanionProfileManager manager, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline) { manager.Pump(); if (ready()) return; Thread.Sleep(1); }
            throw new InvalidOperationException("Companion worker did not reach the expected observable state: " + manager.Error);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class FailOnceArchive : ICompanionArchive
        {
            private readonly ICompanionArchive inner; private bool failed;
            internal FailOnceArchive(ICompanionArchive inner) { this.inner = inner; }
            public CompanionOwnerArchive LoadOwner(string key) { return inner.LoadOwner(key); }
            public void LinkOwner(string key, Guid owner) { inner.LinkOwner(key, owner); }
            public void Save(CompanionProfile profile) { if (!failed) { failed = true; throw new IOException("isolated first save failure"); } inner.Save(profile); }
            public void Dispose() { inner.Dispose(); }
        }
        private sealed class GatedArchive : ICompanionArchive
        {
            private readonly ICompanionArchive inner;
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(), Allow = new ManualResetEventSlim();
            internal int Writes;
            internal GatedArchive(ICompanionArchive inner) { this.inner = inner; }
            public CompanionOwnerArchive LoadOwner(string key) { return inner.LoadOwner(key); }
            public void LinkOwner(string key, Guid owner) { inner.LinkOwner(key, owner); }
            public void Save(CompanionProfile profile)
            { if (Interlocked.Increment(ref Writes) == 1) { Entered.Set(); if (!Allow.Wait(5000)) throw new IOException("isolated write gate timed out"); } inner.Save(profile); }
            public void Dispose() { Allow.Set(); inner.Dispose(); Entered.Dispose(); Allow.Dispose(); }
        }
        private sealed class PausedLoadArchive : ICompanionArchive
        {
            private readonly ICompanionArchive inner;
            internal readonly ManualResetEventSlim Loaded = new ManualResetEventSlim(), Continue = new ManualResetEventSlim();
            internal bool PauseNextLoad;
            private bool disposed;
            internal PausedLoadArchive(ICompanionArchive inner) { this.inner = inner; }
            public CompanionOwnerArchive LoadOwner(string key)
            {
                var snapshot = inner.LoadOwner(key);
                if (PauseNextLoad) { PauseNextLoad = false; Loaded.Set(); if (!Continue.Wait(5000)) throw new IOException("isolated migration load gate timed out"); }
                return snapshot;
            }
            public void LinkOwner(string key, Guid owner) { inner.LinkOwner(key, owner); }
            public void Save(CompanionProfile profile) { inner.Save(profile); }
            public void Dispose() { if (disposed) return; disposed = true; Continue.Set(); inner.Dispose(); Loaded.Dispose(); Continue.Dispose(); }
        }
    }
}
