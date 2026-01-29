using System.Linq;
using RimTalk.Client;
using RimTalk.Data;
using RimTalk.Error;
using Multiplayer.API;
using RimTalk.Multiplayer;
using RimTalk.Patch;
using RimTalk.Service;
using Verse;

namespace RimTalk;

public class RimTalk : GameComponent
{
    public RimTalk(Game game)
    {
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();

        // Log multiplayer mode status
        Log.Message($"[RimTalk][MP diag] start new game: MP.IsInMultiplayer={MP.IsInMultiplayer} MP.IsHosting={MP.IsHosting} IsExecutingSync={MP.IsExecutingSyncCommand}");
        if (MpCompatShim.IsInMultiplayer)
        {
            if (MpCompatShim.IsHosting)
                Log.Message("[RimTalk] Multiplayer Host mode - AI generation enabled");
            else
                Log.Message("[RimTalk] Multiplayer Client mode - receiving synced data only");
        }

        Reset();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        Log.Message($"[RimTalk][MP diag] loaded game: MP.IsInMultiplayer={MP.IsInMultiplayer} MP.IsHosting={MP.IsHosting} IsExecutingSync={MP.IsExecutingSyncCommand}");
        Reset();
    }

    public static void Reset(bool soft = false)
    {
        var settings = Settings.Get();
        if (settings != null)
        {
            settings.CurrentCloudConfigIndex = 0;
        }

        // Reset deterministic ID generator in multiplayer
        if (MpCompatShim.IsInMultiplayer)
            DeterministicIdGenerator.Reset();

        AIErrorHandler.ResetQuotaWarning();
        TickManagerPatch.Reset();
        AIClientFactory.Clear();
        AIService.Clear();
        TalkHistory.Clear();
        PatchThoughtHandlerGetDistinctMoodThoughtGroups.Clear();
        Cache.GetAll().ToList().ForEach(pawnState => pawnState.IgnoreAllTalkResponses());
        Cache.InitializePlayerPawn();
        UserRequestPool.Clear();

        if (soft) return;

        Counter.Tick = 0;
        Cache.Clear();
        Stats.Reset();
        TalkRequestPool.Clear();
        ApiHistory.Clear();
    }
}
