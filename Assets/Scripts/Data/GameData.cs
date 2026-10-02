using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Game", menuName = "Game/Game")]
public sealed class GameData : ScriptableObject
{
    public Level[] levels = Array.Empty<Level>();
}
