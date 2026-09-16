using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class SelectionController : MonoBehaviour
{
    [SerializeField] Color attackColor;
    [SerializeField] Color moveColor;
    private Unit selectedUnit;
    private List<Tile> highlightedTiles = new List<Tile>();
    private readonly List<Tile> moveTiles = new(64);
    private static readonly System.Func<Tile, Unit> GetLiveOccupant = tile => tile.currentUnit;
    [Header("Combat Preview")]
    [SerializeField, Min(0f)] private float previewHoldSeconds = 0.3f;
    [SerializeField] private Texture2D previewSkull;
    private Vector3 lastPointerPosition;
    private float pointerDistance;
    private float pressTime;
    private bool trackingPress;
    private bool previewShown;
    private bool pressedOnEnemy;
    private Unit pressedEnemy;
    private CombatPreviewUI combatPreview;
    private CameraController cameraController;
    private const float DragThreshold = 10f;
    private DiplomacyState diplomacy;

    private void OnDisable()
    {
        CancelPress();
        if (diplomacy != null) diplomacy.RelationChanged -= OnRelationChanged;
        diplomacy = null;
    }

    private void OnRelationChanged(Player a, Player b, DiplomaticRelation relation)
    {
        if (selectedUnit != null) HighlightActions(selectedUnit);
    }

    private void Update()
    {
        if (GridGenerator.Instance == null || !GridGenerator.Instance.IsReady)
        {
            CancelPress();
            return;
        }
        if (diplomacy == null)
        {
            diplomacy = TurnManager.Instance.Diplomacy;
            diplomacy.RelationChanged += OnRelationChanged;
        }
        if (TurnManager.Instance.ActivePlayer.isAI)
        {
            CancelPress();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            CancelPress();
            if (UIRaycastUtility.IsPointerOverBlockingUI(Input.mousePosition))
            {
                return;
            }

            trackingPress = true;
            lastPointerPosition = Input.mousePosition;
            pressTime = Time.unscaledTime;
            if (cameraController == null && Camera.main != null)
                cameraController = Camera.main.GetComponent<CameraController>();
            Tile tile = GetClickedTile();
            if (tile != null && CanPreview(tile.currentUnit))
            {
                pressedEnemy = tile.currentUnit;
                pressedOnEnemy = true;
            }
        }

        if (!trackingPress) return;
        pointerDistance += Vector3.Distance(lastPointerPosition, Input.mousePosition);
        lastPointerPosition = Input.mousePosition;
        float threshold = cameraController != null ? cameraController.DragThreshold : DragThreshold;
        if (pointerDistance > threshold || UIRaycastUtility.IsPointerOverBlockingUI(Input.mousePosition))
        {
            CancelPress();
            return;
        }
        if (pressedOnEnemy)
        {
            if (!CanPreview(pressedEnemy) || GetClickedTile() != pressedEnemy.currentTile)
            {
                CancelPress();
                return;
            }
            if (!previewShown && Time.unscaledTime - pressTime >= previewHoldSeconds)
            {
                previewShown = true;
                if (combatPreview == null)
                    combatPreview = new GameObject("Combat Preview").AddComponent<CombatPreviewUI>();
                combatPreview.Show(selectedUnit, pressedEnemy, previewSkull);
            }
        }
        else if (previewShown)
        {
            CancelPress();
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            bool click = !previewShown;
            CancelPress();
            if (click) HandleClick();
        }
        else if (!Input.GetMouseButton(0)) CancelPress();
    }

    private bool CanPreview(Unit target)
    {
        return selectedUnit != null && selectedUnit.isAlive && selectedUnit.isActive &&
            !selectedUnit.hasAttacked && selectedUnit.owner == TurnManager.Instance.ActivePlayer &&
            target != null && target.isAlive && highlightedTiles.Contains(target.currentTile) &&
            selectedUnit.owner.visibleTiles.IsVisible(target.currentTile) &&
            InteractionRules.CanAttack(selectedUnit.owner, target.owner) &&
            Utils.IsWithinDistance(selectedUnit.currentTile.gridPosition, target.currentTile.gridPosition,
                selectedUnit.data.attackRange);
    }

    private void CancelPress()
    {
        trackingPress = false;
        previewShown = false;
        pressedEnemy = null;
        pressedOnEnemy = false;
        pointerDistance = 0f;
        if (combatPreview != null) combatPreview.Hide();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelPress();
    }

    private void OnDestroy()
    {
        if (combatPreview != null) Destroy(combatPreview.gameObject);
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
        else if (clickedTile.city == null && clickedTile.currentBuilding == null)
        {
            SelectTerritory(clickedTile);
        }
        else if (clickedTile.currentBuilding != null)
        {
            UIManager.Instance.ShowNeuronActions(clickedTile.currentBuilding, null, DeselectAll);
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
            return player.techState.CanBuild(b) && (b.isNeuron ? player.CanPlaceNeuron(b, tile) :
                city != null && city.owner == player && tile.city == null && b.CanPlaceAt(tile, city));
        })
        .ToArray();

        if (availableBuildings.Length > 0)
        {
            UIManager.Instance.ShowBuildButtons(availableBuildings, tile, city);
            tile.SetHighlight(true, moveColor);
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
            if (InteractionRules.CanAttack(selectedUnit.owner, targetUnit.owner) && !selectedUnit.hasAttacked)
            {
                Attack(targetUnit);
                return;
            }
        }

        if (clickedTile == selectedUnit.currentTile)
        {
            Unit unit = selectedUnit;
            DeselectAll();
            if (clickedTile.currentBuilding != null)
                UIManager.Instance.ShowNeuronActions(clickedTile.currentBuilding, unit, DeselectAll);

            if (clickedTile.city == null && clickedTile.currentBuilding == null)
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
        UIManager.Instance.ShowNeuronActions(unit.currentTile.currentBuilding, unit, DeselectAll);

        if (!unit.isActive) return;

        // Highlight Valid Movement Range
        if (!unit.hasMoved)
        {
            GridManager.Instance.GetReachableMoveTiles(unit.currentTile, unit.owner, unit.data.moveRange,
                GetLiveOccupant, moveTiles);
            foreach (Tile tile in moveTiles)

                if (tile.currentUnit == null && unit.owner.visibleTiles.IsVisible(tile))
                {
                    tile.SetHighlight(true, moveColor);
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
                if (visible && tile.currentUnit != null && InteractionRules.CanAttack(unit.owner, tile.currentUnit.owner))
                {
                    tile.SetHighlight(true, attackColor);
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
                bool hasInRangeOpponents = HasInRangeEnemy(unit);

                if (!hasInRangeOpponents) unit.Deactivate();
            }
        }
    }

    private static bool HasInRangeEnemy(Unit unit)
    {
        if (unit.CanSeverNeuron(unit.currentTile.currentBuilding)) return true;
        foreach (Player other in TurnManager.Instance.players)
        {
            if (!InteractionRules.CanAttack(unit.owner, other)) continue;
            foreach (Unit target in other.units)
            {
                if (target != null && target.isAlive &&
                    unit.owner.visibleTiles.IsVisible(target.currentTile) &&
                    Utils.IsWithinDistance(target.currentTile.gridPosition,
                        unit.currentTile.gridPosition, unit.data.attackRange))
                    return true;
            }
        }
        return false;
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
        CancelPress();
        selectedUnit = null;
        highlightedTiles.Clear();
        GridManager.Instance.ClearAllHighlights();
        UIManager.Instance.CloseBuildPanel();
        UIManager.Instance.CloseSpawnPanel();
    }
}
