using System;
using Ink.Runtime;

// Owns story execution; the scene only renders messages and choices.
public sealed class InterludeConversation
{
    public Story Story { get; }
    private readonly CampaignState campaign;

    public InterludeConversation(string json, CampaignState campaign)
    {
        this.campaign = campaign;
        Story = new Story(json);
        Story.onError += (message, type) =>
        {
            if (type == Ink.ErrorType.Warning) UnityEngine.Debug.LogWarning(message);
            else throw new InvalidOperationException(message);
        };
        Story.BindExternalFunction("get_flag", (string key) => campaign.GetFlag(key), true);
        Story.BindExternalFunction("set_flag", (string key, bool value) => campaign.SetFlag(key, value));
        Story.BindExternalFunction("set_relation", (string a, string b, string value) =>
        {
            if (!Enum.TryParse(value, true, out DiplomaticRelation relation) || !Enum.IsDefined(typeof(DiplomaticRelation), relation))
                throw new ArgumentException($"Unknown diplomatic relation '{value}'. Use peace, war or allied.");
            campaign.SetRelation(a, b, relation);
        });
        Story.BindExternalFunction("get_score", (string key) => campaign.scores.Find(s => s.key == key)?.value ?? 0f, true);
        Story.BindExternalFunction("has_link", (string key) => campaign.cityLinks.Contains(key), true);
        Story.BindExternalFunction("get_links", () =>
        {
            var links = new InkList();
            if (!Story.listDefinitions.TryListGetDefinition("city_links", out var definition)) return links;
            links = new InkList("city_links", Story);
            foreach (string key in campaign.cityLinks)
                if (definition.ContainsItemWithName(key)) links.AddItem(key);
            return links;
        }, true);
        foreach (var score in campaign.scores)
        {
            float value = score.value;
            Story.BindExternalFunction(score.key, () => value, true);
        }
        if (!string.IsNullOrEmpty(campaign.inkState)) Story.state.LoadJson(campaign.inkState);
    }

    public void Advance()
    {
        string speaker = campaign.messages.FindLast(message => !message.outgoing)?.speaker ?? "Contact";
        while (Story.canContinue)
        {
            string line = Story.Continue().Trim();
            if (line.Length == 0) continue;
            bool outgoing = false;
            foreach (string tag in Story.currentTags)
            {
                if (tag.StartsWith("speaker:", StringComparison.OrdinalIgnoreCase))
                    speaker = tag.Substring("speaker:".Length).Trim();
                if (tag.Equals("outgoing", StringComparison.OrdinalIgnoreCase)) outgoing = true;
            }
            campaign.messages.Add(new CampaignMessage { speaker = speaker, text = line, outgoing = outgoing });
        }
        campaign.inkState = Story.state.ToJson();
    }

    public void Choose(int index)
    {
        string reply = Story.currentChoices[index].text;
        Story.ChooseChoiceIndex(index);
        campaign.messages.Add(new CampaignMessage { speaker = "You", text = reply, outgoing = true });
        Advance();
    }
}
