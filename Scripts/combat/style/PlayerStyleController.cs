using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CombatController))]
[RequireComponent(typeof(DamageHandler))]
public sealed class PlayerStyleController : NetworkBehaviour
{
    private const int RecentActionCapacity = 6;
    private const float StaleActionLifetimeSeconds = 5f;

    [Header("Config")]
    [SerializeField] private StyleConfig _config;
    [SerializeField] private bool _debugLogs;

    public readonly SyncVar<float> ComboSync = new SyncVar<float>();
    public readonly SyncVar<float> StyleScoreSync = new SyncVar<float>();
    public readonly SyncVar<StyleRank> RankSync = new SyncVar<StyleRank>();
    public readonly SyncVar<StyleRank> HighestRankSync = new SyncVar<StyleRank>();

    private sealed class ActiveActionState
    {
        public int ActionInstanceId;
        public string FamilyId;
        public StyleTag Tags;
        public WeaponActionSlot Slot;
        public float StartedAt;
        public float CloseRangeMeters;
        public int MaxAcceptedEventsPerSecond;
        public float EventWindowStartedAt;
        public int AcceptedEventsInWindow;
    }

    private struct RecentActionEntry
    {
        public string FamilyId;
        public float Timestamp;
    }

    private readonly Dictionary<int, ActiveActionState> _activeActions = new Dictionary<int, ActiveActionState>();
    private readonly RecentActionEntry[] _recentActions = new RecentActionEntry[RecentActionCapacity];

    private CombatController _combatController;
    private DamageHandler _damageHandler;
    private StyleConfig _runtimeConfig;
    private int _recentActionCount;
    private int _nextActionInstanceId = 1;
    private float _lastComboEventAt = float.NegativeInfinity;
    private float _lastStyleEventAt = float.NegativeInfinity;
    private float _lastCommittedActionAt = float.NegativeInfinity;
    private WeaponActionSlot _lastCommittedSlot = WeaponActionSlot.PrimaryFire;
    private float _lastWeaponSwapAt = float.NegativeInfinity;
    private float _rankDropGraceEndsAt = float.NegativeInfinity;
    private float _killStreakEndsAt = float.NegativeInfinity;
    private int _killStreakCount;

    public float Combo { get { return ComboSync.Value; } }
    public float StyleScore { get { return StyleScoreSync.Value; } }
    public StyleRank Rank { get { return RankSync.Value; } }
    public float ComboNormalized
    {
        get
        {
            StyleConfig config = ResolveConfig();
            return config != null ? Mathf.Clamp01(ComboSync.Value / config.MaxCombo) : 0f;
        }
    }
    public float HeavyHitHealthRatio { get { return ResolveConfig().HeavyHitHealthRatio; } }

    private void Awake()
    {
        _combatController = GetComponent<CombatController>();
        _damageHandler = GetComponent<DamageHandler>();
        ResolveConfig();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ComboSync.Value = 0f;
        StyleScoreSync.Value = 0f;
        RankSync.Value = StyleRank.D;
        HighestRankSync.Value = StyleRank.D;
    }

    private void Update()
    {
        if (!IsServerInitialized)
            return;

        TickDecay(Time.deltaTime);
        PruneStaleActions(Time.time);
    }

    public float GetDamageMultiplier()
    {
        return 1f + (ResolveConfig().MaxDamageBonus * ComboNormalized);
    }

    public float GetCooldownMultiplier()
    {
        return 1f - (ResolveConfig().MaxCooldownReduction * ComboNormalized);
    }

    public float GetMoveSpeedMultiplier()
    {
        return 1f + (ResolveConfig().MaxMoveSpeedBonus * ComboNormalized);
    }

    public int ApplyOutgoingDamageBonus(int amount)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, amount) * GetDamageMultiplier()));
    }

    public bool IsEmpoweredStateActive()
    {
        return RankSync.Value >= StyleRank.S;
    }

    public bool CanOpenFinishers()
    {
        return RankSync.Value >= StyleRank.A;
    }

    public void ServerNotifyWeaponSwapped()
    {
        if (!IsServerInitialized)
            return;

        _lastWeaponSwapAt = Time.time;
    }

    public int ServerPrepareAction(
        StyleActionProfile profile, //This is what is sent to "server" and will determin what action
        WeaponActionSlot slot,
        PlayerMovementState movementState,
        out string familyId,
        out StyleTag styleTags)
    {
        StyleActionProfile resolvedProfile = ResolveProfile(profile, slot);
        familyId = resolvedProfile.FamilyId;
        styleTags = resolvedProfile.DefaultTags | GetMovementTags(movementState);

        if (!IsServerInitialized)
            return 0;

        if (Time.time <= _lastWeaponSwapAt + ResolveConfig().SwapWindowSeconds)
            styleTags |= StyleTag.Swap;
        if (Time.time <= _lastCommittedActionAt + ResolveConfig().ChainWindowSeconds && slot != _lastCommittedSlot)
            styleTags |= StyleTag.Chain;

        int actionInstanceId = _nextActionInstanceId++;
        if (_nextActionInstanceId <= 0)
            _nextActionInstanceId = 1;

        _activeActions[actionInstanceId] = new ActiveActionState
        {
            ActionInstanceId = actionInstanceId,
            FamilyId = familyId,
            Tags = styleTags,
            Slot = slot,
            StartedAt = Time.time,
            CloseRangeMeters = resolvedProfile.CloseRangeMeters,
            MaxAcceptedEventsPerSecond = Mathf.Max(1, resolvedProfile.MaxAcceptedEventsPerSecond),
            EventWindowStartedAt = Time.time
        };

        return actionInstanceId;
    }

    public void ServerCommitPreparedAction(int actionInstanceId, WeaponActionSlot slot)
    {
        if (!IsServerInitialized)
            return;

        _lastCommittedActionAt = Time.time;
        _lastCommittedSlot = slot;

        ActiveActionState state;
        if (actionInstanceId == 0 || !_activeActions.TryGetValue(actionInstanceId, out state))
            return;

        state.StartedAt = Time.time;
    }

    public void ServerCancelPreparedAction(int actionInstanceId)
    {
        if (!IsServerInitialized || actionInstanceId == 0)
            return;

        _activeActions.Remove(actionInstanceId);
    }

    public void ServerNotifyResolvedHit(in DamageRequest damage, IDamageable victim, int finalDamage, bool killed)
    {
        if (!IsServerInitialized || finalDamage <= 0)
            return;

        ActiveActionState actionState = ResolveActionState(damage);
        if (!CanAcceptEvent(actionState))
            return;

        StyleTag tags = damage.StyleTags | StyleTag.Hit;
        if (killed)
            tags |= StyleTag.Kill;
        if (damage.IsWeakPoint)
            tags |= StyleTag.WeakPoint;
        if (damage.TargetsHitSoFar > 1)
            tags |= StyleTag.MultiTarget;

        float comboGain = 4f;
        float styleGain = 8f;

        if (Time.time <= _lastComboEventAt + ResolveConfig().ConsecutiveHitWindowSeconds)
        {
            comboGain += 2f;
            styleGain += 4f;
        }

        if (killed)
        {
            comboGain += 8f;
            styleGain += 16f;
            ApplyKillStreak(ref comboGain, ref styleGain);
        }

        if ((tags & (StyleTag.Aerial | StyleTag.Dash | StyleTag.Slide | StyleTag.WallRun | StyleTag.Slam)) != 0)
        {
            comboGain += 4f;
            styleGain += 8f;
        }

        if ((tags & StyleTag.Swap) != 0)
        {
            comboGain += 5f;
            styleGain += 10f;
        }

        if ((tags & StyleTag.Chain) != 0)
        {
            comboGain += 5f;
            styleGain += 10f;
        }

        if ((tags & StyleTag.WeakPoint) != 0)
        {
            comboGain += 3f;
            styleGain += 6f;
            if (killed)
            {
                comboGain += 5f;
                styleGain += 10f;
            }
        }

        int extraTargets = Mathf.Max(0, damage.TargetsHitSoFar - 1);
        if (extraTargets > 0)
        {
            comboGain += 2f * extraTargets;
            styleGain += 5f * extraTargets;
        }

        float closeRangeMeters = actionState != null ? actionState.CloseRangeMeters : 7f;
        if (IsCloseRange(damage, closeRangeMeters))
        {
            tags |= StyleTag.CloseRange;
            comboGain += 2f;
            styleGain += 4f;
        }

        if ((tags & StyleTag.Finisher) != 0)
        {
            comboGain += 12f;
            styleGain += 22f;
        }

        if ((tags & StyleTag.Risky) != 0)
        {
            comboGain += 4f;
            styleGain += 8f;
        }

        ApplyEventGains(actionState != null ? actionState.FamilyId : ResolveFamilyId(damage.ActionFamilyId, damage.ActionSlot), comboGain, styleGain);
    }

    public void ServerNotifyParry(in DamageRequest damage, IDamageable defender)
    {
        if (!IsServerInitialized)
            return;

        ActiveActionState actionState = ResolveActionState(damage);
        StyleTag tags = damage.StyleTags | StyleTag.Parry;
        float comboGain = 10f;
        float styleGain = 18f;

        if ((tags & (StyleTag.Aerial | StyleTag.Dash | StyleTag.Slide | StyleTag.WallRun | StyleTag.Slam)) != 0)
        {
            comboGain += 4f;
            styleGain += 8f;
        }

        if ((tags & StyleTag.Swap) != 0)
        {
            comboGain += 5f;
            styleGain += 10f;
        }

        if ((tags & StyleTag.Chain) != 0)
        {
            comboGain += 5f;
            styleGain += 10f;
        }

        ApplyEventGains(actionState != null ? actionState.FamilyId : ResolveFamilyId(damage.ActionFamilyId, damage.ActionSlot), comboGain, styleGain);
    }

    public void ServerNotifyOwnerDamaged(int amount, bool heavyHit, bool died)
    {
        if (!IsServerInitialized)
            return;

        if (died)
        {
            ResetAll();
            return;
        }

        float comboLoss = ResolveConfig().DamagePenaltyComboFlat + (ComboSync.Value * ResolveConfig().DamagePenaltyComboPercent);
        float styleLoss = ResolveConfig().DamagePenaltyStyleFlat + (StyleScoreSync.Value * ResolveConfig().DamagePenaltyStylePercent);
        if (heavyHit)
        {
            comboLoss += ResolveConfig().HeavyHitPenaltyComboFlat + (ComboSync.Value * ResolveConfig().HeavyHitPenaltyComboPercent);
            styleLoss += ResolveConfig().HeavyHitPenaltyStyleFlat + (StyleScoreSync.Value * ResolveConfig().HeavyHitPenaltyStylePercent);
        }

        ComboSync.Value = Mathf.Clamp(ComboSync.Value - comboLoss, 0f, ResolveConfig().MaxCombo);
        StyleScoreSync.Value = Mathf.Clamp(StyleScoreSync.Value - styleLoss, 0f, ResolveConfig().MaxStyleScore);
        RefreshRank();
    }

    private void ApplyEventGains(string familyId, float baseComboGain, float baseStyleGain)
    {
        int repetitionCount = CountRecentFamilyOccurrences(familyId, ResolveConfig().RepetitionLookbackSeconds, ResolveConfig().RepetitionLookbackEntries);
        float comboGain = baseComboGain * ResolveConfig().GetComboRepeatMultiplier(repetitionCount);
        float styleGain = baseStyleGain
            * ResolveConfig().GetStyleRepeatMultiplier(repetitionCount)
            * GetVarietyMultiplier(familyId)
            * (1f + (ResolveConfig().MaxMomentumStyleBonus * ComboNormalized));

        if (comboGain > 0f)
        {
            ComboSync.Value = Mathf.Clamp(ComboSync.Value + comboGain, 0f, ResolveConfig().MaxCombo);
            _lastComboEventAt = Time.time;
        }

        if (styleGain > 0f)
        {
            StyleScoreSync.Value = Mathf.Clamp(StyleScoreSync.Value + styleGain, 0f, ResolveConfig().MaxStyleScore);
            _lastStyleEventAt = Time.time;
            PushRecentFamily(familyId);
        }

        RefreshRank();

        if (_debugLogs)
            Debug.Log(string.Format("{0} style gain '{1}' => combo {2:0.0}, style {3:0.0}, rank {4}", name, familyId, comboGain, styleGain, RankSync.Value));
    }

    private void ApplyKillStreak(ref float comboGain, ref float styleGain)
    {
        if (Time.time <= _killStreakEndsAt)
            _killStreakCount++;
        else
            _killStreakCount = 1;

        _killStreakEndsAt = Time.time + ResolveConfig().KillStreakWindowSeconds;

        int bonusStacks = Mathf.Clamp(_killStreakCount - 1, 0, 3);
        comboGain += 4f * bonusStacks;
        styleGain += 8f * bonusStacks;
    }

    private void TickDecay(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        bool changed = false;

        if (Time.time > _lastComboEventAt + ResolveConfig().ComboDecayDelaySeconds && ComboSync.Value > 0f)
        {
            float comboDecay = ResolveConfig().ComboDecayBasePerSecond + (ComboSync.Value * ResolveConfig().ComboDecayPercentPerSecond);
            ComboSync.Value = Mathf.Max(0f, ComboSync.Value - (comboDecay * deltaTime));
            changed = true;
        }

        if (Time.time > _lastStyleEventAt + ResolveConfig().StyleDecayDelaySeconds && StyleScoreSync.Value > 0f)
        {
            float styleDecay = ResolveConfig().GetStyleDecayPerSecond(RankSync.Value);
            if (Time.time > _lastStyleEventAt + ResolveConfig().StyleHardDecayDelaySeconds)
                styleDecay += ResolveConfig().StyleHardDecayPerSecond;

            StyleScoreSync.Value = Mathf.Max(0f, StyleScoreSync.Value - (styleDecay * deltaTime));
            changed = true;
        }

        if (changed)
            RefreshRank();
    }

    private void RefreshRank()
    {
        StyleRank current = RankSync.Value;
        StyleRank candidate = ResolveConfig().EvaluateRank(StyleScoreSync.Value, ComboSync.Value);

        if (candidate < current)
        {
            StyleConfig.StyleRankThreshold currentThreshold;
            if (Time.time < _rankDropGraceEndsAt)
            {
                candidate = current;
            }
            else if (ResolveConfig().TryGetThreshold(current, out currentThreshold) &&
                     StyleScoreSync.Value >= currentThreshold.StyleScore - ResolveConfig().RankDownStyleBuffer &&
                     ComboSync.Value >= currentThreshold.ComboGate - ResolveConfig().RankDownComboBuffer)
            {
                candidate = current;
            }
        }

        if (candidate > current)
            _rankDropGraceEndsAt = Time.time + ResolveConfig().RankDropGraceSeconds;

        RankSync.Value = candidate;
        if (candidate > HighestRankSync.Value)
            HighestRankSync.Value = candidate;
    }

    private void ResetAll()
    {
        ComboSync.Value = 0f;
        StyleScoreSync.Value = 0f;
        RankSync.Value = StyleRank.D;
        HighestRankSync.Value = StyleRank.D;
        _recentActionCount = 0;
        _activeActions.Clear();
        _lastComboEventAt = float.NegativeInfinity;
        _lastStyleEventAt = float.NegativeInfinity;
        _lastCommittedActionAt = float.NegativeInfinity;
        _lastWeaponSwapAt = float.NegativeInfinity;
        _rankDropGraceEndsAt = float.NegativeInfinity;
        _killStreakEndsAt = float.NegativeInfinity;
        _killStreakCount = 0;
    }

    private ActiveActionState ResolveActionState(in DamageRequest damage)
    {
        ActiveActionState state;
        if (damage.ActionInstanceId != 0 && _activeActions.TryGetValue(damage.ActionInstanceId, out state))
            return state;

        if (damage.ActionInstanceId == 0 && string.IsNullOrWhiteSpace(damage.ActionFamilyId))
            return null;

        return new ActiveActionState
        {
            ActionInstanceId = damage.ActionInstanceId,
            FamilyId = ResolveFamilyId(damage.ActionFamilyId, damage.ActionSlot),
            Tags = damage.StyleTags,
            Slot = damage.ActionSlot,
            StartedAt = Time.time,
            CloseRangeMeters = 7f,
            MaxAcceptedEventsPerSecond = 4,
            EventWindowStartedAt = Time.time
        };
    }

    private bool CanAcceptEvent(ActiveActionState state)
    {
        if (state == null)
            return true;

        if (Time.time > state.EventWindowStartedAt + 1f)
        {
            state.EventWindowStartedAt = Time.time;
            state.AcceptedEventsInWindow = 0;
        }

        if (state.AcceptedEventsInWindow >= Mathf.Max(1, state.MaxAcceptedEventsPerSecond))
            return false;

        state.AcceptedEventsInWindow++;
        if (state.ActionInstanceId != 0)
            _activeActions[state.ActionInstanceId] = state;

        return true;
    }

    private float GetVarietyMultiplier(string familyId)
    {
        int uniqueFamilies = GetUniqueRecentFamilyCount(ResolveConfig().VarietyWindowSeconds);
        if (!HasRecentFamily(familyId, ResolveConfig().VarietyWindowSeconds))
            uniqueFamilies++;

        int extraFamilies = Mathf.Clamp(uniqueFamilies - 1, 0, ResolveConfig().MaxVarietyFamilies - 1);
        return 1f + (ResolveConfig().VarietyBonusPerExtraFamily * extraFamilies);
    }

    private int CountRecentFamilyOccurrences(string familyId, float windowSeconds, int maxEntries)
    {
        if (string.IsNullOrWhiteSpace(familyId))
            return 0;

        int count = 0;
        int entriesToScan = Mathf.Min(_recentActionCount, Mathf.Max(1, maxEntries));
        for (int i = 0; i < entriesToScan; i++)
        {
            if (Time.time > _recentActions[i].Timestamp + windowSeconds)
                continue;
            if (!string.Equals(_recentActions[i].FamilyId, familyId))
                continue;

            count++;
        }

        return count;
    }

    private int GetUniqueRecentFamilyCount(float windowSeconds)
    {
        HashSet<string> uniqueFamilies = new HashSet<string>();
        for (int i = 0; i < _recentActionCount; i++)
        {
            if (Time.time > _recentActions[i].Timestamp + windowSeconds)
                continue;
            if (string.IsNullOrWhiteSpace(_recentActions[i].FamilyId))
                continue;

            uniqueFamilies.Add(_recentActions[i].FamilyId);
        }

        return uniqueFamilies.Count;
    }

    private bool HasRecentFamily(string familyId, float windowSeconds)
    {
        if (string.IsNullOrWhiteSpace(familyId))
            return false;

        for (int i = 0; i < _recentActionCount; i++)
        {
            if (Time.time > _recentActions[i].Timestamp + windowSeconds)
                continue;
            if (string.Equals(_recentActions[i].FamilyId, familyId))
                return true;
        }

        return false;
    }

    private void PushRecentFamily(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId))
            return;

        int length = Mathf.Min(_recentActionCount, _recentActions.Length - 1);
        for (int i = length; i > 0; i--)
            _recentActions[i] = _recentActions[i - 1];

        _recentActions[0] = new RecentActionEntry
        {
            FamilyId = familyId,
            Timestamp = Time.time
        };

        _recentActionCount = Mathf.Min(_recentActionCount + 1, _recentActions.Length);
    }

    private void PruneStaleActions(float now)
    {
        if (_activeActions.Count == 0)
            return;

        List<int> staleActionIds = null;
        foreach (KeyValuePair<int, ActiveActionState> pair in _activeActions)
        {
            if (now <= pair.Value.StartedAt + StaleActionLifetimeSeconds)
                continue;

            if (staleActionIds == null)
                staleActionIds = new List<int>();
            staleActionIds.Add(pair.Key);
        }

        if (staleActionIds == null)
            return;

        for (int i = 0; i < staleActionIds.Count; i++)
            _activeActions.Remove(staleActionIds[i]);
    }

    private bool IsCloseRange(in DamageRequest damage, float closeRangeMeters)
    {
        if (damage.Attacker == null)
            return false;

        float distance = Vector3.Distance(damage.Attacker.transform.position, damage.HitPoint);
        return distance <= Mathf.Max(0.1f, closeRangeMeters);
    }

    private StyleActionProfile ResolveProfile(StyleActionProfile profile, WeaponActionSlot slot)
    {
        return profile.Resolve(slot.ToString(), slot);
    }

    private string ResolveFamilyId(string familyId, WeaponActionSlot slot)
    {
        return string.IsNullOrWhiteSpace(familyId) ? slot.ToString() : familyId;
    }

    private static StyleTag GetMovementTags(PlayerMovementState movementState)
    {
        switch (movementState)
        {
            case PlayerMovementState.Air:
                return StyleTag.Aerial;
            case PlayerMovementState.Dashing:
                return StyleTag.Dash;
            case PlayerMovementState.Sliding:
                return StyleTag.Slide;
            case PlayerMovementState.WallRunning:
                return StyleTag.WallRun;
            case PlayerMovementState.Slamming:
            case PlayerMovementState.SuperSlamming:
                return StyleTag.Slam;
            default:
                return StyleTag.None;
        }
    }

    private StyleConfig ResolveConfig()
    {
        if (_runtimeConfig != null)
            return _runtimeConfig;

        _runtimeConfig = _config != null ? _config : StyleConfig.CreateRuntimeDefault();
        return _runtimeConfig;
    }
}
