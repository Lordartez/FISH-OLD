using Content.Shared.Objectives;
using Robust.Shared.Serialization;
using System.Text;

namespace Content.Shared.CharacterInfo;

[Serializable, NetSerializable]
public sealed class RequestCharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;

    public RequestCharacterInfoEvent(NetEntity netEntity)
    {
        NetEntity = netEntity;
    }
}

[Serializable, NetSerializable]
public sealed class CharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly string JobTitle;
    public readonly string JobDescription;
    public readonly Dictionary<string, List<ObjectiveInfo>> Objectives;
    public readonly string? Briefing;

    public CharacterInfoEvent(NetEntity netEntity, string jobTitle, string jobDescription, Dictionary<string, List<ObjectiveInfo>> objectives, string? briefing)
    {
        NetEntity = netEntity;
        JobTitle = jobTitle;
        // needed for character window description cuz it doesnt support width line breaks
        JobDescription = InsertLineBreaks(jobDescription, 42);
        Objectives = objectives;
        Briefing = briefing;
    }

    // Костыль хули
    static string InsertLineBreaks(string input, int maxLineLength)
    {
        string[] words = input.Split(' ');
        StringBuilder result = new StringBuilder();

        int lineLength = 0;
        foreach (string word in words)
        {
            if (lineLength + word.Length + 1 > maxLineLength)
            {
                result.Append('\n');
                lineLength = 0;
            }

            result.Append(word);
            result.Append(' ');

            lineLength += word.Length + 1;
        }

        return result.ToString().Trim();
    }
}
