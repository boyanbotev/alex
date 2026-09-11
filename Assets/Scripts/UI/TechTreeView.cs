using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owns the persistent view. Layout can later be replaced with authored positions.
public class TechTreeView : MonoBehaviour
{
    private sealed class Node
    {
        public TechData tech;
        public RectTransform rect;
        public Image image;
        public TextMeshProUGUI label;
        public int state = -1;
        public readonly List<Node> children = new List<Node>();
    }

    private sealed class Edge
    {
        public TechData prerequisite;
        public Image image;
    }

    private readonly List<Node> nodes = new List<Node>();
    private readonly List<Edge> edges = new List<Edge>();
    private readonly Dictionary<TechData, Node> lookup = new Dictionary<TechData, Node>();
    private RectTransform viewport, content, graph;
    private ScrollRect scroll;
    private TMP_FontAsset font;
    private Faction faction;
    private Player player;
    private Action<TechData, Player> onSelect;
    private Vector2 graphSize, lastViewportSize;
    private bool fitPending;
    private static readonly Color Locked = new Color(.23f, .26f, .31f);
    private static readonly Color Available = new Color(.16f, .43f, .65f);
    private static readonly Color Researched = new Color(.16f, .49f, .30f);
    private static readonly Color Unaffordable = new Color(.48f, .35f, .17f);

    public void Initialize(RectTransform holder, GameObject buttonPrefab, Action<TechData, Player> select)
    {
        onSelect = select;
        font = buttonPrefab.GetComponentInChildren<TextMeshProUGUI>(true).font;
        viewport = holder;
        var layout = holder.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = new Vector2(24, 30);
        holder.offsetMax = new Vector2(-24, -80);
        holder.gameObject.AddComponent<RectMask2D>();
        var background = holder.gameObject.AddComponent<Image>();
        background.color = new Color(.06f, .08f, .12f, .96f);
        scroll = holder.gameObject.AddComponent<ScrollRect>();
        content = Rect("Tech Tree Content", holder);
        graph = Rect("Graph", content);
        scroll.viewport = holder;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
    }

    public void Show(Player owner)
    {
        player = owner;
        if (faction != owner.faction)
        {
            faction = owner.faction;
            Build();
        }
        RefreshState();
    }

    private void Build()
    {
        foreach (Transform child in graph)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        nodes.Clear();
        edges.Clear();
        lookup.Clear();
        if (faction.availableTech != null)
        foreach (var tech in faction.availableTech)
        {
            if (tech == null || lookup.ContainsKey(tech)) continue;
            var rect = Rect(tech.techName, graph);
            rect.sizeDelta = new Vector2(170, 110);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onSelect(tech, player));
            var label = Label(rect, "");
            var node = new Node { tech = tech, rect = rect, image = image, label = label };
            nodes.Add(node);
            lookup.Add(tech, node);
        }

        // A spanning forest determines placement; all prerequisite edges are drawn.
        var roots = new List<Node>();
        foreach (var node in nodes)
        {
            Node parent = null;
            if (node.tech.prerequisites != null)
            foreach (var prerequisite in node.tech.prerequisites)
                if (prerequisite != null && prerequisite != node.tech && lookup.TryGetValue(prerequisite, out parent)) break;
            if (parent == null) roots.Add(node);
            else parent.children.Add(node);
        }
        var placed = new HashSet<Node>();
        for (int i = 0; i < roots.Count; i++)
            Place(roots[i], 45 - i * 360f / roots.Count, 360f / roots.Count, 1, placed);
        // Malformed cyclic data must not recurse forever or hide nodes.
        foreach (var node in nodes)
            if (!placed.Contains(node)) Place(node, 90 - placed.Count * 137.5f, 60, 1, placed);

        Vector2 min = new Vector2(-85, -55), max = new Vector2(85, 55);
        foreach (var node in nodes)
        {
            min = Vector2.Min(min, node.rect.anchoredPosition - node.rect.sizeDelta / 2);
            max = Vector2.Max(max, node.rect.anchoredPosition + node.rect.sizeDelta / 2);
            bool hasEdge = false;
            if (node.tech.prerequisites != null)
            foreach (var prerequisite in node.tech.prerequisites)
            {
                if (prerequisite == null || !lookup.TryGetValue(prerequisite, out var source)) continue;
                Connect(source.rect.anchoredPosition, node.rect.anchoredPosition, prerequisite);
                hasEdge = true;
            }
            if (!hasEdge) Connect(Vector2.zero, node.rect.anchoredPosition, null);
        }
        var hub = Rect("Faction", graph);
        hub.sizeDelta = new Vector2(150, 90);
        hub.gameObject.AddComponent<Image>().color = new Color(.12f, .18f, .26f);
        Label(hub, faction.name + "\nTechnologies");
        graphSize = max - min + new Vector2(48, 48);
        graph.sizeDelta = graphSize;
        Vector2 centre = (min + max) / 2;
        foreach (RectTransform child in graph) child.anchoredPosition -= centre;
        fitPending = true;
    }

    private static void Place(Node node, float angle, float sector, int depth, HashSet<Node> placed)
    {
        if (!placed.Add(node)) return;
        float radians = angle * Mathf.Deg2Rad;
        node.rect.anchoredPosition = new Vector2(Mathf.Cos(radians) * 230, Mathf.Sin(radians) * 180) * depth;
        float spread = Mathf.Min(sector, 90);
        for (int i = 0; i < node.children.Count; i++)
            Place(node.children[i], angle + ((i + .5f) / node.children.Count - .5f) * spread,
                spread / node.children.Count, depth + 1, placed);
    }

    private void Connect(Vector2 start, Vector2 end, TechData prerequisite)
    {
        var rect = Rect("Connection", graph);
        rect.SetAsFirstSibling();
        Vector2 delta = end - start;
        rect.anchoredPosition = (start + end) / 2;
        rect.sizeDelta = new Vector2(delta.magnitude, 5);
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        edges.Add(new Edge { prerequisite = prerequisite, image = image });
    }

    public void RefreshState()
    {
        if (player == null) return;
        foreach (var node in nodes)
        {
            int state = player.techState.IsUnlocked(node.tech) ? 3
                : !player.techState.CanResearch(node.tech) ? 0 : player.stars < node.tech.cost ? 1 : 2;
            if (state == node.state) continue;
            node.state = state;
            node.image.color = state == 3 ? Researched : state == 2 ? Available : state == 1 ? Unaffordable : Locked;
            string status = state == 3 ? "Researched" : state == 2 ? "Available" : state == 1 ? "Need stars" : "Locked";
            node.label.text = node.tech.techName + "\n<size=75%>" + node.tech.cost + " stars · " + status + "</size>";
        }
        foreach (var edge in edges)
            edge.image.color = edge.prerequisite == null || player.techState.IsUnlocked(edge.prerequisite)
                ? Researched : new Color(.4f, .43f, .48f);
    }

    private void LateUpdate()
    {
        if (viewport == null || graphSize == Vector2.zero) return;
        Vector2 size = viewport.rect.size;
        if (!fitPending && size == lastViewportSize) return;
        if (size.x <= 0 || size.y <= 0) return;
        lastViewportSize = size;
        fitPending = false;
        float scale = Mathf.Clamp(Mathf.Min(size.x / graphSize.x, size.y / graphSize.y), .65f, 1);
        graph.localScale = Vector3.one * scale;
        content.sizeDelta = Vector2.Max(size, graphSize * scale);
        scroll.StopMovement();
        content.anchoredPosition = Vector2.zero;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        return rect;
    }

    private TextMeshProUGUI Label(RectTransform parent, string text)
    {
        var rect = Rect("Label", parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8, 8);
        rect.offsetMax = new Vector2(-8, -8);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 22;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
}
