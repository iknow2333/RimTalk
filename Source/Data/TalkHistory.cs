using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimTalk.Multiplayer;
using RimTalk.Source.Data;
using RimTalk.Util;
using Verse;

namespace RimTalk.Data;

public static class TalkHistory
{
    private static readonly ConcurrentDictionary<int, List<(Role role, string message)>> MessageHistory = new();
    private static readonly ConcurrentDictionary<Guid, int> SpokenTickCache = new() { [Guid.Empty] = 0 };
    private static readonly ConcurrentBag<Guid> IgnoredCache = [];

    // Add a new talk with the current game tick
    public static void AddSpoken(Guid id)
    {
        SpokenTickCache.TryAdd(id, GenTicks.TicksGame);
    }

    public static void AddIgnored(Guid id)
    {
        IgnoredCache.Add(id);
    }

    public static int GetSpokenTick(Guid id)
    {
        return SpokenTickCache.TryGetValue(id, out var tick) ? tick : -1;
    }

    public static bool IsTalkIgnored(Guid id)
    {
        return IgnoredCache.Contains(id);
    }

    public static void AddMessageHistory(Pawn pawn, string request, string response)
    {
        // In multiplayer, use sync method to ensure consistency
        if (MpCompatShim.IsInMultiplayer && !MP.IsExecutingSyncCommand)
        {
            SyncAddHistory(pawn.thingIDNumber, (int)Role.User, request);
            SyncAddHistory(pawn.thingIDNumber, (int)Role.AI, response);
        }
        else
        {
            // Direct add (singleplayer or receiving synced command)
            AddMessageHistoryInternal(pawn.thingIDNumber, Role.User, request);
            AddMessageHistoryInternal(pawn.thingIDNumber, Role.AI, response);
        }
    }

    /// <summary>
    /// Internal method to add message directly without sync.
    /// 内部方法，直接添加消息不经过同步。
    /// </summary>
    private static void AddMessageHistoryInternal(int pawnId, Role role, string message)
    {
        var messages = MessageHistory.GetOrAdd(pawnId, _ => []);

        lock (messages)
        {
            messages.Add((role, message));
            EnsureMessageLimit(messages);
        }
    }

    /// <summary>
    /// Sync method to add history entry (called by Multiplayer API).
    /// 用于添加历史记录的同步方法（由Multiplayer API调用）。
    /// </summary>
    public static void SyncAddHistory(int pawnId, int role, string message)
    {
        // This method will be registered as a sync method
        AddMessageHistoryInternal(pawnId, (Role)role, message);

        // For AI responses, deserialize JSON and rebuild TalkResponse queue
        if ((Role)role == Role.AI && !string.IsNullOrWhiteSpace(message))
        {
            // Host端在生成对话时已经把 TalkResponse 入队，
            // 如果这里再重建队列，会导致主机重复展示同一条对话。
            // 客机需要此分支来接收并入队同步的响应。
            if (MP.IsHosting)
                return;

            try
            {
                // Response is JSON-serialized List<TalkResponse>
                var responses = JsonUtil.DeserializeFromJson<List<TalkResponse>>(message);
                if (responses != null && responses.Any())
                {
                    // Find pawn and add responses to queue
                    var pawn = Find.Maps?
                        .SelectMany(m => m.mapPawns.AllPawnsSpawned)
                        .FirstOrDefault(p => p.thingIDNumber == pawnId);

                    if (pawn != null)
                    {
                        var pawnState = Cache.Get(pawn);
                        if (pawnState != null && pawnState.TalkResponses != null)
                        {
                            foreach (var response in responses)
                            {
                                if (response == null) continue;

                                // Check if already in queue (Host side already added in GenerateAndProcessTalkAsync)
                                if (!pawnState.TalkResponses.Any(r => r != null && r.Id == response.Id))
                                {
                                    pawnState.TalkResponses.Add(response);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Avoid logging in sync context to prevent concurrent modification
                Logger.ErrorOnce($"Failed to rebuild TalkResponse queue: {ex.Message}", ex.GetHashCode());
            }
        }
    }

    /// <summary>
    /// Sync method to ignore a talk (called by Multiplayer API).
    /// 用于忽略对话的同步方法（由Multiplayer API调用）。
    /// </summary>
    public static void SyncIgnoreTalk(string talkIdStr, bool ignoreChildren)
    {
        var guid = Guid.Parse(talkIdStr);
        IgnoredCache.Add(guid);

        // TODO: Propagate to child talks if ignoreChildren is true
        // This requires traversing all TalkResponses to find children with ParentTalkId == guid
    }

    public static List<(Role role, string message)> GetMessageHistory(Pawn pawn, bool simplified = false)
    {
        if (!MessageHistory.TryGetValue(pawn.thingIDNumber, out var history))
            return [];

        lock (history)
        {
            var result = new List<(Role role, string message)>();
            foreach (var msg in history)
            {
                var content = msg.message;
                if (simplified)
                {
                    if (msg.role == Role.AI)
                        content = BuildAssistantHistoryText(content);

                    content = CleanHistoryText(content);
                }

                if (!string.IsNullOrWhiteSpace(content))
                    result.Add((msg.role, content));
            }
            return result;
        }
    }

    private static void EnsureMessageLimit(List<(Role role, string message)> messages)
    {
        // First, ensure alternating pattern by removing consecutive duplicates from the end
        for (int i = messages.Count - 1; i > 0; i--)
        {
            if (messages[i].role == messages[i - 1].role)
            {
                // Remove the earlier message of the consecutive pair
                messages.RemoveAt(i - 1);
            }
        }

        // Then, enforce the maximum message limit by removing the oldest messages
        int maxMessages = Settings.Get().Context.ConversationHistoryCount;
        while (messages.Count > maxMessages * 2)
        {
            messages.RemoveAt(0);
        }
    }

    private static string CleanHistoryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var cleaned = CommonUtil.StripFormattingTags(text);
        return cleaned.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
    }

    private static string BuildAssistantHistoryText(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return "";

        var lines = new List<string>();
        var trimmed = response.Trim();
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
        {
            try
            {
                var parsed = JsonUtil.DeserializeFromJson<List<TalkResponse>>(trimmed);
                if (parsed != null)
                {
                    foreach (var r in parsed)
                    {
                        if (r == null) continue;
                        var text = r.Text;
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        var name = r.Name;
                        lines.Add(string.IsNullOrWhiteSpace(name) ? text : $"{name}: {text}");
                    }
                }
            }
            catch
            {
                lines.Clear();
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(response);
        }

        return string.Join("\n", lines);
    }

    public static void Clear()
    {
        MessageHistory.Clear();
        // clearing spokenCache may block child talks waiting to display
    }
}
