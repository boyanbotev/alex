using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TacticsAI : MonoBehaviour
{
    private AIProfile profile;
    private Player controlledPlayer;
    private Player humanPlayer;

    private readonly TacticalScorer _scorer = new();
    private readonly CandidateGenerator _candidates = new();
    private readonly List<CandidateAction> _shortlist = new List<CandidateAction>(32);
    private readonly List<Tile> severMoveTiles = new();
    private readonly System.Diagnostics.Stopwatch _frameBudgetTimer = new System.Diagnostics.Stopwatch();
    private static readonly WaitForSeconds ActionAnimationWait = new WaitForSeconds(0.3f);

    public IEnumerator PlayTurn(Player player, AIProfile profile)
    {
        Configure(player, profile);

        while (true)
        {
            int diplomacyRevision = TurnManager.Instance.Diplomacy.Revision;
            _candidates.Generate(controlledPlayer, BoardState.Live);

            if (_candidates.Candidates.Count == 0)
                break;

            int perUnitCount = Mathf.Max(1, profile.perUnitLookaheadCandidates);
            _candidates.SelectShortlist(perUnitCount, _shortlist);

            CandidateAction best = default;
            float bestScore = float.NegativeInfinity;

            _frameBudgetTimer.Restart();

            int len = Mathf.Min(_shortlist.Count, profile.maxShortlistSize);

            if (len == 1)
            {
                yield return Execute(_shortlist[0], diplomacyRevision);
                continue;
            }

            for (int i = 0; i < len; i++)
            {
                if (TurnManager.Instance.Diplomacy.Revision != diplomacyRevision) break;
                float score = EvaluateWithLookahead(_shortlist[i], BoardState.Live);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = _shortlist[i];
                }

                bool moreToEvaluate = i < len - 1;
                if (moreToEvaluate && _frameBudgetTimer.Elapsed.TotalMilliseconds >= profile.LookaheadFrameBudgetMs)
                {
                    yield return null;
                    _frameBudgetTimer.Restart();
                }
            }

            // A manual transition may happen while lookahead yields between frames.
            // Discard every old score and regenerate, including move-only plans.
            if (TurnManager.Instance.Diplomacy.Revision != diplomacyRevision) continue;
            yield return Execute(best, diplomacyRevision);
        }
    }

    private void Configure(Player player, AIProfile profile)
    {
        this.profile = profile;
        controlledPlayer = player;
        humanPlayer = TurnManager.Instance.players.Find(p => !p.isAI);
        _scorer.Configure(profile, TurnManager.Instance.players, WorldPopulationManager.Instance.allCities);
        _candidates.Configure(GridManager.Instance, _scorer);
    }

    // Each candidate starts from the same board, even if evaluation throws.
    private float EvaluateWithLookahead(CandidateAction action, BoardState board)
    {
        int checkpoint = board.Checkpoint();

        try
        {
            ActionSimulator.Apply(board, action);

            float ownFollowUpScore =
                RolloutGreedyTurn(
                    controlledPlayer,
                    board,
                    profile.ownRolloutSteps);

            float enemyThreat = 0f;

            foreach (Player enemy in TurnManager.Instance.players)
            {
                if (!board.IsAtWar(controlledPlayer, enemy))
                    continue;

                enemyThreat += RolloutGreedyTurn(
                    enemy,
                    board,
                    profile.enemyRolloutSteps);
            }

            return action.score +
                   ownFollowUpScore -
                   enemyThreat * profile.enemyThreatWeight;
        }
        finally
        {
            board.Rollback(checkpoint);
        }
    }

    private float RolloutGreedyTurn(
        Player player,
        BoardState board,
        int maxSteps)
    {
        float total = 0f;

        for (int step = 0; step < maxSteps; step++)
        {
            _candidates.Generate(player, board);

            if (_candidates.Candidates.Count == 0)
                break;

            CandidateAction bestNext = _candidates.Candidates[0];

            for (int i = 1; i < _candidates.Candidates.Count; i++)
            {
                if (_candidates.Candidates[i].score > bestNext.score)
                    bestNext = _candidates.Candidates[i];
            }

            ActionSimulator.Apply(board, bestNext);
            total += bestNext.score;
        }

        return total;
    }

    private IEnumerator Execute(CandidateAction action, int diplomacyRevision)
    {
        if (TurnManager.Instance.Diplomacy.Revision != diplomacyRevision) yield break;
        if (action.kind == ActionKind.SeverNeuron &&
            !BoardState.Live.CanSeverNeuron(action.unit, action.moveTile, action.neuron)) yield break;
        if (action.kind == ActionKind.SeverNeuron)
        {
            if (TurnManager.Instance.ActivePlayer != action.unit.owner) yield break;
            if (action.moveTile != action.unit.currentTile)
            {
                GridManager.Instance.GetReachableMoveTiles(action.unit.currentTile, action.unit.owner,
                    BoardState.Live.GetMoveRange(action.unit), BoardState.Live.GetOccupant, severMoveTiles);
                if (!severMoveTiles.Contains(action.moveTile)) yield break;
            }
        }
        if (action.kind == ActionKind.Attack &&
            (action.target == null || !action.target.isAlive ||
             !InteractionRules.CanAttack(action.unit.owner, action.target.owner)))
            yield break;

        bool visible = IsVisibleToLocalPlayer(action);

        Tile targetTile = action.target != null
            ? action.target.currentTile
            : null;

        bool meleeAttack =
            action.kind == ActionKind.Attack &&
            action.unit.data.attackRange == 1;

        if (action.kind == ActionKind.DoNothing)
        {
            action.unit.Deactivate();

            yield return null;
        }

        // move first
        if (action.moveTile != action.unit.currentTile)
        {
            action.unit.MoveTo(action.moveTile);
        }

        // then attack
        if (action.kind == ActionKind.SeverNeuron)
            action.unit.TrySeverNeuron(action.neuron);
        if (action.kind == ActionKind.Attack &&
            action.target != null &&
            action.target.isAlive)
        {
            action.unit.Attack(action.target);

            if (meleeAttack &&
                !action.target.isAlive &&
                targetTile != null)
            {
                action.unit.MoveTo(targetTile);
            }
        }

        if (visible) yield return ActionAnimationWait;
        else yield return new WaitForSeconds(0.2f);
    }

    private bool IsVisibleToLocalPlayer(CandidateAction action)
    {
        if (humanPlayer == null || humanPlayer.visibleTiles == null) return false;
        if (humanPlayer.visibleTiles.IsVisible(action.unit.currentTile))
            return true;

        if (humanPlayer.visibleTiles.IsVisible(action.moveTile))
            return true;

        return false;
    }
}
