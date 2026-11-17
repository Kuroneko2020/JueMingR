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
            try { ArchiveRoundTrip(root); PreferenceRoundTrip(root); FailedCreation(root); CoalescedReset(root); CorruptIsolation(root); MenuMigration(root); TerminalArchive(root); LegacyArchive(root); ContinuousJump(); RejectedTakeoff(); }
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
            foreach(int version in new[]{1,2})
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
                writer.Write("旧人物");writer.Write(1L);writer.Write(1L);writer.Write(true);writer.Write(true);writer.Write(false);writer.Write(true);
                if(version==2)writer.Write(true);
                writer.Write(2);writer.Write(new byte[]{44,55});writer.Flush();
                using(var sha=System.Security.Cryptography.SHA256.Create())writer.Write(sha.ComputeHash(stream.ToArray()));writer.Flush();File.WriteAllBytes(path,stream.ToArray());
            }
            using(var archive=new FileCompanionArchive(game))
            {
                var old=archive.LoadOwner("legacy").Profiles.Single();Require(old.PermanentlyDead==(version==2) && old.NativeData.SequenceEqual(new byte[]{44,55}) && old.Behavior.AutoRespawn && old.Behavior.ManualWeaponSlot==-1,"legacy archive preserves native bytes/terminal identity and defaults respawn on with no manual selection");
                archive.Save(old.Revise(old.NativeData));
            }
            using(var archive=new FileCompanionArchive(game))Require(archive.LoadOwner("legacy").Profiles.Single().PermanentlyDead==(version==2),"normal next revision upgrades legacy without rewriting terminal semantics");
            }
        }
        private static void PreferenceRoundTrip(string root)
        {
            string game=Path.Combine(root,"preferences");Guid owner,id=Guid.NewGuid();
            var behavior=new CompanionBehavior{AutoRespawn=false,ManualWeaponSlot=12,ManualWeaponType=39,ManualWeaponPrefix=60};
            var clone=behavior.Clone();clone.Follow=false;
            Require(!clone.AutoRespawn && clone.ManualWeaponSlot==12 && clone.ManualWeaponType==39 && clone.ManualWeaponPrefix==60,"editing one cloned preference preserves new settings");
            using(var archive=new FileCompanionArchive(game))
            {owner=archive.LoadOwner("creator").OwnerId;archive.Save(new CompanionProfile(id,owner,"偏好保存",1,1,clone,new byte[]{7,9},true));}
            using(var archive=new FileCompanionArchive(game))
            {
                var profile=archive.LoadOwner("creator").Profiles.Single();var saved=profile.Behavior;
                Require(profile.OwnerId==owner && profile.Id==id && profile.PermanentlyDead && profile.NativeData.SequenceEqual(new byte[]{7,9}) && !saved.Follow && !saved.AutoRespawn && saved.ManualWeaponSlot==12 && saved.ManualWeaponType==39 && saved.ManualWeaponPrefix==60,"one v3 record reopens settings with native/owner/terminal data intact");
                saved.AutoRespawn=true;Require(!profile.Behavior.AutoRespawn,"caller's preference edits cannot mutate the stored profile");
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
            var observation = new CompanionMovementObservation { X = 0, Y = 0, TargetX = 200, TargetY = 0, Grounded = true, WalkSafe = false, Wall = true, JumpSafe = true, JumpHoldTicks = 15 };
            var start = brain.Decide(observation); Require(start.Jump && start.Move == 1, "reachable obstacle starts a jump");
            observation.Grounded = false; observation.VelocityY = -4; observation.JumpSafe = false; observation.TargetX = -200;
            var movingOwner = brain.Decide(observation);
            Require(movingOwner.Jump && movingOwner.Move == 1, "owner turn and deferred planning preserve the launched jump prefix");
            observation.CanHeal = true;
            Require(brain.Decide(observation).Heal, "native healing request can coexist with movement");
            for (int n = 0; n < 20; n++)
            {
                var airborne = brain.Decide(observation);
                Require(airborne.Move == 1 && airborne.Heal, "exhausted jump hold does not retire an actually airborne movement prefix or healing");
            }
            observation.Grounded = true; observation.VelocityY = 0; observation.Wall = false; observation.WalkSafe = true;
            Require(brain.Decide(observation).Move == -1, "actual landing retires the old jump and accepts the new target");
            observation.Dead = true; Require(!brain.Decide(observation).Jump, "death cancels old movement intent");
        }
        private static void RejectedTakeoff()
        {
            var observation = new CompanionMovementObservation { X = 0, Y = 0, TargetX = 200, Grounded = true, WalkSafe = false, Wall = true, JumpSafe = true, JumpHoldTicks = 3 };
            var brain = new CompanionBehaviorBrain();
            Require(brain.Decide(observation).Jump, "native takeoff request is issued");
            observation.JumpSafe = false; observation.Wall = false; observation.WalkSafe = true; observation.TargetX = -200;
            for (int n = 1; n < observation.JumpHoldTicks; n++)
                Require(brain.Decide(observation).Jump, "unstarted request consumes its existing finite hold");
            var recovered = brain.Decide(observation);
            Require(!recovered.Jump && recovered.Move == -1, "continuous grounded state cannot retain an exhausted jump direction forever");
            observation.TargetX = 200; observation.Wall = true; observation.WalkSafe = false; observation.JumpSafe = true;
            brain = new CompanionBehaviorBrain(); brain.Decide(observation);
            for (int n = 1; n < observation.JumpHoldTicks; n++) brain.Decide(observation);
            observation.JumpSafe = false; observation.Danger = true; observation.EscapeDirection = -1; observation.CanHeal = true;
            recovered = brain.Decide(observation);
            Require(recovered.Move == -1 && recovered.Heal && recovered.Status == "避险中", "rejected takeoff returns to current danger and healing decisions after its own hold");
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
