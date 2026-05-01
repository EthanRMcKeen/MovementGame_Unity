using System;
using UnityEngine;

[Serializable]
public struct StyleActionProfile
{
    public string FamilyId;
    public StyleTag DefaultTags;
    [Min(0f)] public float CloseRangeMeters;
    [Min(0)] public int MaxAcceptedEventsPerSecond;
    public bool CountsAsAbility;
    public bool CountsAsMelee;

    public StyleActionProfile Resolve(string fallbackFamilyId, WeaponActionSlot slot)
    {
        StyleActionProfile resolved = this;
        if (string.IsNullOrWhiteSpace(resolved.FamilyId))
            resolved.FamilyId = string.IsNullOrWhiteSpace(fallbackFamilyId) ? slot.ToString() : fallbackFamilyId;
        if (resolved.CloseRangeMeters <= 0f)
            resolved.CloseRangeMeters = 7f;
        if (resolved.MaxAcceptedEventsPerSecond <= 0)
            resolved.MaxAcceptedEventsPerSecond = 4;
        if (resolved.CountsAsAbility || slot == WeaponActionSlot.AbilityOne || slot == WeaponActionSlot.AbilityTwo)
            resolved.DefaultTags |= StyleTag.Ability;
        if (resolved.CountsAsMelee)
            resolved.DefaultTags |= StyleTag.Melee;

        return resolved;
    }
}
