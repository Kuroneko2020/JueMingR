using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private float contentY;
        private bool bodyOffsetClamped;
        private CompanionProfile CurrentProfile()
        {
            var profiles = host.Profiles;
            for (int i = 0; i < profiles.Count; i++) if (profiles[i].Id == selected) return profiles[i];
            return null;
        }
        private void BuildSelected(float row)
        {
            field = default(F5Rect); contentY = bodyView.Y - bodyOffset;
            var profile = CurrentProfile();
            if (page == 0)
            {
                BuildActor(Main.LocalPlayer, true, ownerView, ownerOffset, row);
                BuildActor(profile == null ? null : host.GetBody(selected), false, companionView, bodyOffset, row);
                return;
            }
            if (profile == null) { BodyText("点击左下角＋创建队友", row); FinishBody(); return; }
            BodyText(profile.Name + " · " + host.Status(selected), row);
            if (host.SaveFailed(selected)) BodyButton("retry-save", "保存失败 · 重试保存", row, () => Command(() => host.RetrySave(selected)));
            Pair("join", "加入当前世界", () => Command(() => host.Join(selected)), false,
                "recall", "收回", () => Command(() => host.Recall(selected)), false, row);
            BuildConfiguration(profile, row); FinishBody();
        }
        private void BuildActor(Player body, bool owner, F5Rect view, int offset, float row)
        {
            // The same final rectangles determine visible cells, clipping and hit
            // targets. Scrolling projects native slot identities; it never renumbers them.
            float y = view.Y - offset, x = view.X + 8, width = view.Width - 16;
            string name = owner ? Main.LocalPlayer?.name ?? "我的人物" : CurrentProfile()?.Name ?? "AI 队友";
            ActorText(name, new F5Rect(x,y,width - (owner ? 0 : 74),row), view);
            if (!owner && CurrentProfile() != null)
            {
                bool joined = host.CanTransfer(selected);
                var r = new F5Rect(view.Right - 76,y,68,row);
                if (ActorVisible(r,view)) Button(joined ? "recall" : "join", joined ? "收回" : "加入",r,
                    () => Command(() => joined ? host.Recall(selected) : host.Join(selected)));
            }
            y += row + 4;
            float heroHeight=Math.Max(150,body==null?150:CompanionResourceDisplay.MeasureHeight(body,Math.Max(20,width-98))+8);
            var hero = new F5Rect(x,y,width,heroHeight);
            if (owner) ownerHero = hero; else companionHero = hero;
            y += heroHeight+4;
            if (!owner && !host.CanTransfer(selected)) { ActorText(host.Status(selected),new F5Rect(x,y,width,26),view); y += 28; }
            var tabs = new[] { "装备", "时装", "特殊" }; int gear = owner ? ownerGearPage : companionGearPage;
            float tw = Math.Min(86,(width - 8) / 3);
            for (int t=0;t<3;t++)
            {
                int target=t; var r = new F5Rect(x+t*(tw+4),y,tw,28);
                if (ActorVisible(r,view)) Button("gear:"+owner+":"+t,tabs[t],r,() => {
                    host.CancelItemGesture(); if(owner) ownerGearPage=target; else companionGearPage=target; Invalidate();
                },true,gear==t);
            }
            y += 32;
            if (body != null)
            {
                if (gear == 0) { ActorSlots(body,owner,CompanionInventoryArea.Armor,0,Math.Min(10,body.armor.Length),ref y,view); ActorSlots(body,owner,CompanionInventoryArea.Dye,0,body.dye.Length,ref y,view); }
                else if (gear == 1) { ActorSlots(body,owner,CompanionInventoryArea.Armor,10,Math.Max(0,body.armor.Length-10),ref y,view); ActorSlots(body,owner,CompanionInventoryArea.Dye,0,body.dye.Length,ref y,view); }
                else { ActorSlots(body,owner,CompanionInventoryArea.Misc,0,body.miscEquips.Length,ref y,view); ActorSlots(body,owner,CompanionInventoryArea.MiscDye,0,body.miscDyes.Length,ref y,view); }
            }
            ActorText("背包",new F5Rect(x,y,width,26),view); y += 28;
            if (body != null) ActorSlots(body,owner,CompanionInventoryArea.Inventory,0,50,ref y,view);
            ActorText("钱币 / 弹药",new F5Rect(x,y,width,26),view); y += 28;
            if (body != null) ActorSlots(body,owner,CompanionInventoryArea.Inventory,50,8,ref y,view);
            int maximum=Math.Max(0,(int)Math.Ceiling(y + offset + 8 - view.Bottom));
            if (owner) { ownerMaximum=maximum; if(ownerOffset>maximum) { ownerOffset=maximum; bodyOffsetClamped=true; Invalidate(); } }
            else { bodyMaximum=maximum; if(bodyOffset>maximum) { bodyOffset=maximum; bodyOffsetClamped=true; Invalidate(); } }
        }
        private void ActorSlots(Player body, bool owner, CompanionInventoryArea area, int start, int count, ref float y, F5Rect view)
        {
            // Ten native inventory columns remain readable; a narrow viewport
            // shows fewer columns using the same layout, with vertical scrolling.
            int columns=Math.Max(1,Math.Min(10,(int)((view.Width-16)/40)));
            float cell=Math.Min(44,(view.Width-16)/columns);
            for(int i=0;i<count;i++)
            {
                int slot=start+i;
                var rect=new F5Rect(view.X+8+i%columns*cell,y+i/columns*cell,cell-3,cell-3);
                if(!ActorVisible(rect,view)) continue;
                Item item=ReadSlotPreview(body,owner,area,slot);
                var c=Button("slot:"+owner+":"+area+":"+slot,"",rect,null,true,
                    !owner && area==CompanionInventoryArea.Inventory && slot==body.selectedItem);
                c.IsSlot=true; c.Owner=owner; c.Area=area; c.Slot=slot; c.Element=null;
                ApplyItemPresentation(c,body,item); icons.AddVisible(c.ItemType);
            }
            y+=(float)Math.Ceiling((double)count/columns)*cell+4;
        }
        private Item ReadSlotPreview(Player player,bool owner,CompanionInventoryArea area,int slot)
        {
            // Idle presentation reuses its already resolved character instead of
            // finding that same profile for every cell. An active gesture reads
            // the Host's projection; all mutations still use its atomic outlet.
            if(host.HasItemGesture) return host.ItemPreview(selected,owner,area,slot);
            var items=CompanionResources.Items(player,area);return slot>=0&&slot<items.Length?items[slot]:null;
        }
        private bool ApplyItemPresentation(Control c,Player body,Item item)
        {
            int type=item?.type??0,stack=item?.stack??0,prefix=item?.prefix??0;
            bool locked=(c.Area==CompanionInventoryArea.Armor||c.Area==CompanionInventoryArea.Dye)&&!body.IsItemSlotUnlockedAndUsable(c.Slot);
            bool enabled=(c.Owner||host.CanTransfer(selected))&&!locked;
            bool chosen=!c.Owner&&c.Area==CompanionInventoryArea.Inventory&&c.Slot==body.selectedItem;
            if(c.Prefix==prefix&&c.ItemType==type&&c.Stack==stack&&c.Enabled==enabled&&c.Selected==chosen) return false;
            bool changedType=c.ItemType!=type;
            c.ItemType=type;c.Stack=stack;c.StackText=stack.ToString();c.Prefix=prefix;c.Enabled=enabled;c.Selected=chosen;
            c.Hint=locked?"此饰品槽尚未解锁。":!c.Owner&&!host.CanTransfer(selected)?"未加入，只能查看。":SlotHint(c.Area,c.Slot,item,c.Owner);
            c.Placeholder=type!=0?null:c.Area==CompanionInventoryArea.Dye||c.Area==CompanionInventoryArea.MiscDye?"染":c.Area==CompanionInventoryArea.Misc?new[]{"宠","光","车","坐","钩"}[c.Slot]:c.Area==CompanionInventoryArea.Armor?(c.Slot%10==0?"头":c.Slot%10==1?"衣":c.Slot%10==2?"裤":"饰"):c.Area==CompanionInventoryArea.Inventory&&c.Slot>=50?(c.Slot<54?"币":"弹"):null;
            ItemPresentationUpdates++;return changedType;
        }
        private void RefreshItemPresentation(bool resources)
        {
            bool types=false;
            if(!creating&&page==0)
            {
                Player owner=Main.LocalPlayer,body=host.GetBody(selected);
                foreach(var c in controls) if(c.IsSlot)
                {
                    Player player=c.Owner?owner:body;
                    if(player!=null) types|=ApplyItemPresentation(c,player,ReadSlotPreview(player,c.Owner,c.Area,c.Slot));
                }
            }
            if(types) {icons.BeginVisibleSet();foreach(var c in controls)icons.AddVisible(c.ItemType);icons.EndVisibleSet();}
            if(resources) icons.Prepare();
        }
        private static string SlotHint(CompanionInventoryArea area,int slot,Item item,bool owner)
        {
            string text=item==null||item.IsAir ? area==CompanionInventoryArea.Dye||area==CompanionInventoryArea.MiscDye ? "染料" : "空格" : item.Name+" ×"+item.stack;
            if (!owner && area==CompanionInventoryArea.Inventory && slot<50 && item!=null && !item.IsAir) text+="\nAlt＋左键设为武器";
            return text;
        }
        private static bool ActorVisible(F5Rect rect,F5Rect view) { return rect.Y>=view.Y && rect.Bottom<=view.Bottom && rect.X>=view.X && rect.Right<=view.Right; }
        private void ActorText(string text,F5Rect rect,F5Rect view) { if(ActorVisible(rect,view)) Text(text,rect,.65f); }
        private void ProcessItemInput(Control hit, Microsoft.Xna.Framework.Input.KeyboardState keys, bool left,bool right,bool wasDragging,bool resources)
        {
            bool changed=false;
            if(creating||page!=0||editor!=null||wasDragging) { host.EndItemRepeat(); rightSlot=null; return; }
            if(hit==null||!hit.IsSlot) { host.EndItemRepeat(); rightSlot=null; rightRepeat=0; return; }
            if(left&&!previousLeft)
            {
                armedKey=null;
                if((keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightAlt)) && !hit.Owner && hit.Area==CompanionInventoryArea.Inventory && hit.Slot<50 && !host.HasItemGesture)
                { host.SelectWeapon(selected,hit.Slot); changed=true; }
                else { bool shift=keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                    host.ItemGesture(selected,hit.Owner,hit.Area,hit.Slot,shift?CompanionItemGesture.Shift:CompanionItemGesture.Left); changed=true; }
            }
            if(right)
            {
                bool fresh=!previousRight;
                if(fresh) { host.EndItemRepeat(); rightSlot=hit.Key; rightRepeat=0; }
                if(rightSlot!=null&&rightSlot!=hit.Key) { host.EndItemRepeat(); rightSlot=null; rightRepeat=0; }
                if(rightSlot==hit.Key && (fresh||++rightRepeat>=18 && (rightRepeat-18)%6==0))
                { host.ItemGesture(selected,hit.Owner,hit.Area,hit.Slot,CompanionItemGesture.Right); changed=true; }
            }
            else { host.EndItemRepeat(); rightSlot=null; rightRepeat=0; }
            if(changed) {RefreshItemPresentation(resources);RefreshNotice();}
        }
        private void DrawHero(F5Rect view,F5Rect hero,CompanionPreview preview,Player source)
        {
            if(hero.Bottom<=view.Y||hero.Y>=view.Bottom) return;
            var portrait=new F5Rect(hero.X,hero.Y,90,hero.Height);
            renderer.Pass(matrix,view,()=> {
                renderer.Panel(portrait);
                if(source!=null) resourceDisplay.Draw(renderer,source,new F5Rect(hero.X+98,hero.Y+4,Math.Max(20,hero.Width-98),hero.Height-8));
            });
            var clip=new F5Rect(Math.Max(view.X,portrait.X),Math.Max(view.Y,portrait.Y),Math.Min(view.Right,portrait.Right)-Math.Max(view.X,portrait.X),Math.Min(view.Bottom,portrait.Bottom)-Math.Max(view.Y,portrait.Y));
            if(clip.Width>0&&clip.Height>0) renderer.Pass(matrix,clip,()=>preview.Draw(portraits,portrait,matrix));
        }
        private void BuildConfiguration(CompanionProfile profile, float row)
        {
            var behavior = profile.Behavior;
            BodyText("移动", row);
            Pair("follow", "跟随主人", () => Behavior(true, null, null, null), behavior.Follow,
                "stay", "原地停留", () => Behavior(false, null, null, null), !behavior.Follow, row);
            BodyText("攻击", row);
            Pair("attack-on", "开启", () => Behavior(null, true, null, null), behavior.AttackEnabled,
                "attack-off", "关闭", () => Behavior(null, false, null, null), !behavior.AttackEnabled, row, true, true);
            BodyText("攻击模式", row);
            Pair("proactive", "主动攻击", () => Behavior(null, null, false, null), !behavior.SelfDefense,
                "defensive", "仅自卫", () => Behavior(null, null, true, null), behavior.SelfDefense, row);
            BodyText("目标优先", row);
            Pair("prefer-owner", "主人附近优先", () => Behavior(null, null, null, true), behavior.PreferOwner,
                "prefer-self", "自身附近优先", () => Behavior(null, null, null, false), !behavior.PreferOwner, row);
            var body = host.GetBody(selected);
            if (body != null && body.selectedItem >= 0 && body.selectedItem < body.inventory.Length)
                BodyText("当前武器：" + body.inventory[body.selectedItem].Name, row);
            BodyButton("password", editor != null && editKind == "password" ? "" : "连接密码：点击输入", row,
                () => BeginEdit("password", host.ConnectionPassword ?? ""));
            if (editKind == "password")
            {
                field = new F5Rect(bodyView.X + 5, contentY - row - 8, bodyView.Width - 10, row);
                BodyButton("save-password", "应用密码", row, () => RequestFinish());
            }
            BodyButton("restore-behavior", "恢复行为设置", row, () => Command(() => host.RestoreBehavior(selected)));
            if (!confirmingReset) BodyButton("request-reset", "重置人物档案", row, () => { confirmingReset = true; Invalidate(); });
            else
            {
                BodyText("将清空物品、装备、成长和 AI 设置。", row);
                BodyText("保留名字、外观、难度和归属。", row);
                Pair("confirm-reset", "确认重置", () => { if (host.Reset(selected)) confirmingReset = false; notice = null; Invalidate(); }, false,
                    "cancel-reset", "取消", () => { confirmingReset = false; Invalidate(); }, false, row);
            }
        }
        private void Behavior(bool? follow, bool? attack, bool? defense, bool? prefer)
        {
            var profile = CurrentProfile(); if (profile == null) return;
            var b = profile.Behavior;
            Command(() => host.SetBehavior(selected, new CompanionBehavior {
                Follow = follow ?? b.Follow, AttackEnabled = attack ?? b.AttackEnabled,
                SelfDefense = defense ?? b.SelfDefense, PreferOwner = prefer ?? b.PreferOwner }));
        }
        private void Command(Func<bool> command)
        { command(); notice = null; Invalidate(); }
        private F5Rect BodyRect(float row) { return new F5Rect(bodyView.X, contentY, bodyView.Width, row); }
        private bool FullyVisible(F5Rect rect) { return rect.Y >= bodyView.Y && rect.Bottom <= bodyView.Bottom; }
        private void BodyText(string text, float row)
        { var rect = BodyRect(row); if (FullyVisible(rect)) Text(text, rect); contentY += row + 4; }
        private void BodyButton(string key, string text, float row, Action action, bool enabled = true)
        { var rect = BodyRect(row); if (FullyVisible(rect)) Button(key, text, rect, action, enabled); contentY += row + 8; }
        private void Pair(string key1, string text1, Action action1, bool selected1, string key2, string text2, Action action2, bool selected2,
            float row, bool enabled = true, bool offSecond = false)
        {
            float width = (bodyView.Width - 6) / 2;
            // The shared button renderer converts its underline to exactly two
            // device pixels using this pass's actual UI matrix.
            var first = new F5Rect(bodyView.X, contentY, width, row);
            var second = new F5Rect(bodyView.X + width + 6, contentY, width, row);
            if (FullyVisible(first)) Button(key1, text1, first, action1, enabled, selected1);
            if (FullyVisible(second)) Button(key2, text2, second, action2, enabled, selected2, offSecond);
            contentY += row + 8;
        }
        private void FinishBody()
        {
            int maximum = Math.Max(0, (int)Math.Ceiling(contentY + bodyOffset - bodyView.Bottom));
            bodyMaximum = maximum;
            if (bodyOffset > maximum) { bodyOffset = maximum; bodyOffsetClamped = true; Invalidate(); }
        }
    }
}
