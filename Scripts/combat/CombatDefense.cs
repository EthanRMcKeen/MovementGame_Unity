using System.Collections;
using UnityEngine;

public enum CombatDefenseOutcome
{
    None,
    Ignored,
    Blocked,
    Parried
}

[DisallowMultipleComponent]
public class CombatDefense : MonoBehaviour
{
    [Header("Rules")]
    [SerializeField] private bool _allowBlocking = true;
    [SerializeField] private bool _allowParrying = true;
    [SerializeField] private bool _startsBlocking;
    [SerializeField] private bool _startsParrying;
    [SerializeField] private bool _startsInvulnerable;
    [SerializeField] [Range(0f, 1f)] private float _defaultBlockDamageMultiplier = 0.5f;

    private Coroutine _parryRoutine;
    private IDamageable _selfDamageable;

    public bool IsBlocking { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsInvulnerable { get; private set; }

    private void Awake()
    {
        DamageResolver.TryGetDamageable(this, out _selfDamageable);
        IsBlocking = _allowBlocking && _startsBlocking;
        IsParrying = _allowParrying && _startsParrying;
        IsInvulnerable = _startsInvulnerable;
    }

    public void SetBlocking(bool isBlocking)
    {
        IsBlocking = _allowBlocking && isBlocking;
    }

    public void SetParrying(bool isParrying)
    {
        IsParrying = _allowParrying && isParrying;
    }

    public void SetInvulnerable(bool isInvulnerable)
    {
        IsInvulnerable = isInvulnerable;
    }

    public void OpenParryWindow(float durationSeconds)
    {
        if (!_allowParrying)
            return;

        if (_parryRoutine != null)
            StopCoroutine(_parryRoutine);

        _parryRoutine = StartCoroutine(ParryWindowRoutine(Mathf.Max(0f, durationSeconds)));
    }

    public CombatDefenseOutcome ResolveIncomingDamage(ref DamageRequest damage)
    {
        if (IsInvulnerable)
            return CombatDefenseOutcome.Ignored;

        if (IsParrying && damage.CanBeParried)
        {
            NotifyResponder(damage, true);
            return CombatDefenseOutcome.Parried;
        }

        if (IsBlocking && damage.CanBeBlocked)
        {
            float multiplier = damage.BlockDamageMultiplier > 0f
                ? damage.BlockDamageMultiplier
                : _defaultBlockDamageMultiplier;
            damage.Amount = Mathf.Max(0, Mathf.RoundToInt(damage.Amount * Mathf.Clamp01(multiplier)));
            NotifyResponder(damage, false);
            return CombatDefenseOutcome.Blocked;
        }

        return CombatDefenseOutcome.None;
    }

    private IEnumerator ParryWindowRoutine(float durationSeconds)
    {
        IsParrying = true;

        if (durationSeconds > 0f)
            yield return new WaitForSeconds(durationSeconds);

        IsParrying = false;
        _parryRoutine = null;
    }

    private void NotifyResponder(DamageRequest damage, bool parried)
    {
        if (damage.Source == null)
            return;

        MonoBehaviour[] behaviours = damage.Source.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is ICombatHitResponder responder))
                continue;

            if (parried)
                responder.OnAttackParried(damage, _selfDamageable);
            else
                responder.OnAttackBlocked(damage, _selfDamageable);

            return;
        }
    }
}
