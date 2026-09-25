using System;
using System.Collections.Generic;
using JueMingR.Features.QuickItems;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.QuickItems
{
    // The existing Items page owns scrolling, pointer gestures and clipping.
    // This section owns only its draft and a one-time inventory picker snapshot;
    // keyboard binding and input capture stay with the public hotkey owner.
    internal sealed class QuickItemPanel
    {
        private enum Command { None, FavoriteOn, FavoriteOff, QuickOn, QuickOff, Add, Edit, Enable, Delete, Cancel, Pick, Mode, Policy, RetryCleanup }
        private sealed class Part
        {
            internal F5Element Element;
            internal Command Command;
            internal string Id, Hint;
            internal int Type;
            internal bool Enabled=true, Selected, Icon, Cross;
        }
        private readonly HostQuickItems host;
        private readonly F5Interaction shell;
        private readonly ItemPickerInput input=new ItemPickerInput();
        private readonly List<Part> logical=new List<Part>(),visible=new List<Part>();
        private readonly List<int> candidates=new List<int>(),removed=new List<int>();
        private readonly Dictionary<int,Texture2D> icons=new Dictionary<int,Texture2D>();
        private readonly HashSet<int> visibleTypes=new HashSet<int>();
        private string draftId;
        private int target,generation;
        private QuickItemMode mode;
        private bool compatible,entryEnabled,dirty=true,builtQuickAvailable,builtFavoriteAvailable,builtFavoriteCapability,builtQuickCapability,builtCleanup;
        private long editSession,builtRevision=-1,builtBindings=-1;
        private float width,row;
        private Func<string,float,F5Size> measure;
        internal bool Editing {get;private set;}
        internal float Height {get;private set;}
        internal bool RevealRequested {get;set;}
        internal float SelectorTop {get;private set;}
        internal bool OwnsTextToken {get{return input.OwnsTextToken;}}
#if DEBUG
        internal long PickerReads {get;private set;}
        internal long IconLoads {get;private set;}
#endif
        internal bool NeedsBuild {get{return dirty || builtRevision!=host.Settings.Revision || builtBindings!=host.BindingRevision ||
            builtQuickAvailable!=host.ControlsEnabled || builtFavoriteAvailable!=host.FavoriteControlsEnabled ||
            builtFavoriteCapability!=host.FavoriteAvailable || builtQuickCapability!=host.Available || builtCleanup!=host.CanRetryCleanup;}}
        internal QuickItemPanel(HostQuickItems host,F5Interaction shell) {this.host=host;this.shell=shell;}
        internal void BeforeInput(bool active)
        {if(Editing && editSession!=host.Runtime.Generation)Suspend();input.BeforeInput(active && shell.Visible && shell.Page==0 && Editing);}
        internal void Process(bool active,KeyboardState sample,bool focused)
        {input.Sample(sample,focused);if(!active || !focused)Suspend();}
        internal void Suspend()
        {input.Release();Editing=false;logical.Clear();visible.Clear();icons.Clear();visibleTypes.Clear();candidates.Clear();dirty=true;}
        internal void Execute(ItemUiControl control)
        {
            if(control.Generation!=generation || control.Argument<0 || control.Argument>=logical.Count)return;
            Part part=logical[control.Argument];if(!part.Enabled)return;
            var existing=part.Id==null?null:host.Settings.Current.Find(part.Id);dirty=true;
            switch(part.Command)
            {
                case Command.FavoriteOn: if(!host.Settings.KeepFavorited)host.ToggleFavorite();break;
                case Command.FavoriteOff: if(host.Settings.KeepFavorited)host.ToggleFavorite();break;
                case Command.QuickOn: if(!host.Settings.Enabled)host.ToggleQuick();break;
                case Command.QuickOff: if(host.Settings.Enabled)host.ToggleQuick();break;
                case Command.Add: Open(null);break;
                case Command.Edit: if(existing!=null)Open(existing);break;
                case Command.Delete: if(existing!=null)host.Delete(existing.Id);break;
                case Command.Cancel: Suspend();break;
                case Command.Pick: if(target!=part.Type){target=part.Type;mode=QuickItemRules.DefaultMode(target);}Save();break;
                case Command.Mode: mode=(QuickItemMode)part.Type;Save();break;
                case Command.Policy: compatible=!compatible;Save();break;
                case Command.Enable: entryEnabled=!entryEnabled;Save();break;
                case Command.RetryCleanup: host.RetryCleanup();break;
            }
        }
        private void Save()
        {if(target>0 && host.Save(new QuickItemEntry(draftId??Guid.NewGuid().ToString("N"),target,mode,compatible,entryEnabled)))Suspend();}
        private void Open(QuickItemEntry entry)
        {
            Editing=true;editSession=host.Runtime.Generation;draftId=entry?.Id;target=entry?.Target??0;
            mode=entry?.Mode??QuickItemMode.Use;compatible=entry?.Compatible??true;entryEnabled=entry?.Enabled??true;
            RevealRequested=true;candidates.Clear();var seen=new HashSet<int>();Player player=host.Player;
            // Same grid interaction as auto-discard; its inventory domain and
            // persistence owner differ, so never route through a discard list.
            if(player!=null)for(int i=0;i<50;i++)
            {
#if DEBUG
                PickerReads++;
#endif
                Item item=player.inventory[i];if(item==null || item.IsAir)continue;
                int type=item.type==5437?5358:item.type;
                if(item.useStyle==0 && !QuickItemRules.HasState(type))continue;
                for(int step=0;step<4 && type>0;step++)
                {if(seen.Add(type))candidates.Add(type);int next=QuickItemRules.NextState(type);if(next==item.type)break;type=next;}
            }
            dirty=true;
        }
        internal void Build(float start,float width,float row,Func<string,float,F5Size> measure)
        {
            this.width=width;this.row=row;this.measure=measure;logical.Clear();generation++;
            float y=start;bool can=host.ControlsEnabled;
            Row(ref y,"保持收藏",new[]{"开启","关闭","键"},new[]{Command.FavoriteOn,Command.FavoriteOff,Command.None},host.FavoriteControlsEnabled,HostQuickItems.FavoriteAction,
                "保持随身物品的收藏标记。",!host.FavoriteAvailable?-1:host.Settings.KeepFavorited?0:1);
            Row(ref y,"快捷物品",Editing?new[]{"开启","关闭","键"}:new[]{"添加","开启","关闭","键"},
                Editing?new[]{Command.QuickOn,Command.QuickOff,Command.None}:new[]{Command.Add,Command.QuickOn,Command.QuickOff,Command.None},can,HostQuickItems.ToggleAction,
                "用快捷键使用背包物品或切换形态。",!host.Available?-1:host.Settings.Enabled?(Editing?0:1):(Editing?1:2));
            if(Editing)BuildPicker(ref y,can);
            if(host.CanRetryCleanup)Buttons(ref y,new[]{"重试清理旧按键"},new[]{Command.RetryCleanup},true);
            // Legacy's small icon + key field, wrapping from one to three
            // columns. Hidden rows carry no textures or projected hit targets.
            int columns=Math.Min(3,ItemsLayout.Columns(width,160));float card=(width-16-(columns-1)*5)/columns;
            int index=0;float top=y;
            foreach(var entry in host.Settings.Current.Entries)
            {
                float x=8+(index%columns)*(card+5),cy=top+(index/columns)*39;
                var icon=new F5Rect(x,cy,34,34);
                Add(new F5Rect(icon.Right-ItemsLayout.CrossSize,cy,ItemsLayout.CrossSize,ItemsLayout.CrossSize),"",Command.Delete,can,entry.Id,cross:true,hint:"移除此快捷物品");
                Add(icon,"",Command.Edit,can,entry.Id,entry.Target,icon:true,hint:HostQuickItems.SafeName(entry.Target)+" · "+ModeName(entry.Mode)+" · "+(entry.Enabled?"点击更换或设置":"已停用；点击设置"));
                Add(new F5Rect(icon.Right+4,cy,card-38,34),host.BindingText(entry.ActionId),Command.None,can,entry.Id,key:entry.ActionId);
                index++;
            }
            if(index>0)y=top+(float)Math.Ceiling(index/(double)columns)*39;
            Height=y;builtRevision=host.Settings.Revision;builtBindings=host.BindingRevision;
            builtQuickAvailable=host.ControlsEnabled;builtFavoriteAvailable=host.FavoriteControlsEnabled;
            builtFavoriteCapability=host.FavoriteAvailable;builtQuickCapability=host.Available;builtCleanup=host.CanRetryCleanup;dirty=false;
        }
        private void BuildPicker(ref float y,bool can)
        {
            SelectorTop=y;
            Row(ref y,draftId==null?"选择背包物品":"更换快捷物品",new[]{"×"},new[]{Command.Cancel},true,null,null);
            if(target>0)
            {
                if(QuickItemRules.HasState(target))
                {
                    int first=logical.Count;
                    Buttons(ref y,new[]{"普通使用","只设置形态","设置后使用"},new[]{Command.Mode,Command.Mode,Command.Mode},can);
                    for(int i=0;i<3;i++){logical[first+i].Type=i;logical[first+i].Selected=(int)mode==i;}
                }
                int policy=logical.Count;
                Buttons(ref y,new[]{compatible?"同用途替代：开":"仅此物品",entryEnabled?"停用此项":"启用此项"},new[]{Command.Policy,Command.Enable},can);
                logical[policy].Hint=ProviderHelp(target);
            }
            if(candidates.Count==0){Text(ref y,"背包中没有可选的物品。");return;}
            int columns=ItemsLayout.Columns(width,ItemsLayout.CandidateWidth);
            for(int i=0;i<candidates.Count;i++)
                Add(new F5Rect(8+i%columns*(ItemsLayout.CandidateWidth+ItemsLayout.Gap),y+i/columns*(ItemsLayout.CandidateSize+ItemsLayout.Gap),ItemsLayout.CandidateWidth,ItemsLayout.CandidateSize),
                    "",Command.Pick,can,type:candidates[i],icon:true,selected:target==candidates[i],hint:HostQuickItems.SafeName(candidates[i])+" · 点击选择");
            y+=(float)Math.Ceiling(candidates.Count/(double)columns)*(ItemsLayout.CandidateSize+ItemsLayout.Gap)+6;
        }
        private static string ModeName(QuickItemMode value) {return value==QuickItemMode.SetState?"只设置形态":value==QuickItemMode.SetStateAndUse?"设置后使用":"普通使用";}
        private static string ProviderHelp(int type)
        {
            string providers=type==50 || type==3199 || type==3124 || type==5358?"魔镜、冰雪镜、手机、贝壳电话（家）":
                type==4263 || type==5360?"魔法海螺、贝壳电话（海洋）":type==4819 || type==5361?"恶魔海螺、贝壳电话（地狱）":"此物品的合法形态";
            return "优先所选物品，其次已就绪形态；兼容顺序："+providers+"。仅此物品仍可切换自身形态。";
        }
        private void Add(F5Rect r,string text,Command command,bool enabled,string id=null,int type=0,bool icon=false,bool cross=false,bool selected=false,string key=null,string hint=null)
        {logical.Add(new Part{Element=new F5Element(F5ElementKind.Button,r,text,measure(text,.7f),.7f,F5Command.None,key),Command=command,Enabled=enabled,Id=id,Type=type,Icon=icon,Cross=cross,Selected=selected,Hint=hint});}
        private void Row(ref float y,string name,string[] labels,Command[] commands,bool enabled,string key,string help,int selected=-1)
        {
            var values=new List<F5Element>();new F5RowLayout(values,measure).Row(ref y,0,width,name,labels,description:help==null?null:new F5RowDescription(key,help));int action=0;
            foreach(var e in values)
            {bool button=e.Kind==F5ElementKind.Button || e.Kind==F5ElementKind.Hotkey;
                logical.Add(new Part{Element=e.Kind==F5ElementKind.Hotkey?new F5Element(e.Kind,e.Rect,null,e.TextSize,e.TextScale,F5Command.None,key):e,Command=button?commands[action]:Command.None,Enabled=enabled,Selected=button && action==selected});if(button)action++;}
        }
        private void Buttons(ref float y,string[] labels,Command[] commands,bool enabled)
        {var values=new List<F5Element>();new F5RowLayout(values,measure).Buttons(ref y,8,width-16,labels);for(int i=0;i<values.Count;i++)logical.Add(new Part{Element=values[i],Command=commands[i],Enabled=enabled});}
        private void Text(ref float y,string value)
        {var lines=new List<F5Element>();new F5RowLayout(lines,measure).TextLines(value,8,ref y,width-16,.65f);foreach(var e in lines)logical.Add(new Part{Element=e});y+=5;}
        internal void Project(F5Rect view,float scroll,List<ItemUiControl> controls,List<F5Element> elements)
        {
            visible.Clear();visibleTypes.Clear();
            for(int i=0;i<logical.Count;i++)
            {
                Part p=logical[i];var e=p.Element;if(e.Rect.Bottom<=scroll || e.Rect.Y>=scroll+view.Height)continue;
                var r=e.Rect.Offset(view.X,view.Y-scroll);var projected=new F5Element(e.Kind,r,e.Text,e.TextSize,e.TextScale,e.Command,e.HotkeyTarget,e.Description,e.HintRect.Offset(view.X,view.Y-scroll));
                visible.Add(new Part{Element=projected,Command=p.Command,Id=p.Id,Type=p.Type,Enabled=p.Enabled,Selected=p.Selected,Icon=p.Icon,Cross=p.Cross,Hint=p.Hint});
                if(p.Icon && p.Type>0)visibleTypes.Add(p.Type);
                if(p.Command!=Command.None || e.HotkeyTarget!=null)
                    controls.Add(new ItemUiControl{Command=e.HotkeyTarget!=null?ItemUiCommand.QuickHotkey:ItemUiCommand.Quick,Argument=i,Generation=generation,Rect=r,Element=projected,Enabled=p.Enabled});
            }
            removed.Clear();foreach(int type in icons.Keys)if(!visibleTypes.Contains(type))removed.Add(type);foreach(int type in removed)icons.Remove(type);
        }
        internal void PrepareIcons()
        {
            foreach(int type in visibleTypes)
            {Texture2D prior;if(!icons.TryGetValue(type,out prior) || prior==null || prior.IsDisposed || !ReferenceEquals(prior,TextureAssets.Item[type]?.Value))
                {Main.instance.LoadItem(type);icons[type]=TextureAssets.Item[type]?.Value;
#if DEBUG
                    IconLoads++;
#endif
                }}
        }
        internal void Draw(ItemsRenderer renderer,Vector2 pointer,Action<F5Rect> keyboard)
        {
            foreach(var part in visible)
            {
                var e=part.Element;bool hover=e.Rect.Contains(pointer.X,pointer.Y);
                if(part.Cross)continue;
                if(e.Kind==F5ElementKind.Hotkey){keyboard?.Invoke(e.Rect);continue;}
                if(part.Icon)
                {
                    renderer.ItemButton(e.Rect,part.Enabled,hover);Texture2D icon;
                    if(icons.TryGetValue(part.Type,out icon))renderer.PreparedItem(part.Type,icon,ItemsLayout.IconBounds(e.Rect,part.Command==Command.Pick));
                    if(part.Selected)renderer.Selection(e.Rect);
                    if(part.Command==Command.Edit && hover)renderer.Cross(new F5Rect(e.Rect.Right-ItemsLayout.CrossSize,e.Rect.Y,ItemsLayout.CrossSize,ItemsLayout.CrossSize),part.Enabled);
                }
                else if(e.Kind==F5ElementKind.Button)
                {
                    if(e.HotkeyTarget!=null && e.TextSize.Width>e.Rect.Width-8)
                    {renderer.Button(new F5Element(e.Kind,e.Rect,"",default(F5Size),e.TextScale,F5Command.None),false,part.Enabled,false,hover);renderer.Text(e.Text,e.Rect,Color.White,e.TextScale);}
                    else renderer.Button(e,part.Selected,part.Enabled,false,hover);
                }
                else if(e.Kind==F5ElementKind.Panel)renderer.Panel(e.Rect);
                else renderer.Label(e);
            }
        }
        internal string Hint(float x,float y,out F5Rect rect)
        {
            foreach(var p in visible)
            {if(p.Hint!=null && p.Element.Rect.Contains(x,y)){rect=p.Element.Rect;return p.Hint;}
                if(p.Element.Description!=null && p.Element.HintRect.Contains(x,y))
                {rect=p.Element.HintRect;return (p.Element.Description.Id==HostQuickItems.FavoriteAction?host.FavoriteHint:host.QuickHint)??p.Element.Description.Text;}}
            rect=default(F5Rect);return null;
        }
    }
}
