using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class CardWeapon : WeaponBase
{
    [Header("Card")]
    [SerializeField] private NetworkObject _cardPrefab;
    [SerializeField] private int _maxCards = 6;
    [SerializeField] private float _spawnOffset = 0.7f;
    [SerializeField] private float _throwSpeed = 32f;
    [SerializeField] private float _projectileLifetime = 2.5f;
    [SerializeField] private int _pierceCount;
    [SerializeField] private bool _destroyOnWorldHit = true;
    [SerializeField] private float _cardRechargeSeconds = 0.45f;
    [SerializeField] private float _spawnHeightOffset = 0.7f;

    [Header("Damage")]
    [SerializeField] private int _damage = 12;
    [SerializeField] private string _damageType = "Card";
    [SerializeField] private bool _canBeBlocked = true;
    [SerializeField] private bool _canBeParried;
    [SerializeField] [Range(0f, 1f)] private float _blockedDamageMultiplier = 0.5f;

    [Header("Style")]
    [SerializeField] private StyleActionProfile _primaryStyleProfile;

    private int _currentCards;
    private float _nextRechargeAt;
    private bool _stateInitialized;

    public int MaxCards { get { return Mathf.Clamp(_maxCards, 1, 16); } }
    public int CurrentCards { get { return Mathf.Clamp(_currentCards, 0, MaxCards); } }

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
        if (_cardPrefab == null)
            return false;
        if (_currentCards <= 0)
            return false;

        Vector3 launchDirection = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : (Controller != null ? Controller.transform.forward : transform.forward);
        Vector3 spawnPosition = context.Origin + (launchDirection * _spawnOffset) + (Vector3.up * _spawnHeightOffset);

        NetworkObject cardObject = Instantiate(
            _cardPrefab,
            spawnPosition,
            Quaternion.LookRotation(launchDirection, Vector3.up));

        CardProjectile cardProjectile = cardObject.GetComponent<CardProjectile>();
        if (cardProjectile == null)
        {
            Debug.LogWarning($"Card prefab '{_cardPrefab.name}' is missing a CardProjectile component.", this);
            Destroy(cardObject.gameObject);
            return false;
        }

        cardProjectile.Initialize(
            Controller,
            context.SelfDamageable,
            context.Attacker,
            launchDirection,
            _throwSpeed,
            _damage,
            _damageType,
            _projectileLifetime,
            Mathf.Max(0, _pierceCount),
            _destroyOnWorldHit,
            _canBeBlocked,
            _canBeParried,
            _blockedDamageMultiplier,
            context.ActionInstanceId,
            context.ActionSlot,
            context.StyleFamilyId,
            context.StyleTags);

        Controller.SpawnServer(cardObject);

        _currentCards = Mathf.Max(0, _currentCards - 1);
        if (_currentCards < MaxCards)
            _nextRechargeAt = Time.time + Mathf.Max(0.01f, _cardRechargeSeconds);

        NotifyHud();
        return true;
    }

    public override bool TryGetAmmo(out int current, out int max)
    {
        EnsureRuntimeState();
        current = CurrentCards;
        max = MaxCards;
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

    private void Update()
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return;

        EnsureRuntimeState();
        if (_currentCards >= MaxCards)
            return;
        if (Time.time < _nextRechargeAt)
            return;

        _currentCards = Mathf.Min(MaxCards, _currentCards + 1);
        if (_currentCards < MaxCards)
            _nextRechargeAt = Time.time + Mathf.Max(0.01f, _cardRechargeSeconds);

        NotifyHud();
    }

    private void EnsureRuntimeState()
    {
        if (!_stateInitialized)
        {
            _currentCards = MaxCards;
            _nextRechargeAt = 0f;
            _stateInitialized = true;
            return;
        }

        _currentCards = Mathf.Clamp(_currentCards, 0, MaxCards);
    }

    private void NotifyHud()
    {
        if (Controller != null && Controller.IsServerInitialized)
            Controller.NotifyRuntimeStateChanged();
    }
}
