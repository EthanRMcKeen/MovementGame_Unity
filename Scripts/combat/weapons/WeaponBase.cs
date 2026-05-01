using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    public CombatController Controller { get; private set; }
    public WeaponDefinition Definition { get; private set; }

    public virtual void Initialize(CombatController controller, WeaponDefinition definition)
    {
        Controller = controller;
        Definition = definition;
    }

    public virtual bool CanUse(WeaponActionSlot slot)
    {
        return Definition != null && Definition.IsSlotEnabled(slot);
    }

    public virtual float GetCooldown(WeaponActionSlot slot)
    {
        return Definition != null ? Definition.GetCooldown(slot) : 0f;
    }

    public virtual bool TryGetStyleProfile(WeaponActionSlot slot, out StyleActionProfile profile)
    {
        profile = new StyleActionProfile
        {
            FamilyId = $"{GetType().Name}.{slot}",
            CloseRangeMeters = 7f,
            MaxAcceptedEventsPerSecond = 4,
            CountsAsAbility = slot == WeaponActionSlot.AbilityOne || slot == WeaponActionSlot.AbilityTwo
        }.Resolve($"{GetType().Name}.{slot}", slot);

        return true;
    }

    public virtual void OnEquip() { }

    public virtual void OnUnequip() { }

    public virtual bool TryGetAmmo(out int current, out int max)
    {
        current = 0;
        max = 0;
        return false;
    }

    public bool ServerUse(WeaponUseContext context)
    {
        if (!CanUse(context.ActionSlot))
            return false;

        switch (context.ActionSlot)
        {
            case WeaponActionSlot.PrimaryFire:
                return PrimaryFire(context);
            case WeaponActionSlot.SecondaryFire:
                return SecondaryFire(context);
            case WeaponActionSlot.AbilityOne:
                return AbilityOne(context);
            case WeaponActionSlot.AbilityTwo:
                return AbilityTwo(context);
            default:
                return false;
        }
    }

    public virtual bool PrimaryFire(WeaponUseContext context)
    {
        return false;
    }

    public virtual bool SecondaryFire(WeaponUseContext context)
    {
        return false;
    }

    public virtual bool AbilityOne(WeaponUseContext context)
    {
        return false;
    }

    public virtual bool AbilityTwo(WeaponUseContext context)
    {
        return false;
    }

    public virtual bool Reload()
    {
        return false;
    }

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
            source != null ? source : context.Source,
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
