using System;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;

namespace Terraria
{
    // Read the same final message as NotesPresentation.Prepare. Controlled storage
    // results exercise the real worker/Workspace path without touching user files.
    internal static class NotesCopyChecks
    {
        private static readonly MethodInfo feedback = typeof(NotesPresentation).GetMethod("Feedback", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static string Feedback(NotesPresentation presentation, out Color color)
        {
            object[] args = { Color.LightGray };
            string text = (string)feedback.Invoke(presentation, args); color = (Color)args[0]; return text;
        }
        internal static void Run()
        {
            NotesHostChecks.WithWorkspace(workspace =>
            {
                var presentation = new NotesPresentation(workspace); Color color;
                string id = workspace.Feature.Saved.Notes[0].Id;
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, true));
                Check(Feedback(presentation, out color) == "Enter 保存，Esc 取消编辑。", "title describes save");
                workspace.CancelEdit(); workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id, false));
                Check(Feedback(presentation, out color) == "Enter 换行，Esc 取消编辑。", "body describes newline");
                workspace.Editor.Insert("\ud800");
                Check(Feedback(presentation, out color) == "部分字符不完整，未添加到草稿。", "real editor validation reaches final feedback");
            });
            foreach (bool unknown in new[] { false, true }) WithSaveFailure(unknown, (workspace, presentation) =>
            {
                Color color; string text = Feedback(presentation, out color);
                Check(text == (unknown ? "无法确认是否保存成功，已暂停保存。" : "保存失败，草稿已保留，本次操作未完成。"), "final feedback distinguishes failed and unconfirmed writes without technical suffixes");
                Check(workspace.Editor.Text.Contains("未保存的草稿") && workspace.Editor.Dirty && workspace.Feature.NeedsRecovery == unknown,
                    "message does not replace draft or change storage protection");
                for (int i = 0; i < 100; i++) Check(Feedback(presentation, out color) == text, "stable state keeps the same message");
            });
            Console.WriteLine("PASS: Notes final player messages follow real edit/save results and retain drafts.");
        }
        internal static void WithSaveFailure(bool unknown, Action<NotesWorkspace, NotesPresentation> inspect)
        {
            var codec = new NotebookCodec(); var note = Note.Create().WithText(true, "旅途笔记").WithText(false, "已保存的内容").Pin(840, 80);
            var storage = new FailedStorage(codec.Encode(new Notebook(new[] { note })), unknown);
            using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
            {
                var workspace = new NotesWorkspace(new NotesFeature(worker));
                Check(SpinWait.SpinUntil(() => { workspace.Poll(); return workspace.Feature.Loaded; }, 5000), "load deadline");
                var presentation = new NotesPresentation(workspace);
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, note.Id, false)); workspace.Editor.Insert("未保存的草稿");
                Check(workspace.Request(new NotesAction(NotesActionKind.Save)), "real save accepted");
                Color color;
                Check(Feedback(presentation, out color) == "正在保存；取消编辑不会取消这次保存。", "in-flight result stays distinct before collection");
                Check(SpinWait.SpinUntil(() => { workspace.Poll(); return !workspace.Feature.Busy; }, 5000), "save deadline");
                Check(storage.Writes == 1 && workspace.Feature.Saved.Find(note.Id).Body == "已保存的内容", "failed completion never accepts the draft as saved");
                inspect(workspace, presentation);
            }
        }
        private sealed class FailedStorage : IPreferenceStorage
        {
            private readonly byte[] bytes; private readonly bool unknown;
            internal int Writes;
            internal FailedStorage(byte[] bytes, bool unknown) { this.bytes = bytes; this.unknown = unknown; }
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Loaded, bytes, "initial", null); }
            public PreferenceWriteResult Write(string identity, byte[] contents)
            { Writes++; return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "controlled-copy-check", commitUnconfirmed: unknown); }
            public void Dispose() { }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Notes copy: " + message); }
    }
}
