using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public static GridGenerator Instance;

    [Header("Grid Size")]
    public int width = 11;
    public int height = 11;
    public float tileSize = 1.0f;

    [Header("Grid Type")]
    [Tooltip("Check this for 3D low-poly models with rotated camera. Uncheck for 2D isometric sprites.")]
    public bool is3DIsometric = true;

    [Header("Tile Prefabs")]
    public GameObject fieldTilePrefab;
    public GameObject forestTilePrefab;
    public GameObject mountainTilePrefab;
    public GameObject waterTilePrefab;

    [Header("Generation Settings")]
    public float noiseScale = 0.15f;
    public float seed = 0f;

    private void Awake()
    {
        Instance = this;
        seed = Random.Range(0f, 100f);
    }

    private void Start()
    {
        GenerateGrid();

        // Trigger World Population after grid is ready
        if (WorldPopulationGenerator.Instance != null)
        {
            WorldPopulationGenerator.Instance.PopulateWorld();
        }
    }

    public void GenerateGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                Vector3 worldPos = GridToWorldPosition(x, y);

                // Determine terrain type procedurally via Perlin Noise
                GameObject tilePrefab = GetTerrainPrefabForPosition(x, y);

                // Instantiate Tile
                GameObject tileObj = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                tileObj.name = $"Tile_{x}_{y}";

                Tile tileScript = tileObj.GetComponent<Tile>();
                if (tileScript == null) tileScript = tileObj.AddComponent<Tile>();

                tileScript.gridPosition = gridPos;

                // Handle 2D Isometric Sprite Sorting Order
                if (!is3DIsometric)
                {
                    SpriteRenderer sr = tileObj.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        // Higher (x + y) means closer to camera bottom in 2D iso -> higher sorting order
                        sr.sortingOrder = -(x + y);
                    }
                }

                // Register tile into the GridManager dictionary from Script 2
                GridManager.Instance.grid.Add(gridPos, tileScript);
            }
        }
    }

    // Convert Grid Index (X, Y) into Isometric World Coordinates
    public Vector3 GridToWorldPosition(int x, int y)
    {
        if (is3DIsometric)
        {
            // Flat 3D Plane — standard position. Isometric look comes from the Orthographic Camera angle!
            return new Vector3(x * tileSize, 0, y * tileSize);
        }
        else
        {
            // 2D Diamond Isometric Transformation
            float halfWidth = tileSize / 2f;
            float halfHeight = tileSize / 4f; // Standard 2:1 isometric ratio

            float worldX = (x - y) * halfWidth;
            float worldY = (x + y) * halfHeight;

            return new Vector3(worldX, worldY, 0);
        }
    }

    private GameObject GetTerrainPrefabForPosition(int x, int y)
    {
        // Generate values using noise
        float noiseValue = Mathf.PerlinNoise((x + seed) * noiseScale, (y + seed) * noiseScale);

        if (noiseValue < 0.35f) return waterTilePrefab;
        if (noiseValue < 0.65f) return fieldTilePrefab;
        if (noiseValue < 0.82f) return forestTilePrefab;
        return mountainTilePrefab;
    }
}