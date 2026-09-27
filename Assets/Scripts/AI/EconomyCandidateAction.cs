public enum EconomyActionKind { PlaceBuilding, SpawnUnit, PlaceNeuron, UpgradePerk }

public class EconomyCandidateAction
{
    public EconomyActionKind kind;
    public float score;
    public int cost;

    public BuildingData building;
    public Tile buildTile;
    public City city;
    public FactionUnit unit;

    public override string ToString()
    {
        return $"EconomyAction " +
            $"score: {score}, "+
            $"unit name: {unit?.name},  " +
            $"city name: {city?.cityName},  " +
            $"cost: {cost},  " +
            $"building name: {building?.buildingName}";
    }
}
