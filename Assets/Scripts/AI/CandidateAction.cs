public enum ActionKind { MoveOnly, Attack, DoNothing }

public struct CandidateAction
{
    public Unit unit;
    public Tile moveTile;
    public Unit target;
    public ActionKind kind;
    public float score;
}