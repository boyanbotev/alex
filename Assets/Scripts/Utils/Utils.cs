using UnityEngine;

public static class Utils
{
    public static int GridDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        return Mathf.Max(dx, dy);
    }

    public static bool IsWithinDistance(Vector2Int a, Vector2Int b, int maxDistance)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        if (dx == 0 && dy == 0) return false;

        return Mathf.Max(dx, dy) <= maxDistance;
    }
}
