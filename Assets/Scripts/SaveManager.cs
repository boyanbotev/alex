using UnityEngine;

public static class SaveManager
{
    private const string LevelIndexKey = "CurrentLevelIndex";

    public static int LevelIndex => Mathf.Max(0, PlayerPrefs.GetInt(LevelIndexKey, 0));

    public static void SetLevelIndex(int index)
    {
        if (index < 0) throw new System.ArgumentOutOfRangeException(nameof(index));
        PlayerPrefs.SetInt(LevelIndexKey, index);
        PlayerPrefs.Save();
    }
}
