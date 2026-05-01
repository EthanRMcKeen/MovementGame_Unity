using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ThrownProjectile : MonoBehaviour
{
    [SerializeField] private bool _rotateToVelocity = true;

    private CombatController _ownerController;
    private IDamageable _ownerDamageable;
    private NetworkObject _attacker;
    private NetworkObject _networkObject;
    private Rigidbody _rigidbody;
    private Collider _projectileCollider;

    private int _damage;
    private string _damageType;
    private float _remainingLifetime;
    private bool _destroyOnWorldHit;
    private bool _canBeBlocked;
    private bool _canBeParried;
    private float _blockedDamageMultiplier;
    private int _actionInstanceId;
    private WeaponActionSlot _actionSlot;
    private string _actionFamilyId;
    private StyleTag _styleTags;
    private bool _isInitialized;
    private bool _hasImpacted;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _rigidbody = GetComponent<Rigidbody>();
        _projectileCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!_isInitialized || _ownerController == null || !_ownerController.IsServerInitialized)
            return;

        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime <= 0f)
        {
            Despawn();
            return;
        }

        if (_rotateToVelocity && _rigidbody != null)
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }
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
        _damageType = string.IsNullOrWhiteSpace(damageType) ? "Thrown" : damageType;
        _remainingLifetime = Mathf.Max(0.1f, lifetimeSeconds);
        _destroyOnWorldHit = destroyOnWorldHit;
        _canBeBlocked = canBeBlocked;
        _canBeParried = canBeParried;
        _blockedDamageMultiplier = Mathf.Clamp01(blockedDamageMultiplier);
        _actionInstanceId = Mathf.Max(0, actionInstanceId);
        _actionSlot = actionSlot;
        _actionFamilyId = actionFamilyId;
        _styleTags = styleTags;
        _hasImpacted = false;
        _isInitialized = true;

        if (_rigidbody != null)
        {
            Vector3 launchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            _rigidbody.linearVelocity = launchDirection * Mathf.Max(0f, launchSpeed);
            _rigidbody.angularVelocity = Vector3.zero;
        }

        IgnoreOwnerCollisions();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isInitialized || _hasImpacted || _ownerController == null || !_ownerController.IsServerInitialized)
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
        if (!_isInitialized || _hasImpacted || _ownerController == null || !_ownerController.IsServerInitialized)
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

        _hasImpacted = true;

        if (DamageResolver.TryGetDamageable(hit, out IDamageable damageable) &&
            !ReferenceEquals(damageable, _ownerDamageable) &&
            damageable.CanTakeDamage)
        {
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
                1));

            Despawn();
            return;
        }

        if (_destroyOnWorldHit)
            Despawn();
        else
            _hasImpacted = false;
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
        if (_ownerController != null && _networkObject != null && _ownerController.IsServerInitialized)
            _ownerController.DespawnServer(_networkObject);
        else
            Destroy(gameObject);
    }
}
