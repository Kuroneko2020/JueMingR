using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private enum ConfigurationCategory { Combat, Behavior, Character, Connection }
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
        // These are the currently implemented sections/options, not a registry
        // or reserved layout slots. Both rows and navigation measure their data.
        private static readonly ConfigurationTab[] configurationTabs={
            new ConfigurationTab(ConfigurationCategory.Combat,"战斗"),new ConfigurationTab(ConfigurationCategory.Behavior,"行为"),
            new ConfigurationTab(ConfigurationCategory.Character,"角色"),new ConfigurationTab(ConfigurationCategory.Connection,"连接")};
        private static readonly ConfigurationOption[] movementOptions={new ConfigurationOption("follow","跟随主人",1),new ConfigurationOption("stay","原地停留",0)};
        private static readonly ConfigurationOption[] attackOptions={new ConfigurationOption("proactive","主动攻击",0),new ConfigurationOption("defensive","仅自卫",1)};
        private static readonly ConfigurationOption[] targetOptions={new ConfigurationOption("prefer-owner","主人附近",1),new ConfigurationOption("prefer-self","自身附近",0)};
        private ConfigurationCategory configurationCategory;
        private string expandedConfiguration;
        private F5Rect configurationIdentity,configurationSurface,configurationViewport,configurationNameRect;
        private string configurationName;
        private readonly List<F5Rect> configurationDividers=new List<F5Rect>();
        private int configurationWeaponLabel=-1;
        private bool configurationHasWeapon;

        private F5Rect ConfigurationScrollView {get {return configurationViewport.Width>0?configurationViewport:bodyView;}}
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
            string heading="";foreach(var tab in configurationTabs)if(tab.Id==configurationCategory)heading=tab.Text;
            ConfigurationText(heading,row,.85f);contentY+=8;
            var behavior=profile.Behavior;
            switch(configurationCategory)
            {
                case ConfigurationCategory.Combat:
                    ConfigurationToggle("attack","攻击",behavior.AttackEnabled,()=>Behavior(null,!behavior.AttackEnabled,null,null),row);
                    ConfigurationChoice("mode","攻击模式",attackOptions,behavior.SelfDefense?1:0,value=>Behavior(null,null,value!=0,null),row);
                    ConfigurationChoice("target","目标优先",targetOptions,behavior.PreferOwner?1:0,value=>Behavior(null,null,null,value!=0),row);
                    var body=host.GetBody(selected);
                    if(ConfigurationWeaponAvailable(body)) {
                        ConfigurationDivider();var weaponRect=ConfigurationField("当前武器",row);
                        if(ConfigurationVisible(weaponRect)){configurationWeaponLabel=labels.Count;Text(ConfigurationWeaponName(body),weaponRect);}
                    }
                    break;
                case ConfigurationCategory.Behavior:
                    ConfigurationChoice("movement","移动",movementOptions,behavior.Follow?1:0,value=>Behavior(value!=0,null,null,null),row);
                    break;
                case ConfigurationCategory.Character:
                    ConfigurationToggle("respawn","自动复活",behavior.AutoRespawn,()=>SetRespawn(!behavior.AutoRespawn),row);
                    ConfigurationDivider();
                    if(!confirmingReset) ConfigurationAction("request-reset","重置人物档案",()=>{confirmingReset=true;Invalidate();},row);
                    else {
                        ConfigurationText("将清空物品、装备、成长和 AI 设置。",row);
                        ConfigurationText("保留名字、外观、难度和归属。",row);
                        ConfigurationActions(row,new ConfigurationOption("confirm-reset","确认重置",1),new ConfigurationOption("cancel-reset","取消",0));
                    }
                    break;
                case ConfigurationCategory.Connection:
                    BuildConfigurationPassword(row);
                    break;
            }
            contentY+=8;bodyMaximum=Math.Max(0,(int)Math.Ceiling(contentY+bodyOffset-configurationViewport.Bottom));
            if(bodyOffset>bodyMaximum){bodyOffset=bodyMaximum;bodyOffsetClamped=true;Invalidate();}
        }
        private static bool ConfigurationWeaponAvailable(Player body)
        {return body!=null&&body.selectedItem>=0&&body.selectedItem<body.inventory.Length;}
        private static string ConfigurationWeaponName(Player body)
        {return !ConfigurationWeaponAvailable(body)||body.inventory[body.selectedItem]==null||body.inventory[body.selectedItem].IsAir?"无":body.inventory[body.selectedItem].Name;}
        // Reuse the body already resolved by this frame's presentation. A slot
        // or item-name change changes text only, not geometry or an armed action.
        private void RefreshConfigurationPresentation(Player body)
        {if(page==1)UpdateLabel(configurationWeaponLabel,ConfigurationWeaponName(body));}
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
        private void BuildConfigurationPassword(float row)
        {
            var area=ConfigurationField("连接密码",row);
            var rect=new F5Rect(area.X,area.Y,Math.Min(area.Width,Math.Max(160,ConfigurationWidth("已设置 · 点击修改"))),row);
            bool editingPassword=editor!=null&&editKind=="password";
            if(ConfigurationVisible(rect)) {
                ConfigurationButton("password",editingPassword?"":String.IsNullOrEmpty(host.ConnectionPassword)?"未设置":"已设置",rect,()=>BeginEdit("password",host.ConnectionPassword??""));
                if(editingPassword)field=new F5Rect(rect.X+4,rect.Y,rect.Width-8,rect.Height);
            }
            if(!editingPassword)return;
            float x=area.X,y=contentY;
            foreach(string action in new[]{"save-password","cancel-password"}) {
                bool apply=action=="save-password";string text=apply?"应用密码":"取消";float width=ConfigurationWidth(text);
                if(x>area.X&&x+width>area.Right){x=area.X;y+=row+6;}
                var button=new F5Rect(x,y,Math.Min(width,area.Width),row);
                if(ConfigurationVisible(button))ConfigurationButton(action,text,button,()=>{if(apply)RequestFinish();else{CancelEdit();Invalidate();}});
                x+=width+6;
            }
            contentY=y+row+10;
        }
    }
}
