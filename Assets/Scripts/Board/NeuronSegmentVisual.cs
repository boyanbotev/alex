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
    private readonly List<Vector3> vertices = new(36);
    private readonly List<int> triangles = new(54);

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
        }

        Player viewer = TurnManager.Instance.players.Find(p => p != null && !p.isAI);
        bool visible = viewer != null && viewer.visibleTiles != null && viewer.visibleTiles.IsVisible(building.tile);
        meshRenderer.enabled = visible;
        if (!visible) return;

        vertices.Clear();
        triangles.Clear();
        Vector3 centre = SurfacePosition(building.tile, building.data.neuronHeight);
        float radius = Mathf.Max(0.01f, building.data.neuronWidth) * 0.5f;
        if (TurnManager.Instance.Bonds.IsReinforced(building.tile)) radius *= 1.8f;
        AddQuad(centre + new Vector3(-radius, 0, -radius), centre + new Vector3(-radius, 0, radius),
            centre + new Vector3(radius, 0, radius), centre + new Vector3(radius, 0, -radius));

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            Tile next = GridManager.Instance.GetTileAt(building.tile.gridPosition + new Vector2Int(dx, dy));
            if (next == null || !viewer.visibleTiles.IsVisible(next)) continue;
            Building other = next.currentBuilding;
            if (next.city == null && (other == null || !other.IsPlacedNeuron)) continue;
            Vector3 end = SurfacePosition(next, next.city == null ? other.data.neuronHeight : building.data.neuronHeight);
            if (next.city == null) end = (centre + end) * 0.5f;
            Vector3 side = Vector3.Cross(Vector3.up, end - centre).normalized * radius;
            AddQuad(centre - side, end - side, end + side, centre + side);
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(a));
        vertices.Add(transform.InverseTransformPoint(b));
        vertices.Add(transform.InverseTransformPoint(c));
        vertices.Add(transform.InverseTransformPoint(d));
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
