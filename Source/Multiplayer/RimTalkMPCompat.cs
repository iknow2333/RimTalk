using System;
using HarmonyLib;
using Multiplayer.API;
using RimTalk.Data;
using RimTalk.Source.Data;
using Verse;

namespace RimTalk.Multiplayer
{
    /// <summary>
    /// Multiplayer compatibility layer for RimTalk.
    /// Registers SyncWorkers and SyncMethods for network synchronization.
    /// RimTalk的多人兼容层，注册SyncWorker和SyncMethod用于网络同步。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkMPCompat
    {
        static RimTalkMPCompat()
        {
            if (!MP.enabled)
            {
                Log.Message("[RimTalk] Multiplayer mod not detected, skipping MP compatibility initialization");
                return;
            }

            Log.Message("[RimTalk] Initializing Multiplayer compatibility...");

            try
            {
                Log.Message("[RimTalk][MP diag] Registering SyncWorkers/Methods...");
                RegisterSyncWorkers();
                RegisterSyncMethods();
                Log.Message("[RimTalk] Multiplayer compatibility initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk] Failed to initialize MP compatibility: {ex}");
            }
        }

        /// <summary>
        /// Register SyncWorkers for custom data types.
        /// 为自定义数据类型注册SyncWorker。
        /// </summary>
        private static void RegisterSyncWorkers()
        {
            // Register TalkResponse serializer
            // Note: TalkResponse is already using deterministic IDs in multiplayer,
            // so we just need to serialize/deserialize the fields
            MP.RegisterSyncWorker<TalkResponse>(SyncTalkResponse);

            Log.Message("[RimTalk] Registered SyncWorkers for TalkResponse");
        }

        /// <summary>
        /// Register methods that should be synced across all clients.
        /// 注册需要在所有客户端同步的方法。
        /// </summary>
        private static void RegisterSyncMethods()
        {
            // Register TalkHistory sync methods
            MP.RegisterSyncMethod(typeof(TalkHistory), nameof(TalkHistory.SyncAddHistory));
            MP.RegisterSyncMethod(typeof(TalkHistory), nameof(TalkHistory.SyncIgnoreTalk));

            Log.Message("[RimTalk] Registered SyncMethods for TalkHistory");
        }

        /// <summary>
        /// SyncWorker for TalkResponse serialization.
        /// TalkResponse序列化的SyncWorker。
        /// </summary>
        private static void SyncTalkResponse(SyncWorker sync, ref TalkResponse obj)
        {
            if (sync.isWriting)
            {
                // Host/sender: write data to network
                // MP API lacks a default writer for Guid, so send as string.
                sync.Write(obj.Id.ToString());
                sync.Write(obj.TalkType);
                sync.Write(obj.Name);
                sync.Write(obj.Text);
                sync.Write(obj.InteractionRaw);
                sync.Write(obj.TargetName);
                sync.Write(obj.ParentTalkId.ToString());
            }
            else
            {
                // Client/receiver: read data from network
                var id = Guid.Parse(sync.Read<string>());
                var talkType = sync.Read<TalkType>();
                var name = sync.Read<string>();
                var text = sync.Read<string>();
                var interactionRaw = sync.Read<string>();
                var targetName = sync.Read<string>();
                var parentTalkId = Guid.Parse(sync.Read<string>());

                // Reconstruct TalkResponse with received data
                obj = new TalkResponse(talkType, name, text)
                {
                    Id = id,
                    InteractionRaw = interactionRaw,
                    TargetName = targetName,
                    ParentTalkId = parentTalkId
                };
            }
        }

    }
}
