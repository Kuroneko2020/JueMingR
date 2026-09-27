using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Features.Fishing;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");Main.InitializeItemAnimations();
            object shell=Get(context,"Shell"),state=Get(shell,"State"),ui=Get(shell,"FishingUi"),host=Get(context,"Fishing"),renderer=Get(shell,"renderer");
            Call(renderer,"RefreshResources");
            var list=new FishList(new[]{new FishKey(FishKind.Item,2290),new FishKey(FishKind.Item,2297),new FishKey(FishKind.Item,2334),new FishKey(FishKind.Npc,682)});
            NativeFishingUiChecks.Save(host,new FishingOptions(filterMode:2).WithList(2,0,list).SavePreset(2,0));
            Call(state,"Navigate",7);Call(state,"RestoreVisible");
            foreach(var size in new[]{new[]{960,760,100},new[]{1440,900,150},new[]{960,440,100},new[]{604,220,100},new[]{906,330,150}})
            {
                float scale=size[2]/100f;var matrix=Matrix.CreateScale(scale);Call(state,"ScrollTo",0f);
                string notice="当前名单已保存为预设。";Call(host,"ClearReport",notice);Prepare(context,graphics,size);Call(host,"Report",notice);
                Prepare(context,graphics,size);Image("page");
                if(!((IEnumerable)Get(ui,"Parts")).Cast<object>().Any(p=>Get(p,"Command").ToString()=="Plus"))
                {var local=((IEnumerable)Get(ui,"pageParts")).Cast<object>().First(p=>Get(p,"Command").ToString()=="Plus");Call(state,"ScrollTo",Get(Get(Get(local,"Element"),"Rect"),"Y"));Prepare(context,graphics,size);Image("scrolled");}
                var plus=NativeFishingUiChecks.Part(ui,"Plus");Call(ui,"Execute",plus);NativeFishingUiChecks.Step(context,false,Vector2.Zero,"鱼");Prepare(context,graphics,size);Image("search");
                var body=Get(ui,"popupBody");Require((float)Get(body,"Height")>=36,"actual-font minimum logical viewport retains a full candidate-card height");
                long builds=(long)Get(ui,"LayoutBuilds"),rules=(long)Get(Get(host,"Catalog"),"RuleChecks"),tiles=(long)Get(Get(host,"Catalog"),"TileReads");
                for(int i=0;i<12;i++)Prepare(context,graphics,size);
                Require((long)Get(ui,"LayoutBuilds")==builds && (long)Get(Get(host,"Catalog"),"RuleChecks")==rules && (long)Get(Get(host,"Catalog"),"TileReads")==tiles,"stable actual-font page performs no new layout or fishing query work");
                Call(ui,"CloseOverlay");Prepare(context,graphics,size);Call(ui,"Execute",NativeFishingUiChecks.Part(ui,"PresetList"));Prepare(context,graphics,size);Image("presets");Call(ui,"CloseOverlay");
                void Image(string name)
                {graphics.Image(Path.Combine(output,"fishing-"+name+"-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>{Call(renderer,"Draw",state,matrix,false,false);Call(ui,"Draw",Get(shell,"drawKeyboard"));Call(ui,"DrawPopup",Get(shell,"drawKeyboard"));},matrix,size[0],size[1]);}
            }
            var keywords=new FishList(keywords:new[]{"鱼","任务鱼","金匣","魔法海螺","虹鳟鱼"});
            NativeFishingUiChecks.Save(host,new FishingOptions(filterMode:1,match:1).WithList(1,1,keywords).SavePreset(1,1));
            Prepare(context,graphics,new[]{960,760,100});Call(ui,"Execute",NativeFishingUiChecks.Part(ui,"PresetList"));Prepare(context,graphics,new[]{960,760,100});
            graphics.Image(Path.Combine(output,"fishing-keyword-presets-960-760-100.png"),()=>{Call(renderer,"Draw",state,Matrix.Identity,false,false);Call(ui,"Draw",Get(shell,"drawKeyboard"));Call(ui,"DrawPopup",Get(shell,"drawKeyboard"));},Matrix.Identity,960,760);
            Call(ui,"CloseOverlay");Call(state,"Close");Call(ui,"Suspend");NativeFishingUiChecks.Save(host,new FishingOptions());
            NativeFishingUiChecks.FullBindings(context);
            Console.WriteLine("PASS G10 original-font/texture page, search and presets at normal, 150 percent and short viewports; stable prepared layouts.");
        }
        private static void Prepare(object context,ProbeGraphics graphics,int[] size)
        {
            NativeFishingUiChecks.Prepare(context,size[0],size[1],size[2]/100f);var state=Get(Get(context,"Shell"),"State");
            var sample=Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input"));
            foreach(var pair in new[]{Tuple.Create("Width",(object)(float)size[0]),Tuple.Create("Height",(object)(float)size[1]),Tuple.Create("Scale",(object)(size[2]/100f)),Tuple.Create("Active",(object)true),Tuple.Create("Focused",(object)true)})
                sample.GetType().GetField(pair.Item1,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(sample,pair.Item2);
            // Real geometry admission clamps the window after a resize. Merely
            // remeasuring its layout would retain the prior screen's X/Y.
            Call(state,"Update",sample);NativeFishingUiChecks.Prepare(context,size[0],size[1],size[2]/100f);var ui=Get(Get(context,"Shell"),"FishingUi");
            var fish=((IEnumerable)Get(ui,"Parts")).Cast<object>().Select(p=>GetOptional(p,"Fish")).Where(k=>k!=null).Cast<FishKey>().Distinct().ToArray();
            graphics.LoadItemTextures(fish.Where(k=>k.Kind==FishKind.Item).Select(k=>k.Id));
            foreach(var key in fish.Where(k=>k.Kind==FishKind.Npc))graphics.LoadTexture("Npc","Images/NPC_"+key.Id,key.Id);
            Call(ui,"Prepare",true,Matrix.CreateScale(size[2]/100f),new Vector2(size[0],size[1]));
        }
    }
}
