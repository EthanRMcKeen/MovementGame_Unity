using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class PistolWeapon : WeaponBase
{
    [Header("Pistol")]
    [SerializeField] private float _range = 1000f;
    [SerializeField] private int _maxBullets = 12;
    [SerializeField] private float _reloadTimeSeconds = 2f;
    [SerializeField] private float _rayHeightOffset = 1f;
    [SerializeField] private LayerMask _damageMask = ~0;

    [Header("Damage")]
    [SerializeField] private int _damage = 25;
    [SerializeField] private string _damageType = "Pistol";
    [SerializeField] private bool _canBeBlocked = true;
    [SerializeField] private bool _canBeParried;
    [SerializeField] [Range(0f, 1f)] private float _blockedDamageMultiplier = 0.5f;

    [Header("Style")]
    [SerializeField] private StyleActionProfile _primaryStyleProfile;

    private int _currentBullets;
    private float _reloadEndTime;
    private bool _isReloading;
    private bool _stateInitialized;

    public int MaxBullets { get { return Mathf.Clamp(_maxBullets, 1, 50); } }
    public int CurrentBullets { get { return Mathf.Clamp(_currentBullets, 0, MaxBullets); } }

    public override void Initialize(CombatController controller, WeaponDefinition definition)
    {
        base.Initialize(controller, definition);
        EnsureRuntimeState();
    }

    public override void OnEquip()
    {
        EnsureRuntimeState();
        NotifyHud();
    }

    public override bool PrimaryFire(WeaponUseContext context)
    {
        EnsureRuntimeState();
        if (Controller == null || !Controller.IsServerInitialized)
            return false;
        if (_currentBullets <= 0)
            return false;

        Vector3 launchDirection = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : (Controller != null ? Controller.transform.forward : transform.forward);

        Ray ray = new Ray(context.Origin + _rayHeightOffset * Vector3.up, launchDirection);
        
        if (Physics.Raycast(ray, out RaycastHit hit, _range, _damageMask))
        {
            if (TryResolveDamageable(context, hit.collider, out IDamageable damageable))
            {
                int finalDamage = _damage;
                if (Controller != null)
                {
                    finalDamage = Controller.ApplyPrimaryFireDamageBonus(finalDamage);
                    if (Controller.StyleController != null)
                        finalDamage = Controller.StyleController.ApplyOutgoingDamageBonus(finalDamage);
                }

                damageable.ServerReceiveDamage(new DamageRequest(
                    finalDamage,
                    context.Attacker,
                    Controller != null ? Controller.gameObject : gameObject,
                    hit.point,
                    hit.normal,
                    _damageType,
                    _canBeBlocked,
                    _canBeParried,
                    _blockedDamageMultiplier,
                    context.ActionInstanceId,
                    context.ActionSlot,
                    context.StyleFamilyId,
                    context.StyleTags,
                    false,
                    1));
            }
        }

        _currentBullets = Mathf.Max(0, _currentBullets - 1);

        NotifyHud();
        return true;
    }

    public override bool TryGetAmmo(out int current, out int max)
    {
        EnsureRuntimeState();
        current = CurrentBullets;
        max = MaxBullets;
        return true;
    }

    public override bool TryGetStyleProfile(WeaponActionSlot slot, out StyleActionProfile profile)
    {
        if (slot == WeaponActionSlot.PrimaryFire)
        {
            profile = _primaryStyleProfile.Resolve($"{GetType().Name}.{slot}", slot);
            return true;
        }

        return base.TryGetStyleProfile(slot, out profile);
    }

    public override bool Reload()
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return false;
        if (_currentBullets >= MaxBullets || _isReloading)
            return false;

        _isReloading = true;
        _reloadEndTime = Time.time + Mathf.Max(0.1f, _reloadTimeSeconds);
        NotifyHud();
        return true;
    }

    private void Update()
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return;

        EnsureRuntimeState();
        if (_isReloading && Time.time >= _reloadEndTime)
        {
            _currentBullets = MaxBullets;
            _isReloading = false;
            NotifyHud();
        }
    }

    private void EnsureRuntimeState()
    {
        if (!_stateInitialized)
        {
            _currentBullets = MaxBullets;
            _reloadEndTime = 0f;
            _isReloading = false;
            _stateInitialized = true;
            return;
        }

        _currentBullets = Mathf.Clamp(_currentBullets, 0, MaxBullets);
    }

    private void NotifyHud()
    {
        if (Controller != null && Controller.IsServerInitialized)
            Controller.NotifyRuntimeStateChanged();
    }
}
