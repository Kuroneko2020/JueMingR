using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Platform.Hotkeys
{
    // Submission-time values only. No native dictionaries, live localization or
    // UI state escape into the asynchronous result or its later presentation.
    public sealed class HotkeyOverlap
    {
        public string Id { get; }
        public string Name { get; }
        public ReadOnlyCollection<string> Keys { get; }
        public string Text { get; }
        public HotkeyOverlap(string id, string name, IEnumerable<string> keys)
        {
            Id = id; Name = name; Keys = new List<string>(keys).AsReadOnly();
            Text = String.Join("、", Keys) + " → " + Name;
        }
    }
    public sealed class HotkeyAdvisory
    {
        public ReadOnlyCollection<HotkeyOverlap> Items { get; }
        public ReadOnlyCollection<string> UncheckedParts { get; }
        public bool Complete { get { return UncheckedParts.Count == 0; } }
        public bool HasNotice { get { return Items.Count != 0 || !Complete; } }
        public string Summary { get; }
        public string Message { get; }
        public static readonly HotkeyAdvisory Empty = new HotkeyAdvisory(new HotkeyOverlap[0], new string[0]);
        public static HotkeyAdvisory Unavailable(string part)
        { return new HotkeyAdvisory(new HotkeyOverlap[0], new[] { part }); }
        public HotkeyAdvisory(IEnumerable<HotkeyOverlap> items, IEnumerable<string> uncheckedParts)
        {
            Items = new List<HotkeyOverlap>(items).AsReadOnly(); UncheckedParts = new List<string>(uncheckedParts).AsReadOnly();
            Summary = Complete ? Items.Count == 0 ? null : "与原版键位重合（" + Items.Count + "项）" :
                Items.Count == 0 ? "未能核对原版键位" : "已发现" + Items.Count + "项重合，原版键位未完整核对";
            if (Items.Count != 0) Summary += "，可能同时响应。";
            var lines = new List<string>(); if (Summary != null) lines.Add(Summary);
            foreach (var item in Items) lines.Add(item.Text);
            foreach (var part in UncheckedParts) lines.Add(part);
            Message = String.Join("；", lines);
        }
    }
}
