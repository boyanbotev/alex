using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionController : MonoBehaviour
{
    private Unit selectedUnit;
    private List<Tile> highlightedTiles = new List<Tile>();
    private Vector3 mouseDownPosition;
    private const float DragThreshold = 10f;

    private void Update()
    {
        if (TurnManager.Instance.ActivePlayer.isAI)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            mouseDownPosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            float distance = Vector3.Distance(mouseDownPosition, Input.mousePosition);

            if (distance < DragThreshold)
            {
                HandleClick();
            }
        }
    }

    private void HandleClick()
    {
        Tile clickedTile = GetClickedTile();
        if (clickedTile == null)
        {
            DeselectAll();
            return;
        }

        if (selectedUnit != null) HandleSelectedUnitActions(clickedTile);
        else SelectTileItem(clickedTile);
    }

    private void SelectTileItem(Tile clickedTile)
    {
        var player = TurnManager.Instance.ActivePlayer;

        DeselectAll();

        if (clickedTile.currentUnit != null)
        {
            Unit unit = clickedTile.currentUnit;
            if (unit.owner == player)
            {
                selectedUnit = unit;
                HighlightActions(selectedUnit);
            }
        }
        else if (clickedTile.city != null && clickedTile.city.owner == player)
        {
            ShowSpawnOptions(clickedTile);
        }
        else if (clickedTile.territoryCity != null 
            && clickedTile.currentBuilding == null 
            && clickedTile.territoryCity.owner == player)
        {
            SelectTerritory(clickedTile);
        }
    }

    public void ShowSpawnOptions(Tile tile)
    {
        Player player = TurnManager.Instance.ActivePlayer;

        var availableUnits = player.faction.availableUnits
            .Where(u => !u.unitData.requiredTech || player.techState.IsUnlocked(u.unitData.requiredTech)).ToArray();

        UIManager.Instance.ShowSpawnButtons(availableUnits, tile.city);
    }

    public void SelectTerritory(Tile tile)
    {
        City city = tile.territoryCity;
        Player player = TurnManager.Instance.ActivePlayer;

        var availableBuildings = player.faction.availableBuildings.Where(b =>
        {
            return (!b.requiredTech || player.techState.IsUnlocked(b.requiredTech))
                && b.CanPlaceAt(tile, city);
        })
        .ToArray();

        if (availableBuildings.Length > 0)
        {
            UIManager.Instance.ShowBuildButtons(availableBuildings, tile, city);
        }
    }

    private void HandleSelectedUnitActions(Tile clickedTile)
    {
        if (highlightedTiles.Contains(clickedTile) && clickedTile.currentUnit == null && !selectedUnit.hasMoved)
        {
            MoveTo(clickedTile);
            return;
        }

        if (highlightedTiles.Contains(clickedTile) && clickedTile.currentUnit != null)
        {
            Unit targetUnit = clickedTile.currentUnit;
            if (targetUnit.owner != TurnManager.Instance.ActivePlayer && !selectedUnit.hasAttacked)
            {
                Attack(targetUnit);
                return;
            }
        }

        if (clickedTile == selectedUnit.currentTile)
        {
            DeselectAll();

            if (clickedTile.territoryCity != null
            && clickedTile.currentBuilding == null
            && clickedTile.territoryCity.owner == TurnManager.Instance.ActivePlayer)
            {
                SelectTerritory(clickedTile);
            }

            if (clickedTile.city != null && clickedTile.city.owner == TurnManager.Instance.ActivePlayer)
            {
                UIManager.Instance.ShowCityInfo(clickedTile.city);
            }
        } else
        {
            DeselectAll();
        }
    }

    private void Attack(Unit targetUnit)
    {
        selectedUnit.Attack(targetUnit);

        Tile targetTile = targetUnit.currentTile;
        bool isMeleeAttack = selectedUnit.data.attackRange == 1;

        if (isMeleeAttack && (targetUnit.gameObject == null || !targetUnit.isAlive))
        {
            selectedUnit.MoveTo(targetTile);
        }

        DeactivateUsedUnits(selectedUnit.owner.units);
        DeselectAll();
    }

    private void MoveTo(Tile tile)
    {
        selectedUnit.MoveTo(tile);

        HighlightActions(selectedUnit);
        DeactivateUsedUnits(selectedUnit.owner.units);
        if (!selectedUnit.isActive) DeselectAll();
    }

    private void HighlightActions(Unit unit)
    {
        GridManager.Instance.ClearAllHighlights();
        highlightedTiles.Clear();

        if (!unit.isActive) return;

        // Highlight Valid Movement Range
        if (!unit.hasMoved)
        {
            List<Tile> moveTiles = GridManager.Instance.GetTilesInRange(unit.currentTile, unit.data.moveRange);
            foreach (Tile tile in moveTiles)

                if (tile.currentUnit == null && unit.owner.visibleTiles.IsVisible(tile))
                {
                    tile.SetHighlight(true, Color.blue);
                    highlightedTiles.Add(tile);
                }
        }

        // Highlight Valid Attack Range
        if (!unit.hasAttacked)
        {
            List<Tile> attackTiles = GridManager.Instance.GetTilesInRange(unit.currentTile, unit.data.attackRange);
            foreach (Tile tile in attackTiles)
            {
                bool visible = unit.owner.visibleTiles.IsVisible(tile);
                if (visible && tile.currentUnit != null && tile.currentUnit.owner != unit.owner)
                {
                    tile.SetHighlight(true, Color.red);
                    highlightedTiles.Add(tile);
                }
            }
        }
    }

    private void DeactivateUsedUnits(List<Unit> units)
    {
        foreach (var unit in units)
        {
            if ((unit.hasMoved && unit.hasAttacked) || !unit.isActive) unit.Deactivate();
            else if (unit.hasMoved)
            {
                List<Player> enemyPlayers = TurnManager.Instance.players.ToList(); // TODO: get the player's enemies, which will be stored
                enemyPlayers.Remove(unit.owner);

                bool hasInRangeOpponents = enemyPlayers.Any(enemyPlayer =>
                    enemyPlayer.units.Visible(unit.owner.visibleTiles)
                        .Any(u => Utils.IsWithinDistance(u.currentTile.gridPosition, unit.currentTile.gridPosition, unit.data.attackRange))
                );

                if (!hasInRangeOpponents) unit.Deactivate();
            }
        }
    }

    private Tile GetClickedTile()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 1. Try 3D Physics Raycast (For 3D Isometric setup)
        if (Physics.Raycast(ray, out RaycastHit hit3D))
        {
            return hit3D.collider.GetComponent<Tile>();
        }

        return null;
    }

    private void DeselectAll()
    {
        selectedUnit = null;
        highlightedTiles.Clear();
        GridManager.Instance.ClearAllHighlights();
    }
}