using UnityEngine;

[CreateAssetMenu(fileName = "Board Settings", menuName = "Game/Board Settings")]
public class BoardSettings : ScriptableObject {
    public int width = 11;
    public int height = 11;
}
