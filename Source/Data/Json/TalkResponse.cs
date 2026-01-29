#nullable enable
using System;
using System.Runtime.Serialization;
using Multiplayer.API;
using RimTalk.Multiplayer;
using RimTalk.Source.Data;
using Verse;

namespace RimTalk.Data;

[DataContract]
public class TalkResponse : IJsonData
{
    [DataMember(Name = "id")]
    public Guid Id { get; set; }

    [DataMember(Name = "talkType")]
    public TalkType TalkType { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "text")]
    public string Text { get; set; }

    [DataMember(Name = "act", EmitDefaultValue = false)]
    public string? InteractionRaw { get; set; }

    [DataMember(Name = "target", EmitDefaultValue = false)]
    public string? TargetName { get; set; }

    [DataMember(Name = "parentTalkId")]
    public Guid ParentTalkId { get; set; }

    // Constructor with deterministic ID generation for multiplayer
    public TalkResponse(TalkType talkType, string name, string text)
    {
        TalkType = talkType;
        Name = name;
        Text = text;

        // Use deterministic ID in multiplayer, random GUID in singleplayer
        if (MP.IsInMultiplayer)
            Id = DeterministicIdGenerator.GenerateTalkResponseId();
        else
            Id = Guid.NewGuid();
    }

    public bool IsReply()
    {
        return ParentTalkId != Guid.Empty;
    }

    public string GetText()
    {
        return Text;
    }

    public InteractionType GetInteractionType()
    {
        if (string.IsNullOrWhiteSpace(InteractionRaw))
            return InteractionType.None;

        return Enum.TryParse(InteractionRaw, true, out InteractionType result) ? result : InteractionType.None;
    }

    public Pawn? GetTarget()
    {
        return TargetName != null ? Cache.GetByName(TargetName)?.Pawn : null;
    }

    public override string ToString()
    {
        return $"Type: {TalkType} | Name: {Name} | Text: \"{Text}\" | " +
               $"Int: {InteractionRaw} | Target: {TargetName}";
    }
}
