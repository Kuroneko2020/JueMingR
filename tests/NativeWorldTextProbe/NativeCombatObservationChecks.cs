using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatObservationChecks
    {
        internal static void Save(object host,ObservationOptions value)
        {var settings=(ObservationSettings)Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Loaded;});Require(settings.Set(value),"observation preference accepted");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Require(settings.CompletionSucceeded,"observation preference actually committed");}
        internal static void Run(object context)
        {
            var host=GetOptional(context,"CombatObservation");Require(host!=null,"full candidate has an independent observation owner");
            Require((bool)Get(Get(host,"Hooks"),"Ready"),"native collision observation ABI installed: "+GetOptional(Get(host,"Hooks"),"Error"));
            Save(host,new ObservationOptions());
            var selection=Get(host,"Selection");var geometry=Get(host,"Geometry");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            int before=(int)Get(selection,"Candidates"),samples=(int)Get(geometry,"ProjectileSamples"),steps=cache.Steps;
            for(int i=0;i<600;i++)Call(host,"Update",(ulong)i);
            Require((int)Get(selection,"Candidates")==before && (int)Get(geometry,"ProjectileSamples")==samples && cache.Steps==steps,"OFF real entry has no candidate/shape/prediction work");
            foreach(var n in Main.npc)n.active=false;
            var player=Main.LocalPlayer;player.position=new Vector2(640,640);player.itemAnimation=player.itemTime=0;player.inventory[0].SetDefaults(ItemID.CopperPickaxe);
            var bubble=Main.npc[0];bubble.SetDefaults(371);bubble.whoAmI=0;bubble.active=true;bubble.position=new Vector2(700,650);bubble.target=0;bubble.ai[3]=1;
            Save(host,new ObservationOptions(path:true,radius:0));Fresh(context,host);
            Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Type==371,"path alone selects 1 HP bubble while holding a tool with no attack; player range ignores mouse radius zero");
            var result=cache.Read(0);Require(result!=null && result.Count>1,"real path demand prepares a future without aim provider");
            int existing=cache.Steps;for(int i=0;i<2000;i++)Require(ReferenceEquals(cache.Read(0),result),"cached reads preserve result identity");Require(cache.Steps==existing,"reading never advances NPC simulation");
            cache.Demand(1,60);Save(host,new ObservationOptions());Require(cache.Read(1)!=null,"real Host turning path off preserves another registered consumer");Fresh(context,host);Require((bool)Get(selection,"HasTarget") && cache.Read(1)!=null,"remaining demand still prepares through Host");cache.Release(1);Call(host,"Poll");Require(!(bool)Get(selection,"HasTarget"),"last consumer retirement clears shared selection");Save(host,new ObservationOptions(path:true,radius:0));Fresh(context,host);
            var old=(NpcIdentity)Get(selection,"Target");var replacement=new NPC();replacement.SetDefaults(371);replacement.whoAmI=0;replacement.active=true;replacement.position=bubble.position;replacement.target=0;replacement.ai[3]=1;Main.npc[0]=bubble=replacement;Fresh(context,host);Require(!((NpcIdentity)Get(selection,"Target")).Equals(old),"slot object replacement retires old identity");
            bubble.dontTakeDamage=true;Fresh(context,host);Require(!(bool)Get(selection,"HasTarget") && cache.Read(0)==null,"invulnerability retires target without Draw or a boss lock");
            bubble.active=false;var dummy=Main.npc[1];dummy.SetDefaults(NPCID.TargetDummy);dummy.whoAmI=1;dummy.active=true;dummy.position=new Vector2(680,650);
            Fresh(context,host);Require(!(bool)Get(selection,"HasTarget"),"dummy excluded by preference");
            Save(host,new ObservationOptions(path:true,dummy:true));Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Type==NPCID.TargetDummy,"dummy opt-in handles immortal dummy separately");
            Save(host,new ObservationOptions(collision:true));Fresh(context,host);Require(cache.Read(0)==null && !(bool)Get(selection,"HasTarget"),"collision alone does not select or predict");
            foreach(var n in Main.npc)n.active=false;
            var near=Main.npc[2];near.SetDefaults(2);near.active=true;near.whoAmI=2;near.target=0;near.position=new Vector2(760,650);
            var far=Main.npc[3];far.SetDefaults(2);far.active=true;far.whoAmI=3;far.target=0;far.position=new Vector2(640,880);
            Main.tileSolid[TileID.Stone]=true;for(int y=36;y<49;y++){Main.tile[45,y].active(true);Main.tile[45,y].type=TileID.Stone;}
            Save(host,new ObservationOptions(path:true));Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Slot==2,"nearest ranks hitbox distance regardless of current occlusion");
            Save(host,new ObservationOptions(path:true,clearLine:true));Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Slot==3,"clear-line preference can choose a farther unblocked target");
            far.active=false;Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Slot==2,"occlusion never hard-removes the remaining candidate");
            for(int y=36;y<49;y++)Main.tile[45,y].active(false);
            Main.screenPosition=Vector2.Zero;Main.LocalPlayer.gravDir=1;PlayerInput.CacheOriginalScreenDimensions();
            var input=Get(context,"Input");Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState(765,655,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));Call(host,"SampleMouse");var physical=(Vector2)Get(selection,"RealMouse");
            Main.mouseX=Main.mouseY=2;Fresh(context,host);Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==2,"zero-radius physical point can overlap a body and ignores later virtual mouse writes");
            Set(input,"mapped",false);Set(input,"PhysicalMapX",1);Call(host,"SampleMouse");Require((Vector2)Get(selection,"RealMouse")==physical,"invalid/focusless input retains last reliable intent");
            for(int i=0;i<300;i++){near.active=false;Fresh(context,host);}Require(cache.Read(0)==null,"enabled empty world never preserves a stale path");
            Save(host,new ObservationOptions());
            Console.WriteLine("PASS first batch: actual OFF=600, independent toggles, no-weapon path, bubble/dummy eligibility, identity retirement, 2000 cached reads.");
        }
        internal static void Release(object context)
        {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name.StartsWith("JueMingR.",StringComparison.Ordinal))
            {var config=(System.Reflection.AssemblyConfigurationAttribute)Attribute.GetCustomAttribute(assembly,typeof(System.Reflection.AssemblyConfigurationAttribute));Require(config!=null && config.Configuration=="Release","Release probe must not mix Debug dependencies: "+assembly.Location);Console.WriteLine("RELEASE ASSEMBLY "+assembly.GetName().Name+" MVID="+assembly.ManifestModule.ModuleVersionId+" path="+assembly.Location);}
            var host=Get(context,"CombatObservation");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");foreach(var n in Main.npc)n.active=false;
            Main.LocalPlayer.position=new Vector2(640,640);Main.LocalPlayer.itemAnimation=Main.LocalPlayer.itemTime=0;
            var npc=Main.npc[0];npc.SetDefaults(371);npc.whoAmI=0;npc.active=true;npc.position=new Vector2(700,650);npc.target=0;npc.ai[3]=1;
            Save(host,new ObservationOptions(path:true));Fresh(context,host);Require(cache.Read(0)!=null && cache.Read(0).Count>1,"Release path creates a real shared forecast");
            Save(host,new ObservationOptions(collision:true));Fresh(context,host);Require(cache.Read(0)==null && !(bool)Get(Get(host,"Selection"),"HasTarget"),"Release collision alone does not forecast");
            var p=Main.projectile[4];p.SetDefaults(632);p.active=true;p.whoAmI=4;p.owner=0;p.damage=10;p.friendly=true;p.velocity=Vector2.UnitX;p.localAI[1]=900;p.position=new Vector2(300,300);p.Damage();
            var geometry=Get(host,"Geometry");Require((int)Get(((Array)Get(geometry,"Attacks")).GetValue(4),"Count")==2,"Release natural Damage produces beam geometry");
            Save(host,new ObservationOptions(collision:true,path:true));Fresh(context,host);Require(cache.Read(0)!=null,"Release combined mode retains prediction");Call(host,"OnSessionEnded");Require(cache.Read(0)==null && cache.Required==0 && !(bool)Get(Get(host,"Selection"),"HasTarget") && ((Array)Get(geometry,"Attacks")).GetValue(4)==null,"Release session exit retires demands identities and geometry");
            Call(host,"OnSessionStarted");Save(host,new ObservationOptions());Fresh(context,host);Require(!(bool)Get(host,"Enabled"),"Release all OFF");Console.WriteLine("PASS Release first batch: actual shared forecast, natural beam, independent modes and complete session retirement.");
        }
        internal static void Fresh(object context,object host)
        {NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");Call(host,"Update",(ulong)Main.GameUpdateCount);}
    }
}
