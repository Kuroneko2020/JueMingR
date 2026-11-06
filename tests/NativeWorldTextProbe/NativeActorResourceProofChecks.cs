using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.ResourceSets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeActorResourceProofChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            using(var fixture=new NativeActorGestureChecks.Fixture(context))
            using(var ui=new NativeActorGestureChecks.UiHarness())
            using(var observer=new Producer())
            {
                ui.Configure("none");var previous=Main.ResourceSetsManager;bool errors=Main.ignoreErrors;
                var player=Main.LocalPlayer;string[] fields={"statLife","statLifeMax","statLifeMax2","statMana","statManaMax2","breath"};int[] saved=fields.Select(n=>(int)Get(player,n)).ToArray();
                try
                {
                    Main.netMode=0;Main.ignoreErrors=true;player.statLife=80;player.statLifeMax=player.statLifeMax2=100;player.statMana=20;player.statManaMax2=100;player.breath=player.breathMax;
                    foreach(Type type in new[]{typeof(FancyClassicPlayerResourcesDisplaySet),typeof(HorizontalBarsPlayerResourcesDisplaySet),typeof(ClassicPlayerResourcesDisplaySet)})
                    {
                        var display=type==typeof(ClassicPlayerResourcesDisplaySet)?(IPlayerResourcesDisplaySet)new ClassicPlayerResourcesDisplaySet("Classic","Classic"):(IPlayerResourcesDisplaySet)FormatterServices.GetUninitializedObject(type);
                        foreach(var field in type.GetFields(F).Where(f=>f.FieldType==typeof(Asset<Texture2D>)))field.SetValue(display,ui.Texture);
                        var manager=new PlayerResourceSetsManager();Call(manager,"SetActiveFrame",display);Main.ResourceSetsManager=manager;
                        foreach(string mode in new[]{"normal","late","skip","missing"})
                        {
                            fixture.Reset();observer.Reset("normal");fixture.Actor(0,1,new Vector2(320,320));fixture.Neutral(288,328);
                            fixture.Proof(false,()=>Main.instance.GUIBarsDraw(),()=>manager.TryToHoverOverResources());
                            var snapshot=Get(fixture.Targets,"snapshot");Require((bool)Get(snapshot,"committedResourcesReady"),"normal active resource whole Draw establishes the preceding visible receipt");
                            fixture.Sample(true,328,328);Require((bool)Get(fixture.Targets,"Busy"),"resource case starts an admitted original actor request");fixture.Update();fixture.Sample(false,344,328);fixture.Update();
                            observer.Reset(mode);
                            fixture.Proof(false,()=>{if(mode!="missing")Main.instance.GUIBarsDraw();},()=>manager.TryToHoverOverResources());
                            bool ready=(bool)Get(snapshot,"committedResourcesReady");fixture.Update();for(int i=0;i<5 && (bool)Get(fixture.Targets,"Busy");i++)fixture.Update();
                            if(mode=="late")Require(observer.Faults==1 && observer.Caught==1 && (type==typeof(ClassicPlayerResourcesDisplaySet)?observer.ClassicLife>0:observer.Completed>0),"actual active Draw completes earlier fragments before a later exception reaches the real GUIBarsDraw ignoreErrors catch");
                            if(mode=="normal")Require(observer.Returned==1 && observer.Caught==0 && (type==typeof(ClassicPlayerResourcesDisplaySet)?observer.ClassicLife>0:observer.Completed>=2),"positive actually finishes the active Draw and original settings without a swallowed precondition failure");
                            Console.WriteLine("ACTOR-RESOURCE-ROOT family="+type.Name+" case="+mode+" roots="+observer.Roots+" returned="+observer.Returned+" settings="+observer.Completed+" classicLife="+observer.ClassicLife+" faults="+observer.Faults+" nativeCaught="+observer.Caught+" ready="+ready+" public="+NativeActorGestureChecks.Created+" notices="+NativeActorGestureChecks.Notices);
                            bool success=mode=="normal";Require(ready==success && NativeActorGestureChecks.Created==(success?1:0),"only a complete actual active resource Draw may authorize PQR actor proof: "+type.Name+"/"+mode);
                        }
                    }
                }
                finally{Main.ResourceSetsManager=previous;Main.ignoreErrors=errors;for(int i=0;i<fields.Length;i++)Set(player,fields[i],saved[i]);}
            }
            Console.WriteLine("PASS active resource proof L2: real Fancy/Horizontal/Classic Draw -> original settings/fragments -> real GUIBarsDraw ignoreErrors catch -> original TryToHover -> controlled complete UI -> Host/native chat. Normal/late exception/skip/missing; assets/player/input and terminal graphics/network controlled. No Main loop/GPU/OS/network; incomplete proof is not a claim of reproduced UI penetration.");
        }
        private sealed class Producer:IDisposable
        {
            private static Producer current;
            private readonly Harmony hook=new Harmony("JueMingR.Tests.ActorResourceRoot."+Guid.NewGuid().ToString("N"));
            private string mode;private int visits;
            internal int Roots,Returned,Completed,ClassicLife,Faults,Caught;
            internal Producer()
            {
                current=this;
                hook.Patch(typeof(ResourceDrawSettings).GetMethod("Draw"),prefix:Method(nameof(Fragment)),postfix:Method(nameof(FragmentDone)));
                foreach(Type type in new[]{typeof(FancyClassicPlayerResourcesDisplaySet),typeof(HorizontalBarsPlayerResourcesDisplaySet),typeof(ClassicPlayerResourcesDisplaySet)})
                    hook.Patch(type.GetMethod("Draw"),prefix:Method(nameof(Root)),postfix:Method(nameof(RootDone)));
                hook.Patch(typeof(ClassicPlayerResourcesDisplaySet).GetMethod("DrawLife",F),postfix:Method(nameof(LifeDone)));
                hook.Patch(typeof(ClassicPlayerResourcesDisplaySet).GetMethod("DrawMana",F),prefix:Method(nameof(Mana)));
                hook.Patch(typeof(TimeLogger).GetMethod("DrawException"),prefix:Method(nameof(Catch)));
            }
            private static HarmonyMethod Method(string name){return new HarmonyMethod(typeof(Producer).GetMethod(name,F));}
            internal void Reset(string value){mode=value;visits=Roots=Returned=Completed=ClassicLife=Faults=Caught=0;}
            private static bool Root(){current.Roots++;return current.mode!="skip";}
            private static void RootDone(bool __runOriginal){if(__runOriginal)current.Returned++;}
            private static void Fragment(){if(++current.visits==2 && current.mode=="late")Fail();}
            private static void FragmentDone(bool __runOriginal){if(__runOriginal)current.Completed++;}
            private static void LifeDone(bool __runOriginal){if(__runOriginal)current.ClassicLife++;}
            private static void Mana(){if(current.mode=="late")Fail();}
            private static void Fail(){current.Faults++;throw new InvalidOperationException("controlled-later-resource-fragment");}
            private static void Catch(){current.Caught++;}
            public void Dispose(){hook.UnpatchAll(hook.Id);current=null;}
        }
    }
}
