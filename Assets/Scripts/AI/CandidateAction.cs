public enum ActionKind { MoveOnly, Attack, DoNothing, SeverNeuron }

public struct CandidateAction
{
    public Unit unit;
    public Tile moveTile;
    public Unit target;
    public Building neuron;
    public ActionKind kind;
    public float score;
}
