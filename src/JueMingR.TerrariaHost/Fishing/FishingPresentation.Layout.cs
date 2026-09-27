using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Fishing;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Fishing
{
    internal sealed partial class FishingPresentation
    {
        private F5Rect listLocal;
        private string playerName;
        private long renameRevision=-1;
        private bool renameAllowed;
        private long editRevision=-1,caretRevision=-1;
        private string composition,inputError,draftError,hostError;
        private float editWidth;
        private long projection;
        private static readonly FishPreset TrashPreset=new FishPreset(2,0,new FishList(new[]{new FishKey(FishKind.Item,ItemID.OldShoe),new FishKey(FishKind.Item,ItemID.Seaweed),new FishKey(FishKind.Item,ItemID.TinCan)}),"builtin-trash");
        private IReadOnlyList<FishPreset> presetSource;
        private FishPreset[] scopedPresets=Array.Empty<FishPreset>();
        private int presetMode,presetMatch;
        private sealed class PresetContent
        {internal readonly List<Part> Chips=new List<Part>();internal float Height;}
        private readonly Dictionary<FishPreset,PresetContent> presetDescriptions=new Dictionary<FishPreset,PresetContent>();
        private float presetWidth;
        private int presetSkin=-1;
        internal long PresetScans {get;private set;}
        internal long PresetDescriptions {get;private set;}
        private static readonly string[] help={"收竿后自动在原位抛竿","进入钓鱼后自动将保存的套装换成适合钓鱼的","进入钓鱼后自动将当前套装饰品换成适合钓鱼的","钓上的鱼获尝试放进附近箱子","有可用过滤模式下，尝试跳过不要的鱼获"};
        internal long LayoutBuilds {get;private set;}
        internal void Prepare(bool active,Matrix transform,Vector2 viewport)
        {
            if(!active || !shell.Visible || shell.Page!=7){if(ready || Visible || Editor!=null)Suspend();return;}
            if(!renderer.Refresh()){ready=false;return;}
            PrepareLayout(transform,viewport);PrepareIcons();
        }
        internal void PrepareLayout(Matrix transform,Vector2 viewport)
        {
            // Retire drafts before publishing the next generation or processing
            // a late save receipt. Prepare can precede the next input callback.
            if(session!=host.Tools.Runtime.Generation){CloseOverlay();message=null;listScroll=0;}
            matrix=transform;screen=viewport;PollReceipt();RefreshSearch();
            var next=shell.Layout.Viewport.Offset(shell.X,shell.Y);
            bool shape=dirty || hostError!=host.Error || inputError!=TextInput.Error || draftError!=draft?.Error || renameRevision!=host.Rename.Revision || renameAllowed!=host.Rename.CanRename || generation!=shell.Layout.Generation || skin!=renderer.Generation || revision!=host.Settings.Revision || canConfigure!=host.Controls || session!=host.Tools.Runtime.Generation || !ReferenceEquals(culture,Language.ActiveCulture) || playerName!=host.Player?.name || view.X!=next.X || view.Y!=next.Y || view.Width!=next.Width || view.Height!=next.Height;
            view=next;ready=true;
            if(shape)
            {
                LayoutBuilds++;armed=null;inputError=TextInput.Error;draftError=draft?.Error;hostError=host.Error;
                if(!SameScope() || Visible && host.Settings.Value.FilterMode==0){CloseOverlay();listScroll=0;}
                scopeMode=host.Settings.Value.EditingMode;scopeMatch=host.Settings.Value.Match;
                pageParts.Clear();listParts.Clear();popupParts.Clear();BuildPage();BuildPopup();
                revision=host.Settings.Revision;canConfigure=host.Controls;session=host.Tools.Runtime.Generation;generation=shell.Layout.Generation;skin=renderer.Generation;culture=Language.ActiveCulture;playerName=host.Player?.name;renameRevision=host.Rename.Revision;renameAllowed=host.Rename.CanRename;dirty=false;
            }
            if(shape || scroll!=shell.Scroll)
            {
                projection++;armed=null;Parts.Clear();editRect=default(F5Rect);
                listRect=F5HintLayout.Intersect(listLocal.Offset(view.X,view.Y-shell.Scroll),view);
                foreach(var p in pageParts)Project(p,view.X,view.Y-shell.Scroll,view);
                foreach(var p in listParts)Project(p,view.X+listLocal.X,view.Y+listLocal.Y-shell.Scroll-listScroll,listRect);
                foreach(var p in popupParts)Project(p,p.Region==3?popupBody.X:popupRect.X,p.Region==3?popupBody.Y-popupScroll:popupRect.Y,p.Region==3?popupBody:popupRect);
                scroll=shell.Scroll;
            }
            if(Editor!=null && editRect.Width>0 && (shape || editRevision!=draft.Revision || caretRevision!=draft.CaretRevision || composition!=TextInput.Composition || editWidth!=editRect.Width))
            {editView.Prepare(draft,TextInput.Composition,editRect.Width-12,shell.Layout.DynamicTextSize);editRevision=draft.Revision;caretRevision=draft.CaretRevision;composition=TextInput.Composition;editWidth=editRect.Width;}
        }
        private void Project(Part p,float dx,float dy,F5Rect clip)
        {
            var e=p.Element;var rect=e.Rect.Offset(dx,dy);if(rect.Right<=clip.X || rect.X>=clip.Right || rect.Bottom<=clip.Y || rect.Y>=clip.Bottom)return;
            var copy=new Part{Element=new F5Element(e.Kind,rect,e.Text,e.TextSize,e.TextScale,e.Command,e.HotkeyTarget,e.Description,e.HintRect.Offset(dx,dy)),
                Label=p.Label==null?null:new F5Element(F5ElementKind.Text,p.Label.Rect.Offset(dx,dy),p.Label.Text,p.Label.TextSize,p.Label.TextScale,F5Command.None),
                Command=p.Command,Feature=p.Feature,Value=p.Value,Mode=p.Mode,Match=p.Match,Region=p.Region,Fish=p.Fish,Name=p.Name,Hint=p.Hint,Preset=p.Preset,Enabled=p.Enabled,Selected=p.Selected,PlainIcon=p.PlainIcon,Ink=p.Ink};
            Parts.Add(copy);if(Editor!=null && (copy.Command==Command.Field || copy.Command==Command.RenameField && editing==Edit.Rename))editRect=rect;
        }
        private Part Make(F5Element element,Command command=Command.None,int region=0)
        {return new Part{Element=element,Command=command,Mode=scopeMode,Match=scopeMatch,Region=region,Enabled=PresentationAvailable && (!ListCommand(command) || host.Settings.Value.FilterMode!=0)};}
        private void BuildPage()
        {
            float y=0;
            for(int feature=0;feature<5;feature++)
            {
                var elements=new List<F5Element>();var labels=feature==3?new[]{"所有","任务鱼","关闭","键"}:new[]{"开启","关闭","键"};
                new F5RowLayout(elements,shell.Layout.TextSize).Row(ref y,0,view.Width,HostFishing.Names[feature],labels,description:new F5RowDescription(HostFishing.Actions[feature],help[feature]));
                foreach(var e in elements)
                {
                    var p=Make(e,e.Kind==F5ElementKind.Hotkey?Command.Hotkey:e.Kind==F5ElementKind.Button?Command.Feature:Command.None);
                    p.Feature=feature;p.Value=e.Text=="关闭"?0:e.Text=="任务鱼"?2:1;p.Selected=p.Command==Command.Feature && host.Settings.Value.State(feature)==p.Value;p.Hint=e.Description!=null?help[feature]:null;pageParts.Add(p);
                    if(feature==3 && p.Command==Command.Feature && p.Value!=0)p.Hint=p.Value==1?"会尝试把所有鱼获放进箱子":"仅会尝试把任务鱼获放进箱子";
                }
            }
            var nameRow=new List<F5Element>();new F5RowLayout(nameRow,shell.Layout.TextSize).Row(ref y,0,view.Width,"快捷改名",new[]{null,editing==Edit.Rename?"确定":"快捷改名"});
            foreach(var e in nameRow)
            {
                var p=Make(e,e.Kind==F5ElementKind.Field?Command.RenameField:e.Kind==F5ElementKind.Button?Command.Rename:Command.None);p.Enabled=CanRename?.Invoke()??false;
                p.Hint=p.Command==Command.RenameField?"双击编辑名字，回车或点击确定保存。":p.Command==Command.Rename?"未编辑时将名字末尾数字加一。":null;
                if(p.Command==Command.RenameField)p.Label=Fit(host.Player?.name??"",e.Rect,.7f,6);pageParts.Add(p);
            }
            string rename=(editing==Edit.Rename?TextInput.Error??draft?.Error:null)??RenameMessage?.Invoke();if(rename!=null)Text(pageParts,rename,0,ref y,view.Width);
            float top=y,left=y+8;
            float rightWidth=Math.Max(140,shell.Layout.DynamicTextSize("任务鱼：不要",.7f).Width+36);
            string matchLabel=scopeMatch==0?"精确匹配":"关键词";
            float titleWidth=shell.Layout.DynamicTextSize("当前过滤模式：",.7f).Width;
            float buttonMinimum=shell.Layout.DynamicTextSize("精确匹配",.7f).Width+16;
            bool side=view.Width-rightWidth-12>=Math.Max(300,titleWidth+buttonMinimum*2+32);float leftWidth=side?view.Width-rightWidth-12:view.Width;
            var leftPanel=Panel(new F5Rect(0,top,leftWidth,0));pageParts.Add(leftPanel);
            float toggleWidth=(leftWidth-32-titleWidth)/2;
            float headerHeight=Math.Max(30,shell.Layout.DynamicTextSize(matchLabel,.7f).Height+8);
            var heading=Fit("当前过滤模式：",new F5Rect(8,left,titleWidth,headerHeight),.7f,0);
            var headingPart=Make(heading);headingPart.Hint="点击右边按钮切换过滤模式，需要声呐buff";pageParts.Add(headingPart);
            var filterButton=Make(Element(F5ElementKind.Button,new F5Rect(16+titleWidth,left,toggleWidth,headerHeight),HostFishing.ModeName(5,host.Settings.Value.FilterMode)),Command.Filter);
            filterButton.Feature=5;filterButton.Selected=host.Settings.Value.FilterMode!=0;pageParts.Add(filterButton);
            filterButton.Hint=host.Settings.Value.FilterMode==1?"仅会尝试钓上名单内的物品":host.Settings.Value.FilterMode==2?"不会钓上名单内的物品":null;
            var matchButton=Make(Element(F5ElementKind.Button,new F5Rect(leftWidth-8-toggleWidth,left,toggleWidth,headerHeight),matchLabel),Command.Match);
            matchButton.Value=1-scopeMatch;matchButton.Hint=scopeMatch==0?"精确匹配名单内的物品":"匹配物品名包含名单内关键词的物品";pageParts.Add(matchButton);left+=headerHeight+6;
            string[] toolbar=scopeMatch==0?new[]{"添加当前","＋","清空","保存预设","预设列表"}:new[]{"＋","清空","保存预设","预设列表"};
            Command[] actions=scopeMatch==0?new[]{Command.Current,Command.Plus,Command.Clear,Command.Save,Command.PresetList}:new[]{Command.Plus,Command.Clear,Command.Save,Command.PresetList};
            Buttons(pageParts,ref left,8,leftWidth-16,toolbar,actions,configure:p=>{p.Hint=p.Command==Command.Current?"选择添加当前鱼获，需要先抛竿":p.Command==Command.Plus?(scopeMatch==0?"搜索物品并添加":"输入关键词"):p.Command==Command.Clear?"清空当前名单":null;},fill:true);
            pageParts.Add(Make(Element(F5ElementKind.Divider,new F5Rect(8,left+1,leftWidth-16,1))));left+=10;
            var list=host.Settings.Value.List(scopeMode,scopeMatch);int count=scopeMatch==0?list.Exact.Count:list.Keywords.Count;
            int columns=CardColumns(leftWidth-16,scopeMatch==0),rows=Math.Max(1,(count+columns-1)/columns);
            float cardHeight=CardHeight();float bodyHeight=Math.Max(cardHeight+8,Math.Min(228,rows*(cardHeight+4)));
            listHeight=rows*(cardHeight+4);listScroll=Math.Min(listScroll,Math.Max(0,listHeight-bodyHeight));
            listLocal=new F5Rect(8,left,leftWidth-16,bodyHeight);
            if(count==0){float zero=0;Text(listParts,"名单为空。",0,ref zero,listLocal.Width,1);}
            else
            {
                int first=Math.Max(0,(int)(listScroll/(cardHeight+4))*columns),last=Math.Min(count,first+((int)(bodyHeight/(cardHeight+4))+2)*columns);
                for(int i=first;i<last;i++)Card(listParts,new F5Rect(i%columns*(listLocal.Width+4)/columns,i/columns*(cardHeight+4),(listLocal.Width-4*(columns-1))/columns,cardHeight),scopeMatch==0?(FishKey?)list.Exact[i]:null,scopeMatch==0?FishingCatalog.Name(list.Exact[i]):list.Keywords[i],Command.Remove,1);
            }
            left+=bodyHeight+8;string status=host.Settings.Message??host.Error??message;if(status!=null)Text(pageParts,status,8,ref left,leftWidth-16);
            float right=side?top+8:left+20,rx=side?leftWidth+12:0,rw=side?rightWidth:view.Width;float rightTop=right-8;
            var rightPanel=Panel(new F5Rect(rx,rightTop,rw,0));pageParts.Add(rightPanel);
            Text(pageParts,"特殊规则",rx+8,ref right,rw-16);
            foreach(int feature in new[]{7,9,8})
            {
                string name=feature==7?"匣子":feature==8?"任务鱼":"怪物";int state=host.Settings.Value.State(feature);string mode=state==0?"跟随":state==1?"要":"不要";
                Buttons(pageParts,ref right,rx+8,rw-16,new[]{name+"："+mode},new[]{Command.Special},0,p=>{p.Feature=feature;p.Hint=state==0?"按照黑白名单过滤"+name+"，需要声呐buff":"忽略黑白名单，"+(state==1?"会":"不会")+"钓上"+name+"，需要声呐buff";},fill:true);
            }
            float bottom=Math.Max(left,right+8);leftPanel.Element=Element(F5ElementKind.Panel,new F5Rect(0,top,leftWidth,left-top));
            rightPanel.Element=Element(F5ElementKind.Panel,new F5Rect(rx,rightTop,rw,right+8-rightTop));
            shell.Layout.SetFishingContentHeight(bottom+6);shell.ClampScroll();
        }
        private void BuildPopup()
        {
            if(!Visible)return;
            var presets=overlay==Overlay.Presets?ScopedPresets():Array.Empty<FishPreset>();
            float nameWidth=0,contentWidth=0,deleteWidth=0;
            if(overlay==Overlay.Presets)
            {
                deleteWidth=24;
                for(int i=0;i<presets.Length;i++)
                {
                    var preset=presets[i];nameWidth=Math.Max(nameWidth,shell.Layout.DynamicTextSize(PresetName(preset,i),.7f).Width+8);
                    float natural=preset.Match==0?preset.Content.Exact.Count*40-4:preset.Content.Keywords.Sum(word=>shell.Layout.DynamicTextSize(word,.7f).Width+20)-4;
                    contentWidth=Math.Max(contentWidth,Math.Min(236,natural));
                }
                nameWidth=Math.Max(64,nameWidth);contentWidth=Math.Max(76,contentWidth);
            }
            float desiredWidth=overlay==Overlay.Presets?Math.Max(260,Math.Min(460,24+nameWidth+12+contentWidth+deleteWidth+8)):overlay==Overlay.Keyword?340:overlay==Overlay.Current?Math.Min(420,Math.Max(260,Math.Min(3,candidates.Length)*136+24)):420;
            float width=Math.Min(desiredWidth,screen.X/matrix.M11-24),bodyWidth=width-24;
            if(presetWidth!=bodyWidth || presetSkin!=renderer.Generation){presetDescriptions.Clear();presetWidth=bodyWidth;presetSkin=renderer.Generation;}
            float y=10;string title=overlay==Overlay.Search?"添加鱼获":overlay==Overlay.Current?"当前水域候选":overlay==Overlay.Keyword?"添加关键词":"名单预设";
            if(overlay==Overlay.Presets)
            {
                float closeWidth=shell.Layout.DynamicTextSize("关闭",.6f).Width+16;
                var titleSize=shell.Layout.DynamicTextSize(title,.85f);
                popupParts.Add(Make(Fit(title,new F5Rect(14,y,titleSize.Width,titleSize.Height+4),.85f,0),region:2));
                var close=Make(Element(F5ElementKind.Button,new F5Rect(width-closeWidth-12,y,closeWidth,titleSize.Height+4)),Command.Close,2);close.Label=Fit("关闭",close.Element.Rect,.6f,4);close.Enabled=true;popupParts.Add(close);
                string context=(scopeMode==1?"白名单":"黑名单")+" · "+(scopeMatch==0?"精确匹配":"关键词");
                var contextSize=shell.Layout.DynamicTextSize(context,.55f);float contextX=22+titleSize.Width;
                bool inline=contextX+contextSize.Width+8<=close.Element.Rect.X;
                var contextPart=Make(Fit(context,new F5Rect(inline?contextX:14,inline?y:y+titleSize.Height+6,inline?contextSize.Width+1:bodyWidth,inline?titleSize.Height+4:contextSize.Height),.55f,0),region:2);contextPart.Ink=Color.LightSteelBlue;popupParts.Add(contextPart);
                y+=titleSize.Height+10+(inline?0:contextSize.Height+6);
                popupParts.Add(Make(Element(F5ElementKind.Divider,new F5Rect(14,y,bodyWidth-4,1)),region:2));y+=5;
                nameWidth=Math.Min(nameWidth,Math.Max(64,bodyWidth*.34f));
                contentWidth=bodyWidth-nameWidth-12-deleteWidth-8;
            }
            else Text(popupParts,title+" · "+(scopeMode==1?"白名单":"黑名单"),12,ref y,width-24,2);
            if(overlay==Overlay.Search || overlay==Overlay.Keyword)
            {
                var field=Make(Element(F5ElementKind.Field,new F5Rect(12,y,width-24,CardHeight())),Command.Field,2);field.Enabled=true;
                if(Editor==null)field.Label=Fit(draft?.Text??"",field.Element.Rect,.7f,6);popupParts.Add(field);y+=CardHeight()+6;
            }
            string popupMessage=host.Settings.Message??TextInput.Error??draft?.Error??message;
            var footer=new List<Part>();float fy=0;
            if(overlay!=Overlay.Presets)Buttons(footer,ref fy,12,width-24,overlay==Overlay.Keyword?new[]{"确认","取消"}:new[]{"添加至名单","取消"},overlay==Overlay.Keyword?new[]{Command.Confirm,Command.Close}:new[]{Command.Add,Command.Close},2,p=>{if(p.Command==Command.Close)p.Enabled=true;else if(p.Command==Command.Add)p.Enabled=PresentationAvailable && selected.Count>0;},fill:true);
            // Errors belong to the scrolling body; long file-system messages
            // cannot push the input field or confirmation footer out of reach.
            // Only the title/input and footer are fixed. Explanations, counts
            // and errors scroll with the body, preserving a usable short view.
            float bodyStart=0;
            string summary=overlay==Overlay.Presets || overlay==Overlay.Keyword?null:
                (overlay==Overlay.Search?"名称 / #ID · ":"")+(candidates.Length>96?"共 "+candidates.Length+" 项，显示前 96 项；已选 "+selected.Count+" 项":"已选 "+selected.Count+" 项");
            if(summary!=null){Text(popupParts,summary,0,ref bodyStart,bodyWidth,3);bodyStart+=4;}
            if(popupMessage!=null){Text(popupParts,popupMessage,0,ref bodyStart,bodyWidth,3);bodyStart+=6;}
            float row=CardHeight()+4;int count=Math.Min(96,candidates.Length),columns=CardColumns(bodyWidth,true);
            popupHeight=bodyStart;
            if(overlay==Overlay.Presets)foreach(var preset in presets)popupHeight+=PreparePreset(preset,contentWidth).Height+6;
            else popupHeight+=((count+columns-1)/columns)*row;
            if(overlay==Overlay.Presets?presets.Length==0:count==0 && popupMessage==null)
            {
                string empty=overlay==Overlay.Presets?"当前作用域还没有预设。":overlay==Overlay.Search?(string.IsNullOrWhiteSpace(draft?.Text)?"输入后显示可钓候选。":"没有匹配的可钓候选。"):overlay==Overlay.Keyword?"确认后加入当前关键词名单。":"当前没有可用候选。";
                Text(popupParts,empty,0,ref popupHeight,bodyWidth,3);
            }
            float height=Math.Min(Math.Min(450,screen.Y/matrix.M22-24),y+Math.Max(CardHeight(),popupHeight)+fy+14);
            popupRect=new F5Rect((screen.X/matrix.M11-width)/2,(screen.Y/matrix.M22-height)/2,width,height);
            float footerY=height-fy-8;foreach(var p in footer){p.Element=Move(p.Element,0,footerY);popupParts.Add(p);}
            popupBody=new F5Rect(popupRect.X+12,popupRect.Y+Math.Min(y,footerY),bodyWidth,Math.Max(0,footerY-y-6));
            popupScroll=Math.Min(popupScroll,Math.Max(0,popupHeight-popupBody.Height));
            if(overlay==Overlay.Presets)
            {
                float py=bodyStart;
                for(int i=0;i<presets.Length;i++)
                {
                    var preset=presets[i];bool builtin=ReferenceEquals(preset,TrashPreset);float pw=bodyWidth-deleteWidth-8;var content=PreparePreset(preset,contentWidth);
                    if(py+content.Height>=popupScroll && py<popupScroll+popupBody.Height)
                    {
                        var p=Make(Element(F5ElementKind.Panel,new F5Rect(0,py,pw,content.Height)),Command.ApplyPreset,3);p.Preset=preset;popupParts.Add(p);
                        var label=Make(Fit(PresetName(preset,i),new F5Rect(4,py+6,nameWidth-4,CardHeight()-12),.7f,0),region:3);popupParts.Add(label);
                        foreach(var chip in content.Chips)
                        {
                            if(py+chip.Element.Rect.Bottom<popupScroll || py+chip.Element.Rect.Y>=popupScroll+popupBody.Height)continue;
                            var copy=Make(Move(chip.Element,nameWidth+12,py),region:3);copy.Fish=chip.Fish;copy.PlainIcon=chip.Fish.HasValue;copy.Label=chip.Label==null?null:Move(chip.Label,nameWidth+12,py);popupParts.Add(copy);
                        }
                        if(!builtin){var remove=Make(Element(F5ElementKind.Button,new F5Rect(bodyWidth-deleteWidth,py+2,deleteWidth,CardHeight())),Command.DeletePreset,3);remove.Name=preset.Name;popupParts.Add(remove);}
                        if(i<presets.Length-1)popupParts.Add(Make(Element(F5ElementKind.Divider,new F5Rect(4,py+content.Height+2,bodyWidth-8,1)),region:3));
                    }
                    py+=content.Height+6;
                }
            }
            else if(count>0)
            {
                int first=Math.Max(0,(int)((popupScroll-bodyStart)/row)*columns),last=Math.Min(count,first+((int)(popupBody.Height/row)+2)*columns);
                for(int i=first;i<last;i++)Card(popupParts,new F5Rect(i%columns*(popupBody.Width+4)/columns,bodyStart+i/columns*row,(popupBody.Width-4*(columns-1))/columns,CardHeight()),candidates[i],FishingCatalog.Name(candidates[i]),Command.Select,3);
            }
        }
        private string PresetName(FishPreset preset,int index)
        {return ReferenceEquals(preset,TrashPreset)?"低渔力垃圾":"预设 "+(index+1-(scopeMode==2 && scopeMatch==0?1:0));}
        private FishPreset[] ScopedPresets()
        {
            var source=host.Settings.Value.Presets;
            if(ReferenceEquals(presetSource,source) && presetMode==scopeMode && presetMatch==scopeMatch)return scopedPresets;
            if(!ReferenceEquals(presetSource,source))presetDescriptions.Clear();
            presetSource=source;presetMode=scopeMode;presetMatch=scopeMatch;PresetScans++;
            var values=source.Where(p=>p.Mode==scopeMode && p.Match==scopeMatch);
            scopedPresets=(scopeMode==2 && scopeMatch==0?new[]{TrashPreset}.Concat(values):values).ToArray();return scopedPresets;
        }
        private PresetContent PreparePreset(FishPreset preset,float width)
        {
            PresetContent content;if(presetDescriptions.TryGetValue(preset,out content))return content;
            content=new PresetContent();float x=0,y=2,rowHeight=0,available=Math.Max(36,width);
            int count=preset.Match==0?preset.Content.Exact.Count:preset.Content.Keywords.Count;
            for(int i=0;i<count;i++)
            {
                string word=preset.Match==0?null:preset.Content.Keywords[i];
                float w=word==null?36:Math.Min(available,(float)Math.Ceiling(shell.Layout.DynamicTextSize(word,.7f).Width)+20);
                var lines=word==null?Array.Empty<string>():PresetWordLines(word,w-16);
                float lineHeight=word==null?0:shell.Layout.DynamicTextSize(word,.7f).Height+2;
                float height=Math.Max(CardHeight(),lines.Length*lineHeight+10);
                if(x>0 && x+w>available){x=0;y+=rowHeight+4;rowHeight=0;}
                var chip=Make(Element(F5ElementKind.Button,new F5Rect(x,y,w,height)),region:3);
                if(word==null)chip.Fish=preset.Content.Exact[i];else chip.Label=Fit(lines[0],new F5Rect(x,y+5,w,lineHeight),.7f,8);
                content.Chips.Add(chip);
                for(int line=1;line<lines.Length;line++)content.Chips.Add(Make(Fit(lines[line],new F5Rect(x,y+5+line*lineHeight,w,lineHeight),.7f,8),region:3));
                rowHeight=Math.Max(rowHeight,height);x+=w+4;
            }
            content.Height=Math.Max(CardHeight()+4,y+rowHeight+2);presetDescriptions.Add(preset,content);PresetDescriptions++;return content;
        }
        private string[] PresetWordLines(string word,float width)
        {
            // Presets deliberately have no tooltips: an oversized keyword must
            // remain readable inside its capsule. Normally keep text elements
            // together; a pathological over-wide combining/ZWJ element alone
            // falls back to scalar boundaries, never dropping text or splitting
            // a UTF-16 surrogate pair to manufacture an ellipsis.
            var lines=new List<string>();var elements=TextElements.Boundaries(word);var stops=new List<int>{0};
            for(int i=1;i<elements.Length;i++)
            {
                int from=elements[i-1],end=elements[i];
                if(shell.Layout.DynamicTextSize(word.Substring(from,end-from),.7f).Width<=width)stops.Add(end);
                else while(from<end){from+=char.IsHighSurrogate(word[from]) && from+1<end && char.IsLowSurrogate(word[from+1])?2:1;stops.Add(from);}
            }
            var boundaries=stops.ToArray();int start=0;
            while(start<boundaries.Length-1)
            {
                int low=start+1,high=boundaries.Length-1;
                while(low<high){int mid=(low+high+1)/2;string part=word.Substring(boundaries[start],boundaries[mid]-boundaries[start]);if(shell.Layout.DynamicTextSize(part,.7f).Width<=width)low=mid;else high=mid-1;}
                lines.Add(word.Substring(boundaries[start],boundaries[low]-boundaries[start]));start=low;
            }
            return lines.ToArray();
        }
        private float CardHeight(){return Math.Max(36,shell.Layout.TextSize("鱼获",.7f).Height+12);}
        private int CardColumns(float width,bool icons)
        {float minimum=icons?Math.Max(112,36+shell.Layout.DynamicTextSize("鱼获",.7f).Width+28):140;return Math.Max(1,Math.Min(icons?3:2,(int)((width+4)/(minimum+4))));}
        private void Card(List<Part> list,F5Rect rect,FishKey? fish,string name,Command command,int region)
        {
            var p=Make(Element(F5ElementKind.Button,rect),command,region);p.Fish=fish;p.Name=name;p.Selected=fish.HasValue && selected.Contains(fish.Value);p.Hint=name+(fish.HasValue?" #"+fish.Value.Id+(fish.Value.Kind==FishKind.Npc?"（NPC）":""):"");
            float inset=fish.HasValue?36:6;p.Label=Fit(name,new F5Rect(rect.X+inset,rect.Y,rect.Width-inset-(command==Command.Remove?22:8),rect.Height),.7f,0);
            if(command==Command.Remove)
            {
                var remove=Make(Element(F5ElementKind.Button,new F5Rect(rect.Right-24,rect.Y,24,rect.Height)),Command.Remove,region);remove.Fish=fish;remove.Name=name;remove.Hint="移除「"+name+"」";list.Add(remove);p.Command=Command.None;
            }
            list.Add(p);
        }
        private Part Panel(F5Rect rect){return Make(Element(F5ElementKind.Panel,rect));}
        private F5Element Element(F5ElementKind kind,F5Rect rect,string text=null)
        {return new F5Element(kind,rect,text,text==null?default(F5Size):shell.Layout.DynamicTextSize(text,.7f),.7f,F5Command.None);}
        private static F5Element Move(F5Element e,float x,float y){return new F5Element(e.Kind,e.Rect.Offset(x,y),e.Text,e.TextSize,e.TextScale,e.Command,e.HotkeyTarget,e.Description,e.HintRect.Offset(x,y));}
        private F5Element Fit(string text,F5Rect rect,float scale,float inset)
        {
            string fitted=text;var size=shell.Layout.DynamicTextSize(fitted,scale);var boundaries=TextElements.Boundaries(text);
            if(size.Width>rect.Width-inset*2)
            {
                int low=0,high=boundaries.Length-2;
                while(low<high){int mid=(low+high+1)/2;var measured=shell.Layout.DynamicTextSize(text.Substring(0,boundaries[mid])+"…",scale);if(measured.Width<=rect.Width-inset*2)low=mid;else high=mid-1;}
                fitted=text.Substring(0,boundaries[low])+"…";size=shell.Layout.DynamicTextSize(fitted,scale);
            }
            return new F5Element(F5ElementKind.Text,new F5Rect(rect.X+inset,rect.Y+(rect.Height-size.Height)/2,size.Width,size.Height),fitted,size,scale,F5Command.None);
        }
        private void Text(List<Part> target,string text,float x,ref float y,float width,int region=0)
        {var elements=new List<F5Element>();new F5RowLayout(elements,shell.Layout.DynamicTextSize).TextLines(text,x,ref y,width,.7f);foreach(var e in elements)target.Add(Make(e,Command.None,region));}
        private void Buttons(List<Part> target,ref float y,float x,float width,string[] labels,Command[] commands,int region=0,Action<Part> configure=null,bool fill=false)
        {
            var elements=new List<F5Element>();new F5RowLayout(elements,shell.Layout.DynamicTextSize).Buttons(ref y,x,width,labels);
            // Preserve readable native text widths, then share each row's spare
            // width equally. The compact plus button does not crowd long labels.
            if(fill)for(int first=0;first<elements.Count;)
            {
                int end=first+1;while(end<elements.Count && elements[end].Rect.Y==elements[first].Rect.Y)end++;
                float extra=Math.Max(0,x+width-elements[end-1].Rect.Right)/(end-first),cursor=x;
                for(int i=first;i<end;i++){var e=elements[i];elements[i]=new F5Element(e.Kind,new F5Rect(cursor,e.Rect.Y,e.Rect.Width+extra,e.Rect.Height),e.Text,e.TextSize,e.TextScale,e.Command);cursor+=e.Rect.Width+extra+4;}first=end;
            }
            for(int i=0;i<elements.Count;i++){var p=Make(elements[i],commands[i],region);configure?.Invoke(p);target.Add(p);}
        }
    }
}
