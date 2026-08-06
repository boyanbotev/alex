using UnityEngine;
using System.Collections.Generic;

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


        // Option A: Active Unit Action Executions
        if (selectedUnit != null)
        {
            // Move to Empty Highlighted Tile
            if (highlightedTiles.Contains(clickedTile) && clickedTile.currentUnit == null && !selectedUnit.hasMoved)
            {
                Debug.Log("moving to new location");
                selectedUnit.MoveTo(clickedTile);

                if (clickedTile.city != null && clickedTile.city.owner != selectedUnit.owner)
                {
                    Debug.Log("Claiming City");
                    clickedTile.city.Claim(selectedUnit.owner);
                }

                HighlightActions(selectedUnit);
                return;
            }

            // Attack Enemy on Highlighted Tile
            if (highlightedTiles.Contains(clickedTile) && clickedTile.currentUnit != null)
            {
                Unit targetUnit = clickedTile.currentUnit;
                if (targetUnit.owner != TurnManager.Instance.ActivePlayer && !selectedUnit.hasAttacked)
                {
                    Debug.Log("attack");
                    selectedUnit.Attack(targetUnit);

                    Tile targetTile = targetUnit.currentTile;
                    if (targetUnit.gameObject == null || !targetUnit.isAlive)
                    {
                        selectedUnit.MoveTo(targetTile);

                        if (targetTile.city != null)
                        {
                            targetTile.city.Claim(selectedUnit.owner);
                        }
                        // TODO: too much nesting, separate selection logic from attack logic
                    }
                    DeselectAll();
                    return;
                }
            }
        }

        // Option B: Select Unit or Tile
        DeselectAll();

        if (clickedTile.currentUnit != null)
        {
            Debug.Log("has clicked unit");
            Unit unit = clickedTile.currentUnit;
            if (unit.owner == TurnManager.Instance.ActivePlayer)
            {
                Debug.Log("unit belongs to active player");
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
                UIManager.Instance.ShowSpawnButton(() => city.SpawnUnit(city.owner.faction.unitPrefab, 3));
                // TODO: separation of concerns
            }
        }
    }

    private void HighlightActions(Unit unit)
    {
        GridManager.Instance.ClearAllHighlights();
        highlightedTiles.Clear();

        // Highlight Valid Movement Range
        if (!unit.hasMoved)
        {
            List<Tile> moveTiles = GridManager.Instance.GetTilesInRange(unit.currentTile, unit.moveRange);
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
            List<Tile> attackTiles = GridManager.Instance.GetTilesInRange(unit.currentTile, unit.attackRange);
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
}