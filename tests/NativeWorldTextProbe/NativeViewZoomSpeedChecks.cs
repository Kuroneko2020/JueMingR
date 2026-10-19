using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Owner's 2026-10-19 equal-ratio input supplement. Counts test perceived
    // scale travel, independently of the chosen curve; native Draw observations
    // stay in the existing disposable Main and do not reproduce input incidents.
    internal static class NativeViewZoomSpeedChecks
    {
        internal static void Model(Assembly assembly)
        {
            var method=assembly.GetType("JueMingR.TerrariaHost.View.ViewZoom",true).GetMethod("Step",BindingFlags.Static|BindingFlags.NonPublic);
            var step=(Func<float,int,bool,float,float>)Delegate.CreateDelegate(typeof(Func<float,int,bool,float,float>),method);
            var counts=new List<int>();
            foreach(float start in new[]{.3f,.5f,1f,2f,5f})counts.Add(Travel(step,start,start*2,1));
            foreach(float start in new[]{.6f,1f,2f,5f,10f})counts.Add(Travel(step,start,start/2,-1));
            // Old fixed .02 gives15..250 opportunities for these same ratios.
            // Do not assert the implementation's multiply/divide formula here.
            Require(counts.Max()-counts.Min()<=1&&counts.Min()>=30&&counts.Max()<=40,"equal factor-two scale travel takes approximately the same Mapping count throughout the range, both directions");
            foreach(float start in new[]{.4f,.5f,1f,2f,5f,9f})
            {
                float value=start;
                for(int i=0;i<1000;i++)value=step(step(value,1,true,.3f),-1,true,.3f);
                Require(Math.Abs(value/start-1)<.0001f,"unclipped reciprocal round trips have no material drift or stored direction state");
            }
            Require(step(.3f,-1,true,.3f)==.3f&&step(10f,1,true,.3f)==10f,"ON exact bounds");
            Require(step(.99f,1,false,.3f)==1f&&step(2.01f,-1,false,.3f)==2f,"OFF inward lands exactly at each ordinary boundary");
            foreach(float value in new[]{.3f,.5f,1f,2f,5f,10f})Require(step(value,0,true,.3f)==value&&step(value,0,false,.3f)==value,"zero input never changes selection");
            Require(step(.5f,-1,false,.3f)==.5f&&step(5f,1,false,.3f)==5f,"OFF retained outward is blocked");
            Require(Math.Abs(step(1.5f,1,false,.3f)-1.52f)<.000001f&&Math.Abs(step(1.5f,-1,false,.3f)-1.48f)<.000001f,"ordinary OFF retains native two-percentage-point controls");
            foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {float result=step(bad,1,true,.3f);Require(float.IsNaN(bad)?float.IsNaN(result):result==bad,"invalid target is not revived by held input");}
            var minimum=method.DeclaringType.GetMethod("MinimumTarget",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(float forced in new[]{1f,2f})
            {
                float floor=(float)minimum.Invoke(null,new object[]{3840,2160,forced});
                Require(step(floor,-1,true,floor)==floor&&step(floor,1,true,floor)>floor,"actual geometry/forced target floor clamps outward and permits inward");
                Console.WriteLine("ZOOM SPEED geometry3840 forced="+forced+" floor="+floor+" native="+NativeZoom(floor,forced));
            }
            float low=.3f,previous=NativeZoom(low,1),run=0,maxRun=0;
            for(int i=0;i<70;i++){low=step(low,1,true,.3f);float next=NativeZoom(low,1);run=next==previous?run+1:0;maxRun=Math.Max(maxRun,run);previous=next;}
            Require(maxRun<=1&&low>1f,"unrounded target accumulates through low-end native quantization; no long stationary stair");
            for(int i=0;i<10000;i++)step(1f,1,true,.3f);
            long allocation=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            float sample=1;
            for(int i=0;i<100000;i++)sample=step(sample,(i&1)==0?1:-1,true,.3f);
            long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocation;
            Require(allocated==0&&sample>.99f&&sample<1.01f,"warmed arithmetic-only100000 opportunities allocate zero bytes without resources, IO or new clock");
            Console.WriteLine("PASS ZOOM SPEED model mappings="+String.Join(",",counts)+" low-native-max-still="+maxRun+" allocated100000="+allocated);
        }
        private static int Travel(Func<float,int,bool,float,float> step,float start,float end,int direction)
        {
            float value=start;int n=0;
            while((direction>0?value<end:value>end)&&n<1000){float next=step(value,direction,true,.3f);Require(direction>0?next>value:next<value,"held target advances before endpoint");value=next;n++;}
            Require(n<1000,"ratio travel reaches its endpoint");
            Console.WriteLine("ZOOM SPEED factor-two start="+start+" end="+end+" direction="+direction+" mappings="+n+" target="+value+" native="+NativeZoom(value,1)+" forced2-native="+NativeZoom(value,2));
            return n;
        }
        private static float NativeZoom(float target,float forced)
        {return (float)(Math.Round((float)((float)(target*forced)/(1f/128f)))*(1f/128f));}

        internal static void Run(Main game,object view,Action<Keys[]> input,Action draw,Func<RenderTarget2D> final,string output)
        {
            Model(view.GetType().Assembly);
            var ownerInput=Get(view,"input");float original=Main.GameZoomTarget;bool enabled=(bool)Get(view,"UnlockEnabled");
            Action release=()=>{input(new Keys[0]);input(new Keys[0]);};
            Func<float,float,int,int> hold=(start,end,direction)=>
            {
                release();Main.GameZoomTarget=start;
                Keys[] keys={direction>0?Keys.X:Keys.Z};input(keys);int count=0;
                while((direction>0?Main.GameZoomTarget<end:Main.GameZoomTarget>end)&&count<1000)
                {
                    float before=Main.GameZoomTarget;input(keys);count++;
                    Require(direction>0?Main.GameZoomTarget>before:Main.GameZoomTarget<before,"real remapped hold advances each mapped target opportunity");
                    float selected=Main.GameZoomTarget;Call(view,"ConsumeZoom");Require(Main.GameZoomTarget==selected,"same real Mapping cannot be consumed twice");
                }
                Console.WriteLine("ZOOM SPEED actual-mapping start="+start+" end="+end+" count="+count+" target="+Main.GameZoomTarget);
                return count;
            };
            try
            {
                Call(view,"SetUnlockEnabled",true);
                var counts=new List<int>();
                foreach(float start in new[]{.3f,.5f,1f,2f,5f})counts.Add(hold(start,start*2,1));
                foreach(float start in new[]{.6f,1f,2f,5f,10f})counts.Add(hold(start,start/2,-1));
                Require(counts.Max()-counts.Min()<=1,"actual original Mapping factor-two counts uniform in both directions");
                foreach(bool on in new[]{true,false})foreach(float start in new[]{.3f,1.5f,10f})
                {
                    release();Call(view,"SetUnlockEnabled",on);Main.GameZoomTarget=start;
                    input(new[]{Keys.X,Keys.Z});input(new[]{Keys.X,Keys.Z});Require(Main.GameZoomTarget==start,"actual two-key net zero at all three domains");
                    release();Require(Main.GameZoomTarget==start,"no keys never jump either permission state");
                    foreach(bool fullMap in new[]{true,false})
                    {
                        if(fullMap)Main.mapFullscreen=true;else Set(ownerInput,"HotkeyCapture",true);
                        try{input(new[]{Keys.X});input(new[]{Keys.X});Require(Main.GameZoomTarget==start,"existing full-map/capture input gate remains closed");release();}
                        finally{Main.mapFullscreen=false;Set(ownerInput,"HotkeyCapture",false);}
                    }
                }
                Call(view,"SetUnlockEnabled",false);
                foreach(float start in new[]{.3f,10f})
                {
                    release();Main.GameZoomTarget=start;draw();float actual=Main.GameViewMatrix.RenderZoom.X;
                    input(new Keys[0]);draw();Require(Main.GameZoomTarget==start&&Main.GameViewMatrix.RenderZoom.X==actual,"OFF retained selection and actual image do not jump");
                    int inward=start<1?1:-1;Keys[] outward={inward>0?Keys.Z:Keys.X};input(outward);input(outward);Require(Main.GameZoomTarget==start,"OFF actual outward hold blocked");
                    int steps=hold(start,start<1?1:2,inward);Require(steps>1&&Main.GameZoomTarget==(start<1?1:2),"OFF actual inward reaches exact ordinary endpoint");draw();
                }
                Call(view,"SetUnlockEnabled",true);
                foreach(float start in new[]{.3f,10f})
                {
                    release();Main.GameZoomTarget=start;
                    foreach(int direction in start<1?new[]{1,-1}:new[]{-1,1})
                    {
                        release();Keys[] keys={direction>0?Keys.X:Keys.Z};input(keys);
                        for(int i=0;i<8;i++)
                        {
                            input(keys);draw();float expected=NativeZoom(Main.GameZoomTarget,Main.ForcedMinimumZoom);
                            Require(Main.GameViewMatrix.RenderZoom.X==expected,"continuous native Draw publishes actual selected target/forced quantization");
                            Require(Get(view,"Plan")!=null,"continuous zoom preserves real render-plan route");
                            Console.WriteLine("ZOOM SPEED actual-draw direction="+direction+" target="+Main.GameZoomTarget+" native="+expected+" route="+Get(Get(view,"Plan"),"Route"));
                        }
                    }
                    using(var file=File.Create(Path.Combine(output,start<1?"zoom-speed-wide.png":"zoom-speed-narrow.png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                }
                Console.WriteLine("PASS ZOOM SPEED original Mapping/duplicate/two-key/UI gates, both OFF retained endpoints and32 actual continuous low/high Draw; no owner/FPS/full network matrix claim.");
            }
            finally{release();Call(view,"SetUnlockEnabled",enabled);Main.GameZoomTarget=original;}
        }
    }
}
