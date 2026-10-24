using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One small mesh per segment. Adjacent segments meet at their shared midpoint;
// city arms reach the centre. Crossing diagonal arms do not create a graph junction.
public sealed class NeuronSegmentVisual : MonoBehaviour
{
    private Building building;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MeshRenderer shadowRenderer;
    private readonly List<Vector3> vertices = new(208);
    private readonly List<Vector3> uvs = new(208);
    private readonly List<Vector3> acrossDirections = new(208);
    private readonly List<int> triangles = new(324);
    private static readonly int widthScaleId = Shader.PropertyToID("_WidthScale");
    private static readonly int curveAmountId = Shader.PropertyToID("_CurveAmount");

    public static void RefreshAround(Tile tile)
    {
        if (tile == null || GridManager.Instance == null) return;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            Tile nearby = GridManager.Instance.GetTileAt(tile.gridPosition + new Vector2Int(dx, dy));
            Building segment = nearby != null ? nearby.currentBuilding : null;
            if (segment == null || !segment.IsPlacedNeuron) continue;
            var visual = segment.GetComponent<NeuronSegmentVisual>();
            if (visual == null) visual = segment.gameObject.AddComponent<NeuronSegmentVisual>();
            visual.Refresh();
        }
    }

    public void Refresh()
    {
        if (building == null) building = GetComponent<Building>();
        if (building == null || !building.IsPlacedNeuron) return;
        if (mesh == null)
        {
            var original = GetComponentsInChildren<MeshRenderer>();
            if (original.Length == 0) return;
            Material material = original[0].sharedMaterial;
            foreach (var renderer in original) renderer.enabled = false;
            var visual = new GameObject("Neuron connections");
            visual.transform.SetParent(transform, false);
            meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            mesh = new Mesh { name = "Neuron segment" };
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shadow = new GameObject("Neuron contact shadow");
            shadow.transform.SetParent(transform, false);
            shadowRenderer = shadow.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = Resources.Load<Material>("NeuronShadow");
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
        }

        Player viewer = TurnManager.Instance.players.Find(p => p != null && !p.isAI);
        bool visible = viewer != null && viewer.visibleTiles != null && viewer.visibleTiles.IsVisible(building.tile);
        meshRenderer.enabled = visible;
        shadowRenderer.enabled = visible;
        if (!visible) return;

        vertices.Clear();
        uvs.Clear();
        acrossDirections.Clear();
        triangles.Clear();
        Vector3 centre = SurfacePosition(building.tile, building.data.neuronHeight);
        Material appearanceMaterial = meshRenderer.sharedMaterial;
        float widthScale = appearanceMaterial != null && appearanceMaterial.HasProperty(widthScaleId) ? appearanceMaterial.GetFloat(widthScaleId) : 1f;
        float curveAmount = appearanceMaterial != null && appearanceMaterial.HasProperty(curveAmountId) ? appearanceMaterial.GetFloat(curveAmountId) : 0.045f;
        float radius = Mathf.Max(0.01f, building.data.neuronWidth * widthScale) * 0.5f;
        int connections = 0;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            Tile next = GridManager.Instance.GetTileAt(building.tile.gridPosition + new Vector2Int(dx, dy));
            if (next == null || !viewer.visibleTiles.IsVisible(next)) continue;
            Building other = next.currentBuilding;
            if (next.city == null && (other == null || !other.IsPlacedNeuron)) continue;
            Vector3 end = SurfacePosition(next, next.city == null ? other.data.neuronHeight : building.data.neuronHeight);
            AddConnection(centre, end, radius, next.city == null, Mathf.Max(0, curveAmount));
            connections++;
        }
        if (connections > 1) AddJunction(centre, radius);
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, acrossDirections);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // The contact-shadow shader can shift vertices by up to 0.1 on each world axis.
        Bounds bounds = mesh.bounds;
        Vector3 scale = transform.lossyScale;
        float minScale = Mathf.Max(0.001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        bounds.Expand(0.2f / minScale);
        mesh.bounds = bounds;
    }

    private void AddJunction(Vector3 centre, float radius)
    {
        // A round fill only at branching/bending tiles; endpoints remain plain strips.
        const int sides = 12;
        int start = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(centre));
        uvs.Add(new Vector3(0.5f, 0.5f, 1));
        acrossDirections.Add(Vector3.zero);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            vertices.Add(transform.InverseTransformPoint(centre + offset * radius));
            uvs.Add(new Vector3(offset.x * 0.5f + 0.5f, offset.z * 0.5f + 0.5f, 1));
            acrossDirections.Add(Vector3.zero);
            triangles.Add(start);
            triangles.Add(start + 1 + i);
            triangles.Add(start + 1 + (i + 1) % sides);
        }
    }

    private void AddConnection(Vector3 start, Vector3 end, float radius, bool halfConnection, float curveAmount)
    {
        // Evaluate both halves on the same curve so neighbours meet without a kink.
        bool reverse = start.x > end.x || (start.x == end.x && start.z > end.z);
        Vector3 a = reverse ? end : start;
        Vector3 b = reverse ? start : end;
        Vector3 direction = b - a;
        Vector3 bend = Vector3.Cross(Vector3.up, direction).normalized;
        float amplitude = Mathf.Min(direction.magnitude * curveAmount, radius * 0.8f);
        float from = reverse ? 1f : 0f;
        float to = halfConnection ? 0.5f : 1f - from;
        Vector3 previous = start;
        Vector3 previousSide = Vector3.Cross(Vector3.up, direction).normalized * radius;
        const int steps = 6;
        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(from, to, i / (float)steps);
            float wave = Mathf.Sin(Mathf.PI * t);
            Vector3 point = Vector3.Lerp(a, b, t) + bend * (amplitude * wave * wave);
            Vector3 tangent = direction + bend * (amplitude * Mathf.PI * Mathf.Sin(2f * Mathf.PI * t));
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * radius;
            AddQuad(previous - previousSide, point - side, point + side, previous + previousSide);
            previous = point;
            previousSide = side;
        }
    }

    private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(a));
        vertices.Add(transform.InverseTransformPoint(b));
        vertices.Add(transform.InverseTransformPoint(c));
        vertices.Add(transform.InverseTransformPoint(d));
        // Z distinguishes strip edges from a radial junction edge in the shader.
        uvs.Add(new Vector3(0, 0, 0));
        uvs.Add(new Vector3(0, 1, 0));
        uvs.Add(new Vector3(1, 1, 0));
        uvs.Add(new Vector3(1, 0, 0));
        Vector3 startAcross = (d - a).normalized;
        Vector3 endAcross = (c - b).normalized;
        acrossDirections.Add(startAcross);
        acrossDirections.Add(endAcross);
        acrossDirections.Add(endAcross);
        acrossDirections.Add(startAcross);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static Vector3 SurfacePosition(Tile tile, float offset)
    {
        Vector3 position = tile.transform.position;
        var surface = tile.GetComponent<Collider>();
        if (surface != null) position.y = Mathf.Max(position.y, surface.bounds.max.y);
        position.y += Mathf.Max(0.01f, offset);
        return position;
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }
}
