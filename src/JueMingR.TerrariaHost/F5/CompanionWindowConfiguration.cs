using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private enum ConfigurationCategory { Behavior, Healing, Mana, Consumables, Character }
        private sealed class ConfigurationTab
        {
            internal readonly ConfigurationCategory Id;
            internal readonly string Text;
            internal ConfigurationTab(ConfigurationCategory id,string text) {Id=id;Text=text;}
        }
        private sealed class ConfigurationOption
        {
            internal readonly string Key,Text;
            internal readonly int Value;
            internal ConfigurationOption(string key,string text,int value) {Key=key;Text=text;Value=value;}
        }
        private static readonly ConfigurationTab[] configurationTabs={
            new ConfigurationTab(ConfigurationCategory.Behavior,"行为"),new ConfigurationTab(ConfigurationCategory.Healing,"回血"),
            new ConfigurationTab(ConfigurationCategory.Mana,"回蓝"),new ConfigurationTab(ConfigurationCategory.Consumables,"消耗品"),
            new ConfigurationTab(ConfigurationCategory.Character,"角色")};
        private static readonly ConfigurationOption[] movementOptions={new ConfigurationOption("follow","跟随主人",1),new ConfigurationOption("stay","原地停留",0)};
        private static readonly ConfigurationOption[] healingOptions = {
            new ConfigurationOption("heal-fast", "快速", (int)CompanionHealthRecoveryMode.Fast),
            new ConfigurationOption("heal-smart", "智能", (int)CompanionHealthRecoveryMode.Smart) };
        private ConfigurationCategory configurationCategory;
        private string expandedConfiguration;
        private F5Rect configurationIdentity,configurationSurface,configurationViewport,configurationNameRect;
        private string configurationName;
        private readonly List<F5Rect> configurationDividers=new List<F5Rect>();
        private int configurationWeaponLabel=-1, configurationMainWeaponsLabel=-1;
        private readonly List<SupplyPresentation> supplyRows = new List<SupplyPresentation>();
        private readonly Dictionary<CompanionSupplyKind, Item[]> supplyDefinitions = new Dictionary<CompanionSupplyKind, Item[]>();
        private uint nextSupplyPresentation;

        private sealed class SupplyPresentation
        {
            internal CompanionSupplyKind Kind;
            internal int Type, Label;
        }

        private F5Rect ConfigurationScrollView {get {return configurationViewport.Width>0?configurationViewport:bodyView;}}

        private Item[] ConfigurationDefinitions(CompanionSupplyKind kind)
        {
            if (!supplyDefinitions.TryGetValue(kind, out var definitions))
                supplyDefinitions.Add(kind, definitions = CompanionSupplyCatalog.Definitions(kind).ToArray());
            return definitions;
        }

        private float PreferredConfigurationHeight(float width, float row)
        {
            float tabWidth = width - 128, x = 0, tabHeight = Math.Max(28, row - 4);
            int tabRows = 1;
            foreach (var tab in configurationTabs)
            {
                float measured = Math.Min(tabWidth, ConfigurationWidth(tab.Text));
                if (x > 0 && x + measured > tabWidth) { x = 0; tabRows++; }
                x += measured + 6;
            }
            float flow = row + 10;
            switch (configurationCategory)
            {
                case ConfigurationCategory.Behavior:
                    flow = 5 * (row + 10) + 25;
                    if (expandedConfiguration == "movement") flow += row + 12;
                    break;
                case ConfigurationCategory.Healing:
                    flow = 4 * (row + 10) + 28;
                    if (expandedConfiguration == "heal-mode") flow += row + 12;
                    break;
                case ConfigurationCategory.Mana: flow = 2 * (row + 10); break;
                case ConfigurationCategory.Consumables: flow = 3 * (row + 10) + 28; break;
                case ConfigurationCategory.Character:
                    if (confirmingReset) flow += 2 * (row + 4);
                    break;
            }
            if (expandedConfiguration != null && supplyKinds.TryGetValue(expandedConfiguration, out var kind))
                flow += ConfigurationDefinitions(kind).Length * (row + 4);
            return Math.Max(270, row + 53 + row + 26 + 6 + tabRows * (tabHeight + 6) + 6 + 20 + flow + 8);
        }

        private static readonly Dictionary<string, CompanionSupplyKind> supplyKinds = new Dictionary<string, CompanionSupplyKind>
        {
            { "supply-list:Life", CompanionSupplyKind.Life }, { "supply-list:Mana", CompanionSupplyKind.Mana },
            { "supply-list:Growth", CompanionSupplyKind.Growth }, { "supply-list:Buff", CompanionSupplyKind.Buff }
        };
        private void RetireConfigurationInput()
        {expandedConfiguration=null;confirmingReset=false;CancelEdit();}
        private void SelectConfiguration(ConfigurationCategory category)
        {
            // Directory/object changes abandon only the old UI gesture/edit.
            // Accepted Host writes and its archive worker retain their owners.
            host.CancelItemGesture();RetireConfigurationInput();configurationCategory=category;bodyOffset=0;notice=null;Invalidate();
        }
        private void BuildConfiguration(CompanionProfile profile,float row)
        {
            float x=bodyView.X+12,width=bodyView.Width-24,y=bodyView.Y;
            float restoreWidth=Math.Min(width,ConfigurationWidth("恢复行为设置",.65f));
            var restore=new F5Rect(x+width-restoreWidth,y,restoreWidth,row);
            float identityWidth=Math.Max(40,width-restoreWidth-12);
            float nameWidth=Math.Min(identityWidth*.55f,Math.Max(40,measure(profile.Name,.85f).Width+8));
            configurationName=profile.Name;configurationNameRect=new F5Rect(x,y,nameWidth,row);
            // ItemsRenderer.Text performs measured ellipsis; the same name rect
            // is also a hint target so its complete original remains discoverable.
            Text(profile.Name,configurationNameRect,.85f);
            selectedStatusLabel=labels.Count;labels.Add(new F5Element(F5ElementKind.Text,new F5Rect(x+nameWidth+10,y,Math.Max(0,identityWidth-nameWidth-10),row),host.Status(selected),default(F5Size),.65f,F5Command.None));
            ConfigurationButton("restore-behavior","恢复行为设置",restore,()=>Command(()=>host.RestoreBehavior(selected)),scale:.65f);
            // A fixed second status baseline keeps a ticking countdown from
            // resizing the editor or invalidating an otherwise valid press.
            float retryWidth=host.SaveFailed(selected)?Math.Min(width,ConfigurationWidth("保存失败 · 重试保存",.65f)):0;
            respawnLabel=labels.Count;labels.Add(new F5Element(F5ElementKind.Text,new F5Rect(x,y+row,Math.Max(0,width-(retryWidth>0?retryWidth+8:0)),22),host.RespawnStatus(selected),default(F5Size),.65f,F5Command.None));
            if(host.SaveFailed(selected)) {
                ConfigurationButton("retry-save","保存失败 · 重试保存",new F5Rect(x+width-retryWidth,y+row,retryWidth,22),()=>Command(()=>host.RetrySave(selected)),scale:.65f);
            }
            configurationIdentity=new F5Rect(bodyView.X,y,bodyView.Width,row+26);y=configurationIdentity.Bottom+6;
            float tabX=x,tabHeight=Math.Max(28,row-4);
            foreach(var tab in configurationTabs) {
                float tabWidth=Math.Min(width,ConfigurationWidth(tab.Text));
                if(tabX>x&&tabX+tabWidth>x+width){tabX=x;y+=tabHeight+6;}
                var target=tab.Id;
                ConfigurationButton("config-tab:"+tab.Id,tab.Text,new F5Rect(tabX,y,tabWidth,tabHeight),()=>SelectConfiguration(target),selectedValue:configurationCategory==tab.Id);
                tabX+=tabWidth+6;
            }
            y+=tabHeight+12;
            configurationSurface=new F5Rect(bodyView.X,y,bodyView.Width,Math.Max(0,bodyView.Bottom-y));
            configurationViewport=new F5Rect(configurationSurface.X+12,configurationSurface.Y+10,Math.Max(0,configurationSurface.Width-24),Math.Max(0,configurationSurface.Height-20));
            actorHeaders.Add(configurationSurface);
            contentY=configurationViewport.Y-bodyOffset;
            var behavior=profile.Behavior;
            switch(configurationCategory)
            {
                case ConfigurationCategory.Behavior:
                    ConfigurationChoice("movement","移动",movementOptions,behavior.Follow?1:0,value=>EditBehavior(b=>b.Follow=value!=0),row);
                    ConfigurationToggle("attack","允许攻击",behavior.AttackEnabled,()=>EditBehavior(b=>b.AttackEnabled=!behavior.AttackEnabled),row);
                    ConfigurationToggle("respawn","自动复活",behavior.AutoRespawn,()=>EditBehavior(b=>b.AutoRespawn=!behavior.AutoRespawn),row);
                    ConfigurationDivider();
                    configurationWeaponLabel = ConfigurationValue("当前武器", row, "无");
                    configurationMainWeaponsLabel = ConfigurationValue("主要武器", row, ConfigurationMainWeapons());
                    break;
                case ConfigurationCategory.Healing:
                    ConfigurationToggle("heal","主动回血",behavior.HealthRecoveryEnabled,()=>EditBehavior(b=>b.HealthRecoveryEnabled=!behavior.HealthRecoveryEnabled),row);
                    ConfigurationChoice("heal-mode","回血方式",healingOptions,(int)behavior.HealthRecoveryMode,value=>EditBehavior(b=>b.HealthRecoveryMode=(CompanionHealthRecoveryMode)value),row);
                    ConfigurationToggle("nurse","护士治疗",behavior.AllowNurse,()=>EditBehavior(b=>b.AllowNurse=!behavior.AllowNurse),row);
                    ConfigurationText("附近有可用护士时治疗，先用队友的钱，不足由你补足。",24,.65f);
                    ConfigurationSupplyList("回血药品", CompanionSupplyKind.Life, behavior, row);
                    break;
                case ConfigurationCategory.Mana:
                    ConfigurationToggle("mana","主动回蓝",behavior.ManaRecoveryEnabled,()=>EditBehavior(b=>b.ManaRecoveryEnabled=!behavior.ManaRecoveryEnabled),row);
                    ConfigurationSupplyList("回蓝药品", CompanionSupplyKind.Mana, behavior, row);
                    break;
                case ConfigurationCategory.Consumables:
                    ConfigurationToggle("consumables","允许使用",behavior.ConsumablesEnabled,()=>EditBehavior(b=>b.ConsumablesEnabled=!behavior.ConsumablesEnabled),row);
                    ConfigurationText("勾选的类型长期允许使用；缺货时等待，补货后继续。",24,.65f);
                    ConfigurationSupplyList("成长物品", CompanionSupplyKind.Growth, behavior, row);
                    ConfigurationSupplyList("增益物品", CompanionSupplyKind.Buff, behavior, row);
                    break;
                case ConfigurationCategory.Character:
                    if(!confirmingReset) ConfigurationAction("request-reset","重置人物档案",()=>{confirmingReset=true;Invalidate();},row);
                    else {
                        ConfigurationText("将清空物品、装备、成长和 AI 设置。",row);
                        ConfigurationText("保留名字、外观、难度和归属。",row);
                        ConfigurationActions(row,new ConfigurationOption("confirm-reset","确认重置",1),new ConfigurationOption("cancel-reset","取消",0));
                    }
                    break;
            }
            contentY+=8;bodyMaximum=Math.Max(0,(int)Math.Ceiling(contentY+bodyOffset-configurationViewport.Bottom));
            if(bodyOffset>bodyMaximum){bodyOffset=bodyMaximum;bodyOffsetClamped=true;Invalidate();}
        }
        private int ConfigurationValue(string label, float row, string value)
        {
            var rect = ConfigurationField(label, row);
            if (!ConfigurationVisible(rect)) return -1;
            int index = labels.Count;
            Text(value, rect);
            return index;
        }
        private string ConfigurationMainWeapons()
        {
            var weapons = SavedWeapons();
            if (weapons.Length == 0) return "未指定主要武器，自动选择背包武器";
            var body = host.GetBody(selected);
            HostCompanions.MapWeaponSlots(body, weapons, displayedWeaponSlots);
            string first = displayedWeaponSlots[0] >= 0 ? body.inventory[displayedWeaponSlots[0]].AffixName() : Lang.GetItemNameValue(weapons[0].Type);
            if (weapons.Length == 1) return first;
            string second = displayedWeaponSlots[1] >= 0 ? body.inventory[displayedWeaponSlots[1]].AffixName() : Lang.GetItemNameValue(weapons[1].Type);
            return first + "、" + second;
        }

        private void RefreshConfigurationPresentation(Player body)
        {
            if (page != 1) return;
            int slot = host.ActiveWeaponSlot(selected);
            if (configurationWeaponLabel >= 0)
                UpdateLabel(configurationWeaponLabel, body != null && slot >= 0 && slot < body.inventory.Length &&
                    body.selectedItem == slot && CompanionItemRules.WeaponEligible(body.inventory[slot]) ? body.inventory[slot].AffixName() : "无");
            if (configurationMainWeaponsLabel >= 0) UpdateLabel(configurationMainWeaponsLabel, ConfigurationMainWeapons());
            if (supplyRows.Count == 0 || Main.GameUpdateCount < nextSupplyPresentation) return;
            nextSupplyPresentation = Main.GameUpdateCount + 20;
            foreach (var row in supplyRows)
                UpdateLabel(row.Label, ConfigurationStockText(body, row.Kind, row.Type));
        }

        private static string ConfigurationStockText(Player body, CompanionSupplyKind kind, int type)
        {
            if (body != null && kind == CompanionSupplyKind.Growth && CompanionSupplyCatalog.GrowthComplete(body, type)) return "已完成";
            int stock = CompanionSupplyCatalog.Count(body, kind, type);
            return stock > 0 ? "持有 " + stock : "缺货";
        }

        private void ConfigurationSupplyList(string title, CompanionSupplyKind kind, CompanionBehavior behavior, float row)
        {
            string key = "supply-list:" + kind;
            bool expanded = expandedConfiguration == key;
            ConfigurationAction(key, title + (expanded ? " ▴" : " ▾"), () => { expandedConfiguration = expanded ? null : key; Invalidate(); }, row);
            if (!expanded) return;
            var entries = ConfigurationDefinitions(kind);
            var selectedTypes = SupplyTypes(behavior, kind);
            bool potion = kind == CompanionSupplyKind.Life || kind == CompanionSupplyKind.Mana;
            var body = host.GetBody(selected);
            foreach (var item in entries)
            {
                int type = item.type;
                var rect = new F5Rect(configurationViewport.X, contentY, configurationViewport.Width, row);
                contentY += row + 4;
                if (!ConfigurationVisible(rect)) continue;
                bool has = Array.IndexOf(selectedTypes, type) >= 0;
                bool allowed = potion ? CompanionSupplyCatalog.PotionAllowedByDefault(kind, type) && !has : has;
                float toggleWidth = ConfigurationWidth("未允许", .65f), countWidth = ConfigurationWidth("持有 9999", .65f);
                var toggle = new F5Rect(rect.Right - toggleWidth, rect.Y, toggleWidth, row);
                var count = new F5Rect(toggle.X - countWidth - 6, rect.Y, countWidth, row);
                var name = new F5Rect(rect.X, rect.Y, Math.Max(0, count.X - rect.X - 5), row);
                Text(item.Name, name, .65f);
                int label = labels.Count;
                Text(ConfigurationStockText(body, kind, type), count, .65f);
                supplyRows.Add(new SupplyPresentation { Kind = kind, Type = type, Label = label });
                ConfigurationButton("supply:" + kind + ":" + type + ":" + allowed, allowed ? "已允许" : "未允许", toggle,
                    () => EditBehavior(b => UpdateSupplyType(b, kind, type, !has)), allowed, !allowed, .65f).Hint = item.Name;
            }
            nextSupplyPresentation = Main.GameUpdateCount + 20;
        }

        private static int[] SupplyTypes(CompanionBehavior preferences, CompanionSupplyKind kind)
        {
            switch (kind)
            {
                case CompanionSupplyKind.Life: return preferences.ExcludedHealingTypes;
                case CompanionSupplyKind.Mana: return preferences.ExcludedManaTypes;
                case CompanionSupplyKind.Growth: return preferences.AllowedGrowthTypes;
                default: return preferences.AllowedBuffTypes;
            }
        }

        private static void UpdateSupplyType(CompanionBehavior preferences, CompanionSupplyKind kind, int type, bool add)
        {
            var types = new List<int>(SupplyTypes(preferences, kind));
            if (add && !types.Contains(type)) types.Add(type);
            if (!add) types.Remove(type);
            types.Sort();
            switch (kind)
            {
                case CompanionSupplyKind.Life: preferences.ExcludedHealingTypes = types.ToArray(); break;
                case CompanionSupplyKind.Mana: preferences.ExcludedManaTypes = types.ToArray(); break;
                case CompanionSupplyKind.Growth: preferences.AllowedGrowthTypes = types.ToArray(); break;
                case CompanionSupplyKind.Buff: preferences.AllowedBuffTypes = types.ToArray(); break;
            }
        }
        private float ConfigurationWidth(string text,float scale=.7f)
        {return Math.Max(58,(float)Math.Ceiling(measure(text,scale).Width+24));}
        private Control ConfigurationButton(string key,string text,F5Rect rect,Action execute,bool selectedValue=false,bool off=false,float scale=.7f)
        {
            var control=Button(key,text,rect,execute,true,selectedValue,off);control.Configuration=true;
            if(scale!=.7f)control.Element=new F5Element(F5ElementKind.Button,rect,text,measure(text,scale),scale,F5Command.None);
            return control;
        }
        private bool ConfigurationVisible(F5Rect rect)
        {return rect.X>=configurationViewport.X&&rect.Right<=configurationViewport.Right&&rect.Y>=configurationViewport.Y&&rect.Bottom<=configurationViewport.Bottom;}
        private void ConfigurationText(string text,float height,float scale=.7f)
        {
            var rect=new F5Rect(configurationViewport.X,contentY,configurationViewport.Width,height);
            if(ConfigurationVisible(rect))Text(text,rect,scale);contentY+=height+4;
        }
        private F5Rect ConfigurationField(string label,float row)
        {
            float labelWidth=Math.Min(110,configurationViewport.Width*.3f),gap=12;
            var labelRect=new F5Rect(configurationViewport.X,contentY,labelWidth,row);
            if(ConfigurationVisible(labelRect))Text(label,labelRect);
            var value=new F5Rect(labelRect.Right+gap,contentY,Math.Max(0,configurationViewport.Right-labelRect.Right-gap),row);
            contentY+=row+10;return value;
        }
        private void ConfigurationToggle(string key,string label,bool value,Action apply,float row)
        {
            var rect=ConfigurationField(label,row);rect=new F5Rect(rect.X,rect.Y,Math.Min(rect.Width,ConfigurationWidth("开启")),rect.Height);
            // A toggle press owns its captured target value. A changed actual
            // value changes this key, so release cannot reverse a newer setting.
            if(ConfigurationVisible(rect))ConfigurationButton("config-toggle:"+key+":"+value,value?"开启":"关闭",rect,apply,true,!value);
        }
        private void ConfigurationChoice(string key,string label,ConfigurationOption[] options,int value,Action<int> apply,float row)
        {
            var area=ConfigurationField(label,row);float width=0;string text="";
            foreach(var option in options){width=Math.Max(width,ConfigurationWidth(option.Text+" ▾"));if(option.Value==value)text=option.Text;}
            var rect=new F5Rect(area.X,area.Y,Math.Min(area.Width,width),row);
            if(ConfigurationVisible(rect))ConfigurationButton("config-choice:"+key,text+(expandedConfiguration==key?" ▴":" ▾"),rect,()=>{
                host.CancelItemGesture();CancelEdit();expandedConfiguration=expandedConfiguration==key?null:key;Invalidate();
            });
            if(expandedConfiguration!=key)return;
            // Options are ordinary flow content. Their measured widths/count
            // advance the same y used by drawing, clipping, hit testing and scroll.
            float x=area.X,y=contentY;
            foreach(var option in options) {
                float optionWidth=Math.Min(area.Width,ConfigurationWidth(option.Text));
                if(x>area.X&&x+optionWidth>area.Right){x=area.X;y+=row+6;}
                var optionRect=new F5Rect(x,y,optionWidth,row);var target=option;
                if(ConfigurationVisible(optionRect))ConfigurationButton(option.Key,option.Text,optionRect,()=>{expandedConfiguration=null;Invalidate();apply(target.Value);},option.Value==value);
                x+=optionWidth+6;
            }
            contentY=y+row+12;
        }
        private void ConfigurationDivider()
        {
            contentY+=8;var rect=new F5Rect(configurationViewport.X,contentY,configurationViewport.Width,1);
            if(ConfigurationVisible(rect))configurationDividers.Add(rect);contentY+=17;
        }
        private void ConfigurationAction(string key,string text,Action execute,float row)
        {
            var rect=new F5Rect(configurationViewport.X,contentY,Math.Min(configurationViewport.Width,ConfigurationWidth(text)),row);
            if(ConfigurationVisible(rect))ConfigurationButton(key,text,rect,execute);contentY+=row+10;
        }
        private void ConfigurationActions(float row,params ConfigurationOption[] actions)
        {
            float x=configurationViewport.X,y=contentY;
            foreach(var action in actions) {
                float width=Math.Min(configurationViewport.Width,ConfigurationWidth(action.Text));
                if(x>configurationViewport.X&&x+width>configurationViewport.Right){x=configurationViewport.X;y+=row+6;}
                var rect=new F5Rect(x,y,width,row);var target=action;
                if(ConfigurationVisible(rect))ConfigurationButton(action.Key,action.Text,rect,()=>{
                    if(target.Value==0)confirmingReset=false;else if(host.Reset(selected))confirmingReset=false;
                    notice=null;Invalidate();
                });x+=width+6;
            }
            contentY=y+row+10;
        }
    }
}
