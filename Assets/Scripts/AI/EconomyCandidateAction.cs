public enum EconomyActionKind { ResearchTech, PlaceBuilding, SpawnUnit }

public class EconomyCandidateAction
{
    public EconomyActionKind kind;
    public float score;
    public int cost;

    public TechData tech;
    public BuildingData building;
    public Tile buildTile;
    public City city;
    public FactionUnit unit;
}
