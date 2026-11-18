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
            BuildConfiguration(profile,row);
        }
        private float SharedResources(float paneWidth)
        {
            float width=ResourceWidth(paneWidth);var owner=Main.LocalPlayer;var ai=host.GetBody(selected);
            var own=owner==null?default(CompanionResourceDisplay.Blocks):resourceDisplay.MeasureBlocks(owner,width);
            var companion=ai==null?default(CompanionResourceDisplay.Blocks):resourceDisplay.MeasureBlocks(ai,width);
            sharedLifeHeight=Math.Max(own.Life,companion.Life);
            return Math.Max(132,sharedLifeHeight+Math.Max(own.Mana,companion.Mana));
        }
        private static float ShowcaseWidth(float paneWidth) {return Math.Min(116,Math.Max(92,paneWidth*.26f));}
        private static float ShowcaseCenter(float paneWidth) {return Math.Min(paneWidth*.82f,paneWidth-12-ShowcaseWidth(paneWidth)/2);}
        private static float ResourceWidth(float paneWidth) {return Math.Max(20,ShowcaseCenter(paneWidth)-ShowcaseWidth(paneWidth)/2-80);}
        private static float GridHeight(float paneWidth,int count)
        {int columns=Math.Max(1,Math.Min(10,(int)((paneWidth-24)/40)));float cell=Math.Min(44,(paneWidth-24)/columns);return (float)Math.Ceiling((double)count/columns)*cell+4;}
        private float SharedEquipment(float paneWidth)
        {return 2*Math.Max(GridHeight(paneWidth,ownerGearPage==2?5:10),GridHeight(paneWidth,companionGearPage==2?5:10));}
        private float PreferredHeight(float width,float row)
        {
            float pane=(width-104)/2;
            float header=8+row+6+(pane-24<300?24:0)+SharedResources(pane)+4;
            return row+53+header+6+(8+26+6+SharedEquipment(pane))+6+(4+GridHeight(pane,50))+6+(4+GridHeight(pane,8))+8;
        }
        private void BuildActor(Player body, bool owner, F5Rect view, int offset, float row)
        {
            // The same final rectangles determine visible cells, clipping and hit
            // targets. Scrolling projects native slot identities; it never renumbers them.
            float headerTop=view.Y-offset,y=headerTop+8,x=view.X+12,width=view.Width-24;
            string name = owner ? Main.LocalPlayer?.name ?? "我的人物" : CurrentProfile()?.Name ?? pendingCreatedName ?? "AI 队友";
            bool wrapStatus=width<300;
            var nameRect=new F5Rect(x,y,Math.Max(40,width-(wrapStatus?0:124)),row);
            if(ActorVisible(nameRect,view)) Text(name,nameRect,.85f);
            y += row + 6;
            if(!owner) {respawnLabel=labels.Count;labels.Add(new F5Element(F5ElementKind.Text,wrapStatus?new F5Rect(x,y,width,24):new F5Rect(x+width-120,y-row-6,120,row),host.RespawnStatus(selected),default(F5Size),.6f,F5Command.None));}if(wrapStatus)y+=24;
            float resources=sharedResourcesHeight;
            float heroHeight=resources;
            float headerHeight=8+row+6+(wrapStatus?24:0)+heroHeight+4;
            var hero = new F5Rect(x,y,ResourceWidth(view.Width),heroHeight);
            float centerX=view.X+ShowcaseCenter(view.Width),frameTop=y;
            var displayPanel=new F5Rect(centerX-ShowcaseWidth(view.Width)/2,frameTop,ShowcaseWidth(view.Width),128);
            var showcase=new F5Rect(displayPanel.X+2,displayPanel.Y+2,displayPanel.Width-4,104);
            if(owner)ownerShowcase=showcase;else companionShowcase=showcase;
            if(owner)ownerShowcasePanel=displayPanel;else companionShowcasePanel=displayPanel;
            if (owner)ownerHero=hero;else companionHero=hero;
            if(!owner&&CurrentProfile()!=null) {
                var stateRect=new F5Rect(displayPanel.X+5,displayPanel.Bottom-21,displayPanel.Width-10,18);
                bool joined=host.IsJoined(selected);Guid target=selected;
                // A press owns this operation and actual activation. Natural
                // detach/rejoin/respawn must retire it, rather than choosing
                // the opposite operation on release. Timers/text keep the key.
                string stateKey="actor-state/"+(joined?"recall":"join")+"/"+host.ActivationGeneration(target);
                if(ActorVisible(stateRect,view)) {var c=Button(stateKey,ActorStatus(),stateRect,()=>Command(()=>joined?host.Recall(target):host.Join(target)),joined||!CurrentProfile().PermanentlyDead);c.ActorOverlay=c.ActorState=true;c.Hint=joined?"点击收回人物":"点击加入当前世界";c.Element=new F5Element(F5ElementKind.Button,stateRect,"",default(F5Size),.55f,F5Command.None);}
                string[] teams={"无队伍","红队","绿队","蓝队","黄队","粉队"};
                for(int i=0;i<6;i++){int team=i;var r=new F5Rect(displayPanel.X-52+i%2*24,displayPanel.Y+22+i/2*24,20,20);if(ActorVisible(r,view)){var c=Button("team:"+i,"",r,()=>Command(()=>host.SetTeam(selected,team)),true,body!=null&&body.team==team);c.ActorOverlay=true;c.TeamIcon=i;c.Hint=teams[i];}}
            }
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
            if(ReferenceEquals(source,Main.LocalPlayer))renderer.Pass(matrix,view,()=>DrawCentered(source.dead?"已死亡":"主人",new F5Rect(displayPanel.X+5,displayPanel.Bottom-21,displayPanel.Width-10,18),Color.Silver,.55f));
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
