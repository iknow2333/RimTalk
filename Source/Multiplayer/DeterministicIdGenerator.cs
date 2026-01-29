using System;
using System.Threading;
using Verse;

namespace RimTalk.Multiplayer
{
    /// <summary>
    /// Generates deterministic GUIDs for multiplayer synchronization.
    /// 为多人同步生成确定性GUID。
    /// </summary>
    public static class DeterministicIdGenerator
    {
        private static int _sequenceNumber = 0;

        /// <summary>
        /// Generate a deterministic GUID based on game tick, sequence number, and synchronized random value.
        /// 基于游戏tick、序号和同步随机值生成确定性GUID。
        /// </summary>
        public static Guid GenerateTalkResponseId()
        {
            // Get current game tick (synchronized across all clients)
            int tick = Find.TickManager?.TicksGame ?? 0;

            // Atomic increment of sequence number
            int seq = Interlocked.Increment(ref _sequenceNumber);

            // Combine into 16 bytes for GUID
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(tick).CopyTo(bytes, 0);      // bytes 0-3: game tick
            BitConverter.GetBytes(seq).CopyTo(bytes, 4);        // bytes 4-7: sequence
            Rand.PushState();
            Rand.Seed = Gen.HashCombineInt(tick, seq);
            int rand = Rand.Int;                                // deterministic, isolated from global RNG
            Rand.PopState();
            BitConverter.GetBytes(rand).CopyTo(bytes, 8);       // bytes 8-11: deterministic random
            BitConverter.GetBytes(0).CopyTo(bytes, 12);         // bytes 12-15: padding

            return new Guid(bytes);
        }

        /// <summary>
        /// Reset sequence counter (called when starting new game or loading).
        /// 重置序号计数器（启动新游戏或加载时调用）。
        /// </summary>
        public static void Reset()
        {
            _sequenceNumber = 0;
        }
    }
}
