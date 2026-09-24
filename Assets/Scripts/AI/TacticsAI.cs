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
    private readonly GarrisonReplacementPlanner replacements = new();
    private readonly List<CandidateAction> _shortlist = new List<CandidateAction>(32);
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
            // A coordinated purchase must compete with ordinary actions even when
            // the profile keeps only one immediate candidate per unit.
            bool hasReplacement = replacements.TryPlan(player, profile, _scorer, out CandidateAction replacement);

            CandidateAction best = default;
            float bestScore = float.NegativeInfinity;

            _frameBudgetTimer.Restart();

            int len = Mathf.Min(_shortlist.Count, Mathf.Max(1, profile.maxShortlistSize));
            if (hasReplacement)
            {
                if (_shortlist.Count > len) _shortlist.RemoveRange(len, _shortlist.Count - len);
                _shortlist.Add(replacement);
                len++;
            }

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

                board.WithFreshTurn(enemy);
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
        if (TurnManager.Instance.Diplomacy.Revision != diplomacyRevision || action.unit == null) yield break;
        if (action.kind == ActionKind.ReplaceGarrison)
        {
            if (!action.unit.TryReplaceGarrison(action.recruitCity, action.recruit, action.moveTile)) yield break;
            yield return ActionAnimationWait;
            yield break;
        }
        if (action.kind == ActionKind.DoNothing)
        {
            action.unit.Deactivate();
            yield return null;
            yield break;
        }

        bool visible = IsVisibleToLocalPlayer(action);
        bool executed = action.kind switch
        {
            ActionKind.MoveOnly => action.unit.MoveTo(action.moveTile),
            ActionKind.Attack => action.unit.Attack(action.target, action.moveTile),
            ActionKind.SeverNeuron => action.unit.TrySeverNeuron(action.neuron, action.moveTile),
            _ => false
        };
        if (!executed) yield break;

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
