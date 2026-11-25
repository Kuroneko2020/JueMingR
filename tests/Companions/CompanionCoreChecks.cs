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
            try { ArchiveRoundTrip(root); PreferenceRoundTrip(root); FailedCreation(root); CoalescedReset(root); CorruptIsolation(root); RetryRead(root); RetireOwner(root); MenuMigration(root); TerminalArchive(root); LegacyArchive(root); DeletionOrdering(root); DeletionRecovery(root); DeletionAfterArchiveConflict(root); }
            catch (Exception exception) { failures.Add("companion core: " + exception); }
            finally
            {
                if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(root).IndexOf("JueMingR-companion-checks-", StringComparison.Ordinal) != 0)
                    throw new InvalidOperationException("Unsafe isolated test cleanup path.");
                Directory.Delete(root, true);
            }
        }
        private static void DeletionOrdering(string root)
        {
            string game = Path.Combine(root, "deletion-order"), ownerKey = "deletion-order-owner";
            Guid id = Guid.NewGuid();
            using (var files = new FileCompanionArchive(game))
            {
                Guid owner = files.LoadOwner(ownerKey).OwnerId;
                files.Save(new CompanionProfile(id, owner, "排序目标", 1, 1, new CompanionBehavior(), new byte[] { 1 }));
            }
            var gate = new GatedArchive(new FileCompanionArchive(game));
            using (var manager = new CompanionProfileManager(gate))
            {
                manager.SetOwner(ownerKey);
                Wait(manager, () => manager.Profiles.Count == 1);
                var original = manager.Find(id);
                manager.Update(original.Revise(new byte[] { 2 }));
                try
                {
                    Require(gate.Entered.Wait(5000) && manager.RequestDeletion(original.OwnerId, id), "confirm while an original write is inside the real archive worker");
                    manager.RetrySave(id);
                    Require(manager.Profiles.Count == 0 && manager.Find(id) == null, "accepted deletion retires immediately before in-flight settlement");
                }
                finally { gate.Allow.Set(); }
                Wait(manager, () => manager.Deletions.Single().FilesRemoved);
                Require(manager.Profiles.Count == 0 && manager.Find(id) == null && gate.Writes == 1,
                    "earlier original write finishes without publishing the retired character or scheduling a second write");
                manager.FinishDeletion(original.OwnerId, id);
                Wait(manager, () => manager.IsDeletionComplete(id));
            }
            using (var files = new FileCompanionArchive(game))
                Require(files.LoadOwner(ownerKey).Profiles.Count == 0, "a new archive load has neither the old character nor a new recovery candidate");
        }

        private static void DeletionRecovery(string root)
        {
            string game = Path.Combine(root, "deletion"), key = "delete-owner";
            Guid id = Guid.NewGuid(), neighbor = Guid.NewGuid(), owner;
            string path;
            byte[] primary, backup;
            using (var archive = new FileCompanionArchive(game))
            {
                owner = archive.LoadOwner(key).OwnerId;
                var first = new CompanionProfile(id, owner, "删除对象", 1, 1, new CompanionBehavior(), new byte[] { 1 });
                archive.Save(first);
                archive.Save(first.Revise(new byte[] { 2 }));
                archive.Save(new CompanionProfile(neighbor, owner, "保留对象", 1, 1, new CompanionBehavior(), new byte[] { 31 }));
                path = Path.Combine(game, "JueMingRData", "companions", "characters", owner.ToString("N"), id.ToString("N") + ".bin");
                primary = File.ReadAllBytes(path);
                backup = File.ReadAllBytes(path + ".bak");
                File.Copy(path, path + ".tmp");
            }
            using (var manager = new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                manager.SetOwner(key);
                Wait(manager, () => manager.Profiles.Count == 2);
                Require(manager.TryReserve(id, out long generation), "deletion has an active original generation");
                var latest = manager.Find(id).Revise(new byte[] { 3 });
                using (var backupReader = new FileStream(path + ".bak", FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Require(manager.RequestDeletion(owner, id) && manager.Find(id) == null && !manager.OwnsActivation(id, generation) &&
                        !manager.TryReserve(id, out _), "acceptance fences the exact actor and old UI immediately");
                    bool stale = false;
                    try { manager.Update(latest, generation); } catch (InvalidOperationException) { stale = true; }
                    Require(stale, "stale activation cannot enqueue a save after acceptance");
                    Wait(manager, () => manager.Deletions.Single().Error != null);
                    Require(!File.Exists(path) && File.Exists(path + ".bak") && File.Exists(path + ".tmp") && File.Exists(path + ".delete"),
                        "a real sharing failure leaves protected recovery material and a durable receipt after primary removal");
                    manager.SetOwner("another-delete-owner");
                    Wait(manager, () => manager.Ready);
                    Require(!manager.RetryDeletion(owner, id) && manager.Profiles.Count == 0, "only the original owner may explicitly retry");
                }
            }
            // Reconstruct the exact pre-Move cut with the real candidate bytes;
            // an uncommitted marker must neither hide nor remove the live archive.
            byte[] intent = File.ReadAllBytes(path + ".delete");
            File.Move(path + ".delete", path + ".delete.tmp");
            File.WriteAllBytes(path, primary);
            // Remove the fixture's other staged file so the normal archive
            // writer can create its own candidate during this gameplay save.
            File.Delete(path + ".tmp");
            using (var manager = new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                manager.SetOwner(key);
                Wait(manager, () => manager.Ready);
                Require(manager.LoadError == null && manager.Profiles.Count == 2 && manager.Deletions.Count == 0,
                    "a fresh load at the actual pre-commit cut leaves both legitimate characters published");
                var changed = manager.Find(id).Revise(new byte[] { 42 }, new CompanionBehavior { Follow = false });
                manager.Update(changed);
                Wait(manager, () => manager.IsSaved(id));
                byte[] latest = File.ReadAllBytes(path);
                backup = File.ReadAllBytes(path + ".bak");
                Require(!latest.SequenceEqual(primary) && backup.SequenceEqual(primary) &&
                    File.ReadAllBytes(path + ".delete.tmp").SequenceEqual(intent),
                    "an ordinary post-restart save changes the body and backup without consuming an uncommitted deletion");
                File.Copy(path, path + ".tmp");

                File.WriteAllBytes(path + ".delete.tmp", new byte[] { 88 });
                Require(manager.RequestDeletion(owner, id), "new explicit confirmation encounters a staged journal");
                Wait(manager, () => manager.Deletions.Single().Error != null);
                Require(File.ReadAllBytes(path + ".delete.tmp").SequenceEqual(new byte[] { 88 }) && File.ReadAllBytes(path).SequenceEqual(latest),
                    "an unconfirmed changed journal cannot delete or hide the live disk profile");
                foreach (byte[] foreign in new[] { ReplaceDeletionOwner(intent, Guid.NewGuid(), id), ReplaceDeletionOwner(intent, owner, neighbor) })
                {
                    File.WriteAllBytes(path + ".delete.tmp", foreign);
                    Require(manager.RetryDeletion(owner, id), "retry verifies any staged record against its actual owner and profile");
                    Wait(manager, () => manager.Deletions.Single().Error != null);
                    Require(File.ReadAllBytes(path + ".delete.tmp").SequenceEqual(foreign) && File.ReadAllBytes(path).SequenceEqual(latest) &&
                        !File.Exists(path + ".delete"), "a valid record for a different owner or profile preserves itself and the newer live archive");
                }
                File.WriteAllBytes(path + ".delete.tmp", intent);
                using (var backupReader = new FileStream(path + ".bak", FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Require(manager.RetryDeletion(owner, id), "a confirmed older staged journal for the exact character can safely recommit");
                    Wait(manager, () => manager.Deletions.Single().Error != null);
                    Require(!File.Exists(path) && !File.Exists(path + ".delete.tmp") && File.Exists(path + ".delete"),
                        "recommit retires the older verified journal and leaves a usable committed receipt at the later cut");
                }
            }
            File.WriteAllBytes(path + ".bak", new byte[] { 57 });
            using (var manager = new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                manager.SetOwner(key);
                Wait(manager, () => manager.Ready && manager.Deletions.Single().Error != null);
                Require(manager.Profiles.Single().Id == neighbor && !manager.Deletions.Single().FilesRemoved &&
                    File.ReadAllBytes(path + ".bak").SequenceEqual(new byte[] { 57 }) && File.Exists(path + ".tmp"),
                    "fresh owner load hides the interrupted deletion and refuses external replacement before deleting another file");
                File.WriteAllBytes(path + ".bak", backup);
                Require(manager.RetryDeletion(owner, id), "the original owner can retry the exact failed deletion");
                Wait(manager, () => manager.Deletions.Single().FilesRemoved);
                Require(!File.Exists(path + ".bak") && !File.Exists(path + ".tmp") && File.Exists(path + ".delete") && File.Exists(path + ".lock"),
                    "retry cleans both recovery variants and retains intent until shortcuts acknowledge");
                manager.FailDeletionBindings(owner, id);
                Require(manager.Deletions.Single().Error.Contains("快捷键") && manager.NameInUse("删除对象"),
                    "shortcut failure has its own stage and reserves the original name while pending");
            }
            using (var manager = new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                manager.SetOwner(key);
                Wait(manager, () => manager.Ready && manager.Deletions.Single().FilesRemoved);
                manager.FinishDeletion(owner, id);
                Wait(manager, () => manager.Deletions.Count == 0);
                Require(!File.Exists(path + ".delete") && manager.Profiles.Single().Id == neighbor && !manager.NameInUse("删除对象"),
                    "confirmed persistent cleanup preserves the neighbor and releases the exact name");
                var replacement = new CompanionProfile(Guid.NewGuid(), owner, "删除对象", 1, 1, new CompanionBehavior(), new byte[] { 55 });
                manager.Add(replacement);
                Wait(manager, () => manager.Profiles.Count == 2);
                Require(manager.Find(id) == null && manager.Find(replacement.Id) != null, "same-name reconstruction owns a new identity");
            }
        }

        private static byte[] ReplaceDeletionOwner(byte[] intent, Guid owner, Guid profile)
        {
            // Keep a trustworthy checksum so the fixture exercises identity,
            // not the earlier rejection of corrupt or truncated bytes.
            byte[] bytes = (byte[])intent.Clone();
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                reader.ReadString();
                int ownerOffset = checked((int)reader.BaseStream.Position);
                Buffer.BlockCopy(owner.ToByteArray(), 0, bytes, ownerOffset, 16);
                Buffer.BlockCopy(profile.ToByteArray(), 0, bytes, ownerOffset + 16, 16);
            }
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                int checksumOffset = bytes.Length - 32;
                Buffer.BlockCopy(sha.ComputeHash(bytes, 0, checksumOffset), 0, bytes, checksumOffset, 32);
            }
            return bytes;
        }

        private static void DeletionAfterArchiveConflict(string root)
        {
            const string key = "replace-owner", name = "同身份的外部替换";
            string game = Path.Combine(root, "delete-after-replace"), alternateGame = Path.Combine(root, "delete-replacement-source");
            Guid id = Guid.NewGuid(), owner;
            using (var archive = new FileCompanionArchive(game))
            {
                owner = archive.LoadOwner(key).OwnerId;
                archive.Save(new CompanionProfile(id, owner, name, 1, 1, new CompanionBehavior(), new byte[] { 51 }));
            }
            using (var alternate = new FileCompanionArchive(alternateGame))
                alternate.Save(new CompanionProfile(id, owner, name, 1, 1, new CompanionBehavior(), new byte[] { 99 }));
            string leaf = Path.Combine("JueMingRData", "companions", "characters", owner.ToString("N"), id.ToString("N") + ".bin");
            string path = Path.Combine(game, leaf), external = path + ".external-test";
            byte[] original = File.ReadAllBytes(path), replacement = File.ReadAllBytes(Path.Combine(alternateGame, leaf));
            using (var manager = new CompanionProfileManager(new FileCompanionArchive(game)))
            {
                manager.SetOwner(key);
                Wait(manager, () => manager.Profiles.Count == 1);
                var current = manager.Find(id);
                File.WriteAllBytes(external, replacement);
                File.Replace(external, path, null);
                manager.Update(current.Revise(new byte[] { 52 }));
                Wait(manager, () => manager.SaveFailed(id));
                Require(manager.SaveError(id).Contains("external-document-change") && File.ReadAllBytes(path).SequenceEqual(replacement),
                    "the real atomic save detected the outside replacement and released its cached document");

                Require(manager.RequestDeletion(owner, id), "the exact owned character can request deletion after its failed save");
                Wait(manager, () => manager.Deletions.Single().Error != null);
                Require(!manager.Deletions.Single().FilesRemoved && !File.Exists(path + ".delete") && File.ReadAllBytes(path).SequenceEqual(replacement),
                    "deletion preserves the previously rejected replacement even with a legitimate owner, profile, and revision");

                File.WriteAllBytes(external, original);
                File.Replace(external, path, null);
                Require(manager.RetryDeletion(owner, id), "the same operation may retry once the trusted source is restored");
                Wait(manager, () => manager.Deletions.Single().FilesRemoved);
                manager.FinishDeletion(owner, id);
                Wait(manager, () => manager.IsDeletionComplete(id));
                Require(!File.Exists(path), "the original trusted archive becomes deletable after repair");
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
            foreach(var fixture in new[]{new[]{1,1},new[]{2,1},new[]{3,1},new[]{4,0},new[]{4,1},new[]{5,0},new[]{5,1}})
            {
            int version=fixture[0];bool enabled=fixture[1]!=0, terminal=version==2||version>=4;
            string game=Path.Combine(root,"v"+version+"-"+enabled);Guid owner,id=Guid.NewGuid();
            using(var archive=new FileCompanionArchive(game)){owner=archive.LoadOwner("legacy").OwnerId;}
            string folder=Path.Combine(game,"JueMingRData","companions","characters",owner.ToString("N"));Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,id.ToString("N")+".bin");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new System.Text.UTF8Encoding(false,true)))
            {
                // Independently encode the historical formats rather than use
                // the current encoder to pretend it emitted a legacy file.
                writer.Write("JueMingR.Companion");writer.Write(version);writer.Write(id.ToByteArray());writer.Write(owner.ToByteArray());
                writer.Write("旧人物");writer.Write(7L);writer.Write(3L);writer.Write(false);writer.Write(true);
                if(version<4)
                {
                    writer.Write(true);writer.Write(false);
                    if(version>=2)writer.Write(terminal);
                    if(version==3){writer.Write(false);writer.Write(48);writer.Write(39);writer.Write((byte)60);}
                }
                else
                {
                    writer.Write(false);writer.Write(terminal);writer.Write((byte)1);
                    writer.Write(39);writer.Write((byte)60);writer.Write((byte)48);
                    writer.Write(version==5 && enabled);writer.Write((byte)1);writer.Write(true);writer.Write(version==5);writer.Write(enabled);
                    if(version==5)writer.Write(!enabled);
                    foreach (int type in new[]{28,110,29,292}) {writer.Write((ushort)1);writer.Write(type);}
                }
                writer.Write(2);writer.Write(new byte[]{44,55});writer.Flush();
                using(var sha=System.Security.Cryptography.SHA256.Create())writer.Write(sha.ComputeHash(stream.ToArray()));writer.Flush();File.WriteAllBytes(path,stream.ToArray());
            }
            using(var archive=new FileCompanionArchive(game))
            {
                byte[] original = File.ReadAllBytes(path);
                var old=archive.LoadOwner("legacy").Profiles.Single();
                var behavior = old.Behavior;
                Require(old.Id==id && old.OwnerId==owner && old.Revision==7 && old.DataEpoch==3 && old.PermanentlyDead==terminal &&
                    old.NativeData.SequenceEqual(new byte[]{44,55}) && !behavior.Follow && behavior.AttackEnabled && behavior.AutoRespawn==(version<3),
                    "legacy archive preserves native bytes, ownership, terminal identity, and retained behavior fields");
                Require(behavior.BuffEnabled==enabled && behavior.GrowthEnabled==(version==5?!enabled:enabled) && behavior.ManaRecoveryEnabled && !behavior.FurnitureEnabled,
                    "v1–v3 defaults, v4 common permission and v5 separate permissions retain their actual meanings");
                Require(behavior.HealthRecoveryEnabled==(version<4 || version==5&&enabled) && behavior.AllowNurse==(version<4 || version==5&&enabled) &&
                    behavior.HealthRecoveryMode==(version<4?CompanionHealthRecoveryMode.Smart:CompanionHealthRecoveryMode.Fast) &&
                    behavior.ExcludedHealingTypes.SequenceEqual(version<4?Array.Empty<int>():new[]{28}) &&
                    behavior.ExcludedManaTypes.SequenceEqual(version<4?Array.Empty<int>():new[]{110}) &&
                    behavior.AllowedGrowthTypes.SequenceEqual(version<4?Array.Empty<int>():new[]{29}) &&
                    behavior.AllowedBuffTypes.SequenceEqual(version<4?Array.Empty<int>():new[]{292}),
                    "older recovery defaults and v4's four source lists retain their real fields");
                Require(behavior.MainWeapons.Length==(version>=3?1:0),"v3 and v4 preserve a manual selection");
                if(version>=3) Require(behavior.MainWeapons[0].SlotHint==48&&behavior.MainWeapons[0].Type==39&&behavior.MainWeapons[0].Prefix==60,
                    "the historical actual-slot preference is retained");
                archive.Save(old);
                Require(original.SequenceEqual(File.ReadAllBytes(path)),"loading and reaccepting an identical old revision leaves the original format untouched");
                behavior.GrowthEnabled=!enabled;behavior.HealthRecoveryEnabled=false;behavior.AllowNurse=true;behavior.FurnitureEnabled=true;
                archive.Save(old.Revise(old.NativeData,behavior));
            }
            using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream))
            { Require(reader.ReadString()=="JueMingR.Companion"&&reader.ReadInt32()==6,"the next complete revision writes one v6 record"); }
            using(var archive=new FileCompanionArchive(game))
            {
                var saved=archive.LoadOwner("legacy").Profiles.Single();
                Require(saved.PermanentlyDead==terminal && saved.Behavior.BuffEnabled==enabled && saved.Behavior.GrowthEnabled!=enabled && saved.Behavior.FurnitureEnabled && saved.Behavior.AllowNurse && !saved.Behavior.HealthRecoveryEnabled &&
                    saved.NativeData.SequenceEqual(new byte[]{44,55}) && saved.Revision==8 && saved.DataEpoch==3,
                    "new independent nurse and furniture controls save v6 with the same terminal identity and native bytes");
            }
            }
        }
        private static void PreferenceRoundTrip(string root)
        {
            string game=Path.Combine(root,"preferences");Guid owner,id=Guid.NewGuid();
            var behavior=new CompanionBehavior
            {
                AutoRespawn=false,HealthRecoveryEnabled=false,HealthRecoveryMode=CompanionHealthRecoveryMode.Fast,
                ManaRecoveryEnabled=false,AllowNurse=false,BuffEnabled=false,GrowthEnabled=true,FurnitureEnabled=true,
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
                    !saved.AllowNurse && !saved.BuffEnabled && saved.GrowthEnabled && saved.FurnitureEnabled && saved.ExcludedHealingTypes.SequenceEqual(new[]{28}) && saved.ExcludedManaTypes.SequenceEqual(new[]{110}) &&
                    saved.AllowedGrowthTypes.SequenceEqual(new[]{29,109}) && saved.AllowedBuffTypes.SequenceEqual(new[]{292}),
                    "one v6 record reopens the full settings with native, owner and terminal data intact");
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
            public void DeleteFiles(CompanionDeletion deletion, long revision) { inner.DeleteFiles(deletion, revision); }
            public void FinishDeletion(CompanionDeletion deletion) { inner.FinishDeletion(deletion); }
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
            public void DeleteFiles(CompanionDeletion deletion, long revision) { inner.DeleteFiles(deletion, revision); }
            public void FinishDeletion(CompanionDeletion deletion) { inner.FinishDeletion(deletion); }
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
            public void DeleteFiles(CompanionDeletion deletion, long revision) { inner.DeleteFiles(deletion, revision); }
            public void FinishDeletion(CompanionDeletion deletion) { inner.FinishDeletion(deletion); }
            public void Save(CompanionProfile profile) { inner.Save(profile); }
            public void Dispose() { if (disposed) return; disposed = true; Continue.Set(); inner.Dispose(); Loaded.Dispose(); Continue.Dispose(); }
        }
    }
}
