using FishNet.Object;
using UnityEngine;

public readonly struct WeaponUseContext
{
    public CombatController Controller { get; }
    public WeaponBase WeaponRuntime { get; }
    public WeaponDefinition Definition { get; }
    public WeaponActionSlot ActionSlot { get; }
    public NetworkObject Attacker { get; }
    public GameObject Source { get; }
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    public IDamageable SelfDamageable { get; }
    public int ActionInstanceId { get; }
    public string StyleFamilyId { get; }
    public StyleTag StyleTags { get; }

    public WeaponUseContext(
        CombatController controller,
        WeaponBase weaponRuntime,
        WeaponDefinition definition,
        WeaponActionSlot actionSlot,
        NetworkObject attacker,
        GameObject source,
        Vector3 origin,
        Vector3 direction,
        IDamageable selfDamageable,
        int actionInstanceId,
        string styleFamilyId,
        StyleTag styleTags)
    {
        Controller = controller;
        WeaponRuntime = weaponRuntime;
        Definition = definition;
        ActionSlot = actionSlot;
        Attacker = attacker;
        Source = source;
        Origin = origin;
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        SelfDamageable = selfDamageable;
        ActionInstanceId = Mathf.Max(0, actionInstanceId);
        StyleFamilyId = styleFamilyId;
        StyleTags = styleTags;
    }
}
