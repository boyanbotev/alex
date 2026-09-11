using UnityEngine;

[CreateAssetMenu(fileName = "Board Settings", menuName = "Game/Board Settings")]
public class BoardSettings : ScriptableObject {
    public int width = 11;
    public int height = 11;

    [Tooltip("Prevent diagonal movement when both adjacent orthogonal tiles contain enemy units.")]
    public bool blockDiagonalsBetweenEnemies = true;
}
