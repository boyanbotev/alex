using UnityEngine;

public static class SaveManager
{
    private const string LevelIndexKey = "CurrentLevelIndex";
    private const string CampaignKey = "CampaignState";
    private static CampaignState campaign;

    public static CampaignState Campaign
    {
        get
        {
            if (campaign != null) return campaign;
            string json = PlayerPrefs.GetString(CampaignKey, "");
            campaign = string.IsNullOrEmpty(json) ? new CampaignState
            {
                levelIndex = Mathf.Max(0, PlayerPrefs.GetInt(LevelIndexKey, 0))
            } : JsonUtility.FromJson<CampaignState>(json);
            return campaign;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => campaign = null;

    public static int LevelIndex => Campaign.levelIndex;

    public static void Save()
    {
        PlayerPrefs.SetString(CampaignKey, JsonUtility.ToJson(Campaign));
        PlayerPrefs.SetInt(LevelIndexKey, Campaign.levelIndex);
        PlayerPrefs.Save();
    }

    public static void SetLevelIndex(int index)
    {
        if (index < 0) throw new System.ArgumentOutOfRangeException(nameof(index));
        Campaign.levelIndex = index;
        Campaign.ClearInterlude();
        Save();
    }

    public static void ResetCampaign()
    {
        campaign = new CampaignState();
        Save();
    }
}
