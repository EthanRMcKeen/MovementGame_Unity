using FishNet.Object;
using UnityEngine;

public interface IHealth
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsDead { get; }

    void ServerHeal(int amount);
    void ServerRestoreFullHealth();
}

public interface IDamageable
{
    bool CanTakeDamage { get; }
    IHealth Health { get; }
    void ServerReceiveDamage(DamageRequest damage);
}

// Backward-compatible alias while scripts migrate to IDamageable.
public interface IDamage : IDamageable { }

public struct DamageRequest
{
    public int Amount;
    public NetworkObject Attacker;
    public GameObject Source;
    public Vector3 HitPoint;
    public Vector3 HitNormal;
    public string DamageType;
    public bool CanBeBlocked;
    public bool CanBeParried;
    public float BlockDamageMultiplier;
    public int ActionInstanceId;
    public WeaponActionSlot ActionSlot;
    public string ActionFamilyId;
    public StyleTag StyleTags;
    public bool IsWeakPoint;
    public int TargetsHitSoFar;

    public DamageRequest(
        int amount,
        NetworkObject attacker = null,
        GameObject source = null,
        Vector3 hitPoint = default(Vector3),
        Vector3 hitNormal = default(Vector3),
        string damageType = "Generic",
        bool canBeBlocked = false,
        bool canBeParried = false,
        float blockDamageMultiplier = 0.5f,
        int actionInstanceId = 0,
        WeaponActionSlot actionSlot = WeaponActionSlot.PrimaryFire,
        string actionFamilyId = null,
        StyleTag styleTags = StyleTag.None,
        bool isWeakPoint = false,
        int targetsHitSoFar = 1)
    {
        Amount = amount;
        Attacker = attacker;
        Source = source;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        DamageType = string.IsNullOrWhiteSpace(damageType) ? "Generic" : damageType;
        CanBeBlocked = canBeBlocked;
        CanBeParried = canBeParried;
        BlockDamageMultiplier = Mathf.Clamp01(blockDamageMultiplier);
        ActionInstanceId = Mathf.Max(0, actionInstanceId);
        ActionSlot = actionSlot;
        ActionFamilyId = actionFamilyId;
        StyleTags = styleTags;
        IsWeakPoint = isWeakPoint;
        TargetsHitSoFar = Mathf.Max(1, targetsHitSoFar);
    }
}
