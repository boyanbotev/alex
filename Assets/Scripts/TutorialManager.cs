using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialManager : MonoBehaviour
{
    private Tutorial tutorial;
    private TurnManager turns;
    private Player player;
    private GameObject panel;
    private TextMeshProUGUI instruction;
    private int stageIndex;
    private bool dirty;
    private bool actionSatisfied;

    private IEnumerator Start()
    {
        tutorial = GameManager.Instance.Level.tutorial;
        if (tutorial == null || tutorial.stages == null || tutorial.stages.Length == 0) yield break;
        while (!GridGenerator.Instance.IsReady) yield return null;
        turns = TurnManager.Instance;
        player = turns.players.Find(p => !p.isAI);
        NeuronNetwork.IncomeChanged += MarkDirty;
        turns.Bonds.Changed += MarkDirty;
        City.UnitRecruited += OnUnitRecruited;
        Unit.UnitKilled += OnUnitKilled;
        BuildPopup();
        ShowStage();
        MarkDirty();
    }

    private void OnDisable()
    {
        NeuronNetwork.IncomeChanged -= MarkDirty;
        City.UnitRecruited -= OnUnitRecruited;
        Unit.UnitKilled -= OnUnitKilled;
        if (turns != null) turns.Bonds.Changed -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void OnUnitRecruited(Unit unit)
    {
        if (!enabled || unit.owner != player) return;
        TutorialStage stage = tutorial.stages[stageIndex];
        if (stage.condition != TutorialCondition.RecruitUnit || stage.unitType == null ||
            unit.data.CounterType != stage.unitType.CounterType) return;
        actionSatisfied = true;
        MarkDirty();
    }

    private void OnUnitKilled(Player attacker, Player victim)
    {
        if (!enabled || attacker != player || victim == null || victim == player ||
            tutorial.stages[stageIndex].condition != TutorialCondition.KillEnemyUnit) return;
        actionSatisfied = true;
        MarkDirty();
    }

    private void LateUpdate()
    {
        if (turns == null) return;
        if (turns.IsGameOver)
        {
            panel.SetActive(false);
            enabled = false;
            return;
        }
        if (!dirty) return;
        dirty = false;
        while (actionSatisfied || tutorial.stages[stageIndex].IsSatisfied(player, turns))
        {
            if (++stageIndex == tutorial.stages.Length)
            {
                panel.SetActive(false);
                enabled = false;
                turns.EndGame();
                return;
            }
            ShowStage();
        }
    }

    private void ShowStage()
    {
        actionSatisfied = false;
        instruction.text = tutorial.stages[stageIndex].instruction;
        panel.SetActive(true);
    }

    private void BuildPopup()
    {
        var root = new GameObject("Tutorial", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        var rect = Rect("Instruction", root.transform);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -80);
        rect.sizeDelta = new Vector2(560, 160);
        panel = rect.gameObject;
        panel.AddComponent<Image>().color = new Color(0.07f, 0.1f, 0.14f, 0.97f);
        panel.AddComponent<UIInputBlocker>();

        instruction = Rect("Text", rect).gameObject.AddComponent<TextMeshProUGUI>();
        instruction.rectTransform.anchorMin = Vector2.zero;
        instruction.rectTransform.anchorMax = Vector2.one;
        instruction.rectTransform.offsetMin = new Vector2(24, 20);
        instruction.rectTransform.offsetMax = new Vector2(-64, -20);
        instruction.fontSize = 26;
        instruction.enableAutoSizing = true;
        instruction.fontSizeMin = 18;
        instruction.fontSizeMax = 26;
        instruction.color = new Color(0.94f, 0.93f, 0.9f);
        instruction.alignment = TextAlignmentOptions.MidlineLeft;
        instruction.raycastTarget = false;

        var close = Rect("Close", rect);
        close.anchorMin = close.anchorMax = new Vector2(1, 1);
        close.pivot = new Vector2(1, 1);
        close.anchoredPosition = new Vector2(-8, -8);
        close.sizeDelta = new Vector2(44, 44);
        close.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.2f, 0.27f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(() => panel.SetActive(false));
        var caption = Rect("Label", close).gameObject.AddComponent<TextMeshProUGUI>();
        caption.rectTransform.anchorMin = Vector2.zero;
        caption.rectTransform.anchorMax = Vector2.one;
        caption.rectTransform.sizeDelta = Vector2.zero;
        caption.text = "X";
        caption.fontSize = 24;
        caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
}
