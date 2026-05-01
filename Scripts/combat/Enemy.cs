using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CombatController))]
[RequireComponent(typeof(DamageHandler))]
public class Enemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _attackOrigin;
    [SerializeField] private Toggle _debugMode;

    [Header("Movement")]
    [SerializeField] private bool _moveTowardsTarget = true;
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private float _turnSpeedDegrees = 360f;
    [SerializeField] private float _stoppingDistance = 2f;
    [SerializeField] private float _targetRefreshInterval = 0.25f;

    [Header("Attack")]
    [SerializeField] private WeaponActionSlot _attackSlot = WeaponActionSlot.PrimaryFire;
    [SerializeField] private string _attackTrigger = "isAttacking";
    [SerializeField] private float _attackDistance = 2.5f;
    [SerializeField] private float _attackCooldown = 5f;

    private CombatController _combatController;
    private DamageHandler _damageHandler;
    private CombatController _targetController;
    private float _attackTimer;
    private float _nextTargetRefreshTime;

    private void Awake()
    {
        _combatController = GetComponent<CombatController>();
        _damageHandler = GetComponent<DamageHandler>();

        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_attackOrigin == null)
            _attackOrigin = transform;
    }

    private void Update()
    {
        if (_damageHandler != null && _damageHandler.IsDead)
            return;
        if (_combatController == null || !_combatController.IsServerInitialized)
            return;

        ResolveTarget();
        if (_targetController == null)
            return;

        Vector3 flatOffsetToTarget = GetFlatOffsetTo(_targetController.transform.position);
        RotateTowards(flatOffsetToTarget, Time.deltaTime);

        if (_moveTowardsTarget)
            MoveTowards(flatOffsetToTarget, Time.deltaTime);

        _attackTimer -= Time.deltaTime;
        if (_attackTimer > 0f)
            return;
        if (flatOffsetToTarget.magnitude > Mathf.Max(0f, _attackDistance))
            return;

        Vector3 origin = _attackOrigin.position;
        Vector3 direction = flatOffsetToTarget.sqrMagnitude > 0.001f
            ? flatOffsetToTarget.normalized
            : transform.forward;
        if (!_combatController.TryUseSlotServer(_attackSlot, origin, direction))
        {
            _attackTimer = 0.25f;
            return;
        }

        if (_animator != null && !string.IsNullOrWhiteSpace(_attackTrigger))
            _animator.SetTrigger(_attackTrigger);

        _attackTimer = Mathf.Max(0.1f, _attackCooldown);

        if (_debugMode != null && _debugMode.isOn)
            Debug.Log($"{name} used {_attackSlot}.");
    }

    private void ResolveTarget()
    {
        if (_targetController != null && IsValidTarget(_targetController))
        {
            if (Time.time < _nextTargetRefreshTime)
                return;
        }

        _nextTargetRefreshTime = Time.time + Mathf.Max(0.05f, _targetRefreshInterval);
        _targetController = FindClosestTarget();
    }

    private CombatController FindClosestTarget()
    {
        CombatController[] combatControllers = FindObjectsOfType<CombatController>();
        CombatController closest = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < combatControllers.Length; i++)
        {
            CombatController candidate = combatControllers[i];
            if (!IsValidTarget(candidate))
                continue;

            Vector3 offset = GetFlatOffsetTo(candidate.transform.position);
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr >= closestDistanceSqr)
                continue;

            closest = candidate;
            closestDistanceSqr = distanceSqr;
        }

        return closest;
    }

    private bool IsValidTarget(CombatController candidate)
    {
        if (candidate == null || candidate == _combatController)
            return false;
        if (!candidate.gameObject.activeInHierarchy)
            return false;
        if (candidate.GetComponent<Enemy>() != null)
            return false;

        DamageHandler candidateDamageHandler = candidate.GetComponent<DamageHandler>();
        return candidateDamageHandler == null || !candidateDamageHandler.IsDead;
    }

    private void RotateTowards(Vector3 flatOffsetToTarget, float deltaTime)
    {
        if (flatOffsetToTarget.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(flatOffsetToTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Mathf.Max(0f, _turnSpeedDegrees) * deltaTime);
    }

    private void MoveTowards(Vector3 flatOffsetToTarget, float deltaTime)
    {
        float distance = flatOffsetToTarget.magnitude;
        if (distance <= Mathf.Max(0f, _stoppingDistance))
            return;

        Vector3 moveDirection = flatOffsetToTarget / Mathf.Max(0.001f, distance);
        transform.position += moveDirection * (Mathf.Max(0f, _moveSpeed) * deltaTime);
    }

    private Vector3 GetFlatOffsetTo(Vector3 targetPosition)
    {
        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;
        return offset;
    }
}
