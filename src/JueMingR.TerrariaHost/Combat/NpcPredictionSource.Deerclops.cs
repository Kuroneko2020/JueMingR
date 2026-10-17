using JueMingR.Platform.Combat;
using Terraria;
namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private static bool DeerclopsMotor(NPC n){return n!=null && n.type==668 && n.aiStyle==123;}
        private static PredictionDeerclopsPlayer ReadDeerclopsPlayer(int slot)
        {
            if(slot<0 || slot>=Main.player.Length || Main.player[slot]==null)return default(PredictionDeerclopsPlayer);
            var p=Main.player[slot];int index=p.FindBuffIndex(32);
            return new PredictionDeerclopsPlayer(p,slot,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.active,p.dead,p.ghost,p.ZoneSnow,p.creativeGodMode,p.buffImmune[32],index>=0,index<0?0:p.buffTime[index],p.slow);
        }
        // Called AFTER ordinary Tracking.Capture. This flag refers to LOCAL
        // player's completed action: PrepareAction=true, Prepare=false. It is
        // deliberately not the potentially different target's playerAdvanced.
        // No extra native AI, RNG or world copy.
        private static void CaptureDeerclopsInputs(NPC n,ref NpcMotionState state,bool firstPlayerStepCompleted)
        {
            if(!DeerclopsMotor(n))return;
            state.DeerFactsCaptured=true;state.DeerHomeInitialized=n.homeTileX!=-1 || n.homeTileY!=-1;
            state.DeerFirstPlayerStepCompleted=firstPlayerStepCompleted;state.DeerDayRate=Main.dayRate;state.DeerNetMode=Main.netMode;state.DeerSlowConsumerAt=int.MaxValue;
            state.DeerPlayers=new PredictionDeerclopsPlayers(ReadDeerclopsPlayer(n.target),ReadDeerclopsPlayer(state.HasClosestPlayer?state.ClosestPlayerIndex:-1),ReadDeerclopsPlayer(Main.myPlayer));
        }
    }
}
