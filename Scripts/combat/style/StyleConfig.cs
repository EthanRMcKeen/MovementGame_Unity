using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Style Config", fileName = "StyleConfig")]
public sealed class StyleConfig : ScriptableObject
{
    [Serializable]
    public struct StyleRankThreshold
    {
        public StyleRank Rank;
        [Min(0f)] public float StyleScore;
        [Min(0f)] public float ComboGate;
    }

    [Header("Caps")]
    [SerializeField] [Min(1f)] private float _maxCombo = 100f;
    [SerializeField] [Min(1f)] private float _maxStyleScore = 1000f;

    [Header("Timing")]
    [SerializeField] [Min(0f)] private float _comboDecayDelaySeconds = 1.5f;
    [SerializeField] [Min(0f)] private float _styleDecayDelaySeconds = 2f;
    [SerializeField] [Min(0f)] private float _styleHardDecayDelaySeconds = 4f;
    [SerializeField] [Min(0f)] private float _consecutiveHitWindowSeconds = 1.2f;
    [SerializeField] [Min(0f)] private float _chainWindowSeconds = 1.5f;
    [SerializeField] [Min(0f)] private float _swapWindowSeconds = 1.25f;
    [SerializeField] [Min(0f)] private float _killStreakWindowSeconds = 2.5f;

    [Header("Decay")]
    [SerializeField] [Min(0f)] private float _comboDecayBasePerSecond = 8f;
    [SerializeField] [Min(0f)] private float _comboDecayPercentPerSecond = 0.06f;
    [SerializeField] [Min(0f)] private float _styleDecayBasePerSecond = 12f;
    [SerializeField] [Min(0f)] private float _styleDecayPerRankPerSecond = 4f;
    [SerializeField] [Min(0f)] private float _styleHardDecayPerSecond = 20f;

    [Header("Penalties")]
    [SerializeField] [Min(0f)] private float _damagePenaltyComboFlat = 12f;
    [SerializeField] [Min(0f)] private float _damagePenaltyComboPercent = 0.08f;
    [SerializeField] [Min(0f)] private float _damagePenaltyStyleFlat = 18f;
    [SerializeField] [Min(0f)] private float _damagePenaltyStylePercent = 0.07f;
    [SerializeField] [Min(0f)] private float _heavyHitPenaltyComboFlat = 8f;
    [SerializeField] [Min(0f)] private float _heavyHitPenaltyComboPercent = 0.04f;
    [SerializeField] [Min(0f)] private float _heavyHitPenaltyStyleFlat = 17f;
    [SerializeField] [Min(0f)] private float _heavyHitPenaltyStylePercent = 0.03f;
    [SerializeField] [Range(0f, 1f)] private float _heavyHitHealthRatio = 0.2f;

    [Header("Repetition")]
    [SerializeField] [Min(1)] private int _repetitionLookbackEntries = 4;
    [SerializeField] [Min(0f)] private float _repetitionLookbackSeconds = 5f;
    [SerializeField] private float[] _comboRepeatMultipliers = { 1f, 0.9f, 0.75f, 0.6f, 0.45f };
    [SerializeField] private float[] _styleRepeatMultipliers = { 1f, 0.8f, 0.6f, 0.4f, 0.25f };

    [Header("Variety")]
    [SerializeField] [Min(0f)] private float _varietyWindowSeconds = 5f;
    [SerializeField] [Min(1)] private int _maxVarietyFamilies = 5;
    [SerializeField] [Min(0f)] private float _varietyBonusPerExtraFamily = 0.06f;
    [SerializeField] [Min(0f)] private float _maxMomentumStyleBonus = 0.25f;

    [Header("Rank")]
    [SerializeField] [Min(0f)] private float _rankDropGraceSeconds = 0.75f;
    [SerializeField] [Min(0f)] private float _rankDownStyleBuffer = 25f;
    [SerializeField] [Min(0f)] private float _rankDownComboBuffer = 8f;
    [SerializeField] private StyleRankThreshold[] _rankThresholds;

    [Header("Combat Rewards")]
    [SerializeField] [Range(0f, 1f)] private float _maxDamageBonus = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float _maxCooldownReduction = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float _maxMoveSpeedBonus = 0.12f;

    public float MaxCombo => Mathf.Max(1f, _maxCombo);
    public float MaxStyleScore => Mathf.Max(1f, _maxStyleScore);
    public float ComboDecayDelaySeconds => Mathf.Max(0f, _comboDecayDelaySeconds);
    public float StyleDecayDelaySeconds => Mathf.Max(0f, _styleDecayDelaySeconds);
    public float StyleHardDecayDelaySeconds => Mathf.Max(0f, _styleHardDecayDelaySeconds);
    public float ConsecutiveHitWindowSeconds => Mathf.Max(0f, _consecutiveHitWindowSeconds);
    public float ChainWindowSeconds => Mathf.Max(0f, _chainWindowSeconds);
    public float SwapWindowSeconds => Mathf.Max(0f, _swapWindowSeconds);
    public float KillStreakWindowSeconds => Mathf.Max(0f, _killStreakWindowSeconds);
    public float ComboDecayBasePerSecond => Mathf.Max(0f, _comboDecayBasePerSecond);
    public float ComboDecayPercentPerSecond => Mathf.Max(0f, _comboDecayPercentPerSecond);
    public float StyleDecayBasePerSecond => Mathf.Max(0f, _styleDecayBasePerSecond);
    public float StyleDecayPerRankPerSecond => Mathf.Max(0f, _styleDecayPerRankPerSecond);
    public float StyleHardDecayPerSecond => Mathf.Max(0f, _styleHardDecayPerSecond);
    public float DamagePenaltyComboFlat => Mathf.Max(0f, _damagePenaltyComboFlat);
    public float DamagePenaltyComboPercent => Mathf.Max(0f, _damagePenaltyComboPercent);
    public float DamagePenaltyStyleFlat => Mathf.Max(0f, _damagePenaltyStyleFlat);
    public float DamagePenaltyStylePercent => Mathf.Max(0f, _damagePenaltyStylePercent);
    public float HeavyHitPenaltyComboFlat => Mathf.Max(0f, _heavyHitPenaltyComboFlat);
    public float HeavyHitPenaltyComboPercent => Mathf.Max(0f, _heavyHitPenaltyComboPercent);
    public float HeavyHitPenaltyStyleFlat => Mathf.Max(0f, _heavyHitPenaltyStyleFlat);
    public float HeavyHitPenaltyStylePercent => Mathf.Max(0f, _heavyHitPenaltyStylePercent);
    public float HeavyHitHealthRatio => Mathf.Clamp01(_heavyHitHealthRatio);
    public int RepetitionLookbackEntries => Mathf.Max(1, _repetitionLookbackEntries);
    public float RepetitionLookbackSeconds => Mathf.Max(0f, _repetitionLookbackSeconds);
    public float VarietyWindowSeconds => Mathf.Max(0f, _varietyWindowSeconds);
    public int MaxVarietyFamilies => Mathf.Max(1, _maxVarietyFamilies);
    public float VarietyBonusPerExtraFamily => Mathf.Max(0f, _varietyBonusPerExtraFamily);
    public float MaxMomentumStyleBonus => Mathf.Max(0f, _maxMomentumStyleBonus);
    public float RankDropGraceSeconds => Mathf.Max(0f, _rankDropGraceSeconds);
    public float RankDownStyleBuffer => Mathf.Max(0f, _rankDownStyleBuffer);
    public float RankDownComboBuffer => Mathf.Max(0f, _rankDownComboBuffer);
    public float MaxDamageBonus => Mathf.Clamp01(_maxDamageBonus);
    public float MaxCooldownReduction => Mathf.Clamp01(_maxCooldownReduction);
    public float MaxMoveSpeedBonus => Mathf.Clamp01(_maxMoveSpeedBonus);

    private void OnEnable()
    {
        EnsureThresholds();
    }

    public static StyleConfig CreateRuntimeDefault()
    {
        StyleConfig config = CreateInstance<StyleConfig>();
        config.hideFlags = HideFlags.HideAndDontSave;
        config.EnsureThresholds();
        return config;
    }

    public float GetComboRepeatMultiplier(int repetitionCount)
    {
        return GetMultiplier(_comboRepeatMultipliers, repetitionCount);
    }

    public float GetStyleRepeatMultiplier(int repetitionCount)
    {
        return GetMultiplier(_styleRepeatMultipliers, repetitionCount);
    }

    public float GetStyleDecayPerSecond(StyleRank rank)
    {
        return StyleDecayBasePerSecond + ((int)rank * StyleDecayPerRankPerSecond);
    }

    public bool TryGetThreshold(StyleRank rank, out StyleRankThreshold threshold)
    {
        EnsureThresholds();
        for (int i = 0; i < _rankThresholds.Length; i++)
        {
            if (_rankThresholds[i].Rank != rank)
                continue;

            threshold = _rankThresholds[i];
            return true;
        }

        threshold = default(StyleRankThreshold);
        return false;
    }

    public StyleRank EvaluateRank(float styleScore, float combo)
    {
        EnsureThresholds();

        StyleRank best = StyleRank.D;
        for (int i = 0; i < _rankThresholds.Length; i++)
        {
            StyleRankThreshold threshold = _rankThresholds[i];
            if (styleScore < threshold.StyleScore || combo < threshold.ComboGate)
                continue;

            if (threshold.Rank > best)
                best = threshold.Rank;
        }

        return best;
    }

    private void EnsureThresholds()
    {
        if (_rankThresholds != null && _rankThresholds.Length > 0)
            return;

        _rankThresholds = new[]
        {
            new StyleRankThreshold { Rank = StyleRank.D, StyleScore = 0f, ComboGate = 0f },
            new StyleRankThreshold { Rank = StyleRank.C, StyleScore = 60f, ComboGate = 20f },
            new StyleRankThreshold { Rank = StyleRank.B, StyleScore = 140f, ComboGate = 35f },
            new StyleRankThreshold { Rank = StyleRank.A, StyleScore = 260f, ComboGate = 55f },
            new StyleRankThreshold { Rank = StyleRank.S, StyleScore = 420f, ComboGate = 75f },
            new StyleRankThreshold { Rank = StyleRank.SS, StyleScore = 650f, ComboGate = 90f }
        };
    }

    private static float GetMultiplier(float[] values, int index)
    {
        if (values == null || values.Length == 0)
            return 1f;

        int safeIndex = Mathf.Clamp(index, 0, values.Length - 1);
        return Mathf.Max(0f, values[safeIndex]);
    }
}
