using UnityEngine;

[System.Serializable]
public sealed class WeaponSlotSettings
{
    public bool Enabled = true;
    [Min(0f)] public float CooldownSeconds = 0.25f;
}

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Combat/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _weaponId = "weapon";
    [SerializeField] private string _displayName = "Weapon";

    [Header("Runtime")]
    [SerializeField] private GameObject _weaponPrefab;

    [Header("Slots")]
    [SerializeField] private WeaponSlotSettings _primaryFire = new WeaponSlotSettings();
    [SerializeField] private WeaponSlotSettings _secondaryFire = new WeaponSlotSettings { Enabled = false, CooldownSeconds = 0.5f };
    [SerializeField] private WeaponSlotSettings _abilityOne = new WeaponSlotSettings { Enabled = false, CooldownSeconds = 4f };
    [SerializeField] private WeaponSlotSettings _abilityTwo = new WeaponSlotSettings { Enabled = false, CooldownSeconds = 6f };

    public string WeaponId { get { return string.IsNullOrWhiteSpace(_weaponId) ? name : _weaponId; } }
    public string DisplayName { get { return string.IsNullOrWhiteSpace(_displayName) ? name : _displayName; } }
    public GameObject WeaponPrefab { get { return _weaponPrefab; } }

    public bool IsSlotEnabled(WeaponActionSlot slot)
    {
        WeaponSlotSettings settings = GetSlotSettings(slot);
        return settings != null && settings.Enabled;
    }

    public float GetCooldown(WeaponActionSlot slot)
    {
        WeaponSlotSettings settings = GetSlotSettings(slot);
        if (settings == null)
            return 0f;

        return Mathf.Max(0f, settings.CooldownSeconds);
    }

    public WeaponSlotSettings GetSlotSettings(WeaponActionSlot slot)
    {
        switch (slot)
        {
            case WeaponActionSlot.PrimaryFire:
                return GetOrCreateSlotSettings(ref _primaryFire, true, 0.25f);
            case WeaponActionSlot.SecondaryFire:
                return GetOrCreateSlotSettings(ref _secondaryFire, false, 0.5f);
            case WeaponActionSlot.AbilityOne:
                return GetOrCreateSlotSettings(ref _abilityOne, false, 4f);
            case WeaponActionSlot.AbilityTwo:
                return GetOrCreateSlotSettings(ref _abilityTwo, false, 6f);
            default:
                return null;
        }
    }

    private static WeaponSlotSettings GetOrCreateSlotSettings(ref WeaponSlotSettings settings, bool enabled, float cooldownSeconds)
    {
        if (settings == null)
        {
            settings = new WeaponSlotSettings
            {
                Enabled = enabled,
                CooldownSeconds = Mathf.Max(0f, cooldownSeconds)
            };
        }

        return settings;
    }
}
