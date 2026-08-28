using System.Collections.Generic;
using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance;
    public GameObject fogTilePrefab;

    private Dictionary<Vector2Int, GameObject> fogTiles = new Dictionary<Vector2Int, GameObject>();

    private void Awake() => Instance = this;

    public void CreateFogTiles()
    {
        foreach (var kvp in GridManager.Instance.grid)
        {
            Tile tile = kvp.Value;
            GameObject fogObj = Instantiate(fogTilePrefab, tile.transform.position, Quaternion.identity, tile.transform);
            fogTiles[tile.gridPosition] = fogObj;
            tile.city?.Hide();
        }

        foreach (Player player in TurnManager.Instance.players)
        {
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
        player.visibleTiles.SetVisible(tile.gridPosition);

        if (!player.isAI && fogTiles.TryGetValue(tile.gridPosition, out GameObject fogObj))
        {
            fogObj.SetActive(false);
            tile.city?.Reveal();
        }
    }
}
