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
            tile.city?.Hide();
        }

        foreach (Player player in TurnManager.Instance.players)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }
            var settings = GridGenerator.Instance.boardSettings;
            player.visibleTiles = new VisibilityState(settings.width, settings.height);

            Reveal(player, player.cities[0].centerTile, 2);
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

        if (!player.isAI && fogTiles.TryGetValue(tile.gridPosition, out GameObject fogObj))
        {
            fogObj.SetActive(false);
            tile.city?.Reveal();
        }
    }
}
