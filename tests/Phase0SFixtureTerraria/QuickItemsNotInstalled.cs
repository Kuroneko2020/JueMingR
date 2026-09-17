using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

// This older source-linked receiver/UI fixture has no G05 feature installed.
// Keep its null optional section compilable, but fail on ANY attempted use.
// G05 itself is exercised against the real compiled Host and fixed Terraria
// by NativeQuickItemChecks/NativeQuickUiChecks, never by this absent adapter.
namespace JueMingR.TerrariaHost.QuickItems
{
    internal sealed class HostQuickItems
    {
        internal HostQuickItems(HostItems items)
        {items.AllowsOwnedUse=null;throw new InvalidOperationException("G05 cannot be installed in the receiver fixture.");}
    }
    internal sealed class QuickItemPanel
    {
        private static Exception Missing() {return new InvalidOperationException("Use the real G05 native fixture.");}
        internal QuickItemPanel(HostQuickItems host,F5Interaction shell){throw Missing();}
        internal bool Editing {get{throw Missing();}}
        internal bool OwnsTextToken {get{throw Missing();}}
        internal bool NeedsBuild {get{throw Missing();}}
        internal bool RevealRequested {get{throw Missing();}set{throw Missing();}}
        internal float SelectorTop {get{throw Missing();}}
        internal float Height {get{throw Missing();}}
        internal void BeforeInput(bool active){throw Missing();}
        internal void Process(bool active,KeyboardState sample,bool focused){throw Missing();}
        internal void Suspend(){throw Missing();}
        internal void Execute(ItemUiControl control){throw Missing();}
        internal void Build(float start,float width,float row,Func<string,float,F5Size> measure){throw Missing();}
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements){throw Missing();}
        internal void PrepareIcons(){throw Missing();}
        internal void Draw(ItemsRenderer renderer,Vector2 pointer,Action<F5Rect> keyboard){throw Missing();}
        internal string Hint(float x,float y,out F5Rect rect){throw Missing();}
    }
}
