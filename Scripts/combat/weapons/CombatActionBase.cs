using UnityEngine;

public abstract class CombatActionBase : MonoBehaviour, ICombatHitResponder
{
    [SerializeField] private WeaponActionSlot _slot = WeaponActionSlot.PrimaryFire;
    [SerializeField] private bool _overrideCooldown;
    [SerializeField] [Min(0f)] private float _cooldownSeconds = 0.25f;
    [SerializeField] private StyleActionProfile _styleProfile;

    public CombatController Controller { get; private set; }
    public WeaponBase Weapon { get; private set; }
    public WeaponDefinition Definition { get; private set; }
    public WeaponActionSlot Slot { get { return _slot; } }

    public virtual void Initialize(CombatController controller, WeaponBase weapon, WeaponDefinition definition)
    {
        Controller = controller;
        Weapon = weapon;
        Definition = definition;
    }

    public virtual bool CanUse(WeaponUseContext context)
    {
        return true;
    }

    public virtual float GetCooldown(float defaultCooldownSeconds)
    {
        return _overrideCooldown ? _cooldownSeconds : defaultCooldownSeconds;
    }

    public virtual void OnEquip() { }

    public virtual void OnUnequip() { }

    public virtual bool TryGetAmmo(out int current, out int max)
    {
        current = 0;
        max = 0;
        return false;
    }

    public virtual StyleActionProfile GetStyleProfile()
    {
        return _styleProfile.Resolve($"{GetType().Name}.{_slot}", _slot);
    }

    public abstract bool Execute(WeaponUseContext context);

    public virtual void OnAttackBlocked(DamageRequest request, IDamageable defender) { }

    public virtual void OnAttackParried(DamageRequest request, IDamageable defender) { }

    protected bool TryResolveDamageable(WeaponUseContext context, Collider hit, out IDamageable damageable)
    {
        damageable = null;
        if (hit == null)
            return false;
        if (!DamageResolver.TryGetDamageable(hit, out damageable))
            return false;
        if (context.SelfDamageable != null && ReferenceEquals(damageable, context.SelfDamageable))
            return false;

        return damageable.CanTakeDamage;
    }

    protected void ApplyDamage(
        WeaponUseContext context,
        IDamageable damageable,
        int amount,
        string damageType,
        Vector3 hitPoint,
        Vector3 hitNormal,
        GameObject source = null,
        bool canBeBlocked = false,
        bool canBeParried = false,
        float blockDamageMultiplier = 0.5f,
        bool isWeakPoint = false,
        int targetsHitSoFar = 1)
    {
        if (damageable == null)
            return;

        int finalAmount = amount;
        if (Controller != null)
        {
            if (context.ActionSlot == WeaponActionSlot.PrimaryFire)
                finalAmount = Controller.ApplyPrimaryFireDamageBonus(finalAmount);
            if (Controller.StyleController != null)
                finalAmount = Controller.StyleController.ApplyOutgoingDamageBonus(finalAmount);
        }

        damageable.ServerReceiveDamage(new DamageRequest(
            finalAmount,
            context.Attacker,
            source != null ? source : gameObject,
            hitPoint,
            hitNormal,
            damageType,
            canBeBlocked,
            canBeParried,
            blockDamageMultiplier,
            context.ActionInstanceId,
            context.ActionSlot,
            context.StyleFamilyId,
            context.StyleTags,
            isWeakPoint,
            targetsHitSoFar));
    }
}
