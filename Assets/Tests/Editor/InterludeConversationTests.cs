using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class InterludeConversationTests
{
    private static string Example => File.ReadAllText("Assets/Story/Interlude Example.json");

    [TestCase(0, true, DiplomaticRelation.Peace)]
    [TestCase(1, false, DiplomaticRelation.War)]
    public void ChoiceSurvivesSaveAndResumeWithoutRepeatingMessages(int choice, bool accepted, DiplomaticRelation relation)
    {
        var campaign = new CampaignState { phase = CampaignPhase.Interlude };
        var conversation = new InterludeConversation(Example, campaign);
        conversation.Advance();
        Assert.That(conversation.Story.currentChoices.Count, Is.EqualTo(2));
        Assert.That(campaign.messages.Count, Is.EqualTo(1));
        Assert.That(campaign.messages[0].speaker, Is.EqualTo("Malaise"));

        campaign = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(campaign));
        conversation = new InterludeConversation(Example, campaign);
        conversation.Advance();
        Assert.That(campaign.messages.Count, Is.EqualTo(1));
        conversation.Choose(choice);
        Assert.That(campaign.GetFlag("accepted_truce"), Is.EqualTo(accepted));
        Assert.That(campaign.relations.Count, Is.EqualTo(1));
        Assert.That(campaign.relations[0].value, Is.EqualTo(relation));
        Assert.That(campaign.messages.Count, Is.EqualTo(3));
        Assert.That(campaign.messages[1].outgoing, Is.True);

        campaign = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(campaign));
        conversation = new InterludeConversation(Example, campaign);
        conversation.Advance();
        Assert.That(conversation.Story.currentChoices, Is.Empty);
        Assert.That(conversation.Story.canContinue, Is.False);
        Assert.That(campaign.messages.Count, Is.EqualTo(3));
        Assert.That(campaign.relations.Count, Is.EqualTo(1));
    }

    [Test]
    public void NextLevelUsesPersistentDiplomacyWithoutChangingItsAssets()
    {
        string checkpoint = JsonUtility.ToJson(SaveManager.Campaign);
        var root = new GameObject("Campaign diplomacy test");
        var a = ScriptableObject.CreateInstance<Faction>();
        var b = ScriptableObject.CreateInstance<Faction>();
        var level = ScriptableObject.CreateInstance<Level>();
        var previousTurns = TurnManager.Instance;
        try
        {
            a.campaignId = "conscience";
            b.campaignId = "malaise";
            level.factions = new[] { new LevelFaction { faction = a }, new LevelFaction { faction = b } };
            SaveManager.Campaign.relations.Clear();
            SaveManager.Campaign.SetRelation(a.CampaignId, b.CampaignId, DiplomaticRelation.Peace);
            var turns = root.AddComponent<TurnManager>();
            turns.InitializePlayers(level);
            Assert.That(turns.Diplomacy.GetRelation(turns.players[0], turns.players[1]), Is.EqualTo(DiplomaticRelation.Peace));
            Assert.That(level.factions[0].faction, Is.SameAs(a));
            SaveManager.Campaign.ClearInterlude();
            Assert.That(SaveManager.Campaign.relations.Count, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(level);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            TurnManager.Instance = previousTurns;
            JsonUtility.FromJsonOverwrite(checkpoint, SaveManager.Campaign);
        }
    }
}
