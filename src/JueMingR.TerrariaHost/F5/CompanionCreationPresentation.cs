using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private static readonly string[] CreationTabs={"人物","发型","服装","颜色","声音"};
        private static readonly string[] ColorNames={"头发","眼睛","皮肤","上衣","内衣","裤子","鞋子"};
        private void StartCreation()
        {
            host.CancelItemGesture(); CancelEdit(); creationDraft=host.NewCreationDraft();
            creationDraft.head=creationDraft.body=creationDraft.legs=-1;
            creating=true; confirmingReset=false; bodyOffset=0; creationPage=creationHairOffset=0;
            BeginEdit("name",creationDraft.name??""); Invalidate();
        }
        private void BuildCreate(float row)
        {
            contentY=bodyView.Y-bodyOffset;
            BodyText("创建人物",row);
            field=BodyRect(row);
            if(FullyVisible(field)) Button("name",editor!=null&&editKind=="name"?"":creationDraft?.name??"点击输入名字",field,()=>BeginEdit("name",creationDraft?.name??"")); else field=default(F5Rect);
            contentY+=row+8;
            if(creationDraft==null) { FinishBody(); return; }
            creationHero=new F5Rect(bodyView.X+8,contentY,130,172);
            float x=bodyView.X+150, width=bodyView.Width-150;
            for(int i=0;i<CreationTabs.Length;i++)
            {
                int target=i; float w=(width-16)/5; var r=new F5Rect(x+i*(w+4),contentY,w,30);
                if(FullyVisible(r)) Button("create-tab:"+i,CreationTabs[i],r,()=>{
                    if(editor!=null&&!RequestFinish()) return; creationPage=target; Invalidate();
                },true,creationPage==i);
            }
            contentY+=38;
            if(creationPage==0)
            {
                string[] names={"经典","中核","硬核","旅途"};
                for(int i=0;i<4;i++) { int d=i; CreateChoice("difficulty:"+i,names[i],x,width,()=>creationDraft.difficulty=(byte)d,creationDraft.difficulty==i); }
            }
            else if(creationPage==1)
            {
                var hairstyles=host.GetCreationHairstyles(); int columns=Math.Max(1,(int)(width/50)); int perPage=columns*4;
                creationHairOffset=Math.Max(0,Math.Min(creationHairOffset,Math.Max(0,hairstyles.Count-1)));
                for(int i=0;i<Math.Min(perPage,hairstyles.Count-creationHairOffset);i++)
                {
                    int hair=hairstyles[creationHairOffset+i]; var r=new F5Rect(x+i%columns*50,contentY+i/columns*48,46,44);
                    if(FullyVisible(r)) { var c=Button("hair:"+hair,"",r,()=>{creationDraft.hair=hair;Invalidate();},true,creationDraft.hair==hair); c.Element=null;c.HairPreview=hair; }
                }
                contentY+=(float)Math.Ceiling((double)Math.Min(perPage,hairstyles.Count-creationHairOffset)/columns)*48;
                CreateChoice("hair-prev","上一页",x,width/2-4,()=>creationHairOffset=Math.Max(0,creationHairOffset-perPage),false);
                CreateChoice("hair-next","下一页",x,width/2-4,()=>creationHairOffset=Math.Min(Math.Max(0,hairstyles.Count-perPage),creationHairOffset+perPage),false);
            }
            else if(creationPage==2)
            {
                int count=PlayerVariantID.MaleDisplayDoll; int columns=Math.Max(1,(int)(width/66));
                for(int i=0;i<count;i++)
                {
                    int variant=i; var r=new F5Rect(x+i%columns*66,contentY+i/columns*94,62,90);
                    if(FullyVisible(r)) { var c=Button("skin:"+i,"",r,()=>{creationDraft.skinVariant=variant;Invalidate();},true,creationDraft.skinVariant==variant);c.Element=null;c.VariantPreview=variant; }
                }
                contentY+=(float)Math.Ceiling((double)count/columns)*94;
            }
            else if(creationPage==3)
            {
                float cw=(width-12)/4;
                for(int i=0;i<7;i++)
                { int c=i; var r=new F5Rect(x+i%4*(cw+4),contentY+i/4*36,cw,32); if(FullyVisible(r)) Button("color:"+i,ColorNames[i],r,()=>{creationColor=c;Invalidate();},true,creationColor==c); }
                contentY+=76;
                Color value=CreationColor(); Vector3 hsl=Main.rgbToHsl(value);
                for(int i=0;i<18;i++)
                {
                    float hue=i/18f;var r=new F5Rect(x+i%9*(width/9),contentY+i/9*30,width/9-4,26);
                    if(FullyVisible(r)) { Color color=Main.hslToRgb(hue,.75f,.55f);var c=Button("swatch:"+i,"",r,()=>{SetCreationColor(color);Invalidate();});c.Element=null;c.Swatch=color; }
                }
                contentY+=64;
                ColorSlider("色相",0,hsl.X,x,width,hsl);
                ColorSlider("饱和",1,hsl.Y,x,width,hsl);
                ColorSlider("明暗",2,hsl.Z,x,width,hsl);
            }
            else
            {
                int[] order=PlayerVoiceID.VariantOrder;
                for(int i=0;i<order.Length;i++) { int variant=order[i]; CreateChoice("voice:"+variant,"声音 "+(i+1),x,width,()=>creationDraft.voiceVariant=variant,creationDraft.voiceVariant==variant); }
                var pitch=new F5Rect(x,contentY,width,30); if(FullyVisible(pitch)) Text("音调 "+creationDraft.voicePitchOffset.ToString("0.00"),pitch,.65f); contentY+=34;
                CreateChoice("pitch-down","降低",x,width/2-4,()=>creationDraft.voicePitchOffset=Math.Max(-1,creationDraft.voicePitchOffset-.05f),false);
                CreateChoice("pitch-up","升高",x,width/2-4,()=>creationDraft.voicePitchOffset=Math.Min(1,creationDraft.voicePitchOffset+.05f),false);
                CreateChoice("voice-preview","试听",x,width,()=>host.PreviewCreationVoice(creationDraft),false);
            }
            contentY=Math.Max(contentY,creationHero.Bottom+8);
            BodyButton("randomize","随机外观",row,()=> { if(editor!=null&&!RequestFinish()) return; host.RandomizeCreation(creationDraft); Invalidate(); });
            Pair("save-create","创建人物",SubmitCreation,false,"cancel-create","取消",()=>{CancelEdit();creationDraft=null;creating=false;Invalidate();},false,row);
            FinishBody();
        }
        private void CreateChoice(string key,string text,float x,float width,Action change,bool isSelected)
        {
            var r=new F5Rect(x,contentY,width,32); if(FullyVisible(r)) Button(key,text,r,()=>{change();Invalidate();},true,isSelected); contentY+=36;
        }
        private void ColorSlider(string name,int channel,float value,float x,float width,Vector3 original)
        {
            var label=new F5Rect(x,contentY,48,30);if(FullyVisible(label)) Text(name,label,.65f);
            var r=new F5Rect(x+52,contentY,width-52,30);
            if(FullyVisible(r))
            {
                var c=Button("color-slider:"+channel,"",r,()=> {
                    float next=MathHelper.Clamp((pointer.X-r.X-5)/Math.Max(1,r.Width-10),0,1);
                    Vector3 hsl=Main.rgbToHsl(CreationColor());if(channel==0) hsl.X=next;else if(channel==1) hsl.Y=next;else hsl.Z=next;
                    SetCreationColor(Main.hslToRgb(hsl.X,hsl.Y,hsl.Z));Invalidate();
                });c.Element=null;c.ColorRamp=channel;c.RampValue=value;
            }
            contentY+=36;
        }
        private static void DrawSwatch(F5Rect rect,Color color)
        {
            Main.spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,new Rectangle((int)rect.X+5,(int)rect.Y+5,(int)rect.Width-10,(int)rect.Height-10),new Rectangle(0,0,1,1),color);
        }
        private void DrawColorRamp(Control c)
        {
            Vector3 hsl=Main.rgbToHsl(CreationColor());float w=(c.Rect.Width-10)/48;
            var rampPixel=Terraria.GameContent.TextureAssets.MagicPixel.Value;
            for(int i=0;i<48;i++) {Vector3 next=hsl;float v=i/47f;if(c.ColorRamp==0)next.X=v;else if(c.ColorRamp==1)next.Y=v;else next.Z=v;Main.spriteBatch.Draw(rampPixel,new Rectangle((int)(c.Rect.X+5+i*w),(int)c.Rect.Y+5,(int)Math.Ceiling(w)+1,(int)c.Rect.Height-10),new Rectangle(0,0,1,1),Main.hslToRgb(next.X,next.Y,next.Z));}
            var pixel=Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Main.spriteBatch.Draw(pixel,new Rectangle((int)(c.Rect.X+5+(c.Rect.Width-10)*c.RampValue),(int)c.Rect.Y+2,2,(int)c.Rect.Height-4),new Rectangle(0,0,1,1),Color.White);
        }
        private Color CreationColor()
        { switch(creationColor) {case 0:return creationDraft.hairColor;case 1:return creationDraft.eyeColor;case 2:return creationDraft.skinColor;case 3:return creationDraft.shirtColor;case 4:return creationDraft.underShirtColor;case 5:return creationDraft.pantsColor;default:return creationDraft.shoeColor;} }
        private void SetCreationColor(Color value)
        { switch(creationColor) {case 0:creationDraft.hairColor=value;break;case 1:creationDraft.eyeColor=value;break;case 2:creationDraft.skinColor=value;break;case 3:creationDraft.shirtColor=value;break;case 4:creationDraft.underShirtColor=value;break;case 5:creationDraft.pantsColor=value;break;default:creationDraft.shoeColor=value;break;} }
        private void SubmitCreation()
        {
            if(creationDraft==null||editor!=null&&!RequestFinish()) return;
            if(!host.CreateDraft(creationDraft)) {notice=host.Feedback;Invalidate();return;}
            pendingCreatedName=creationDraft.name; creationDraft=null; creating=false; page=0; bodyOffset=0; notice=null; Invalidate();
        }
    }
}
