using System;
using System.Collections.Generic;

public enum CampaignPhase { Gameplay, Interlude }

[Serializable]
public sealed class CampaignState
{
    public int levelIndex;
    public CampaignPhase phase;
    public List<string> cityLinks = new();
    public List<CampaignScore> scores = new();
    public List<CampaignFlag> flags = new();
    public List<CampaignRelation> relations = new();
    public string inkState;
    public List<CampaignMessage> messages = new();

    public bool GetFlag(string key) => flags.Find(f => f.key == key)?.value ?? false;

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A flag needs a key.");
        var flag = flags.Find(f => f.key == key);
        if (flag == null) flags.Add(new CampaignFlag { key = key, value = value });
        else flag.value = value;
    }

    public void SetRelation(string a, string b, DiplomaticRelation value)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || a == b)
            throw new ArgumentException("Relations require two distinct faction IDs.");
        var relation = relations.Find(r => (r.a == a && r.b == b) || (r.a == b && r.b == a));
        if (relation == null) relations.Add(new CampaignRelation { a = a, b = b, value = value });
        else relation.value = value;
    }

    public void ClearInterlude()
    {
        phase = CampaignPhase.Gameplay;
        cityLinks.Clear();
        scores.Clear();
        inkState = null;
        messages.Clear();
    }
}

[Serializable] public sealed class CampaignFlag { public string key; public bool value; }
[Serializable] public sealed class CampaignScore { public string key; public float value; }
[Serializable] public sealed class CampaignRelation { public string a, b; public DiplomaticRelation value; }
[Serializable] public sealed class CampaignMessage { public string speaker, text; public bool outgoing; }
