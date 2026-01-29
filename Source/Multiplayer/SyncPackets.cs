using System;
using System.Collections.Generic;
using RimTalk.Data;
using Verse;

namespace RimTalk.Multiplayer
{
    /// <summary>
    /// Packet to sync TalkResponse from host to clients.
    /// 从主机同步TalkResponse到客户端的数据包。
    /// </summary>
    public class SyncTalkResponsePacket
    {
        public int pawnId;                    // Pawn.thingIDNumber
        public TalkResponse response;
        public int spokenTick;                // GenTicks.TicksGame
    }

    /// <summary>
    /// Packet to sync individual history entry.
    /// 同步单个历史记录条目的数据包。
    /// </summary>
    public class SyncHistoryEntry
    {
        public int pawnId;
        public Role role;
        public string message;
    }

    /// <summary>
    /// Packet to sync ignore talk operation.
    /// 同步忽略对话操作的数据包。
    /// </summary>
    public class SyncIgnoreTalk
    {
        public Guid talkId;
        public bool ignoreChildren;
    }

    /// <summary>
    /// Packet to sync pawn state updates.
    /// 同步Pawn状态更新的数据包。
    /// </summary>
    public class SyncPawnState
    {
        public int pawnId;
        public int lastTalkTick;
        public bool isGeneratingTalk;
    }
}
