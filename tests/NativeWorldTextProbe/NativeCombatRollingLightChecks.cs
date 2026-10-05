using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatRollingBaselineChecks
    {
        // Explicit developer mode, never a product setting. The existing heavy
        // ledger remains intact. Numbers cannot be compared as product speedup:
        // this mode removes observers and defers formatting until AFTER updates.
        private static bool light;
        private static int lightRowCount,lightHitCount,lightHornetBirths;
        private static LightRow[] lightRows;
        private static LightImpact[] lightHits;
        private static Func<NpcIdentity> lightTarget;
        private static Func<bool> lightHasTarget;
        private static Func<int> lightStrokes,lightEvents;
        private static Func<string> lightText;
        private static Action lightDraw,lightBegin,lightMapping,lightKeyboard,lightSample;
        private static Action<List<string>> lightMouseNative;
        private static readonly List<string> lightMouseErrors=new List<string>();
        private struct LightRow
        {
            internal string Phase;internal int Frame,Count,Life,PlayerLife,Strokes,G0,G1,G2,Hits,SourceG0,SourceG1,SourceG2,SourceCalls;
            internal long Tick,CaptureTick,SampleTick,SourceBytes,HostBytes;
            internal NpcIdentity Expected,Selected;
            internal bool Legal,Picked,Shown,Text,Wet,Mounted;
            internal double Host,Shell,Source,Prepare,Draw,Wall;
        }
        private struct LightImpact
        {
            internal string Phase,Role;internal int Frame,Projectile,Damage,Life;internal long Tick;internal NpcIdentity Victim;
        }
        private static Func<T> LightGet<T>(object owner,string name)
        {
            var member=owner.GetType().GetMember(name,Flags);
            if(member.Length!=1)throw new InvalidOperationException("Light getter missing/ambiguous: "+name);
            var target=Expression.Constant(owner,owner.GetType());
            Expression body=member[0] is FieldInfo?Expression.Field(target,(FieldInfo)member[0]):Expression.Property(target,(PropertyInfo)member[0]);
            return Expression.Lambda<Func<T>>(body).Compile();
        }
        private static Action LightAction(object owner,string name)
            =>(Action)Delegate.CreateDelegate(typeof(Action),owner,owner.GetType().GetMethod(name,Flags));
        private static void LightInitialize(object context,object host,object selection,object layer)
        {
            lightRows=new LightRow[8400];lightHits=new LightImpact[2048];lightRowCount=lightHitCount=lightHornetBirths=0;
            lightTarget=LightGet<NpcIdentity>(selection,"Target");lightHasTarget=LightGet<bool>(selection,"HasTarget");
            lightStrokes=LightGet<int>(layer,"StrokeCount");lightEvents=LightGet<int>(layer,"eventEnd");lightText=LightGet<string>(layer,"pathText");
            var draw=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),layer,layer.GetType().GetMethod("Draw",Flags));lightDraw=()=>{draw();};
            var input=NativeCombatAttackMechanismChecks.Get(context,"Input");
            lightBegin=LightAction(input,"BeginUpdate");lightMapping=LightAction(input,"AfterMapping");lightKeyboard=LightAction(input,"AfterKeyboardRefresh");lightSample=LightAction(host,"SampleMouse");
            lightMouseNative=(Action<List<string>>)Delegate.CreateDelegate(typeof(Action<List<string>>),input,input.GetType().GetMethod("AfterNativeMouse",Flags));
        }
        private static void LightMouse(Vector2 point)
        {
            Main.screenPosition=point-new Vector2(Main.screenWidth/2,Main.screenHeight/2);lightBegin();lightMouseErrors.Clear();
            Terraria.GameInput.PlayerInput.MouseInfo=new MouseState(Main.screenWidth/2,Main.screenHeight/2,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            lightMouseNative(lightMouseErrors);lightMapping();lightKeyboard();lightSample();
            if(lightMouseErrors.Count!=0)throw new InvalidOperationException("Light mouse input error.");
        }
        private static void LightScene(string scene,Player player)
        {
            if(scene=="broom-dense" || scene=="player-water")
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();aSlot=-1;aActor=default(NpcIdentity);
                bSlot=Spawn(scene=="broom-dense"?NPCID.MossHornet:NPCID.GiantTortoise,1900);bActor=Actor(Main.npc[bSlot]);
                player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;
                if(scene=="broom-dense")
                {
                    NativeCombatLiveContextChecks.InitializeMount();int local=Main.myPlayer;
                    try{Main.myPlayer=1;player.mount.SetMount(MountID.WitchBroom,player);}finally{Main.myPlayer=local;}
                    player.position.Y=2100;Main.npc[bSlot].position.Y=2100;
                    for(int i=0;i<16;i++)Spawn(NPCID.BlueSlime,2800+i*24);
                }
                else
                {
                    int local=Main.myPlayer;try{Main.myPlayer=1;player.mount.Dismount(player);}finally{Main.myPlayer=local;}
                    for(int x=65;x<75;x++)for(int y=143;y<150;y++)Main.tile[x,y].liquid=255;
                    // Remote liquid is an unrelated world change; ordinary
                    // target-local acquisition must not discover a full world.
                    for(int x=210;x<220;x++)for(int y=143;y<150;y++)Main.tile[x,y].liquid=255;
                }
            }
        }
        private static void LightHit(NPC victim,int damage)
        {
            if(lightHitCount==lightHits.Length)throw new InvalidOperationException("Bounded light impact buffer exhausted; retain failed attempt.");
            lightHits[lightHitCount++]=new LightImpact{Phase=phase,Frame=frame,Tick=Main.GameUpdateCount,Victim=Actor(victim),Life=victim.life,Damage=damage,Projectile=damageSource?.type??-1,Role=Matches(aActor,victim)?"A":Matches(bActor,victim)?"B":"other"};
        }
        private static bool LightRequiredHit(int start,string role,int projectile)
        {for(int i=start;i<lightHitCount;i++)if(lightHits[i].Role==role && lightHits[i].Projectile==projectile)return true;return false;}
        private static void LightRecord(NPC target,NpcIdentity expected,NpcIdentity selected,bool legal,bool picked,NpcTrajectory path,int strokes,bool text,double wall,double draw,int g0,int g1,int g2,int hits)
        {
            if(lightRowCount==lightRows.Length)throw new InvalidOperationException("Bounded light update buffer exhausted; retain failed attempt.");
            lightRows[lightRowCount++]=new LightRow{Phase=phase,Frame=frame,Tick=Main.GameUpdateCount,CaptureTick=path?.CaptureTick??-1,SampleTick=path?.SampleTick??-1,Expected=expected,Selected=selected,Legal=legal,Picked=picked,Shown=path!=null,Count=path?.Count??0,Life=target.life,PlayerLife=Main.LocalPlayer.statLife,Strokes=strokes,Text=text,Wet=Main.LocalPlayer.wet,Mounted=Main.LocalPlayer.mount.Active,Host=Times[8],Shell=Times[9],Source=Times[1],Prepare=Times[5],Draw=Times[6],Wall=wall,G0=g0,G1=g1,G2=g2,Hits=hits,SourceG0=sourceGc[0],SourceG1=sourceGc[1],SourceG2=sourceGc[2],SourceBytes=Bytes[1],HostBytes=Bytes[8],SourceCalls=Calls[1]};
        }
        private static void LightEnd(object host,NpcPredictionCache cache,Action step)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Array.Clear(Times,0,Times.Length);Array.Clear(Bytes,0,Bytes.Length);Array.Clear(Calls,0,Calls.Length);Array.Clear(sourceGc,0,sourceGc.Length);
            phase="exit-off";frame=0;step();
            if(cache.Required!=0 || cache.Read(0)!=null || lightStrokes()!=0 || lightText()!=null || Calls[1]!=0)
                throw new InvalidOperationException("Light final OFF must retire result, display and prediction demand.");
        }
        private static void LightWrite(string output,bool completed)
        {
            var rows=new List<string>{"phase,frame,tick,captureTick,sampleTick,legal,selected,shown,count,expectedToken,expectedSlot,expectedGeneration,expectedType,expectedNetId,selectedToken,selectedSlot,selectedGeneration,selectedType,selectedNetId,life,playerLife,pathStrokes,text,wet,mounted,hostMs,shellMs,sourceMs,prepareMs,drawMs,wallMs,hostBytes,sourceBytes,sourceCalls,gc0,gc1,gc2,sourceGc0,sourceGc1,sourceGc2,hits"};
            for(int i=0;i<lightRowCount;i++){var r=lightRows[i];rows.Add(NativeCombatAttackMechanismChecks.Csv(r.Phase,r.Frame,r.Tick,r.CaptureTick,r.SampleTick,r.Legal,r.Picked,r.Shown,r.Count,Token(r.Expected.Token),r.Expected.Slot,r.Expected.Generation,r.Expected.Type,r.Expected.NetId,Token(r.Selected.Token),r.Selected.Slot,r.Selected.Generation,r.Selected.Type,r.Selected.NetId,r.Life,r.PlayerLife,r.Strokes,r.Text,r.Wet,r.Mounted,r.Host,r.Shell,r.Source,r.Prepare,r.Draw,r.Wall,r.HostBytes,r.SourceBytes,r.SourceCalls,r.G0,r.G1,r.G2,r.SourceG0,r.SourceG1,r.SourceG2,r.Hits));}
            File.WriteAllLines(Path.Combine(output,"light-updates.csv"),rows);
            rows.Clear();rows.Add("phase,frame,tick,projectileType,role,victimToken,victimSlot,victimGeneration,victimType,victimNetId,damage,life");
            for(int i=0;i<lightHitCount;i++){var h=lightHits[i];rows.Add(NativeCombatAttackMechanismChecks.Csv(h.Phase,h.Frame,h.Tick,h.Projectile,h.Role,Token(h.Victim.Token),h.Victim.Slot,h.Victim.Generation,h.Victim.Type,h.Victim.NetId,h.Damage,h.Life));}
            File.WriteAllLines(Path.Combine(output,"light-hits.csv"),rows);
            File.WriteAllText(Path.Combine(output,"light-status.txt"),"completed="+completed+" rows="+lightRowCount+" impacts="+lightHitCount+" selected-hornet55-births="+lightHornetBirths+"; low observer, not product speedup/FPS; source/runtime/shell/prepare/draw inclusive scopes overlap; natural deaths/tails in rolling-summary.csv");
            lightRows=null;lightHits=null;
        }
    }
}
