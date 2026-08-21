using UnityEngine;

[CreateAssetMenu(fileName = "AI Profile", menuName = "AI/AI Profile")]
public class AIProfile : ScriptableObject
{
    [Header("Strategic Weights")]
    public float cityCaptureWeight = 40f;
    public  float cityProgressWeight = 3f;
    public float damageWeight = 1f;
    public float killWeight = 5f;
    public float retaliationWeight = 1f;
    public float survivalWeight = 4f;
    public float positionWeight = 1f;

    [Header("Lookahead")]
    [Tooltip("How many of each unit's best immediate candidates get a full lookahead evaluation. Higher = smarter, slower.")]
    public int perUnitLookaheadCandidates = 2;
    [Tooltip("How many further actions of MY OWN turn to simulate after the candidate being scored.")]
    public int ownRolloutSteps = 2;
    [Tooltip("How many actions of each enemy's best response turn to simulate against the resulting position.")]
    public int enemyRolloutSteps = 2;
    [Tooltip("How heavily to weigh the simulated enemy response when scoring a candidate.")]
    public float enemyThreatWeight = 1f;

    [Header("Economy - Spawning Units")]
    public float expansionWeight = 6f;
    public float meleeVulnerabilityWeight = 0.1f;
    public float counterWeight = 8f;

    [Header("Economy - Buildings")]
    [Tooltip("Flat score every buildable building gets, so population-neutral buildings can still win when nothing else is pressing.")]
    public float buildingBaseWeight = 2f;
    [Tooltip("Scales how much a building's populationGiven is worth, weighted by how close it gets the city to leveling up.")]
    public float cityGrowthWeight = 12f;

    [Header("Economy - Research")]
    [Tooltip("Flat score every researchable tech gets.")]
    public float researchBaseWeight = 3f;
    [Tooltip("Score added per building that a tech directly unlocks.")]
    public float researchBuildingUnlockWeight = 10f;
    [Tooltip("Smaller score added per further tech that lists this tech as a prerequisite.")]
    public float researchBridgeWeight = 4f;
    [Tooltip("If the best building/unit score this turn is below this, research scores get multiplied by researchLullBoost.")]
    public float researchLullThreshold = 8f;
    [Tooltip("Multiplier applied to research scores during a lull (see researchLullThreshold).")]
    public float researchLullBoost = 1.5f;
}
