using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MeleeHitboxAction : CombatActionBase
{
    [Header("Attack")]
    [SerializeField] private int _damage = 10;
    [SerializeField] private string _damageType = "Melee";
    [SerializeField] private float _startupSeconds = 0.15f;
    [SerializeField] private float _activeSeconds = 0.25f;
    [SerializeField] private bool _canBeBlocked = true;
    [SerializeField] private bool _canBeParried = true;
    [SerializeField] [Range(0f, 1f)] private float _blockedDamageMultiplier = 0.5f;

    [Header("Hitbox")]
    [SerializeField] private Collider _hitbox;
    [SerializeField] private LayerMask _hitMask = ~0;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackTrigger = "isAttacking";
    [SerializeField] private string _parriedTrigger = "isHit";
    [SerializeField] private GameObject _hitEffect;
    [SerializeField] private GameObject _parryEffect;

    private readonly Collider[] _hitResults = new Collider[16];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private Coroutine _attackRoutine;
    private Vector3 _lastDirection = Vector3.forward;

    public override void Initialize(CombatController controller, WeaponBase weapon, WeaponDefinition definition)
    {
        base.Initialize(controller, weapon, definition);
        if (_hitbox == null)
            _hitbox = GetComponentInChildren<Collider>(true);
        if (_animator == null)
            _animator = GetComponentInParent<Animator>();

        SetHitboxActive(false);
    }

    public override bool CanUse(WeaponUseContext context)
    {
        return _attackRoutine == null;
    }

    public override bool Execute(WeaponUseContext context)
    {
        if (_attackRoutine != null)
            return false;

        _lastDirection = context.Direction.sqrMagnitude > 0.0001f ? context.Direction.normalized : transform.forward;
        _attackRoutine = StartCoroutine(AttackRoutine(context));

        if (_animator != null && !string.IsNullOrWhiteSpace(_attackTrigger))
            _animator.SetTrigger(_attackTrigger);

        return true;
    }

    public override void OnUnequip()
    {
        CancelAttack();
    }

    public override void OnAttackBlocked(DamageRequest request, IDamageable defender)
    {
    }

    public override void OnAttackParried(DamageRequest request, IDamageable defender)
    {
        CancelAttack();

        if (_animator != null && !string.IsNullOrWhiteSpace(_parriedTrigger))
            _animator.SetTrigger(_parriedTrigger);

        if (_parryEffect == null || defender == null)
            return;

        Component defenderComponent = defender as Component;
        if (defenderComponent != null)
            Instantiate(_parryEffect, defenderComponent.transform.position, Quaternion.identity);
    }

    private IEnumerator AttackRoutine(WeaponUseContext context)
    {
        _hitTargets.Clear();
        SetHitboxActive(false);

        if (_startupSeconds > 0f)
            yield return new WaitForSeconds(_startupSeconds);

        SetHitboxActive(true);

        float remaining = _activeSeconds;
        while (remaining > 0f)
        {
            ScanHitbox(context);
            yield return new WaitForFixedUpdate();
            remaining -= Time.fixedDeltaTime;
        }

        SetHitboxActive(false);
        _hitTargets.Clear();
        _attackRoutine = null;
    }

    private void ScanHitbox(WeaponUseContext context)
    {
        if (_hitbox == null)
            return;

        int hitCount = QueryHitbox();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _hitResults[i];
            if (hit == null)
                continue;
            if (context.Controller != null && hit.transform.IsChildOf(context.Controller.transform))
                continue;
            if (!TryResolveDamageable(context, hit, out IDamageable damageable))
                continue;
            if (_hitTargets.Contains(damageable))
                continue;

            _hitTargets.Add(damageable);
            ApplyDamage(
                context,
                damageable,
                _damage,
                _damageType,
                hit.ClosestPoint(_hitbox.transform.position),
                -_lastDirection,
                gameObject,
                _canBeBlocked,
                _canBeParried,
                _blockedDamageMultiplier,
                false,
                _hitTargets.Count);
        }
    }

    private int QueryHitbox()
    {
        Vector3 scale = _hitbox.transform.lossyScale;
        Vector3 absScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        if (_hitbox is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float radius = sphere.radius * Mathf.Max(absScale.x, absScale.y, absScale.z);
            return Physics.OverlapSphereNonAlloc(center, radius, _hitResults, _hitMask, _triggerInteraction);
        }

        if (_hitbox is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, absScale);
            return Physics.OverlapBoxNonAlloc(center, halfExtents, _hitResults, box.transform.rotation, _hitMask, _triggerInteraction);
        }

        if (_hitbox is CapsuleCollider capsule)
        {
            GetCapsuleWorldPoints(capsule, absScale, out Vector3 point0, out Vector3 point1, out float radius);
            return Physics.OverlapCapsuleNonAlloc(point0, point1, radius, _hitResults, _hitMask, _triggerInteraction);
        }

        Bounds bounds = _hitbox.bounds;
        return Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _hitResults, _hitbox.transform.rotation, _hitMask, _triggerInteraction);
    }

    private static void GetCapsuleWorldPoints(CapsuleCollider capsule, Vector3 absScale, out Vector3 point0, out Vector3 point1, out float radius)
    {
        Transform capsuleTransform = capsule.transform;
        Vector3 center = capsuleTransform.TransformPoint(capsule.center);
        Vector3 axis;
        float heightScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = capsuleTransform.right;
                heightScale = absScale.x;
                radiusScale = Mathf.Max(absScale.y, absScale.z);
                break;
            case 1:
                axis = capsuleTransform.up;
                heightScale = absScale.y;
                radiusScale = Mathf.Max(absScale.x, absScale.z);
                break;
            default:
                axis = capsuleTransform.forward;
                heightScale = absScale.z;
                radiusScale = Mathf.Max(absScale.x, absScale.y);
                break;
        }

        radius = capsule.radius * radiusScale;
        float cylinderHalf = Mathf.Max(0f, (capsule.height * heightScale * 0.5f) - radius);
        Vector3 offset = axis * cylinderHalf;
        point0 = center + offset;
        point1 = center - offset;
    }

    private void CancelAttack()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        _hitTargets.Clear();
        SetHitboxActive(false);
    }

    private void SetHitboxActive(bool isActive)
    {
        if (_hitbox != null)
            _hitbox.enabled = isActive;
    }
}
