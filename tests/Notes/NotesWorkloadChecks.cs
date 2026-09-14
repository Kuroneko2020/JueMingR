using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Notes;

namespace Terraria
{
    // Real card preparation with the texture-free font supplied by NotesHostChecks.
    // References count retained work products, not time or a replacement UI model.
    internal static class NotesWorkloadChecks
    {
        internal static void Run(NotesRenderer renderer)
        {
            CheckTextEdits(renderer, false); CheckTextEdits(renderer, true); CheckBudgetedHeights(renderer);
            foreach (int count in new[] { 2, 256 })
            {
                var notes = new List<Note>();
                for (int i = 0; i < count; i++) notes.Add(Note.Create().WithText(false, "first\nsecond\n" + new string('x', 400)));
                NotesHostChecks.WithWorkspace(workspace =>
                {
                    var input = new NotesInput(workspace, new Clipboard(), new Ime());
                    int requests = 0;
                    var cards = new NotesCards(workspace, input, renderer, action => { requests++; workspace.Request(action); });
                    var shell = new F5Interaction { Ready = true };
                    shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(4);
                    shell.Layout.Ensure(1920, 1080, 1, 4, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, notes[0].Id, false, 0));
                    Prepare(cards, shell, renderer);
                    var controls = new List<NotesControl>(cards.Controls);
                    var layouts = new List<NotesTextLayout>();
                    foreach (var card in cards.Cards) layouts.Add(card.TitleLayout);
#if DEBUG
                    int geometryPasses = cards.DebugGeometryPasses;
#else
                    throw new InvalidOperationException("Notes workload requires the Debug geometry probe.");
#endif
                    for (int sample = 0; sample < 20; sample++)
                    {
                        workspace.Editor.MoveTo(sample % 6, sample % 2 == 0);
                        Prepare(cards, shell, renderer);
                        Require(cards.Controls.Count == controls.Count, "caret does not change actions");
                        for (int i = 0; i < controls.Count; i++) Require(ReferenceEquals(controls[i], cards.Controls[i]), "caret/selection must retain controls at notebook size " + count);
                        for (int i = 0; i < layouts.Count; i++) Require(ReferenceEquals(layouts[i], cards.Cards[i].TitleLayout), "caret retains title wrapping");
                    }
#if DEBUG
                    Require(cards.DebugGeometryPasses == geometryPasses, "caret/selection must not repack the notebook");
#endif
                    NotesCard first = cards.Cards[0]; var bodyLayout = first.BodyLayout;
                    float mainScroll = shell.Scroll;
                    Require(cards.Wheel(shell, shell.X + shell.Layout.Viewport.X + first.Body.X + 2,
                        shell.Y + shell.Layout.Viewport.Y + first.Body.Y + 2 - shell.Scroll, -120), "real body wheel has scrollable text");
                    float bodyScroll = first.BodyScroll; Prepare(cards, shell, renderer);
                    Require(bodyScroll > 0 && first.BodyScroll == bodyScroll && shell.Scroll == mainScroll && ReferenceEquals(bodyLayout, first.BodyLayout), "body wheel changes reading offset without replacing layout or moving the page");
                    for (int i = 0; i < controls.Count; i++) Require(ReferenceEquals(controls[i], cards.Controls[i]), "body wheel retains controls at notebook size " + count);
#if DEBUG
                    Require(cards.DebugGeometryPasses == geometryPasses, "body wheel must not repack the notebook");
#endif
                    var pin = cards.Controls[2]; // add, cancel, first pin
                    Require(pin.Key == notes[0].Id + ":pin", "fixture selects the first pin action");
                    if (count > 2)
                    {
                        float contentHeight = shell.Layout.ContentHeight;
                        shell.Navigate(8); shell.Layout.Ensure(1920, 1080, 1, 8, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                        shell.Navigate(4); shell.Layout.Ensure(1920, 1080, 1, 4, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                        Require(shell.Layout.MaxScroll == 0, "outer Notes layout really resets content height on page reentry");
                        Prepare(cards, shell, renderer);
                        Require(shell.Layout.ContentHeight == contentHeight && shell.Layout.MaxScroll > 12, "cached book republishes its height after page reentry");
                        F5Rect pressedRect = pin.Rect;
                        Pointer(cards, shell, pressedRect, true, false);
                        shell.ScrollTo(12); Require(shell.Scroll == 12, "fixture really scrolls"); Prepare(cards, shell, renderer);
                        Pointer(cards, shell, pressedRect, false, true);
                        Require(requests == 0, "a press before viewport scrolling cannot activate at the new projection");
#if DEBUG
                        Require(cards.DebugGeometryPasses == geometryPasses, "main scrolling must not repack the notebook");
#endif
                    }
                    shell.ScrollTo(0); Prepare(cards, shell, renderer);
                    Pointer(cards, shell, pin.Rect, true, false);
                    Pointer(cards, shell, pin.Rect, false, true);
                    Require(requests == 1, "a fresh press/release still activates the intended pin");
                }, new Notebook(notes));
            }
            Console.WriteLine("PASS: Notes workload core retains card controls for caret/selection and cancels stale projected presses.");
        }
        internal static void CheckResourceChange(NotesRenderer renderer, Action replaceFont)
        {
            Note original = Note.Create().WithText(true, "title").WithText(false, "one\ntwo\nthree");
            NotesHostChecks.WithWorkspace(workspace =>
            {
                int requests = 0;
                var cards = new NotesCards(workspace, new NotesInput(workspace, new Clipboard(), new Ime()), renderer,
                    action => { requests++; workspace.Request(action); });
                var shell = new F5Interaction { Ready = true };
                shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(4);
                shell.Layout.Ensure(1920, 1080, 1, 4, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                Prepare(cards, shell, renderer);
                NotesCard retained = cards.Cards[0]; var pin = retained.PinControl; var oldBody = retained.BodyLayout;
                float oldWidth = retained.Width, oldHeight = retained.Body.Height; F5Rect oldPin = pin.Rect;
                Pointer(cards, shell, oldPin, true, false);
                replaceFont();
                shell.Layout.Ensure(1920, 1080, 1, 4, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                Prepare(cards, shell, renderer);
                // F5 has a fixed logical window width. Wider glyph controls cause
                // the real Notes column choice to change, exercising card width too.
                Require(ReferenceEquals(retained, cards.Cards[0]) && ReferenceEquals(pin, retained.PinControl), "font replacement retains the card and controls");
                Console.WriteLine("Notes resource replacement: width={0}->{1}, body height={2}->{3}, font={4}, replaced={5}, complete={6}",
                    oldWidth, retained.Width, oldHeight, retained.Body.Height, retained.Font == renderer.FontIdentity, !ReferenceEquals(oldBody, retained.BodyLayout), retained.BodyLayout.Complete);
                Require(retained.Font == renderer.FontIdentity && retained.Width > oldWidth && retained.Body.Height > oldHeight &&
                    !ReferenceEquals(oldBody, retained.BodyLayout) && retained.BodyLayout.Complete && retained.BodyLayout.Text == original.Body,
                    "same card invalidates old font/width layout and completes readable replacement geometry");
                Pointer(cards, shell, oldPin, false, true); Require(requests == 0, "font/width change cancels an earlier projected press");
                Require(workspace.Request(new NotesAction(NotesActionKind.Create)), "real create is accepted"); Drain(workspace); Prepare(cards, shell, renderer);
                Require(cards.Cards.Count == 2 && ReferenceEquals(retained, cards.Cards[0]), "acknowledged create adds one card and keeps existing lifetime");
                string added = cards.Cards[1].Note.Id;
                workspace.RequestDelete(added); Prepare(cards, shell, renderer);
                Require(cards.Cards[1].DeleteControl.Text == "确认", "deletion keeps its explicit confirmation step");
                workspace.RequestDelete(added); Drain(workspace); Prepare(cards, shell, renderer);
                var states = (IDictionary<string, NotesCard>)typeof(NotesCards).GetField("states", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cards);
                Require(cards.Cards.Count == 1 && states.Count == 1 && !states.ContainsKey(added) && ReferenceEquals(states[original.Id], retained), "acknowledged delete retires the removed lifetime without a zombie cache entry");
                foreach (NotesControl control in cards.Controls) Require(!control.Key.StartsWith(added + ":", StringComparison.Ordinal), "deleted note has no remaining hit control");
                Pointer(cards, shell, retained.PinControl.Rect, true, false); Pointer(cards, shell, retained.PinControl.Rect, false, true);
                Require(requests == 1, "fresh post-change hit still targets the surviving note"); Drain(workspace);
                Require(workspace.Feature.Saved.Find(original.Id).Pinned, "surviving hit is reliably committed to the intended note");
            }, new Notebook(new[] { original }));
            Console.WriteLine("PASS: retained Notes cards handle font/column width changes, acknowledged create/delete and cache retirement.");
        }
        private static void Drain(NotesWorkspace workspace)
        { Require(SpinWait.SpinUntil(() => { workspace.Poll(); return !workspace.Feature.Busy; }, 5000) && workspace.Error == null, "isolated real save acknowledgment completes"); }
        private static void CheckTextEdits(NotesRenderer renderer, bool title)
        {
            Note note = Note.Create().WithText(true, "title").WithText(false, "body");
            NotesHostChecks.WithWorkspace(workspace =>
            {
                var input = new NotesInput(workspace, new Clipboard(), new Ime());
                var cards = new NotesCards(workspace, input, renderer, action => workspace.Request(action));
                var shell = new F5Interaction { Ready = true };
                shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(4);
                shell.Layout.Ensure(1920, 1080, 1, 4, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                workspace.Request(new NotesAction(NotesActionKind.BeginEdit, note.Id, title, 0)); workspace.Editor.Insert("x"); Prepare(cards, shell, renderer);
#if DEBUG
                int passes = cards.DebugGeometryPasses;
#endif
                workspace.Editor.Insert("y"); Prepare(cards, shell, renderer);
                Require(cards.EditingLayout.Text == workspace.Editor.Text, "already-dirty local edit refreshes actual text layout");
#if DEBUG
                Require(cards.DebugGeometryPasses == passes, "equal-height dirty edit avoids whole-book geometry");
#endif
                float beforeHeight = cards.Cards[0].Rect.Height;
                Require(workspace.Editor.Insert(title ? new string('a', 70) : "\na\nb\nc\nd"), "fixture edit stays within the real title/body contract");
                renderer.BeginLayoutFrame(); cards.Prepare(shell, "workload");
                Require(cards.Cards[0].Rect.Height > beforeHeight && cards.EditingLayout.Text == workspace.Editor.Text, "changed text height updates geometry on the actual preparation frame, title=" + title);
                workspace.CancelEdit(); Prepare(cards, shell, renderer);
                Require(cards.EditingLayout == null && cards.Cards[0].TitleLayout.Text == "title" && cards.Cards[0].BodyLayout.Text == "body", "cancel restores saved previews after cached local edits");
            }, new Notebook(new[] { note }));
        }
        private static void CheckBudgetedHeights(NotesRenderer renderer)
        {
            var notes = new List<Note> { Note.Create().WithText(false, new string('x', Note.MaximumBodyUnits)) };
            for (int i = 0; i < 128; i++) notes.Add(Note.Create().WithText(true, new string('t', 80)).WithText(false, new string('b', 240)));
            NotesHostChecks.WithWorkspace(workspace =>
            {
                var cards = new NotesCards(workspace, new NotesInput(workspace, new Clipboard(), new Ime()), renderer, action => workspace.Request(action));
                var shell = new F5Interaction { Ready = true };
                shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(4);
                shell.Layout.Ensure(1920, 1080, 1, 4, renderer.FontIdentity, text => new F5Size(text.Length * 10, 20));
                renderer.BeginLayoutFrame(); cards.Prepare(shell, "workload"); float coldHeight = shell.Layout.ContentHeight;
                bool pendingShort = false;
                for (int i = 1; i < cards.Cards.Count; i++) pendingShort |= !cards.Cards[i].TitleLayout.Complete || !cards.Cards[i].BodyLayout.Complete;
                Require(pendingShort, "cold fixture exceeds the shared unit budget and really defers short geometry");
                for (int i = 0; i < 600; i++) { renderer.BeginLayoutFrame(); cards.Prepare(shell, "workload"); }
                Require(cards.Cards[0].BodyLayout.Complete && !cards.PendingLayout && shell.Layout.ContentHeight > coldHeight,
                    "deferred short heights eventually repack while visible long text still completes");
                shell.ScrollTo(shell.Layout.MaxScroll); renderer.BeginLayoutFrame(); cards.Prepare(shell, "workload");
                var last = cards.Cards[cards.Cards.Count - 1];
                Require(last.BodyLayout.Complete && last.Rect.Bottom > shell.Scroll && last.Rect.Y < shell.Scroll + shell.Layout.Viewport.Height,
                    "previously offscreen short content becomes readable at the retained final height");
            }, new Notebook(notes));
        }
        private static void Prepare(NotesCards cards, F5Interaction shell, NotesRenderer renderer)
        { for (int i = 0; i < 8; i++) { renderer.BeginLayoutFrame(); cards.Prepare(shell, "workload"); } }
        private static void Pointer(NotesCards cards, F5Interaction shell, F5Rect rect, bool pressed, bool released)
        {
            shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, Left = pressed,
                X = shell.X + shell.Layout.Viewport.X + rect.X + 2, Y = shell.Y + shell.Layout.Viewport.Y + rect.Y + 2 - shell.Scroll });
            cards.Pointer(shell, pressed, released, pressed);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Notes workload: " + message); }
        private sealed class Clipboard : INotesClipboard
        { public bool TryCopy(string text) { return true; } public bool TryPaste(out string text) { text = ""; return true; } }
        private sealed class Ime : INotesIme
        { public string Composition { get { return ""; } } public bool Candidates { get { return false; } } public void Toggle(bool enabled) { } }
    }
}
