using System;
using System.IO;
using System.Linq;
using System.Threading;
using Terraria;
using Terraria.Net;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        // A primary connection can be reused by vanilla. The socket, Steam
        // lobby and active world bind credentials to this specific play session.
        private sealed class PasswordSession
        {
            internal object Primary, Socket, World;
            internal ulong Lobby;
            internal long Session;
            internal PasswordCredential Accepted;
        }

        internal sealed class PasswordAttempt
        {
            internal long Session, RejectedRevision;
            internal object Authentication;
            internal CompanionConnection Connection;
            internal PasswordCredential Credential;
            internal bool Challenged, Sent, Waiting;
        }

        internal sealed class PasswordCredential
        {
            internal readonly long Revision;
            internal readonly string Text;
            internal PasswordCredential(long revision, string text) { Revision = revision; Text = text; }
        }

        private PasswordSession authentication, humanAuthentication;
        private PasswordCredential humanCandidate;
        private long passwordRevision, passwordPrompt;
        private bool promptPending;

        private static PasswordSession PrimaryAuthentication()
        {
            var primary = Netplay.Connection;
            var socket = primary?.Socket;
            if (Main.netMode != 1 || socket == null) return null;
            return new PasswordSession { Primary = primary, Socket = socket, Lobby = Main.LobbyId };
        }

        private static bool SamePrimary(PasswordSession context)
        {
            return context != null && Main.netMode == 1 && ReferenceEquals(context.Primary, Netplay.Connection) &&
                ReferenceEquals(context.Socket, Netplay.Connection?.Socket) && context.Lobby == Main.LobbyId;
        }

        private bool CurrentAuthentication()
        {
            return SamePrimary(authentication) && authentication.Session == session &&
                ReferenceEquals(authentication.World, world) && ReferenceEquals(world, Main.ActiveWorldFileData);
        }

        private void BeginPasswordSession()
        {
            authentication = PrimaryAuthentication();
            if (authentication != null)
            {
                authentication.World = world;
                authentication.Session = session;
                if (SamePrimary(humanAuthentication)) authentication.Accepted = humanAuthentication.Accepted;
            }
            humanAuthentication = null;
            humanCandidate = null;
            promptPending = false;
        }

        private void EndPasswordSession()
        { authentication = humanAuthentication = null; humanCandidate = null; promptPending = false; }

        internal void HumanPassword(byte[] frame)
        {
            var proposed = PrimaryAuthentication();
            if (proposed == null || frame.Length < 4 || Netplay.Connection.State != 1) return;
            using (var stream = new MemoryStream(frame, 3, frame.Length - 3, false))
            using (var reader = new BinaryReader(stream))
                humanCandidate = new PasswordCredential(Interlocked.Increment(ref passwordRevision), reader.ReadString());
            // Sent and accepted are distinct. Only native step 3 can promote
            // this provisional value into the entering world's session cache.
            humanAuthentication = proposed;
        }

        internal void HumanAccepted()
        {
            var proposed = humanAuthentication;
            if (Netplay.Connection?.State != 3 || !SamePrimary(proposed) || humanCandidate == null) return;
            proposed.Accepted = humanCandidate;
            humanCandidate = null;
            if (CurrentAuthentication())
            {
                RememberAccepted(proposed.Accepted);
                humanAuthentication = null;
            }
        }

        private void StartConnectionAttempt(Actor actor, PasswordCredential proposed)
        {
            if (proposed == null && !CurrentAuthentication() && ReferenceEquals(world, Main.ActiveWorldFileData)) BeginPasswordSession();
            if (!CurrentAuthentication() || !Netplay.Connection.IsConnected())
                throw new IOException("当前服务器连接已变化。");
            var endpoint = Netplay.Connection.Socket.GetRemoteAddress() as TcpAddress;
            var connection = endpoint == null ? steam.Open() : new CompanionConnection(endpoint.Address.ToString(), endpoint.Port);
            actor.Connection = connection;
            actor.PasswordAttempt = new PasswordAttempt { Session = session, Authentication = authentication, Connection = connection, Credential = proposed };
            actor.Status = "加入中";
            connection.Start();
            if (!connection.TrySend(CompanionConnection.Packet(1, w => w.Write("Terraria326"))))
                throw new IOException("队友连接已结束。");
        }

        private bool CurrentAttempt(Actor actor, PasswordAttempt attempt)
        {
            return attempt != null && !actor.Closing && !actor.Ready && actor.Previous == null &&
                ReferenceEquals(actor.Connection, attempt.Connection) && attempt.Session == session &&
                ReferenceEquals(attempt.Authentication, authentication) && CurrentAuthentication() &&
                profiles.OwnsActivation(actor.Profile.Id, actor.Generation);
        }

        private void ValidatePasswordAcceptance(Actor actor)
        {
            var attempt = actor.PasswordAttempt;
            if (!CurrentAttempt(actor, attempt) || attempt.Challenged && !attempt.Sent)
                throw new InvalidDataException("队友服务器握手顺序异常。");
        }

        private void AskPassword(Actor actor)
        {
            var attempt = actor.PasswordAttempt;
            if (!CurrentAttempt(actor, attempt) || attempt.Challenged)
                throw new InvalidDataException("队友服务器握手顺序异常。");
            attempt.Challenged = true;
            var credential = attempt.Credential ?? authentication.Accepted;
            if (credential == null) WaitForPassword(actor, attempt, "需要当前服务器密码");
            else SendPassword(actor, attempt, credential);
        }

        private void SendPassword(Actor actor, PasswordAttempt attempt, PasswordCredential credential)
        {
            if (!attempt.Connection.TrySend(CompanionConnection.Packet(38, w => w.Write(credential.Text))))
                throw new IOException("队友连接已结束。");
            attempt.Credential = credential;
            attempt.Sent = true;
            attempt.Waiting = false;
            actor.Status = "正在验证密码";
        }

        private void RememberAccepted(PasswordCredential credential)
        {
            if (credential != null && (authentication.Accepted == null || credential.Revision >= authentication.Accepted.Revision))
                authentication.Accepted = credential;
        }

        private void AcceptPassword(Actor actor)
        {
            var attempt = actor.PasswordAttempt;
            if (attempt != null && attempt.Sent && attempt.Session == session && CurrentAuthentication() &&
                ReferenceEquals(attempt.Authentication, authentication) &&
                ReferenceEquals(actor.Connection, attempt.Connection)) RememberAccepted(attempt.Credential);
            actor.PasswordAttempt = null;
        }

        private bool RejectPassword(Actor actor, BinaryReader reason)
        {
            var attempt = actor.PasswordAttempt;
            if (!CurrentAttempt(actor, attempt) || !attempt.Sent) return false;
            long start = reason.BaseStream.Position;
            // Terraria 1.4.5.8 NetworkText serializes mode, key and children.
            // Only its bare localization key in an active challenge is retryable.
            bool wrongKey = reason.ReadByte() == 2 && reason.ReadString() == "LegacyMultiplayer.1" &&
                reason.ReadByte() == 0 && reason.BaseStream.Position == reason.BaseStream.Length;
            if (!wrongKey) { reason.BaseStream.Position = start; return false; }
            long tried = attempt.Credential.Revision;
            if (authentication.Accepted?.Revision == tried) authentication.Accepted = null;

            // Native 2 ends this flow. Authentication precedes slot allocation,
            // so the actor keeps its reserved character while the old byte
            // transport is disposed before another message 1 can be sent.
            actor.Connection = null;
            attempt.Connection.Dispose();
            if (authentication.Accepted != null && authentication.Accepted.Revision > tried)
                StartConnectionAttempt(actor, authentication.Accepted);
            else
            {
                var retry = new PasswordAttempt { Session = session, Authentication = authentication, RejectedRevision = tried };
                actor.PasswordAttempt = retry;
                WaitForPassword(actor, retry, "密码有误，请重试");
            }
            return true;
        }

        private void WaitForPassword(Actor actor, PasswordAttempt attempt, string status)
        {
            if (!promptPending) { passwordPrompt++; promptPending = true; }
            attempt.Waiting = true;
            actor.Status = status;
        }

        private void UpdatePasswordAttempt(Actor actor)
        {
            var attempt = actor.PasswordAttempt;
            if (attempt == null) return;
            if (!CurrentAttempt(actor, attempt))
                throw new IOException("当前服务器连接已变化。");
            // A role may join the prompt after another role has submitted.
            // Wait for genuine acceptance, then continue that still-valid
            // waiter with the new revision rather than ask the owner twice.
            var accepted = authentication.Accepted;
            if (attempt.Waiting && accepted != null && accepted.Revision > attempt.RejectedRevision)
                ContinuePassword(actor, attempt, accepted);
        }

        private void ContinuePassword(Actor actor, PasswordAttempt attempt, PasswordCredential credential)
        {
            if (attempt.Connection == null) StartConnectionAttempt(actor, credential);
            else SendPassword(actor, attempt, credential);
        }

        internal bool PasswordPromptState(out long token, out int waiting)
        {
            token = passwordPrompt;
            waiting = 0;
            if (!promptPending || !CurrentAuthentication()) return false;
            foreach (var actor in actors.Values)
                if (actor.PasswordAttempt?.Waiting == true && CurrentAttempt(actor, actor.PasswordAttempt)) waiting++;
            if (waiting == 0) promptPending = false;
            return waiting > 0;
        }

        internal bool PasswordPromptRejected()
        {
            foreach (var actor in actors.Values)
                if (actor.PasswordAttempt?.Waiting == true && actor.PasswordAttempt.Connection == null &&
                    CurrentAttempt(actor, actor.PasswordAttempt)) return true;
            return false;
        }

        internal bool SubmitPassword(long token, string text)
        {
            if (!PasswordPromptState(out long current, out _) || token != current) return false;
            if (String.IsNullOrEmpty(text)) return Result(false, "请输入当前服务器密码。");
            var credential = new PasswordCredential(Interlocked.Increment(ref passwordRevision), text);
            promptPending = false;
            foreach (var actor in actors.Values.ToArray())
            {
                var attempt = actor.PasswordAttempt;
                if (attempt?.Waiting != true || !CurrentAttempt(actor, attempt)) continue;
                try { ContinuePassword(actor, attempt, credential); }
                catch (Exception exception) { CloseActor(actor, exception.Message); }
            }
            return true;
        }

        internal void CancelPassword(long token)
        {
            if (!PasswordPromptState(out long current, out _) || token != current) return;
            promptPending = false;
            foreach (var actor in actors.Values.ToArray())
                if (actor.PasswordAttempt?.Waiting == true && CurrentAttempt(actor, actor.PasswordAttempt))
                    CloseActor(actor, "已取消输入服务器密码。");
        }
    }
}
