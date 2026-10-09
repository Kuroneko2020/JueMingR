using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Separate quality process only: fixture seeds are legal observed states,
    // not future answers. Every subsequent frame uses the original full step.
    internal static class NativeCombatRollingMechanismChecks
    {
        internal static void Run(object context,object host,NpcPredictionCache cache,Action step,NativeCombatRollingQualityChecks quality,string output)
        {
            var rows=new List<string>{"scene,frame,tick,type,netId,carriedItem,ai0,ai1,ai2,ai3,wet,npcX,npcY,playerX,playerY,playerLife,mountActive,mountType,up,down,jump,selected,published,count,stop"};
            string[] scenes={"derpling-quiet","tortoise-walk","tortoise-prepare","tortoise-launch-wall","tortoise-fall","tortoise-land","tortoise-wet","tortoise-cliff","jungle-slime","jungle-slime-herb","ordinary-jump","broom-up","broom-hover","ufo-up","ufo-hover","player-water","hornet-prepare"};
            try
            {
                foreach(string scene in scenes)
                {
                    quality.ChangeScene(scene,true);
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                    var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=500;
                    for(int i=0;i<3;i++)p.armor[i].SetDefaults(696+i);
                    int type=scene.StartsWith("tortoise",StringComparison.Ordinal)?NPCID.GiantTortoise:scene.StartsWith("jungle",StringComparison.Ordinal)?NPCID.JungleSlime:scene=="hornet-prepare"?NPCID.MossHornet:NPCID.Derpling;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1900,2400,type,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];
                    if(scene=="tortoise-prepare" || scene=="tortoise-launch-wall"){n.ai[0]=1;n.ai[1]=28;}
                    if(scene=="tortoise-fall"){n.ai[0]=4;n.ai[2]=.1f;n.position.Y-=140;n.velocity=new Vector2(-3,2);}
                    if(scene=="tortoise-land"){n.ai[0]=5;n.ai[1]=0;}
                    if(scene=="tortoise-launch-wall")for(int y=130;y<150;y++){Main.tile[90,y].active(true);Main.tile[90,y].type=1;}
                    if(scene=="tortoise-cliff")for(int x=111;x<121;x++)for(int y=150;y<156;y++)Main.tile[x,y].active(false);
                    if(scene=="tortoise-wet" || scene=="player-water")
                    {
                        int center=scene=="player-water"?69:119;
                        for(int x=center-4;x<center+6;x++)for(int y=143;y<150;y++)Main.tile[x,y].liquid=255;
                    }
                    // AI_001's netID -10 branch can carry this exact item (314).
                    // The carried value is logged separately from type/netID;
                    // its presence does not prove it caused an error.
                    if(scene=="jungle-slime-herb")n.ai[1]=314;
                    if(scene.StartsWith("broom",StringComparison.Ordinal) || scene.StartsWith("ufo",StringComparison.Ordinal))
                    {
                        NativeCombatLiveContextChecks.InitializeMount();
                        int local=Main.myPlayer;
                        // Existing isolated original fixture avoids local-only
                        // camera/texture presentation; mount state is native.
                        try{Main.myPlayer=1;p.mount.SetMount(scene.StartsWith("broom",StringComparison.Ordinal)?MountID.WitchBroom:MountID.UFO,p);}
                        finally{Main.myPlayer=local;}
                        p.position.Y=2100;p.velocity=Vector2.Zero;
                    }
                    if(scene=="hornet-prepare"){n.position.Y=2100;n.ai[1]=180;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:25));
                    for(int frame=0;frame<240;frame++)
                    {
                        NativeCombatRollingBaselineChecks.QualityFrame(scene,frame,slot);
                        p.controlUp=scene.EndsWith("-up",StringComparison.Ordinal);p.controlJump=scene=="ordinary-jump";p.controlDown=false;
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                        var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");bool selected=(bool)Get(selection,"HasTarget") && ReferenceEquals(key.Token,n);
                        var path=cache.Read(0);if(path!=null && (!selected || !path.Identity.Equals(key) || path.CaptureTick!=Main.GameUpdateCount))throw new InvalidOperationException("Mechanism ledger consumed a stale/wrong result.");
                        quality.Observe();if(selected)quality.Capture(scene,frame,path);
                        rows.Add(Csv(scene,frame,Main.GameUpdateCount,n.type,n.netID,n.type==1?n.ai[1]:-1,n.ai[0],n.ai[1],n.ai[2],n.ai[3],n.wet,n.position.X,n.position.Y,p.position.X,p.position.Y,p.statLife,p.mount.Active,p.mount.Type,p.controlUp,p.controlDown,p.controlJump,selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none"));
                        if(!n.active || n.life<=0 || p.dead)break;
                    }
                }
            }
            finally{File.WriteAllLines(Path.Combine(output,"rolling-mechanisms.csv"),rows);}
        }
        private static string Csv(params object[] values)
        {var fields=new string[values.Length];for(int i=0;i<fields.Length;i++)fields[i]="\""+Convert.ToString(values[i],CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";return string.Join(",",fields);}
    }
}
