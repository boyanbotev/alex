using System.Collections.Generic;
using UnityEngine;

/// <summary>Generates and shortlists actions using buffers reused across decisions and rollouts.</summary>
public sealed class CandidateGenerator
{
    private GridManager grid;
    private TacticalScorer scorer;
    private readonly List<CandidateAction> _allCandidates = new(64);
    private readonly List<(int start, int count)> _unitRanges = new(16);
    private readonly List<Tile> _scratchPositions = new(16);
    private CandidateAction[] _topKScratch;
    private BoardState _occupancyBoard;
    private System.Func<Tile, Unit> _getOccupant;
    private System.Func<Player, Player, bool> _isAtWar;

    // Valid until the next Generate call. A shortlist is copied into the caller's buffer.
    public IReadOnlyList<CandidateAction> Candidates => _allCandidates;

    public void Configure(GridManager grid, TacticalScorer scorer)
    {
        this.grid = grid;
        this.scorer = scorer;
    }

    public void Generate(Player player, BoardState board)
    {
        scorer.BeginGeneration();
        if (_occupancyBoard != board)
        {
            _occupancyBoard = board;
            _getOccupant = board.GetOccupant;
            _isAtWar = board.IsAtWar;
        }
        _allCandidates.Clear();
        _unitRanges.Clear();

        foreach (Unit unit in player.units)
        {
            if (!IsUnitAvailable(unit, board))
                continue;

            int start = _allCandidates.Count;
            AppendCandidatesForUnit(unit, board, _allCandidates);
            int count = _allCandidates.Count - start;

            if (count > 0)
                _unitRanges.Add((start, count));
        }
    }

    private bool IsUnitAvailable(Unit unit, BoardState board)
    {
        return unit != null
               && board.IsAlive(unit)
               && board.IsActive(unit)
               && !(board.HasMoved(unit) && board.HasAttacked(unit));
    }

    private void AppendCandidatesForUnit(Unit unit, BoardState board, List<CandidateAction> output)
    {
        bool staticUnit = HasSkill(unit.data.skills, Skill.Static);
        Tile currentTile = board.GetTile(unit);

        _scratchPositions.Clear();

        if (!board.HasMoved(unit))
        {
            grid.GetReachableMoveTiles(currentTile, unit.owner, unit.data.moveRange,
                _getOccupant, _scratchPositions, _isAtWar);
        }
        _scratchPositions.Insert(0, currentTile);

        for (int p = 0; p < _scratchPositions.Count; p++)
        {
            Tile position = _scratchPositions[p];
            bool moved = position != currentTile;

            if (moved)
            {
                output.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = null,
                    kind = ActionKind.MoveOnly,
                    score = scorer.ScoreMove(unit, currentTile, position, board)
                });
            }
            else
            {
                output.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = currentTile,
                    target = null,
                    kind = ActionKind.DoNothing,
                    score = scorer.ScoreMove(unit, currentTile, currentTile, board)
                });
            }

            if (board.HasAttacked(unit))
                continue;

            if (moved && staticUnit)
                continue;

            Building segment = board.GetBuilding(position);
            if (board.CanSeverNeuron(unit, position, segment) && scorer.ShouldConsiderSever(unit, segment, board))
            {
                output.Add(new CandidateAction
                {
                    unit = unit, moveTile = position, neuron = segment, kind = ActionKind.SeverNeuron,
                    score = scorer.ScoreSever(unit, position, segment, board)
                });
            }

            foreach (Tile attackTile in grid.GetTilesInRange(position, unit.data.attackRange))
            {
                Unit target = board.GetOccupant(attackTile);

                if (target == null || !board.IsAtWar(unit.owner, target.owner) || !board.IsAlive(target))
                    continue;

                output.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = target,
                    kind = ActionKind.Attack,
                    score = scorer.ScoreAttack(unit, position, target, board)
                });
            }
        }
    }

    // Indexed access avoids allocating an enumerator in candidate generation.
    private static bool HasSkill(IReadOnlyList<Skill> skills, Skill skill)
    {
        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] == skill)
                return true;
        }

        return false;
    }

    private void EnsureTopKScratch(int k)
    {
        if (_topKScratch == null || _topKScratch.Length < k)
            _topKScratch = new CandidateAction[Mathf.Max(1, k)];
    }

    public void SelectShortlist(int perUnitCount, List<CandidateAction> shortlistOut)
    {
        shortlistOut.Clear();
        EnsureTopKScratch(perUnitCount);

        for (int r = 0; r < _unitRanges.Count; r++)
        {
            (int start, int count) = _unitRanges[r];
            int kept = SelectTopKForRange(start, count, perUnitCount);

            for (int i = 0; i < kept; i++)
                shortlistOut.Add(_topKScratch[i]);
        }
    }

    private int SelectTopKForRange(int start, int count, int k)
    {
        int kept = 0;

        for (int i = 0; i < count; i++)
        {
            CandidateAction candidate = _allCandidates[start + i];

            if (kept < k)
            {
                int insertAt = kept;
                while (insertAt > 0 && _topKScratch[insertAt - 1].score < candidate.score)
                {
                    _topKScratch[insertAt] = _topKScratch[insertAt - 1];
                    insertAt--;
                }
                _topKScratch[insertAt] = candidate;
                kept++;
            }
            else if (candidate.score > _topKScratch[kept - 1].score)
            {
                int insertAt = kept - 1;
                while (insertAt > 0 && _topKScratch[insertAt - 1].score < candidate.score)
                {
                    _topKScratch[insertAt] = _topKScratch[insertAt - 1];
                    insertAt--;
                }
                _topKScratch[insertAt] = candidate;
            }
        }

        return kept;
    }

}
