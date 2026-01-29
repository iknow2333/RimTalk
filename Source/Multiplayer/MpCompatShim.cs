using Multiplayer.API;
using Verse;

namespace RimTalk.Multiplayer;

public static class MpCompatShim
{
    // If detection fails too early, flip this to true temporarily.
    private const bool ForceMultiplayer = false;

    public static bool IsInMultiplayer => ForceMultiplayer || MP.IsInMultiplayer;

    public static bool IsHosting => MP.IsHosting; // real host check

    public static bool ShouldSync => IsInMultiplayer && !MP.IsExecutingSyncCommand;

    static MpCompatShim()
    {
        Log.Message($"[RimTalk][MP diag] ForceMultiplayer={ForceMultiplayer} MP.enabled={MP.enabled} IsInMultiplayer={MP.IsInMultiplayer} IsHosting={MP.IsHosting}");
    }
}
