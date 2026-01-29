using System;
using System.Collections.Generic;
using RimTalk.Client;
using RimTalk.Source.Data;
using RimTalk.Multiplayer;
using Multiplayer.API;
using RimTalk.UI;
using Verse;

namespace RimTalk.Data;

// WARNING: ApiHistory is ONLY for debug window display and Overlay UI. NEVER use for game logic!
// 警告：ApiHistory仅用于调试窗口和Overlay显示，不影响游戏状态！
// 客户端创建的ApiLog（通过AddClientResponse）信息不完整，仅用于展示。
public static class ApiHistory
{
    private static readonly Dictionary<Guid, ApiLog> History = new();
    private static int _conversationIdIndex = 0;

    public static ApiLog GetApiLog(Guid id) => History.TryGetValue(id, out var apiLog) ? apiLog : null;

    public static ApiLog AddRequest(TalkRequest request, Channel channel)
    {
        var log = new ApiLog(request.Initiator.LabelShort, request, null, null, DateTime.Now, channel)
        {
            IsFirstDialogue = true,
            ConversationId = request.IsMonologue ? -1 : _conversationIdIndex++
        };
        History[log.Id] = log;
        return log;
    }

    public static void UpdatePayload(Guid id, Payload payload)
    {
        if (History.TryGetValue(id, out var log))
        {
            log.Payload = payload;
        }
    }

    public static ApiLog AddResponse(Guid id, string response, string name, string interactionType, Payload payload = null, int elapsedMs = 0)
    {
        if (!History.TryGetValue(id, out var originalLog)) return null;

        // first message
        if (originalLog.Response == null)
        {
            originalLog.Name = name ?? originalLog.Name;
            originalLog.Response = response;
            originalLog.InteractionType = interactionType;
            originalLog.Payload = payload;
            originalLog.ElapsedMs = (int)(DateTime.Now - originalLog.Timestamp).TotalMilliseconds;
            return originalLog;
        }

        // multi-turn messages
        var newLog = new ApiLog(name, originalLog.TalkRequest, response, payload, DateTime.Now, originalLog.Channel);
        History[newLog.Id] = newLog;
        newLog.InteractionType = interactionType;
        newLog.ElapsedMs = elapsedMs;
        newLog.ConversationId = originalLog.ConversationId;
        return newLog;
    }

    public static ApiLog AddUserHistory(Pawn initiator, Pawn recipient, string text)
    {
        var prompt = $"{initiator.LabelShort} talked to {recipient.LabelShort}";
        TalkRequest talkRequest = new(prompt, initiator, recipient, TalkType.User);
        var log = new ApiLog(initiator.LabelShort, talkRequest, text, null, DateTime.Now, Channel.User);
        History[log.Id] = log;
        return log;
    }

    /// <summary>
    /// Client-side method to create ApiLog for synced TalkResponse.
    /// 客户端为同步的TalkResponse创建ApiLog（用于Overlay展示）。
    /// </summary>
    public static ApiLog AddClientResponse(TalkResponse response, Pawn initiator)
    {
        if (History.ContainsKey(response.Id))
            return History[response.Id];

        // 创建简化的TalkRequest（客户端没有完整的请求信息）
        var dummyRequest = new TalkRequest("", initiator, response.GetTarget(), response.TalkType);

        var log = new ApiLog(
            response.Name ?? initiator.LabelShort,
            dummyRequest,
            response.Text,
            null, // No payload on client
            DateTime.Now,
            Channel.Stream // 假设是流式
        )
        {
            InteractionType = response.InteractionRaw,
            SpokenTick = 0 // 稍后会被设置
        };

        // Set ID after construction (ApiLog.Id changed to get;set; for compatibility)
        log.Id = response.Id;

        History[log.Id] = log;
        return log;
    }

    public static IEnumerable<ApiLog> GetAll()
    {
        foreach (var log in History)
        {
            yield return log.Value;
        }
    }

    public static void Clear()
    {
        History.Clear();
    }
}
