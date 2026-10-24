using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public static event System.Action<Player, Player> UnitKilled;
    [Header("Unit Profile")]
    public string unitName;
    public Player owner;
    public Tile currentTile;
    public City homeCity;
    [Min(0)] public int dopamineBonus;

    [Header("Base Stats")]
    public int currentHealth;
    public UnitData data;

    [Header("State")]
    public bool hasMoved;
    public bool hasAttacked;
    public bool isAlive;
    public bool isActive;
    public bool hasCaptured;
    private readonly List<Unit> splashTargets = new();
    private static readonly List<Tile> moveTiles = new(64);
    private static readonly System.Func<Tile, Unit> GetLiveOccupant = tile => tile.currentUnit;
    private bool CanAct => isAlive && isActive && data != null && owner != null &&
        TurnManager.Instance != null && !TurnManager.Instance.IsGameOver && !TurnManager.Instance.IsAnimatingAction && TurnManager.Instance.ActivePlayer == owner &&
        currentTile != null && currentTile.currentUnit == this;
    private bool IsStatic => data.skills != null && System.Array.IndexOf(data.skills, Skill.Static) >= 0;

    [Header("Animation")]

    public Tween AttackAnimation { get; private set; }
    private Tween selectionAnimation;
    private Unit defeatedUnit;
    private GameObject arrowProjectile;
    private Vector3 originalScale;
    private Vector3 originalVisualScale;

    [SerializeField] private Renderer render;
    private Collider selectionCollider;
    private Player viewer;
    private MaterialPropertyBlock visualProperties;
    private Color activeColor;
    private int colorProperty;
    public Bounds VisualBounds => render != null ? render.bounds : new Bounds(transform.position, Vector3.zero);
    [SerializeField] HealthUI healthUI;

    private void Awake()
    {
        originalScale = transform.localScale;
        originalVisualScale = render != null ? render.transform.localScale : originalScale;
        if (render != null)
        {
            selectionCollider = render.GetComponent<Collider>();
            if (selectionCollider != null) selectionCollider.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (render == null) return;
        if (viewer == null && TurnManager.Instance != null)
            viewer = TurnManager.Instance.players.Find(player => player != null && !player.isAI);

        // Gate the whole artwork by its tile, rather than clipping it against fog geometry.
        // Dying units keep their artwork until the attack animation removes them.
        render.enabled = currentTile != null && viewer != null && viewer.visibleTiles != null &&
            viewer.visibleTiles.IsVisible(currentTile);
        bool selectable = render.enabled && isAlive && currentTile.currentUnit == this;
        if (selectionCollider != null && selectionCollider.enabled != selectable)
            selectionCollider.enabled = selectable;
    }

    private Tween ScaleAttackVisual(float multiplier, float seconds, Ease ease) =>
        (render != null ? render.transform : transform)
            .DOScale(originalVisualScale * multiplier, seconds).SetEase(ease);

    private void ResetAttackScale()
    {
        transform.localScale = originalScale;
        if (render != null) render.transform.localScale = originalVisualScale;
    }

    private void OnDisable()
    {
        AttackAnimation?.Kill();
        AttackAnimation = null;
        RemoveDefeatedVisual();
        RemoveArrowProjectile();
        selectionAnimation?.Kill();
        selectionAnimation = null;
        ResetAttackScale();
        if (isAlive && currentTile != null) transform.position = currentTile.transform.position;
    }

    private void RemoveDefeatedVisual()
    {
        if (defeatedUnit != null) Destroy(defeatedUnit.gameObject);
        defeatedUnit = null;
    }

    private void RemoveArrowProjectile()
    {
        if (arrowProjectile != null) Destroy(arrowProjectile);
        arrowProjectile = null;
    }

    private Sprite ProjectileSprite(UnitAnimationSettings settings) => data.projectileSprite != null
        ? data.projectileSprite
        : data.CounterType.projectileSprite != null ? data.CounterType.projectileSprite
        : settings.archerType != null && data.CounterType == settings.archerType ? settings.arrowSprite : null;

    // Build both strikes before playing; only the outer exchange owns the action lock.
    private Tween CreateStrike(Unit source, Unit target, bool advance, System.Action onImpact)
    {
        var settings = TurnManager.Instance.unitAnimationSettings;
        Sprite artwork = source.ProjectileSprite(settings);
        Camera camera = Camera.main;
        if (artwork != null && camera != null)
        {
            bool cannon = settings.cannonType != null && source.data.CounterType == settings.cannonType;
            float size = cannon ? settings.cannonSize : settings.arrowSize;
            float arcHeight = cannon ? settings.cannonArcHeight : settings.arrowArcHeight;
            float speed = cannon ? settings.cannonSpeed : settings.arrowSpeed;
            float minSeconds = cannon ? settings.cannonMinSeconds : settings.arrowMinSeconds;
            float maxSeconds = cannon ? settings.cannonMaxSeconds : settings.arrowMaxSeconds;
            Vector3 origin = source.VisualBounds.center;
            Vector3 destination = target.VisualBounds.center;
            Vector3 travel = destination - origin;
            Quaternion facing = camera.transform.rotation;
            Transform arrow = null;
            float duration = Mathf.Clamp(travel.magnitude / Mathf.Max(0.01f, speed),
                minSeconds, Mathf.Max(minSeconds, maxSeconds));
            return DOTween.Sequence()
                .AppendCallback(() =>
                {
                    arrowProjectile = new GameObject("Attack projectile");
                    var sprite = arrowProjectile.AddComponent<SpriteRenderer>();
                    sprite.sprite = artwork;
                    arrow = arrowProjectile.transform;
                    arrow.position = origin;
                    arrow.localScale = Vector3.one * (size / artwork.bounds.size.x);
                })
                .Append(DOVirtual.Float(0f, 1f, duration, progress =>
                {
                    if (arrow == null) return;
                    arrow.position = Vector3.Lerp(origin, destination, progress) +
                        Vector3.up * (4f * arcHeight * progress * (1f - progress));
                    Vector3 tangent = travel + Vector3.up * (4f * arcHeight * (1f - 2f * progress));
                    Vector3 localDirection = Quaternion.Inverse(facing) * tangent;
                    float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg + 180f;
                    arrow.rotation = facing * Quaternion.Euler(0f, 0f, angle);
                }).SetEase(Ease.Linear))
                .AppendCallback(() =>
                {
                    RemoveArrowProjectile();
                    onImpact();
                });
        }
        Sequence strike = DOTween.Sequence();
        if (source.data.attackRange != 1)
            return strike.AppendInterval(settings.lungeOutSeconds).AppendCallback(() => onImpact());

        Vector3 start = source.transform.position;
        Vector3 end = target.transform.position;
        strike.AppendCallback(() =>
        {
            source.selectionAnimation?.Kill();
            source.transform.localScale = source.originalScale;
        });
        if (advance)
        {
            Vector3 direction = end - start;
            bool impacted = false;
            void Impact()
            {
                if (impacted) return;
                impacted = true;
                Vector3 position = source.transform.position;
                onImpact();
                source.transform.position = position;
            }
            strike.Append(source.transform.DOMove(end + direction * settings.advanceOvershootFraction,
                settings.advanceSeconds).SetEase(settings.advanceEase).OnUpdate(() =>
                {
                    if (Vector3.Dot(source.transform.position - end, direction) >= 0f) Impact();
                }));
            strike.Join(source.ScaleAttackVisual(settings.advanceScale,
                settings.advanceSeconds, settings.advanceEase));
            strike.AppendCallback(Impact);
            float settleStart = strike.Duration();
            if (settings.advanceOvershootFraction > 0f)
                strike.Append(source.transform.DOMove(end, settings.advanceSnapBackSeconds).SetEase(settings.advanceSnapBackEase));
            strike.Insert(settleStart, source.ScaleAttackVisual(1f,
                settings.advanceSnapBackSeconds, settings.advanceSnapBackEase));
        }
        else
        {
            strike.Append(source.transform.DOMove(Vector3.Lerp(start, end, settings.lungeFraction),
                settings.lungeOutSeconds).SetEase(settings.lungeOutEase));
            strike.Join(source.ScaleAttackVisual(settings.lungeScale,
                settings.lungeOutSeconds, settings.lungeOutEase));
            strike.AppendCallback(() => onImpact());
            strike.Append(source.transform.DOMove(start, settings.lungeBackSeconds).SetEase(settings.lungeBackEase));
            strike.Join(source.ScaleAttackVisual(1f,
                settings.lungeBackSeconds, settings.lungeBackEase));
        }
        return strike;
    }
    public void PulseSelection()
    {
        var settings = TurnManager.Instance.unitAnimationSettings;
        if (!Application.isPlaying || settings == null) return;
        selectionAnimation?.Kill();
        transform.localScale = originalScale;
        selectionAnimation = DOTween.Sequence()
            .Append(transform.DOScale(originalScale * settings.selectionScale, settings.selectionGrowSeconds)
                .SetEase(settings.selectionGrowEase))
            .Append(transform.DOScale(originalScale, settings.selectionShrinkSeconds)
                .SetEase(settings.selectionShrinkEase));
    }

    private void Start()
    {
        currentHealth = data.maxHealth;
        healthUI.Set(currentHealth);
        isAlive = true;
    }

    public bool CanMoveTo(Tile targetTile)
    {
        if (!CanAct || hasMoved || hasAttacked || targetTile == null || targetTile == currentTile ||
            targetTile.currentUnit != null || targetTile.terrainType == TerrainType.Mountain) return false;
        GridManager.Instance.GetReachableMoveTiles(currentTile, owner, BoardState.Live.GetMoveRange(this),
            GetLiveOccupant, moveTiles);
        bool reachable = moveTiles.Contains(targetTile);
        moveTiles.Clear();
        return reachable;
    }

    public bool MoveTo(Tile targetTile)
    {
        if (!CanMoveTo(targetTile)) return false;
        ApplyMove(targetTile);
        return true;
    }

    // Also used for melee advancement, which is part of the attack rather than a second move.
    private void ApplyMove(Tile targetTile)
    {
        if (currentTile.city != null && currentTile.city.pendingCapturer == this)
            currentTile.city.ClearPendingCapture();
        currentTile.currentUnit = null;

        currentTile = targetTile;
        targetTile.currentUnit = this;
        transform.position = targetTile.transform.position;

        hasMoved = true;

        FogOfWarManager.Instance.Reveal(owner, targetTile, 1); // unit sight range

        if (targetTile.city != null && InteractionRules.CanCapture(owner, targetTile.city.owner))
        {
            targetTile.city.SetPendingCapture(this);
        }

        if (IsStatic)
        {
            isActive = false;
        }
    }

    public bool CanAttack(Unit defender, Tile from = null)
    {
        from = from ?? currentTile;
        return CanAct && !hasAttacked && defender != null && defender.isAlive &&
            defender.currentTile != null && defender.currentTile.currentUnit == defender &&
            InteractionRules.CanAttack(owner, defender.owner) &&
            Utils.IsWithinDistance(from.gridPosition, defender.currentTile.gridPosition, data.attackRange) &&
            (from == currentTile || (!IsStatic && CanMoveTo(from)));
    }

    public bool Attack(Unit defender, Tile from = null)
    {
        if (!CanAttack(defender, from)) return false;
        if (from != null && from != currentTile) ApplyMove(from);

        Tile targetTile = defender.currentTile;
        var (attackDamage, retaliationDamage, advance) = CombatMath.PredictAttack(this, defender, BoardState.Live);
        hasAttacked = true;
        hasMoved = true;
        void InitialImpact()
        {
            if (defender == null || !defender.isAlive) return;
            ApplyAttackDamage(defender, attackDamage);
            if (advance && isAlive) ApplyMove(targetTile);
        }
        void RetaliationImpact()
        {
            if (defender == null || !defender.isAlive || !isAlive) return;
            // Keep the exchange owner alive until the defender finishes returning home.
            bool animated = Application.isPlaying && TurnManager.Instance.unitAnimationSettings != null;
            TakeDamage(retaliationDamage, defender.owner, animated);
            if (animated && !isAlive)
            {
                defeatedUnit = this;
                if (render != null) render.enabled = false;
            }
        }
        var settings = TurnManager.Instance.unitAnimationSettings;
        if (!Application.isPlaying || settings == null)
        {
            InitialImpact();
            if (retaliationDamage > 0) RetaliationImpact();
            return true;
        }

        Sequence exchange = DOTween.Sequence();
        exchange.Append(CreateStrike(this, defender, advance, InitialImpact));
        if (retaliationDamage > 0)
        {
            exchange.AppendInterval(settings.retaliationPauseSeconds);
            exchange.Append(CreateStrike(defender, this, false, RetaliationImpact));
        }
        exchange.OnComplete(RemoveDefeatedVisual);
        exchange.OnKill(() =>
        {
            if (this != null) ResetAttackScale();
            if (defender != null) defender.ResetAttackScale();
        });
        AttackAnimation = exchange;
        TurnManager.Instance.TrackActionAnimation(exchange);
        return true;
    }

    private void ApplyAttackDamage(Unit defender, int attackDamage)
    {
        CombatMath.CollectSplashTargets(this, defender, defender.currentTile, BoardState.Live, splashTargets);
        defender.TakeDamage(attackDamage, owner);
        foreach (Unit target in splashTargets) target.TakeDamage(data.splashDamage, owner);
        splashTargets.Clear();
    }

    public bool TryReplaceGarrison(City city, FactionUnit recruit, Tile destination, Unit target = null)
    {
        if (city == null || city.owner != owner || currentTile != city.centerTile ||
            recruit == null || recruit.unitData == null || destination == null ||
            !city.CanSpawnUnit(recruit, recruit.unitData.cost, this)) return false;

        if (target != null)
        {
            if (!CanAttack(target, destination)) return false;
            // Recheck the exact combat outcome before committing an irreversible attack.
            BoardState board = BoardState.Live;
            int checkpoint = board.Checkpoint();
            bool vacates;
            try
            {
                ActionSimulator.Apply(board, new CandidateAction { unit = this, kind = ActionKind.Attack,
                    moveTile = destination, target = target });
                vacates = board.GetOccupant(city.centerTile) == null && board.IsAlive(this);
            }
            finally { board.Rollback(checkpoint); }
            if (!vacates || !Attack(target, destination)) return false;
            if (AttackAnimation != null && AttackAnimation.IsActive())
            {
                AttackAnimation.onComplete += () => city.SpawnUnit(recruit, recruit.unitData.cost);
                return true;
            }
            return city.SpawnUnit(recruit, recruit.unitData.cost);
        }
        if (!CanMoveTo(destination)) return false;

        ApplyMove(destination);
        if (city.SpawnUnit(recruit, recruit.unitData.cost)) return true;

        // Restore occupancy and action state if recruitment fails after vacating.
        ApplyMove(city.centerTile);
        hasMoved = false;
        isActive = true;
        return false;
    }

    public void TakeDamage(int damage, Player attacker = null, bool keepVisualUntilImpact = false)
    {
        currentHealth -= damage;
        healthUI?.Set(currentHealth);

        if (currentHealth <= 0)
        {
            Die(attacker, keepVisualUntilImpact);
        }
    }

    public bool CanSeverNeuron(Building segment, Tile from = null)
    {
        from = from ?? currentTile;
        return CanAct && BoardState.Live.CanSeverNeuron(this, from, segment) &&
            (from == currentTile || CanMoveTo(from));
    }

    public bool TrySeverNeuron(Building segment, Tile from = null)
    {
        if (!CanSeverNeuron(segment, from)) return false;
        if (from != null && from != currentTile) ApplyMove(from);
        hasAttacked = true;
        // Match the existing attack rule: attacking also consumes movement.
        hasMoved = true;
        TurnManager.Instance.Diplomacy.DeclareWar(owner, segment.owner);
        segment.RemoveNeuron(owner);
        if (dopamineBonus > 0) owner.AddStars(dopamineBonus);
        Deactivate();
        return true;
    }

    public void Heal()
    {
        if (hasAttacked || hasMoved || hasCaptured) return;

        City city = currentTile.territoryCity ?? currentTile.city;
        int healthRecoup = 2;
        if (city != null && TurnManager.Instance.Bonds.Friendly(owner, city.owner))
            healthRecoup += city.PerkAmount(CityPerkKind.Healing);

        currentHealth = Mathf.Min(data.maxHealth, currentHealth + healthRecoup);
        healthUI?.Set(currentHealth);
    }

    public (int damage, int retaliation) PredictAttackDamage(Unit defender)
    {
        var (damage, retaliation, _) = CombatMath.PredictAttack(this, defender, BoardState.Live);
        return (damage, retaliation);
    }

    private void Die(Player attacker, bool keepVisualUntilImpact)
    {
        isAlive = false;
        if (selectionCollider != null) selectionCollider.enabled = false;
        if (currentTile != null)
        {
            if (currentTile.city && currentTile.city.pendingCapturer != null)
            {
                currentTile.city.ClearPendingCapture();
            }

            currentTile.currentUnit = null;
        }

        if (owner != null) owner.units.Remove(this);
        if (homeCity != null) homeCity.units.Remove(this);
        TurnManager.Instance?.RequestGameOverCheck();
        if (attacker != null) UnitKilled?.Invoke(attacker, owner);
        if (keepVisualUntilImpact)
        {
            selectionAnimation?.Kill();
            transform.localScale = originalScale;
            if (healthUI != null) healthUI.gameObject.SetActive(false);
        }
        else if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    public void ResetTurn()
    {
        hasMoved = false;
        hasAttacked = false;
        hasCaptured = false;
        Activate();
    }

    public void Activate()
    {
        SetBrightness(1f);
        isActive = true;
    }

    public void Deactivate()
    {
        if (!owner.isAI) SetBrightness(0.65f);
        isActive = false;
    }

    private void SetBrightness(float brightness)
    {
        if (render == null || render.sharedMaterial == null) return;
        if (visualProperties == null)
        {
            colorProperty = Shader.PropertyToID(render.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
            activeColor = render.sharedMaterial.GetColor(colorProperty);
            visualProperties = new MaterialPropertyBlock();
        }
        Color color = activeColor;
        color.r *= brightness;
        color.g *= brightness;
        color.b *= brightness;
        render.GetPropertyBlock(visualProperties);
        visualProperties.SetColor(colorProperty, color);
        render.SetPropertyBlock(visualProperties);
    }
}
