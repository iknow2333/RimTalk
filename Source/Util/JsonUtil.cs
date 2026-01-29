using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using RimTalk.Data;

namespace RimTalk.Util;

public static class JsonUtil
{
    public static string SerializeToJson<T>(T obj)
    {
        // Create a memory stream for serialization
        using var stream = new MemoryStream();
        // Create a DataContractJsonSerializer
        var serializer = new DataContractJsonSerializer(typeof(T));

        // Serialize the ApiRequest object
        serializer.WriteObject(stream, obj);

        // Convert the memory stream to a string
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static T DeserializeFromJson<T>(string json)
    {
        string sanitizedJson = Sanitize(json, typeof(T));
        
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sanitizedJson));
            // Create an instance of DataContractJsonSerializer
            var serializer = new DataContractJsonSerializer(typeof(T));

            // Deserialize the JSON data
            return (T)serializer.ReadObject(stream);
        }
        catch (Exception ex)
        {
            Logger.Error($"Json deserialization failed for {typeof(T).Name}\n{json}");
            throw;
        }
    }

    /// <summary>
    /// The definitive sanitizer that fixes structural, syntax, and formatting errors from LLM-generated JSON.
    /// </summary>
    /// <param name="text">The raw string from the LLM.</param>
    /// <param name="targetType">The C# type we are trying to deserialize into.</param>
    /// <returns>A cleaned and likely valid JSON string.</returns>
    public static string Sanitize(string text, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string sanitized = text.Replace("```json", "").Replace("```", "").Trim();

        int startIndex = sanitized.IndexOfAny(['{', '[']);
        int endIndex = sanitized.LastIndexOfAny(['}', ']']);

        if (startIndex >= 0 && endIndex > startIndex)
        {
            sanitized = sanitized.Substring(startIndex, endIndex - startIndex + 1).Trim();
        }
        else
        {
            return string.Empty;
        }

        sanitized = Regex.Replace(
            sanitized, 
            @"""([^""]+)""\s*:\s*([,}])", 
            @"""$1"":null$2"
        );

        if (sanitized.Contains("]["))
        {
            sanitized = sanitized.Replace("][", ",");
        }
        if (sanitized.Contains("}{"))
        {
            sanitized = sanitized.Replace("}{", "},{");
        }
    
        if (sanitized.StartsWith("{") && sanitized.EndsWith("}"))
        {
            string innerContent = sanitized.Substring(1, sanitized.Length - 2).Trim();
            if (innerContent.StartsWith("[") && innerContent.EndsWith("]"))
            {
                sanitized = innerContent;
            }
        }

        bool isEnumerable = typeof(IEnumerable).IsAssignableFrom(targetType) && targetType != typeof(string);
        if (isEnumerable && sanitized.StartsWith("{"))
        {
            sanitized = $"[{sanitized}]";
        }

        // Fix invalid GUIDs produced by LLM when parsing TalkResponse payloads
        if (IsTalkResponseType(targetType))
        {
            sanitized = FixTalkResponseGuids(sanitized);
        }

        return sanitized;
    }

    private static bool IsTalkResponseType(Type targetType)
    {
        if (targetType == typeof(TalkResponse)) return true;

        if (targetType.IsArray && targetType.GetElementType() == typeof(TalkResponse))
            return true;

        if (typeof(IEnumerable).IsAssignableFrom(targetType) && targetType.IsGenericType)
        {
            var args = targetType.GetGenericArguments();
            if (args.Length == 1 && args[0] == typeof(TalkResponse)) return true;
        }

        return false;
    }

    /// <summary>
    /// Replaces invalid GUID strings in TalkResponse JSON with fresh GUIDs so deserialization succeeds.
    /// </summary>
    private static string FixTalkResponseGuids(string json)
    {
        return Regex.Replace(json,
            "\"(id|parentTalkId)\"\\s*:\\s*\"([^\"]*)\"",
            match =>
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value;

                if (Guid.TryParse(value, out var parsed))
                {
                    return $"\"{key}\":\"{parsed}\"";
                }

                var replacement = Guid.NewGuid();
                return $"\"{key}\":\"{replacement}\"";
            },
            RegexOptions.IgnoreCase);
    }
}
