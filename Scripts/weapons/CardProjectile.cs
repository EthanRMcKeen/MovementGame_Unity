using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CardProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private bool _rotateToVelocity = true;
    [SerializeField] private float _spinDegreesPerSecond = 1440f;
    [SerializeField] private Vector3 _modelEulerOffset = new Vector3(90f, 0f, 0f);

    private readonly HashSet<IDamageable> _damagedTargets = new HashSet<IDamageable>();

    private CombatController _ownerController;
    private IDamageable _ownerDamageable;
    private NetworkObject _attacker;
    private NetworkObject _networkObject;
    private Rigidbody _rigidbody;
    private Collider _projectileCollider;

    private int _damage;
    private string _damageType;
    private int _remainingPierce;
    private float _remainingLifetime;
    private float _spinAngle;
    private bool _destroyOnWorldHit;
    private bool _canBeBlocked;
    private bool _canBeParried;
    private float _blockedDamageMultiplier;
    private int _actionInstanceId;
    private WeaponActionSlot _actionSlot;
    private string _actionFamilyId;
    private StyleTag _styleTags;
    private bool _isInitialized;
    private bool _isDespawning;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _rigidbody = GetComponent<Rigidbody>();
        _projectileCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!_isInitialized || _isDespawning || _ownerController == null || !_ownerController.IsServerInitialized)
            return;

        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime <= 0f)
        {
            Despawn();
            return;
        }

        if (!_rotateToVelocity || _rigidbody == null)
            return;

        Vector3 velocity = _rigidbody.linearVelocity;
        if (velocity.sqrMagnitude <= 0.001f)
            return;

        _spinAngle = Mathf.Repeat(_spinAngle + (_spinDegreesPerSecond * Time.deltaTime), 360f);
        Quaternion lookRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        Quaternion spinRotation = Quaternion.AngleAxis(_spinAngle, Vector3.up);
        Quaternion modelOffset = Quaternion.Euler(_modelEulerOffset);
        transform.rotation = lookRotation * spinRotation * modelOffset;
    }

    public void Initialize(
        CombatController ownerController,
        IDamageable ownerDamageable,
        NetworkObject attacker,
        Vector3 direction,
        float launchSpeed,
        int damage,
        string damageType,
        float lifetimeSeconds,
        int pierceCount,
        bool destroyOnWorldHit,
        bool canBeBlocked,
        bool canBeParried,
        float blockedDamageMultiplier,
        int actionInstanceId,
        WeaponActionSlot actionSlot,
        string actionFamilyId,
        StyleTag styleTags)
    {
        _ownerController = ownerController;
        _ownerDamageable = ownerDamageable;
        _attacker = attacker;
        _damage = Mathf.Max(0, damage);
        _damageType = string.IsNullOrWhiteSpace(damageType) ? "Card" : damageType;
        _remainingPierce = Mathf.Max(0, pierceCount);
        _remainingLifetime = Mathf.Max(0.1f, lifetimeSeconds);
        _destroyOnWorldHit = destroyOnWorldHit;
        _canBeBlocked = canBeBlocked;
        _canBeParried = canBeParried;
        _blockedDamageMultiplier = Mathf.Clamp01(blockedDamageMultiplier);
        _actionInstanceId = Mathf.Max(0, actionInstanceId);
        _actionSlot = actionSlot;
        _actionFamilyId = actionFamilyId;
        _styleTags = styleTags;
        _spinAngle = 0f;
        _isDespawning = false;
        _isInitialized = true;
        _damagedTargets.Clear();

        if (_rigidbody != null)
        {
            Vector3 launchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = launchDirection * Mathf.Max(0f, launchSpeed);
            _rigidbody.angularVelocity = Vector3.zero;

            if (_rotateToVelocity)
            {
                Quaternion lookRotation = Quaternion.LookRotation(launchDirection, Vector3.up);
                transform.rotation = lookRotation * Quaternion.Euler(_modelEulerOffset);
            }
        }

        IgnoreOwnerCollisions();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isInitialized || _isDespawning || _ownerController == null || !_ownerController.IsServerInitialized)
            return;
        if (collision == null || collision.collider == null)
            return;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);
        Vector3 hitNormal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : -GetTravelDirection();

        HandleImpact(collision.collider, hitPoint, hitNormal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized || _isDespawning || _ownerController == null || !_ownerController.IsServerInitialized)
            return;
        if (other == null)
            return;

        HandleImpact(other, other.ClosestPoint(transform.position), -GetTravelDirection());
    }

    private void HandleImpact(Collider hit, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hit == null)
            return;
        if (_ownerController != null && hit.transform.IsChildOf(_ownerController.transform))
            return;

        if (DamageResolver.TryGetDamageable(hit, out IDamageable damageable) &&
            !ReferenceEquals(damageable, _ownerDamageable) &&
            damageable.CanTakeDamage &&
            !_damagedTargets.Contains(damageable))
        {
            _damagedTargets.Add(damageable);
            int finalDamage = _damage;
            if (_ownerController != null)
            {
                if (_actionSlot == WeaponActionSlot.PrimaryFire)
                    finalDamage = _ownerController.ApplyPrimaryFireDamageBonus(finalDamage);
                if (_ownerController.StyleController != null)
                    finalDamage = _ownerController.StyleController.ApplyOutgoingDamageBonus(finalDamage);
            }

            damageable.ServerReceiveDamage(new DamageRequest(
                finalDamage,
                _attacker,
                gameObject,
                hitPoint,
                hitNormal,
                _damageType,
                _canBeBlocked,
                _canBeParried,
                _blockedDamageMultiplier,
                _actionInstanceId,
                _actionSlot,
                _actionFamilyId,
                _styleTags,
                false,
                _damagedTargets.Count));

            if (_remainingPierce > 0)
            {
                _remainingPierce--;
                return;
            }

            Despawn();
            return;
        }

        if (_destroyOnWorldHit)
            Despawn();
    }

    private void IgnoreOwnerCollisions()
    {
        if (_ownerController == null || _projectileCollider == null)
            return;

        Collider[] ownerColliders = _ownerController.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider ownerCollider = ownerColliders[i];
            if (ownerCollider == null || ownerCollider == _projectileCollider)
                continue;

            Physics.IgnoreCollision(_projectileCollider, ownerCollider, true);
        }
    }

    private Vector3 GetTravelDirection()
    {
        if (_rigidbody != null && _rigidbody.linearVelocity.sqrMagnitude > 0.001f)
            return _rigidbody.linearVelocity.normalized;

        return transform.forward;
    }

    private void Despawn()
    {
        if (_isDespawning)
            return;

        _isDespawning = true;

        if (_ownerController != null && _networkObject != null && _ownerController.IsServerInitialized)
            _ownerController.DespawnServer(_networkObject);
        else
            Destroy(gameObject);
    }
}
