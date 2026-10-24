using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Unity.Profiling;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance;
    private static readonly ProfilerMarker creationMarker = new ProfilerMarker("WorldGeneration.FogTile");
    public GameObject fogTilePrefab;

    private Dictionary<Vector2Int, GameObject> fogTiles = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, Renderer> fogRenderers = new();
    private MaterialPropertyBlock fogProperties;
    private static readonly int boundsId = Shader.PropertyToID("_FogBounds");
    private static readonly int sidesId = Shader.PropertyToID("_FogSides");
    private static readonly int cornersId = Shader.PropertyToID("_FogCorners");

    private void Awake() => Instance = this;

    public IEnumerator CreateFogTiles(GenerationBudget budget)
    {
        foreach (var kvp in GridManager.Instance.grid)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }
            Tile tile = kvp.Value;
            GameObject fogObj;
            using (creationMarker.Auto())
                fogObj = Instantiate(fogTilePrefab, tile.transform.position, Quaternion.identity, tile.transform);
            fogTiles[tile.gridPosition] = fogObj;
            fogRenderers[tile.gridPosition] = fogObj.GetComponentInChildren<Renderer>();
            tile.city?.Hide();
        }

        foreach (var position in fogTiles.Keys)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield();
            }
            RefreshFog(position);
        }

        foreach (Player player in TurnManager.Instance.players)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }
            var settings = GameManager.Instance.Level;
            player.visibleTiles = new VisibilityState(settings.Width, settings.Height);

            foreach (City city in player.cities)
                Reveal(player, city.centerTile, 2);
            foreach (Unit unit in player.units)
                Reveal(player, unit.currentTile, 1);
        }
    }

    public void Reveal(Player player, Tile centerTile, int range)
    {
        RevealSingle(player, centerTile);

        foreach (Tile t in GridManager.Instance.GetTilesInRange(centerTile, range))
        {
            RevealSingle(player, t);
        }
    }

    public void RevealSingle(Player player, Tile tile)
    {
        bool newlyVisible = !player.visibleTiles.IsVisible(tile);
        player.visibleTiles.SetVisible(tile.gridPosition);
        if (!player.isAI && newlyVisible) NeuronSegmentVisual.RefreshAround(tile);

        if (!player.isAI && newlyVisible && fogTiles.TryGetValue(tile.gridPosition, out GameObject fogObj))
        {
            fogObj.SetActive(false);
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (x != 0 || y != 0)
                        RefreshFog(tile.gridPosition + new Vector2Int(x, y));
            tile.city?.Reveal();
        }
    }

    private float IsCovered(Vector2Int position, int x, int y)
    {
        return fogTiles.TryGetValue(position + new Vector2Int(x, y), out var fog)
            && fog.activeSelf ? 1f : 0f;
    }

    private void RefreshFog(Vector2Int position)
    {
        if (!fogRenderers.TryGetValue(position, out var renderer) || renderer == null
            || !renderer.gameObject.activeInHierarchy) return;

        fogProperties ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(fogProperties);
        Bounds bounds = renderer.bounds;
        fogProperties.SetVector(boundsId, new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
        fogProperties.SetVector(sidesId, new Vector4(IsCovered(position,-1,0), IsCovered(position,1,0),
            IsCovered(position,0,-1), IsCovered(position,0,1)));
        fogProperties.SetVector(cornersId, new Vector4(IsCovered(position,-1,-1), IsCovered(position,1,-1),
            IsCovered(position,-1,1), IsCovered(position,1,1)));
        renderer.SetPropertyBlock(fogProperties);
    }
}
