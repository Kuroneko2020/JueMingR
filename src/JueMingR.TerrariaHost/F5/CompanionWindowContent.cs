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
            manualWeaponSlot=CurrentProfile()?.Behavior.ManualWeaponSlot??-1;RefreshAvailability(host.GetBody(selected));
            field = default(F5Rect); contentY = bodyView.Y - bodyOffset;
            var profile = CurrentProfile();
            if (page == 0)
            {
                // Shared structural measurements align both columns. Current
                // values/status never change these baselines; only native maxima,
                // style, chosen subpage or viewport geometry can do so.
                sharedResourcesHeight=SharedResources(bodyView.Width/2-6);
                sharedEquipmentHeight=SharedEquipment(bodyView.Width/2-6);
                BuildActor(Main.LocalPlayer, true, ownerView, ownerOffset, row);
                BuildActor(profile == null ? null : host.GetBody(selected), false, companionView, bodyOffset, row);
                return;
            }
            if (profile == null) { BodyText(pendingCreatedName??"点击左下角＋创建队友", row); FinishBody(); return; }
            var nameRect=new F5Rect(bodyView.X+12,contentY,bodyView.Width-24,row);
            float nameWidth=Math.Min(nameRect.Width*.6f,Math.Max(80,measure(profile.Name,.85f).Width+14));
            if(FullyVisible(nameRect)) {
                Text(profile.Name,new F5Rect(nameRect.X,nameRect.Y,nameWidth,row),.85f);
                respawnLabel=labels.Count;labels.Add(new F5Element(F5ElementKind.Text,new F5Rect(nameRect.X+nameWidth+12,nameRect.Y,nameRect.Width-nameWidth-12,row),host.RespawnStatus(selected),default(F5Size),.65f,F5Command.None));
            }
            contentY+=row+4;
            selectedStatusLabel=labels.Count;BodyText(host.Status(selected),row);
            if (host.SaveFailed(selected)) BodyButton("retry-save", "保存失败 · 重试保存", row, () => Command(() => host.RetrySave(selected)));
            Pair("join", "加入当前世界", () => Command(() => host.Join(selected)), false,
                "recall", "收回", () => Command(() => host.Recall(selected)), false, row);
            BuildConfiguration(profile, row); FinishBody();
        }
        private float SharedResources(float paneWidth)
        {
            float width=ResourceWidth(paneWidth);var owner=Main.LocalPlayer;var ai=host.GetBody(selected);
            var own=owner==null?default(CompanionResourceDisplay.Blocks):CompanionResourceDisplay.MeasureBlocks(owner,width);
            var companion=ai==null?default(CompanionResourceDisplay.Blocks):CompanionResourceDisplay.MeasureBlocks(ai,width);
            sharedLifeHeight=Math.Max(own.Life,companion.Life);
            return Math.Max(108,sharedLifeHeight+Math.Max(own.Mana,companion.Mana));
        }
        private static float ShowcaseWidth(float paneWidth) {return Math.Min(116,Math.Max(92,paneWidth*.26f));}
        private static float ShowcaseCenter(float paneWidth) {return Math.Min(paneWidth*.82f,paneWidth-12-ShowcaseWidth(paneWidth)/2);}
        private static float ResourceWidth(float paneWidth) {return Math.Max(20,ShowcaseCenter(paneWidth)-ShowcaseWidth(paneWidth)/2-28);}
        private static float GridHeight(float paneWidth,int count)
        {int columns=Math.Max(1,Math.Min(10,(int)((paneWidth-24)/40)));float cell=Math.Min(44,(paneWidth-24)/columns);return (float)Math.Ceiling((double)count/columns)*cell+4;}
        private float SharedEquipment(float paneWidth)
        {return 2*Math.Max(GridHeight(paneWidth,ownerGearPage==2?5:10),GridHeight(paneWidth,companionGearPage==2?5:10));}
        private float PreferredHeight(float width,float row)
        {
            float pane=(width-104)/2;
            float header=4+row+6+(pane-24<300?24:0)+SharedResources(pane)+4;
            return row+53+header+6+(8+26+6+SharedEquipment(pane))+6+(4+GridHeight(pane,50))+6+(4+GridHeight(pane,8))+8;
        }
        private void BuildActor(Player body, bool owner, F5Rect view, int offset, float row)
        {
            // The same final rectangles determine visible cells, clipping and hit
            // targets. Scrolling projects native slot identities; it never renumbers them.
            float headerTop=view.Y-offset,y=headerTop+4,x=view.X+12,width=view.Width-24;
            string name = owner ? Main.LocalPlayer?.name ?? "我的人物" : CurrentProfile()?.Name ?? pendingCreatedName ?? "AI 队友";
            bool wrapStatus=width<300;
            var nameRect=new F5Rect(x,y,Math.Max(40,width-(owner?0:wrapStatus?76:196)),row);
            if(ActorVisible(nameRect,view)) Text(name,nameRect,.85f);
            if (!owner && CurrentProfile() != null)
            {
                bool joined = host.IsJoined(selected);
                var r = new F5Rect(view.Right - 80,y,68,row);
                if (ActorVisible(r,view)) Button(joined ? "recall" : "join", joined ? "收回" : "加入",r,
                    () => Command(() => host.IsJoined(selected) ? host.Recall(selected) : host.Join(selected)));
            }
            y += row + 6;
            if(!owner) {respawnLabel=labels.Count;labels.Add(new F5Element(F5ElementKind.Text,wrapStatus?new F5Rect(x,y,width,24):new F5Rect(x+width-190,y-row,112,25),host.RespawnStatus(selected),default(F5Size),.6f,F5Command.None));}if(wrapStatus)y+=24;
            float resources=sharedResourcesHeight;
            float heroHeight=resources;
            float headerHeight=4+row+6+(wrapStatus?24:0)+heroHeight+4;
            var hero = new F5Rect(x+8,y-4,ResourceWidth(view.Width),heroHeight);
            // Native full-body scaling pivots at the feet, not at the
            // rectangle center. Its ordinary 48px visible silhouette at the
            // unchanged 1.625 scale is centered 24*scale above those feet.
            float visibleCenterOffset=108/2f-8-24*1.625f;
            float centerX=view.X+ShowcaseCenter(view.Width),centerY=headerTop+headerHeight/2;
            float frameTop=Math.Max(headerTop+row+8,centerY-52);
            var showcase=new F5Rect(centerX-ShowcaseWidth(view.Width)/2,frameTop,ShowcaseWidth(view.Width),Math.Min(104,headerTop+headerHeight-8-frameTop));
            var portrait=new F5Rect(centerX-34,centerY-54-visibleCenterOffset,68,108);
            if(owner)ownerShowcase=showcase;else companionShowcase=showcase;
            if (owner) {ownerHero=hero;ownerPortrait=portrait;} else {companionHero=hero;companionPortrait=portrait;}
            AddActorHeader(new F5Rect(view.X+4,headerTop,view.Width-8,headerHeight),view);
            y+=heroHeight+4+6;
            float groupTop=y;y+=8;
            // Subtabs and their native equipment/dye cells are one module.
            // This same group origin drives background, clipping and hit tests.
            var tabs = new[] { "饰品", "时装", "装备" }; int gear = owner ? ownerGearPage : companionGearPage;
            float totalTabs=8;foreach(string text in tabs)totalTabs+=Math.Max(46,measure(text,.7f).Width+18);
            int columns=Math.Max(1,Math.Min(10,(int)(width/40)));float gridWidth=columns*Math.Min(44,width/columns)-3;
            float tabX=x+Math.Max(0,gridWidth-totalTabs-8);
            for (int t=0;t<3;t++)
            {
                int target=t;float tw=Math.Max(46,measure(tabs[t],.7f).Width+18);var r=new F5Rect(tabX,y,tw,26);tabX+=tw+4;
                if (ActorVisible(r,view)) Button("gear:"+owner+":"+t,tabs[t],r,() => {
                    host.CancelItemGesture(); if(owner) ownerGearPage=target; else companionGearPage=target; Invalidate();
                },true,gear==t);
            }
            y+=26+6;float equipmentTop=y;
            if (body != null)
            {
                if (gear == 0) { ActorSlots(body,owner,CompanionInventoryArea.Armor,0,Math.Min(10,body.armor.Length),ref y,view); ActorSlots(body,owner,CompanionInventoryArea.Dye,0,body.dye.Length,ref y,view); }
                else if (gear == 1) { ActorSlots(body,owner,CompanionInventoryArea.Armor,10,Math.Max(0,body.armor.Length-10),ref y,view); ActorSlots(body,owner,CompanionInventoryArea.Dye,0,body.dye.Length,ref y,view); }
                else { ActorSlots(body,owner,CompanionInventoryArea.Misc,0,body.miscEquips.Length,ref y,view); ActorSlots(body,owner,CompanionInventoryArea.MiscDye,0,body.miscDyes.Length,ref y,view); }
            }
            y=equipmentTop+sharedEquipmentHeight;
            ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);groupTop=y+6;y=groupTop+4;
            if (body != null) ActorSlots(body,owner,CompanionInventoryArea.Inventory,0,50,ref y,view);
            ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);groupTop=y+6;y=groupTop+4;
            if (body != null) ActorSlots(body,owner,CompanionInventoryArea.Inventory,50,8,ref y,view);
            ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);
            int maximum=Math.Max(0,(int)Math.Ceiling(y + offset + 8 - view.Bottom));
            if (owner) { ownerMaximum=maximum; if(ownerOffset>maximum) { ownerOffset=maximum; bodyOffsetClamped=true; Invalidate(); } }
            else { bodyMaximum=maximum; if(bodyOffset>maximum) { bodyOffset=maximum; bodyOffsetClamped=true; Invalidate(); } }
        }
        private void ActorSlots(Player body, bool owner, CompanionInventoryArea area, int start, int count, ref float y, F5Rect view)
        {
            // Ten native inventory columns remain readable; a narrow viewport
            // shows fewer columns using the same layout, with vertical scrolling.
            int columns=Math.Max(1,Math.Min(10,(int)((view.Width-24)/40)));
            float cell=Math.Min(44,(view.Width-24)/columns);
            for(int i=0;i<count;i++)
            {
                int slot=start+i;
                float inset=count<columns?(columns-count)*cell/2:0;
                var rect=new F5Rect(view.X+12+inset+i%columns*cell,y+i/columns*cell,cell-3,cell-3);
                if(!ActorVisible(rect,view)) continue;
                Item item=ReadSlotPreview(body,owner,area,slot);
                var c=Button("slot:"+owner+":"+area+":"+slot,"",rect,null,true,
                    !owner && area==CompanionInventoryArea.Inventory && slot==manualWeaponSlot);
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
            bool enabled=(c.Owner||selectedTransfer)&&!locked;
            bool chosen=!c.Owner&&c.Area==CompanionInventoryArea.Inventory&&c.Slot==manualWeaponSlot;
            string unavailable=c.Owner?null:selectedUnavailable;
            if(c.Prefix==prefix&&c.ItemType==type&&c.Stack==stack&&c.Enabled==enabled&&c.Selected==chosen&&c.Availability==unavailable&&c.Locked==locked) return false;
            bool changedType=c.ItemType!=type;
            if(c.Stack!=stack||c.StackText==null)c.StackText=stack.ToString();c.ItemType=type;c.Stack=stack;c.Prefix=prefix;c.Enabled=enabled;c.Selected=chosen;
            c.Availability=unavailable;c.Locked=locked;c.Hint=unavailable??(locked?"此饰品槽尚未解锁。":SlotHint(c.Area,c.Slot,item,c.Owner));c.EmptyIcon=-1;c.Placeholder=null;
            if(type==0)
            {
                if(c.Area==CompanionInventoryArea.Dye||c.Area==CompanionInventoryArea.MiscDye)c.EmptyIcon=1;
                else if(c.Area==CompanionInventoryArea.Misc)c.EmptyIcon=new[]{4,13,17,10,7}[c.Slot];
                else if(c.Area==CompanionInventoryArea.Armor)c.EmptyIcon=c.Slot%10<3?(c.Slot>=10?3:0)+(c.Slot%10)*6:c.Slot>=10?2:11;
                else if(c.Area==CompanionInventoryArea.Inventory&&c.Slot>=50)c.Placeholder=c.Slot<54?"币":"弹";
            }
            ItemPresentationUpdates++;return changedType;
        }
        private void RefreshAvailability(Player body)
        {
            selectedTransfer=host.CanTransfer(selected);
            selectedUnavailable=!host.IsJoined(selected)?"人物未加入，无法操作":!selectedTransfer?body?.dead==true?"人物已死亡，暂时无法操作":host.Status(selected):null;
        }
        private void RefreshItemPresentation(bool resources)
        {
            bool types=false;
            if(page==0)
            {
                Player owner=Main.LocalPlayer,body=host.GetBody(selected);manualWeaponSlot=CurrentProfile()?.Behavior.ManualWeaponSlot??-1;RefreshAvailability(body);
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
            if(page!=0||editor!=null||wasDragging) { host.EndItemRepeat(); rightSlot=null; return; }
            if(hit==null||!hit.IsSlot) { host.EndItemRepeat(); rightSlot=null; rightRepeat=0; return; }
            if(left&&!previousLeft)
            {
                armedKey=null;
                bool control=keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);
                bool shift=keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                if(hit.Area==CompanionInventoryArea.Inventory&&HostCompanions.QuickTrashRequested(control,shift))
                {host.QuickTrashGesture(selected,hit.Owner,hit.Area,hit.Slot);changed=true;}
                else if((keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightAlt)) && !hit.Owner && hit.Area==CompanionInventoryArea.Inventory && hit.Slot<50 && !host.HasItemGesture)
                { host.SelectWeapon(selected,hit.Slot); changed=true; }
                else {
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
        private void DrawHero(F5Rect view,F5Rect hero,F5Rect portrait,F5Rect showcase,CompanionPreview preview,Player source)
        {
            if(hero.Bottom<=view.Y||hero.Y>=view.Bottom) return;
            renderer.Pass(matrix,view,()=> {
                if(source!=null) resourceDisplay.Draw(renderer,source,hero,sharedLifeHeight);
            });
            var clip=new F5Rect(Math.Max(view.X,showcase.X),Math.Max(view.Y,showcase.Y),Math.Min(view.Right,showcase.Right)-Math.Max(view.X,showcase.X),Math.Min(view.Bottom,showcase.Bottom)-Math.Max(view.Y,showcase.Y));
            if(clip.Width>0&&clip.Height>0) renderer.Pass(matrix,clip,()=> {
                UiSurface.Panel(Main.spriteBatch,Terraria.GameContent.TextureAssets.MagicPixel.Value,showcase,Terraria.GameContent.TextureAssets.InventoryBack.Value,new Color(95,105,155),roundedOuter:true);
                // Equipment visibility changes the local composition only, not
                // controls, hit geometry, scrolling or an already valid press.
                var pose=CompanionPetPreview.Visible(source)?portrait.Offset(-14,0):portrait;
                preview.DrawPet(pose,matrix);preview.Draw(portraits,pose,matrix);
            });
        }
        private void BuildConfiguration(CompanionProfile profile, float row)
        {
            var behavior = profile.Behavior;
            float groupTop=contentY;
            BodyText("移动", row);
            Pair("follow", "跟随主人", () => Behavior(true, null, null, null), behavior.Follow,
                "stay", "原地停留", () => Behavior(false, null, null, null), !behavior.Follow, row);
            ConfigurationGroup(groupTop); groupTop=contentY;
            BodyText("攻击", row);
            Pair("attack-on", "开启", () => Behavior(null, true, null, null), behavior.AttackEnabled,
                "attack-off", "关闭", () => Behavior(null, false, null, null), !behavior.AttackEnabled, row, true, true);
            BodyText("攻击模式", row);
            Pair("proactive", "主动攻击", () => Behavior(null, null, false, null), !behavior.SelfDefense,
                "defensive", "仅自卫", () => Behavior(null, null, true, null), behavior.SelfDefense, row);
            BodyText("目标优先", row);
            Pair("prefer-owner", "主人附近优先", () => Behavior(null, null, null, true), behavior.PreferOwner,
                "prefer-self", "自身附近优先", () => Behavior(null, null, null, false), !behavior.PreferOwner, row);
            ConfigurationGroup(groupTop); groupTop=contentY;
            BodyText("复活",row);
            Pair("respawn-on","自动复活",()=>SetRespawn(true),behavior.AutoRespawn,"respawn-off","关闭",()=>SetRespawn(false),!behavior.AutoRespawn,row);
            BodyText("队伍",row);
            var teamBody=host.GetBody(selected);
            string[] teams={"无队伍","红队","绿队","蓝队","黄队","粉队"};
            float teamWidth=(bodyView.Width-10)/3;
            for(int i=0;i<6;i++){int team=i;var rect=new F5Rect(bodyView.X+i%3*(teamWidth+5),contentY+i/3*(row+6),teamWidth,row);
                if(FullyVisible(rect)) {var c=Button("team:"+i,teams[i],rect,()=>Command(()=>host.SetTeam(selected,team)),true,teamBody!=null&&teamBody.team==team);c.TeamColor=Main.teamColor[i];c.Element=new F5Element(F5ElementKind.Button,rect,"",default(F5Size),.7f,F5Command.None);}}
            contentY+=2*(row+6); ConfigurationGroup(groupTop); groupTop=contentY;
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
            ConfigurationGroup(groupTop);groupTop=contentY;
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
            b.Follow=follow??b.Follow;b.AttackEnabled=attack??b.AttackEnabled;
            b.SelfDefense=defense??b.SelfDefense;b.PreferOwner=prefer??b.PreferOwner;
            Command(()=>host.SetBehavior(selected,b));
        }
        private void Command(Func<bool> command)
        { command(); notice = null; contentInvalidated=true; }
        private void SetRespawn(bool value) {var profile=CurrentProfile();if(profile==null)return;var b=profile.Behavior;b.AutoRespawn=value;Command(()=>host.SetBehavior(selected,b));}
        private void AddActorHeader(F5Rect rect,F5Rect view)
        {float top=Math.Max(rect.Y,view.Y),bottom=Math.Min(rect.Bottom,view.Bottom);if(bottom>top)actorHeaders.Add(new F5Rect(rect.X,top,rect.Width,bottom-top));}
        private void ActorGroup(float x,float y,float width,float height,F5Rect view)
        {float top=Math.Max(y,view.Y),bottom=Math.Min(y+height,view.Bottom);if(bottom>top)groups.Add(new F5Rect(x-3,top,width+6,bottom-top));}
        private void ConfigurationGroup(float top) {ActorGroup(bodyView.X+3,top,bodyView.Width-6,contentY-top-3,bodyView);contentY+=7;}
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
