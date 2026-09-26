using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Feedback;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // Only explicitly registered switch commands enter here. F5, configuration
    // loading and business execution retain their own feedback and state owners.
    internal sealed class HotkeyStateFeedback
    {
        private readonly LocalShortFeedback display;
        private readonly List<Pending> pending = new List<Pending>(12);
        private sealed class Pending
        {
            internal string Id, Name;
            internal int Before, Expected;
            internal long Command;
            internal double Expires;
            internal LocalShortFeedback.Scope Scope;
            internal Func<int> State;
            internal Func<bool> Available, Success;
            internal Func<long> Accepted, Completed;
            internal Func<int, string> Mode;
        }
        internal int PendingCount { get { return pending.Count; } }
        internal HotkeyStateFeedback(LocalShortFeedback display) { this.display = display; display.Ended += () => pending.Clear(); }
        internal Action Immediate(string id, string name, Action command, Func<int> state, Func<bool> available, Action apply = null, Func<int, string> mode = null)
        {
            return () =>
            {
                int before = 0; bool captured = false; LocalShortFeedback.Scope scope = null;
                try { before = state(); scope = display.Capture(); captured = scope != null; } catch { }
                // Never skip or retry a business command because presentation failed.
                command();
                if (!captured) return;
                try { apply?.Invoke(); int after = state(); if (after != before && available() && display.Current(scope)) Publish(id, name, before, after, state, available, mode, scope); }
                catch { }
            };
        }
        internal Action Committed(string id, string name, Action command, Func<int> state, Func<bool> available,
            Func<long> accepted, Func<long> completed, Func<bool> success, Func<int, string> mode = null)
        {
            return () =>
            {
                Pending request = null;
                try
                {
                    var scope = display.Capture();
                    if (scope != null) request = new Pending { Id = id, Name = name, Before = state(), Command = accepted(), Scope = scope,
                        State = state, Available = available, Accepted = accepted, Completed = completed, Success = success, Mode = mode };
                }
                catch { }
                command();
                if (request == null) return;
                try
                {
                    long next = accepted(); if (next == request.Command) return;
                    request.Command = next; request.Expected = request.Before == 0 ? 1 : 0;
                    request.Expires = LocalShortFeedback.Now + 5000;
                    pending.RemoveAll(p => p.Id == id);
                    display.Remove(id); // a newer accepted intent retires its old result, even while saving
                    if (pending.Count == 12) pending.RemoveAt(0);
                    pending.Add(request);
                }
                catch { }
            };
        }
        internal void Poll()
        {
            if (pending.Count == 0) return;
            double now = LocalShortFeedback.Now;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                try
                {
                    if (!display.Current(p.Scope) || p.Accepted() != p.Command || now >= p.Expires) { pending.RemoveAt(i); continue; }
                    if (p.Completed() != p.Command) continue;
                    pending.RemoveAt(i);
                    // Disabling can already be suspended on submission. Only this
                    // exact reliable completion proves the requested final state.
                    if (p.Success() && p.Available() && p.State() == p.Expected && p.Before != p.Expected)
                        Publish(p.Id, p.Name, p.Before, p.Expected, p.State, () => p.Available() && p.Accepted() == p.Command, p.Mode, p.Scope);
                }
                catch { if (i < pending.Count && ReferenceEquals(pending[i], p)) pending.RemoveAt(i); }
            }
        }
        private void Publish(string id, string name, int before, int after, Func<int> state, Func<bool> available, Func<int, string> mode, LocalShortFeedback.Scope scope)
        {
            // Close text uses the captured old mode; recovery may now read zero.
            string detail = mode == null ? null : mode(after == 0 ? before : after);
            string text = name + (string.IsNullOrEmpty(detail) ? "" : "（" + detail + "）") + (after == 0 ? " 已关闭" : " 已开启");
            display.Show(id, text, after != 0, () => available() && state() == after, scope);
        }
        internal void Clear() { pending.Clear(); display.Clear(); }
    }
}
