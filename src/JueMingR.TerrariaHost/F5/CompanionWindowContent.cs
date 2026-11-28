using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private float contentY;
        private bool bodyOffsetClamped;
        private const float CellStep = 42;
        private const float ShowcaseHeight = 128;
        private const float ActorActionGap = 6;
        private static readonly string[] GearTabs = { "饰品", "时装", "装备" };
        private static readonly string[] LoadoutTabs = { "I", "II", "III" };
        private const float LoadoutButtonWidth = 27;
        private CompanionProfile CurrentProfile()
        {
            var profiles = host.Profiles;
            for (int i = 0; i < profiles.Count; i++) if (profiles[i].Id == selected) return profiles[i];
            return null;
        }
        private void BuildSelected(float row)
        {
            if (page >= ChatPage) { BuildAiPage(row); return; }
            var body = host.GetBody(selected);
            RefreshAvailability(body);
            field = default(F5Rect); contentY = bodyView.Y - bodyOffset;
            var profile = CurrentProfile();
            if (page == 0)
            {
                RefreshWeaponSlots(body);
                // Shared structural measurements align both columns. Current
                // values/status never change these baselines; only native maxima,
                // style, chosen subpage or viewport geometry can do so.
                float paneWidth = companionView.Width;
                float resourcesHeight = SharedResources(paneWidth);
                sharedHeroHeight = profile == null ? Math.Max(ShowcaseHeight, resourcesHeight) :
                    Math.Max(Math.Max(ShowcaseHeight, 78 + row), resourcesHeight + 4 + row);
                sharedEquipmentHeight = SharedEquipment(paneWidth);
                BuildActor(Main.LocalPlayer, true, ownerView, ownerOffset, row);
                BuildActor(profile == null ? null : body, false, companionView, bodyOffset, row);
                return;
            }
            if (profile == null)
            {
                float x = bodyView.X + 8;
                configurationViewport = new F5Rect(x, bodyView.Y, bodyView.Width - 16, bodyView.Height);
                BuildDeletionReceipts(row);
                ConfigurationText(pendingCreatedName ?? host.ArchiveNotice ?? (host.CanCreate ? "点击左下角＋创建队友" : ""), row);
                FinishConfiguration(); return;
            }
            BuildConfiguration(profile,row);
        }
        private float SharedResources(float paneWidth)
        {
            float width=ResourceWidth(paneWidth);var owner=Main.LocalPlayer;var ai=host.GetBody(selected);
            var own=owner==null?default(CompanionResourceDisplay.Blocks):resourceDisplay.MeasureBlocks(owner,width);
            var companion=ai==null?default(CompanionResourceDisplay.Blocks):resourceDisplay.MeasureBlocks(ai,width);
            sharedLifeHeight=Math.Max(own.Life,companion.Life);
            return sharedLifeHeight+Math.Max(own.Mana,companion.Mana);
        }
        private static float ShowcaseWidth(float paneWidth) {return Math.Min(116,Math.Max(92,paneWidth*.26f));}
        private static float ShowcaseCenter(float paneWidth) {return Math.Min(paneWidth*.82f,paneWidth-12-ShowcaseWidth(paneWidth)/2);}
        private static float ResourceWidth(float paneWidth) {return Math.Max(20,ShowcaseCenter(paneWidth)-ShowcaseWidth(paneWidth)/2-80);}
        private static float GridHeight(float paneWidth,int count)
        {int columns=Math.Max(1,Math.Min(10,(int)((paneWidth-24)/40)));float cell=Math.Min(CellStep,(paneWidth-24)/columns);return (float)Math.Ceiling((double)count/columns)*cell+4;}
        private float SharedEquipment(float paneWidth)
        {return 2*Math.Max(GridHeight(paneWidth,ownerGearPage==2?5:10),GridHeight(paneWidth,companionGearPage==2?5:10));}
        private float PreferredHeight(float width,float row)
        {
            // Both pages share the preferred backpack envelope. Real resource
            // bars and gear outside this envelope scroll inside their panes.
            float pane=(width-106)/2;
            float header=8+row+6+(pane-24<RespawnTextWidth()+100?24:0)+ShowcaseHeight+4;
            return row+20+header+6+(8+26+6+2*GridHeight(pane,10))+6+row+6+
                (4+GridHeight(pane,50))+6+(4+GridHeight(pane,8))+8;
        }
        private float RespawnTextWidth() => Math.Max(120, measure("复活倒计时 99 秒 · 自动复活已关闭", .65f).Width + 8);
        private void BuildActor(Player body, bool owner, F5Rect view, int offset, float row)
        {
            // The same final rectangles determine visible cells, clipping and hit
            // targets. Scrolling projects native slot identities; it never renumbers them.
            float headerTop=view.Y-offset,y=headerTop+8,x=view.X+12,width=view.Width-24;
            string name = owner ? Main.LocalPlayer?.name ?? "我的人物" : CurrentProfile()?.Name ?? pendingCreatedName ?? "AI 队友";
            float statusWidth=RespawnTextWidth();
            bool wrapStatus=width<statusWidth+100;
            var nameRect=new F5Rect(x,y,Math.Max(40,width-(wrapStatus?0:statusWidth+4)),row);
            if(ActorVisible(nameRect,view)) Text(name,nameRect,.85f);
            y += row + 6;
            if(!owner) {respawnLabel=labels.Count;labels.Add(new F5Element(F5ElementKind.Text,wrapStatus?new F5Rect(x,y,width,24):new F5Rect(x+width-statusWidth,y-row-6,statusWidth,row),host.RespawnStatus(selected),default(F5Size),.6f,F5Command.None));}if(wrapStatus)y+=24;
            float heroHeight = sharedHeroHeight;
            float headerHeight=8+row+6+(wrapStatus?24:0)+heroHeight+4;
            var hero = new F5Rect(x,y,ResourceWidth(view.Width),heroHeight);
            float centerX=view.X+ShowcaseCenter(view.Width),frameTop=y;
            float showcaseWidth = ShowcaseWidth(view.Width);
            float showcaseX = centerX - showcaseWidth / 2;
            var profile = owner ? null : CurrentProfile();
            bool joined = profile != null && host.IsJoined(profile.Id);
            bool retiring = profile != null && host.RecallPending(profile.Id);
            string action = retiring ? (host.SaveFailed(profile.Id) ? "保存失败" : "退出中") : joined ? "退出" : "加入";
            float stateWidth = profile == null ? 0 : ConfigurationWidth(action);
            float recallWidth = profile == null ? 0 : ConfigurationWidth("传送");
            float actionWidth = stateWidth + recallWidth + 2 * ActorActionGap;
            float actionInset = Math.Max(0, view.X + 4 - (showcaseX - actionWidth));
            float showcaseHeight = profile == null ? ShowcaseHeight : heroHeight;
            if (profile != null)
            {
                // Preserve the measured action widths on narrow panes. The
                // shared header reserves the last row below both resource blocks.
                showcaseX += actionInset;
                showcaseWidth -= actionInset;
            }
            var displayPanel = new F5Rect(showcaseX, frameTop, showcaseWidth, showcaseHeight);
            var showcase=new F5Rect(displayPanel.X+2,displayPanel.Y+2,displayPanel.Width-4,displayPanel.Height-4);
            if(owner)ownerShowcase=showcase;else companionShowcase=showcase;
            if(owner)ownerShowcasePanel=displayPanel;else companionShowcasePanel=displayPanel;
            if (owner)ownerHero=hero;else companionHero=hero;
            if(profile != null) {
                Guid target = profile.Id;
                var stateRect = new F5Rect(displayPanel.X - stateWidth - ActorActionGap, displayPanel.Bottom - row, stateWidth, row);
                var recallRect = new F5Rect(stateRect.X - recallWidth - ActorActionGap, stateRect.Y, recallWidth, row);
                if (ActorVisible(recallRect, view))
                    ManualRecallButton("actor-wormhole", profile, recallRect).ActorOverlay = true;
                // A press owns this operation and actual activation. Natural
                // detach/rejoin/respawn must retire it, rather than choosing
                // the opposite operation on release. Timers/text keep the key.
                string stateKey="actor-state/"+(retiring?"saving":joined?"recall":"join")+"/"+host.ActivationGeneration(target);
                if(ActorVisible(stateRect,view))
                {
                    bool canAct = host.CanChangeCompanionPresence(target, joined, out string reason);
                    var control = Button(stateKey, action, stateRect, () => Command(() => joined ? host.Recall(target) : host.Join(target)),canAct);
                    control.ActorOverlay = control.ActorState = true;
                    control.Hint = reason ?? (joined ? "退出当前世界并保存人物" : "加入当前世界");
                }
                string[] teams={"无队伍","红队","绿队","蓝队","黄队","粉队"};
                for(int i=0;i<6;i++){int team=i;var r=new F5Rect(displayPanel.X-52+i%2*24,displayPanel.Y+8+i/2*24,20,20);if(ActorVisible(r,view)){var c=Button("team:"+i,"",r,()=>Command(()=>host.SetTeam(selected,team)),true,body!=null&&body.team==team);c.ActorOverlay=true;c.TeamIcon=i;c.Hint=teams[i];}}
            }
            AddActorHeader(new F5Rect(view.X+4,headerTop,view.Width-8,headerHeight),view);
            y+=heroHeight+4+6;
            float groupTop=y;y+=8;
            // Subtabs and their native equipment/dye cells are one module.
            // This same group origin drives background, clipping and hit tests.
            int gear = owner ? ownerGearPage : companionGearPage;
            float totalTabs=8;foreach(string text in GearTabs)totalTabs+=Math.Max(46,measure(text,.7f).Width+18);
            int columns=Math.Max(1,Math.Min(10,(int)(width/40)));float gridWidth=columns*Math.Min(CellStep,width/columns)-1;
            float tabX=x+Math.Max(0,gridWidth-totalTabs-8);
            float loadoutY = y;
            if (tabX-x < 3*(LoadoutButtonWidth+4))
            {
                y += 26+4;
            }
            for (int t=0;t<LoadoutTabs.Length;t++)
            {
                int target = t;
                var r = new F5Rect(x + t*(LoadoutButtonWidth+4), loadoutY, LoadoutButtonWidth, 26);
                if (!ActorVisible(r, view)) continue;
                var control = Button("loadout:" + owner + ":" + t, LoadoutTabs[t], r,
                    () => SwitchActorLoadout(owner, body, target));
                control.Owner = owner; control.SwitchLoadout = t;
            }
            for (int t=0;t<3;t++)
            {
                int target=t;float tw=Math.Max(46,measure(GearTabs[t],.7f).Width+18);var r=new F5Rect(tabX,y,tw,26);tabX+=tw+4;
                if (ActorVisible(r,view)) Button("gear:"+owner+":"+t,GearTabs[t],r,() => {
                    host.CancelItemGesture(); CancelSelectionModes(); if(owner) ownerGearPage=target; else companionGearPage=target; Invalidate();
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
            ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);
            y += 6;
            InventoryToolbar(owner, view, y, row);
            groupTop = y + row + 6; y = groupTop + 4;
            var area = owner ? CompanionInventoryArea.Inventory : storage;
            if (area == CompanionInventoryArea.Inventory)
            {
                if (body != null) ActorSlots(body,owner,area,0,50,ref y,view);
                else if (!owner && CurrentProfile() == null)
                    ActorText(host.ArchiveNotice ?? (host.CanCreate ? "点击左下角＋创建队友" : ""), new F5Rect(x,y,width,row),view);
                ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);groupTop=y+6;y=groupTop+4;
                if (body != null) ActorSlots(body,owner,area,50,8,ref y,view);
                ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);
            }
            else
            {
                if (body != null && storageAvailable) ActorSlots(body,false,area,0,40,ref y,view);
                else
                {
                    ActorText(storageReason ?? "当前无法访问储存", new F5Rect(x,y,width,row),view);
                    y += GridHeight(view.Width,40);
                }
                ActorGroup(view.X+7,groupTop,view.Width-14,y-groupTop,view);
            }
            int maximum=Math.Max(0,(int)Math.Ceiling(y + offset + 8 - view.Bottom));
            if (owner) { ownerMaximum=maximum; if(ownerOffset>maximum) { ownerOffset=maximum; bodyOffsetClamped=true; Invalidate(); } }
            else { bodyMaximum=maximum; if(bodyOffset>maximum) { bodyOffset=maximum; bodyOffsetClamped=true; Invalidate(); } }
        }

        private void SwitchActorLoadout(bool owner, Player body, int target)
        {
            if (body == null || body.dead || Main.mouseItem?.IsAir == false || host.HasItemGesture) return;
            if (owner)
            {
                if (ReferenceEquals(body, Main.LocalPlayer)) body.TrySwitchingLoadout(target);
            }
            else if (ReferenceEquals(body, host.GetBody(selected)))
                Command(() => host.TrySwitchCompanionLoadout(selected, target));
        }
        private void ActorSlots(Player body, bool owner, CompanionInventoryArea area, int start, int count, ref float y, F5Rect view)
        {
            // Ten native inventory columns remain readable; a narrow viewport
            // shows fewer columns using the same layout, with vertical scrolling.
            int columns=Math.Max(1,Math.Min(10,(int)((view.Width-24)/40)));
            float cell=Math.Min(CellStep,(view.Width-24)/columns);
            for(int i=0;i<count;i++)
            {
                int slot=start+i;
                float inset=count<columns?(columns-count)*cell/2:0;
                var rect=new F5Rect(view.X+12+inset+i%columns*cell,y+i/columns*cell,cell-1,cell-1);
                if(!ActorVisible(rect,view)) continue;
                Item item=ReadSlotPreview(body,owner,area,slot);
                var c=Button("slot:"+owner+":"+area+":"+slot,"",rect,null);
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
            var items=CompanionResources.Items(player,area);return items!=null&&slot>=0&&slot<items.Length?items[slot]:null;
        }

        private static Item SharedSlot(Player body, CompanionInventoryArea area, int slot, out int? sharedFrom)
        {
            if (area == CompanionInventoryArea.Armor) return body.GetEffectiveArmor(slot, out sharedFrom);
            if (area == CompanionInventoryArea.Dye) return body.GetEffectiveDye(slot, out sharedFrom);
            sharedFrom = null; return null;
        }

        internal Item PrepareNativeSlotHover()
        {
            if (!Visible || !ready || page != 0 || PasswordPromptVisible || Captured || host.HasItemGesture ||
                !Main.MouseDisplayItem.IsAir) return null;
            foreach (var c in controls)
            {
                if (!c.IsSlot || c.ItemType <= 0 || !c.Rect.Contains(pointer.X, pointer.Y)) continue;
                var body = c.Owner ? Main.LocalPlayer : host.GetBody(selected);
                if (!CompanionItemRules.ValidSlot(body, c.Area, c.Slot)) return null;
                var array = CompanionResources.Items(body, c.Area);
                if (array[c.Slot]?.IsAir != false)
                {
                    if (c.LoadoutIcon < 313 || SharedSlot(body,c.Area,c.Slot,out int? shared)?.IsAir != false || !shared.HasValue) return null;
                    var loadout=body.Loadouts[shared.Value];
                    array=c.Area==CompanionInventoryArea.Armor?loadout.Armor:loadout.Dye;
                }
                // Only nonempty actual slots reach MouseHover. Its empty armor
                // path would look up shared loadouts on the human LocalPlayer.
                ItemSlot.MouseHover(array, CompanionItemRules.SlotContext(c.Area, c.Slot), c.Slot);
                int x = Main.SettingsEnabled_OpaqueBoxBehindTooltips ? Main.mouseX + 6 : -1;
                int y = Main.SettingsEnabled_OpaqueBoxBehindTooltips ? Main.mouseY + 6 : -1;
                Main.instance.MouseTextNoOverride(Main.hoverItemName, 0, 0, x, y);
                return Main.HoverItem;
            }
            return null;
        }
        private bool ApplyItemPresentation(Control c,Player body,Item item)
        {
            var displayed=item;
            int? sharedFrom=null;
            if(item?.IsAir==true && !host.HasItemGesture)
                displayed=SharedSlot(body,c.Area,c.Slot,out sharedFrom)??item;
            int type=displayed?.type??0,stack=displayed?.stack??0,prefix=displayed?.prefix??0;
            bool marked=item?.IsAir==false && item.favorited && CompanionItemRules.CanMark(c.Area);
            bool shareArea=c.Area==CompanionInventoryArea.Armor||c.Area==CompanionInventoryArea.Dye;
            int loadoutIcon=sharedFrom.HasValue?313+sharedFrom.Value:marked&&shareArea?310+body.CurrentLoadoutIndex:-1;
            bool favorite=marked&&!shareArea;
            bool locked=(c.Area==CompanionInventoryArea.Armor||c.Area==CompanionInventoryArea.Dye)&&!body.IsItemSlotUnlockedAndUsable(c.Slot);
            bool aiWeaponSlot = !c.Owner && c.Area == CompanionInventoryArea.Inventory && c.Slot < CompanionBehavior.MainInventorySlots;
            bool candidate = aiWeaponSlot && weaponDraft != null && CompanionItemRules.WeaponEligible(item);
            bool active = aiWeaponSlot && c.Slot == activeWeaponSlot && body.selectedItem == c.Slot && CompanionItemRules.WeaponEligible(item);
            string onceReason = null;
            bool enabled = onceSelection != null ? aiWeaponSlot && item?.IsAir == false && host.CanUseItemOnce(selected, c.Area, c.Slot, out onceReason) :
                weaponDraft == null ? (c.Owner || selectedTransfer) && (!locked || item?.IsAir == false && !host.HasItemGesture) : candidate;
            bool chosen = aiWeaponSlot && (c.Slot == displayedWeaponSlots[0] || c.Slot == displayedWeaponSlots[1]);
            string unavailable=c.Owner?null:onceReason ?? selectedUnavailable;
            if(c.Prefix==prefix&&c.ItemType==type&&c.Stack==stack&&c.Enabled==enabled&&c.Selected==chosen&&c.Availability==unavailable&&c.Locked==locked&&c.WeaponCandidate==candidate&&c.ActiveWeapon==active&&c.Favorited==favorite&&c.LoadoutIcon==loadoutIcon) return false;
            bool changedType=c.ItemType!=type;
            if(c.Stack!=stack||c.StackText==null)c.StackText=stack.ToString();c.ItemType=type;c.Stack=stack;c.Prefix=prefix;c.Enabled=enabled;c.Selected=chosen;
            c.Availability=unavailable;c.Locked=locked;c.WeaponCandidate=candidate;c.ActiveWeapon=active;
            c.Favorited=favorite;c.LoadoutIcon=loadoutIcon;
            // A real item is described by Terraria's own hover renderer.
            // Empty slots and operation failures still have a window hint.
            c.Hint=type>0?null:unavailable??(locked?"此槽尚未解锁。":
                c.Area==CompanionInventoryArea.Dye||c.Area==CompanionInventoryArea.MiscDye?"染料":"空格");
            c.EmptyIcon=-1;c.Placeholder=null;
            if(type==0)
            {
                if(c.Area==CompanionInventoryArea.Dye||c.Area==CompanionInventoryArea.MiscDye)c.EmptyIcon=1;
                else if(c.Area==CompanionInventoryArea.Misc)c.EmptyIcon=MiscEquipmentIcon(c.Slot);
                else if(c.Area==CompanionInventoryArea.Armor)c.EmptyIcon=c.Slot%10<3?(c.Slot>=10?3:0)+(c.Slot%10)*6:c.Slot>=10?2:11;
                else if(c.Area==CompanionInventoryArea.Inventory&&c.Slot>=50)c.Placeholder=c.Slot<54?"币":"弹";
            }
            ItemPresentationUpdates++;return changedType;
        }
        private static int MiscEquipmentIcon(int slot)
        {
            // Match Terraria's ItemSlot.Draw frames in Extra[54] using the same
            // equipment context as both actors' native slots.
            switch (CompanionItemRules.EquipmentContext(CompanionInventoryArea.Misc, slot))
            {
                case ItemSlot.Context.EquipPet: return 10;
                case ItemSlot.Context.EquipLight: return 17;
                case ItemSlot.Context.EquipMinecart: return 7;
                case ItemSlot.Context.EquipMount: return 13;
                case ItemSlot.Context.EquipGrapple: return 4;
                default: return -1;
            }
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
                Player owner=Main.LocalPlayer,body=host.GetBody(selected);
                RefreshAvailability(body);
                RefreshWeaponSlots(body);
                if (weaponDraft != null) UpdateLabel(weaponCountLabel, WeaponCount());
                foreach(var c in controls) if(c.IsSlot)
                {
                    Player player=c.Owner?owner:body;
                    if(player!=null) types|=ApplyItemPresentation(c,player,ReadSlotPreview(player,c.Owner,c.Area,c.Slot));
                }
            }
            if(types) {icons.BeginVisibleSet();foreach(var c in controls)icons.AddVisible(c.ItemType);icons.EndVisibleSet();}
            if(resources) icons.Prepare();
        }
        private static bool ActorVisible(F5Rect rect,F5Rect view) { return rect.Y>=view.Y && rect.Bottom<=view.Bottom && rect.X>=view.X && rect.Right<=view.Right; }
        private void ActorText(string text,F5Rect rect,F5Rect view) { if(ActorVisible(rect,view)) Text(text,rect,.65f); }
        private void ProcessItemInput(Control hit, Microsoft.Xna.Framework.Input.KeyboardState keys, bool left,bool right,bool wasDragging,bool resources)
        {
            bool changed=false;
            if(page!=0||editor!=null||wasDragging) { rightSlot=null; return; }
            if (weaponDraft != null || onceSelection != null)
            {
                if (onceSelection != null && left && !previousLeft && hit == null) ShowUnavailableSlot();
                if (left && !previousLeft && hit?.IsSlot == true)
                {
                    armedKey = null;
                    if (onceSelection != null)
                    {
                        if (!SelectionCurrent(onceSelection.Value))
                        {
                            CancelSelectionModes();
                            ShowNotice("队友状态已变化，请重新选择物品。");
                        }
                        else
                        {
                            if (host.UseItemOnce(selected, hit.Area, hit.Slot)) CancelSelectionModes();
                            ShowNotice(host.Feedback);
                        }
                    }
                    else ToggleWeapon(hit);
                    RefreshItemPresentation(resources);
                    RefreshNotice();
                }
                return;
            }
            if(hit==null||!hit.IsSlot)
            {
                rightSlot=null;
                if ((left&&!previousLeft || right&&!previousRight) && hit==null) ShowUnavailableSlot();
                return;
            }
            if(left&&!previousLeft)
            {
                armedKey=null;
                bool control=keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);
                bool shift=keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)||keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                // Until the next physical click the modifier chooses one
                // intent. A rejected bank trash cannot fall through to pickup.
                if(keys.IsKeyDown(Main.FavoriteKey) && !Main.drawingPlayerChat && hit.ItemType>0 &&
                    hit.LoadoutIcon<313 && CompanionItemRules.CanMark(hit.Area))
                {
                    if(host.ItemGesture(selected,hit.Owner,hit.Area,hit.Slot,CompanionItemGesture.Mark))
                        Terraria.Audio.SoundEngine.PlaySound(12);
                    changed=true;
                }
                else if((hit.Area == CompanionInventoryArea.Inventory || !hit.Owner && CompanionResources.IsBank(hit.Area)) &&
                    HostCompanions.QuickTrashRequested(control,shift))
                {host.QuickTrashGesture(selected,hit.Owner,hit.Area,hit.Slot);changed=true;}
                else {
                    host.ItemGesture(selected,hit.Owner,hit.Area,hit.Slot,shift?CompanionItemGesture.Shift:CompanionItemGesture.Left); changed=true; }
            }
            if(right)
            {
                bool fresh=!previousRight;
                if(fresh) rightSlot=hit.Key;
                if(rightSlot!=null)
                {
                    int count=RightPickupCount(fresh);
                    if(fresh) {host.ItemGesture(selected,hit.Owner,hit.Area,hit.Slot,CompanionItemGesture.Right);changed=true;}
                    else if(count>0) changed|=host.RepeatItemPickup(selected,hit.Owner,hit.Area,hit.Slot,count);
                }
            }
            else rightSlot=null;
            if(changed) {RefreshItemPresentation(resources);RefreshNotice();}
        }

        private void ShowUnavailableSlot()
        {
            foreach (var c in controls)
                if (c.IsSlot && !c.Enabled && c.Availability != null && c.Rect.Contains(pointer.X, pointer.Y))
                { ShowNotice(c.Availability); return; }
        }

        // Terraria drives these counters from the physical inventory hold.
        // The window owns its hold, so native world input/cooldowns stay free.
        private int RightPickupCount(bool fresh)
        {
            if (fresh) { rightRepeat=30; splitDelay=7; splitCounter=splitSteps=0; return 1; }
            if (++splitCounter >= Math.Max(5, (splitDelay-1)*5))
            {
                splitDelay=Math.Max(2, splitDelay-1); splitCounter=0;
                if (splitSteps < short.MaxValue) splitSteps++;
            }
            if (--rightRepeat > 1) return 0;
            rightRepeat=splitDelay;
            return 1+Math.Max(0,splitSteps-10);
        }
        private void DrawHero(F5Rect view,F5Rect hero,F5Rect showcase,F5Rect displayPanel,CompanionPreview preview,Player source)
        {
            if(hero.Bottom<=view.Y||hero.Y>=view.Bottom) return;
            renderer.Pass(matrix,view,()=> {
                if(source!=null) resourceDisplay.Draw(renderer,source,hero,sharedLifeHeight);
            });
            renderer.Pass(matrix,view,()=>UiSurface.Panel(Main.spriteBatch,Terraria.GameContent.TextureAssets.MagicPixel.Value,displayPanel,Terraria.GameContent.TextureAssets.InventoryBack.Value,new Color(95,105,155),roundedOuter:true));
            var clip=new F5Rect(Math.Max(view.X,showcase.X),Math.Max(view.Y,showcase.Y),Math.Min(view.Right,showcase.Right)-Math.Max(view.X,showcase.X),Math.Min(view.Bottom,showcase.Bottom)-Math.Max(view.Y,showcase.Y));
            if(clip.Width>0&&clip.Height>0) renderer.Pass(matrix,clip,()=> {
                preview.DrawShowcase(portraits,showcase,matrix,renderer.Generation);
            });
        }
        private void EditBehavior(Action<CompanionBehavior> edit)
        {
            var profile = CurrentProfile(); if (profile == null) return;
            var b = profile.Behavior;
            edit(b);
            Command(()=>host.SetBehavior(selected,b));
        }
        private void Command(Func<bool> command)
        { if (!command()) ShowNotice(host.Feedback); contentInvalidated=true; }
        private void AddActorHeader(F5Rect rect,F5Rect view)
        {float top=Math.Max(rect.Y,view.Y),bottom=Math.Min(rect.Bottom,view.Bottom);if(bottom>top)actorHeaders.Add(new F5Rect(rect.X,top,rect.Width,bottom-top));}
        private void ActorGroup(float x,float y,float width,float height,F5Rect view)
        {float top=Math.Max(y,view.Y),bottom=Math.Min(y+height,view.Bottom);if(bottom>top)groups.Add(new F5Rect(x-3,top,width+6,bottom-top));}
        private F5Rect BodyRect(float row) { return new F5Rect(bodyView.X, contentY, bodyView.Width, row); }
        private bool FullyVisible(F5Rect rect) { return rect.Y >= bodyView.Y && rect.Bottom <= bodyView.Bottom; }
        private void BodyText(string text, float row)
        { var rect = BodyRect(row); if (FullyVisible(rect)) Text(text, rect); contentY += row + 4; }
        private void FinishBody()
        {
            int maximum = Math.Max(0, (int)Math.Ceiling(contentY + bodyOffset - bodyView.Bottom));
            bodyMaximum = maximum;
            if (bodyOffset > maximum) { bodyOffset = maximum; bodyOffsetClamped = true; Invalidate(); }
        }
    }
}
