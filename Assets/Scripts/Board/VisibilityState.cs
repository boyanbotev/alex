using Unity.VisualScripting;
using UnityEngine;

public class VisibilityState
{
    private bool[,] fog;

    public VisibilityState(int width, int height)
    {
        fog = new bool[width, height];
    }
    public bool Get(Vector2Int pos) => fog[pos.x, pos.y];
    public bool IsVisible(Tile t) => fog[t.gridPosition.x, t.gridPosition.y];

    public void SetVisible(Vector2Int pos)
    {
        fog[pos.x, pos.y] = true;
    }
}