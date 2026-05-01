using UnityEngine;

[DisallowMultipleComponent]
public class ActionWeapon : WeaponBase
{
    [SerializeField] private CombatActionBase[] _actions;

    private readonly CombatActionBase[] _slotActions = new CombatActionBase[4];
    private CombatActionBase[] _resolvedActions;

    public override void Initialize(CombatController controller, WeaponDefinition definition)
    {
        base.Initialize(controller, definition);

        ResolveActions();
        for (int i = 0; i < _resolvedActions.Length; i++)
            _resolvedActions[i].Initialize(controller, this, definition);
    }

    public override bool CanUse(WeaponActionSlot slot)
    {
        return base.CanUse(slot) && GetAction(slot) != null;
    }

    public override float GetCooldown(WeaponActionSlot slot)
    {
        CombatActionBase action = GetAction(slot);
        float defaultCooldown = base.GetCooldown(slot);
        return action != null ? action.GetCooldown(defaultCooldown) : defaultCooldown;
    }

    public override bool TryGetStyleProfile(WeaponActionSlot slot, out StyleActionProfile profile)
    {
        CombatActionBase action = GetAction(slot);
        if (action == null)
            return base.TryGetStyleProfile(slot, out profile);

        profile = action.GetStyleProfile();
        return true;
    }

    public override void OnEquip()
    {
        if (_resolvedActions == null)
            return;

        for (int i = 0; i < _resolvedActions.Length; i++)
            _resolvedActions[i].OnEquip();
    }

    public override void OnUnequip()
    {
        if (_resolvedActions == null)
            return;

        for (int i = 0; i < _resolvedActions.Length; i++)
            _resolvedActions[i].OnUnequip();
    }

    public override bool TryGetAmmo(out int current, out int max)
    {
        if (_resolvedActions != null)
        {
            for (int i = 0; i < _resolvedActions.Length; i++)
            {
                if (_resolvedActions[i].TryGetAmmo(out current, out max))
                    return true;
            }
        }

        current = 0;
        max = 0;
        return false;
    }

    public override bool PrimaryFire(WeaponUseContext context)
    {
        return ExecuteAction(context);
    }

    public override bool SecondaryFire(WeaponUseContext context)
    {
        return ExecuteAction(context);
    }

    public override bool AbilityOne(WeaponUseContext context)
    {
        return ExecuteAction(context);
    }

    public override bool AbilityTwo(WeaponUseContext context)
    {
        return ExecuteAction(context);
    }

    private bool ExecuteAction(WeaponUseContext context)
    {
        CombatActionBase action = GetAction(context.ActionSlot);
        return action != null && action.CanUse(context) && action.Execute(context);
    }

    private CombatActionBase GetAction(WeaponActionSlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= _slotActions.Length)
            return null;

        return _slotActions[index];
    }

    private void ResolveActions()
    {
        for (int i = 0; i < _slotActions.Length; i++)
            _slotActions[i] = null;

        _resolvedActions = _actions != null && _actions.Length > 0
            ? _actions
            : GetComponentsInChildren<CombatActionBase>(true);

        for (int i = 0; i < _resolvedActions.Length; i++)
        {
            CombatActionBase action = _resolvedActions[i];
            if (action == null)
                continue;

            int slotIndex = (int)action.Slot;
            if (slotIndex < 0 || slotIndex >= _slotActions.Length)
                continue;

            if (_slotActions[slotIndex] == null)
                _slotActions[slotIndex] = action;
        }
    }
}
