using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Infrastructure.Companions;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The existing production capture already established 200 -> 150. This
    // stage reads its actual committed archive in another OS process. No native
    // data bytes or replacement inventory travel through the request manifest.
    internal static class NativeCompanionPersistenceChecks
    {
        private const string RequestName = "companion-persistence-request.bin";
        private const string ReceiptName = "companion-persistence-receipt.bin";
        private sealed class Request
        {
            internal string Nonce, GameRelative, OwnerKey, ArchiveHash, NativeHash, HostHash, ProbeHash;
            internal Guid Profile, Owner;
            internal long Revision;
            internal int Writer;
        }

        // Must be the last use of this host's profiles in the writer fixture:
        // releasing the real archive lease is part of normal persistence proof.
        internal static void RunWriter(object host, Guid profileId, string content, string output)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output);
            Require(((System.Collections.IDictionary)Get(host, "actors")).Count == 0, "new-process persistence starts after all fixture companion bodies are recalled");
            object profiles = Get(host, "profiles"), manager = Get(profiles, "manager"), archive = Get(manager, "archive");
            NativeQuickItemChecks.Until(() =>
            {
                Call(profiles, "Pump");
                if ((bool)Call(profiles, "SaveFailed", profileId)) throw new IOException("Writer's actual companion archive save failed: " + Call(profiles, "SaveError", profileId));
                return (bool)Call(profiles, "IsSaved", profileId);
            });
            var profile = ((System.Collections.Generic.IEnumerable<CompanionProfile>)Get(profiles, "Profiles")).Single(p => p.Id == profileId);
            var body = (Player)Call(profiles, "LoadBody", profile);
            Require(body.inventory[12].type == ItemID.Wood && body.inventory[12].stack == 150, "writer uses the actual prior production capture with 150 wood");
            string archiveRoot = Path.GetFullPath((string)Get(archive, "root"));
            string game = Directory.GetParent(Directory.GetParent(archiveRoot).FullName).FullName;
            Require(String.Equals(archiveRoot, Path.Combine(game, "JueMingRData", "companions"), StringComparison.OrdinalIgnoreCase) && Within(game, output),
                "writer archive is the actual invocation-owned game data root");
            var request = new Request
            {
                Nonce = Guid.NewGuid().ToString("N"), Writer = Process.GetCurrentProcess().Id,
                GameRelative = game.Substring(output.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1),
                OwnerKey = (string)Get(manager, "ownerKey"), Profile = profile.Id, Owner = profile.OwnerId, Revision = profile.Revision,
                ArchiveHash = HashFile(ProfilePath(game, profile.OwnerId, profile.Id)), NativeHash = Hash(profile.NativeData),
                HostHash = HashFile(host.GetType().Assembly.Location), ProbeHash = HashFile(Assembly.GetExecutingAssembly().Location)
            };
            Require(!String.IsNullOrWhiteSpace(request.OwnerKey), "writer retains the real creator file identity");
            WriteRequest(Path.Combine(output, RequestName), request);
            ((IDisposable)profiles).Dispose();
            var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location)
            {
                Arguments = Quote(Program.Repository) + " " + Quote(Path.GetFullPath(content)) + " " + Quote(output) + " CompanionRead",
                WorkingDirectory = output, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            NativeScopeDispatch.PrepareOwnedChild(start);
            using (var child = Process.Start(start))
            {
                Require(child != null && child.Id != request.Writer, "persistence reader is a distinct operating-system process");
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                if (!child.WaitForExit(45000)) { child.Kill(); child.WaitForExit(); throw new TimeoutException("Owned companion archive reader timed out."); }
                string log = stdout.Result + stderr.Result;
                File.WriteAllText(Path.Combine(output, "companion-reader.log"), log, new UTF8Encoding(false));
                if (child.ExitCode != 0)
                {
                    if (child.ExitCode == 3) Require(false, "fresh-process production archive/native reader assertion: " + log);
                    throw new InvalidDataException("Fresh-process companion reader fault, exit=" + child.ExitCode + ": " + log);
                }
                using (var reader = new BinaryReader(File.OpenRead(Path.Combine(output, ReceiptName)), new UTF8Encoding(false, true)))
                {
                    Require(reader.ReadString() == request.Nonce && reader.ReadInt32() == child.Id && reader.ReadInt32() == request.Writer &&
                        reader.ReadString() == request.Profile.ToString("N") && reader.ReadInt32() == 150 && reader.ReadString() == request.NativeHash,
                        "reader receipt binds this invocation, actual child PID, stable profile and recovered native resource bytes");
                    Require(reader.BaseStream.Position == reader.BaseStream.Length, "persistence receipt has no unverified trailing data");
                }
                Console.WriteLine("PASS companion new-process persistence writerPid=" + request.Writer + " readerPid=" + child.Id + " profile=" + request.Profile.ToString("N") +
                    " revision=" + request.Revision + " wood=150 archiveSha256=" + request.ArchiveHash + "; real archive + native LoadBody; no game/world-switch/Steam/FPS acceptance.");
            }
        }

        // Program routes CompanionRead here after installing its normal resolver,
        // before the generic NativeChecks dispatch. SavePath precedes Main JIT.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ReadChild(string output)
        {
            output = Path.GetFullPath(output);
            Terraria.Program.SavePath = Path.Combine(output, "companion-reader-user"); Directory.CreateDirectory(Terraria.Program.SavePath);
            NativeTestAudio.Install(typeof(Main).Assembly);
            return ReadOwned(output);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadOwned(string output)
        {
            var request = ReadRequest(Path.Combine(output, RequestName));
            Require(Process.GetCurrentProcess().Id != request.Writer, "reader cannot reuse the writer process or its in-memory character");
            Require(HashFile(Assembly.GetExecutingAssembly().Location) == request.ProbeHash, "reader runs the exact writer probe executable");
            string game = Path.GetFullPath(Path.Combine(output, request.GameRelative));
            Require(!Path.IsPathRooted(request.GameRelative) && Within(game, output), "reader accepts only the explicit invocation-owned archive root");
            string path = ProfilePath(game, request.Owner, request.Profile);
            Require(HashFile(path) == request.ArchiveHash, "fresh reader opens the exact committed writer archive file");
            NativeQuickItemChecks.Run(context =>
            {
                Terraria.GameContent.Creative.CreativePowerManager.Initialize();
                var host = Get(context, "Companions");
                Require(HashFile(host.GetType().Assembly.Location) == request.HostHash, "reader uses the same production host build");
                // Normal fixture composition installs the real serializer hooks.
                // A separate adapter below targets the WRITER's archive; the
                // composition's own reader-user root cannot provide these bytes.
                using (var archive = new FileCompanionArchive(game))
                {
                    Type type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionProfiles", true);
                    object profiles = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { archive }, null);
                    try
                    {
                        Call(profiles, "SetOwner", request.OwnerKey);
                        NativeQuickItemChecks.Until(() =>
                        {
                            Call(profiles, "Pump"); bool ready = (bool)Get(profiles, "Ready");
                            if (!ready && GetOptional(profiles, "Error") != null) throw new InvalidDataException("Reader's actual companion archive load failed: " + Get(profiles, "Error"));
                            return ready;
                        });
                        var profile = ((System.Collections.Generic.IEnumerable<CompanionProfile>)Get(profiles, "Profiles")).FirstOrDefault(p => p.Id == request.Profile);
                        if (profile == null) throw new InvalidDataException("Reader cannot load expected saved profile " + request.Profile.ToString("N") + ": " + (GetOptional(profiles, "Error") ?? "profile absent"));
                        Require(profile.OwnerId == request.Owner && profile.Revision == request.Revision && Hash(profile.NativeData) == request.NativeHash,
                            "new process loads the same owner/profile/revision/full native bytes from disk");
                        var body = (Player)Call(profiles, "LoadBody", profile);
                        Require(body.inventory[12].type == ItemID.Wood && body.inventory[12].stack == 150, "new process native Deserialize recovers 150 wood without starter regeneration");
                        if (GetOptional(profiles, "Error") != null) Console.WriteLine("COMPANION-READER other-profile-error=" + Get(profiles, "Error"));
                        using (var writer = new BinaryWriter(File.Create(Path.Combine(output, ReceiptName)), new UTF8Encoding(false, true)))
                        {
                            writer.Write(request.Nonce); writer.Write(Process.GetCurrentProcess().Id); writer.Write(request.Writer);
                            writer.Write(profile.Id.ToString("N")); writer.Write(body.inventory[12].stack); writer.Write(Hash(profile.NativeData));
                        }
                        Console.WriteLine("COMPANION-READER pid=" + Process.GetCurrentProcess().Id + " writerPid=" + request.Writer + " profile=" + profile.Id.ToString("N") +
                            " revision=" + profile.Revision + " wood=" + body.inventory[12].stack + " nativeSha256=" + request.NativeHash);
                    }
                    finally { ((IDisposable)profiles).Dispose(); }
                }
            }, processing: true, shortFeedback: true);
            return 0;
        }
        private static string ProfilePath(string game, Guid owner, Guid profile)
        { return Path.Combine(game, "JueMingRData", "companions", "characters", owner.ToString("N"), profile.ToString("N") + ".bin"); }
        private static bool Within(string path, string root)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string HashFile(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        private static void WriteRequest(string path, Request request)
        {
            using (var writer = new BinaryWriter(File.Create(path), new UTF8Encoding(false, true)))
            {
                writer.Write("JueMingR.CompanionNewProcess.v1"); writer.Write(request.Nonce); writer.Write(request.Writer); writer.Write(request.GameRelative);
                writer.Write(request.OwnerKey); writer.Write(request.Profile.ToString("N")); writer.Write(request.Owner.ToString("N")); writer.Write(request.Revision);
                writer.Write(request.ArchiveHash); writer.Write(request.NativeHash); writer.Write(request.HostHash); writer.Write(request.ProbeHash);
            }
        }
        private static Request ReadRequest(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path), new UTF8Encoding(false, true)))
            {
                Require(reader.BaseStream.Length <= 16384 && reader.ReadString() == "JueMingR.CompanionNewProcess.v1", "owned persistence request has the exact bounded format");
                var result = new Request { Nonce = reader.ReadString(), Writer = reader.ReadInt32(), GameRelative = reader.ReadString(), OwnerKey = reader.ReadString(),
                    Profile = Guid.ParseExact(reader.ReadString(), "N"), Owner = Guid.ParseExact(reader.ReadString(), "N"), Revision = reader.ReadInt64(),
                    ArchiveHash = reader.ReadString(), NativeHash = reader.ReadString(), HostHash = reader.ReadString(), ProbeHash = reader.ReadString() };
                Require(reader.BaseStream.Position == reader.BaseStream.Length, "owned persistence request contains no inventory data or trailing payload"); return result;
            }
        }
        private static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
