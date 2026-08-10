using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SelectionController : MonoBehaviour
{
    private Unit selectedUnit;
    private City selectedCity;
    private List<Tile> highlightedTiles = new List<Tile>();

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
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
        SelectTileItem(clickedTile);
    }

    private void SelectTileItem(Tile clickedTile)
    {
        DeselectAll();

        if (clickedTile.currentUnit != null)
        {
            Unit unit = clickedTile.currentUnit;
            if (unit.owner == TurnManager.Instance.ActivePlayer)
            {
                selectedUnit = unit;
                HighlightActions(selectedUnit);
            }
        }
        else if (clickedTile.city != null)
        {
            City city = clickedTile.city;
            if (city.owner == TurnManager.Instance.ActivePlayer)
            {
                Debug.Log("city belongs to active player");
                selectedCity = city;
                GridManager.Instance.ClearAllHighlights();
                highlightedTiles.Clear();

                UIManager.Instance.ShowSpawnButtons(TurnManager.Instance.ActivePlayer.faction.availableUnits, city);
            }
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
    }

    private void Attack(Unit targetUnit)
    {
        selectedUnit.Attack(targetUnit);

        Tile targetTile = targetUnit.currentTile;
        bool isMeleeAttack = selectedUnit.data.attackRange == 1;

        if (isMeleeAttack && (targetUnit.gameObject == null || !targetUnit.isAlive))
        {
            selectedUnit.MoveTo(targetTile);

            if (targetTile.city != null)
            {
                targetTile.city.Claim(selectedUnit.owner);
            }
        }

        DeactivateUsedUnits(selectedUnit.owner.units);
        DeselectAll();
    }

    private void MoveTo(Tile tile)
    {
        selectedUnit.MoveTo(tile);

        if (tile.city != null)
        {
            tile.city.Claim(selectedUnit.owner);
        }

        HighlightActions(selectedUnit);
        DeactivateUsedUnits(selectedUnit.owner.units);
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
            {
                if (tile.currentUnit == null)
                {
                    tile.SetHighlight(true, Color.blue);
                    highlightedTiles.Add(tile);
                }
            }
        }

        // Highlight Valid Attack Range
        if (!unit.hasAttacked)
        {
            List<Tile> attackTiles = GridManager.Instance.GetTilesInRange(unit.currentTile, unit.data.attackRange);
            foreach (Tile tile in attackTiles)
            {
                if (tile.currentUnit != null && tile.currentUnit.owner != unit.owner)
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
                List <Player> enemyPlayers = TurnManager.Instance.players.ToList(); // TODO: get the player's enemies, which will be stored
                enemyPlayers.Remove(unit.owner);

                bool hasInRangeOpponents = enemyPlayers.Any(enemyPlayer => 
                    enemyPlayer.units.Any(u => IsWithinDistance(u.currentTile.gridPosition, unit.currentTile.gridPosition, unit.data.attackRange))
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
        selectedCity = null;
        highlightedTiles.Clear();
        GridManager.Instance.ClearAllHighlights();
    }

    private bool IsWithinDistance(Vector2Int a, Vector2Int b, int maxDistance)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        if (dx == 0 && dy == 0) return false;

        return Mathf.Max(dx, dy) <= maxDistance;
    }
}