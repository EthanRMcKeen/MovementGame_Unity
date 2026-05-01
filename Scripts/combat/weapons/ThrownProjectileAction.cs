using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class ThrownProjectileAction : CombatActionBase
{
    [Header("Projectile")]
    [SerializeField] private NetworkObject _projectilePrefab;
    [SerializeField] private float _spawnOffset = 0.75f;
    [SerializeField] private float _launchSpeed = 24f;
    [SerializeField] private float _lifetimeSeconds = 3f;
    [SerializeField] private bool _destroyOnWorldHit = true;

    [Header("Damage")]
    [SerializeField] private int _damage = 10;
    [SerializeField] private string _damageType = "Thrown";
    [SerializeField] private bool _canBeBlocked = true;
    [SerializeField] private bool _canBeParried = false;
    [SerializeField] [Range(0f, 1f)] private float _blockedDamageMultiplier = 0.5f;

    public override bool Execute(WeaponUseContext context)
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return false;
        if (_projectilePrefab == null)
        {
            Debug.LogWarning($"{name} is missing a projectile prefab.", this);
            return false;
        }

        Vector3 launchDirection = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : (Controller != null ? Controller.transform.forward : transform.forward);
        Vector3 spawnPosition = context.Origin + (launchDirection * _spawnOffset);

        NetworkObject projectileObject = Instantiate(
            _projectilePrefab,
            spawnPosition,
            Quaternion.LookRotation(launchDirection, Vector3.up));

        ThrownProjectile projectile = projectileObject.GetComponent<ThrownProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning($"Projectile prefab '{_projectilePrefab.name}' is missing a ThrownProjectile component.", this);
            Destroy(projectileObject.gameObject);
            return false;
        }

        projectile.Initialize(
            Controller,
            context.SelfDamageable,
            context.Attacker,
            launchDirection,
            _launchSpeed,
            _damage,
            _damageType,
            _lifetimeSeconds,
            _destroyOnWorldHit,
            _canBeBlocked,
            _canBeParried,
            _blockedDamageMultiplier,
            context.ActionInstanceId,
            context.ActionSlot,
            context.StyleFamilyId,
            context.StyleTags);

        Controller.SpawnServer(projectileObject);
        return true;
    }
}
