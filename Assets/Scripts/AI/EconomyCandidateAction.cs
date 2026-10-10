public enum EconomyActionKind { PlaceBuilding, SpawnUnit, PlaceNeuron, UpgradePerk, UpgradeBond }

public class EconomyCandidateAction
{
    public EconomyActionKind kind;
    public float score;
    public int cost;

    public BuildingData building;
    public Tile buildTile;
    public City city;
    public City partner;
    public FactionUnit unit;

    public override string ToString()
    {
        return $"EconomyAction " +
            $"score: {score}, "+
            $"unit name: {unit?.name},  " +
            $"city name: {city?.cityName},  " +
            $"partner: {partner?.cityName},  " +
            $"cost: {cost},  " +
            $"building name: {building?.buildingName}";
    }
}
