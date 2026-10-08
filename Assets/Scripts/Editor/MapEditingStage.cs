using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Preview objects live in a separate stage, never in a saved game scene.
public sealed class MapEditingStage : PreviewSceneStage
{
    public Level level;
    public GameObject[] prefabs;
    private readonly Dictionary<Vector2Int, GameObject> tiles = new();
    protected override GUIContent CreateHeaderContent() => new GUIContent($"Map: {level.map.name}");
    protected override bool OnOpenStage()
    {
        if (!base.OnOpenStage()) return false;
        Rebuild();
        return true;
    }

    protected override void OnCloseStage() { tiles.Clear(); base.OnCloseStage(); }

    public void Rebuild()
    {
        foreach (var tile in tiles.Values) if (tile != null) DestroyImmediate(tile);
        tiles.Clear();
        if (level == null || level.map == null || !scene.IsValid()) return;
        var map = level.map;
        if (map.terrain == null || map.terrain.Length != (long)map.width * map.height) return;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++) RefreshTile(new Vector2Int(x, y));
        SceneView.RepaintAll();
    }

    public void RefreshTile(Vector2Int p)
    {
        if (tiles.TryGetValue(p, out var old) && old != null) DestroyImmediate(old);
        var type = level.map.GetTerrain(p.x, p.y);
        var prefab = prefabs != null ? prefabs[(int)type] : null;
        // A coloured tile keeps the painter usable if the game's prefab is missing.
        var tile = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene) : new GameObject();
        if (prefab == null) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(tile, scene);
        tile.name = $"{type} ({p.x}, {p.y})";
        tile.transform.position = Position(p);
        if (!level.is3DIsometric && tile.TryGetComponent<SpriteRenderer>(out var sprite)) sprite.sortingOrder = -(p.x + p.y);
        tiles[p] = tile;
    }

    public Vector3 Position(Vector2Int p) => level.is3DIsometric
        ? new Vector3(p.x * level.tileSize, 0, p.y * level.tileSize)
        : new Vector3((p.x - p.y) * level.tileSize * .5f, (p.x + p.y) * level.tileSize * .25f, 0);

    public Vector2Int Cell(Vector3 p)
    {
        if (level.is3DIsometric) return new Vector2Int(Mathf.RoundToInt(p.x / level.tileSize), Mathf.RoundToInt(p.z / level.tileSize));
        float x = p.x / (level.tileSize * .5f), y = p.y / (level.tileSize * .25f);
        return new Vector2Int(Mathf.RoundToInt((x + y) * .5f), Mathf.RoundToInt((y - x) * .5f));
    }

    public Vector3[] Corners(Vector2Int p, float radius = .5f)
    {
        var center = Position(p);
        float size = level.tileSize * radius;
        if (level.is3DIsometric)
        {
            center.y = .04f;
            return new[] { center + new Vector3(-size, 0, -size), center + new Vector3(-size, 0, size),
                center + new Vector3(size, 0, size), center + new Vector3(size, 0, -size) };
        }
        center.z = -.04f;
        return new[] { center + Vector3.left * size, center + Vector3.up * size * .5f,
            center + Vector3.right * size, center + Vector3.down * size * .5f };
    }

    public bool HasPrefab(TerrainType type) => prefabs != null && prefabs[(int)type] != null;
}
