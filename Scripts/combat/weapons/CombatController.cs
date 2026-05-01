using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class CombatController : NetworkBehaviour
{
    [Header("Loadout")]
    [SerializeField] private WeaponDefinition[] _loadout;
    [SerializeField] private int _startingWeaponIndex;

    [Header("Runtime")]
    [SerializeField] private Transform _weaponMount;

    [Header("Aim")]
    [SerializeField] private Transform _aimOrigin;
    [SerializeField] private bool _preferCameraAim = true;

    [Header("Debug")]
    [SerializeField] private bool _debugLogs;

    public readonly SyncVar<int> CurrentWeaponIndexSync = new SyncVar<int>();
    public readonly SyncVar<float> PrimaryCooldownEndsAtSync = new SyncVar<float>();
    public readonly SyncVar<float> SecondaryCooldownEndsAtSync = new SyncVar<float>();
    public readonly SyncVar<float> AbilityOneCooldownEndsAtSync = new SyncVar<float>();
    public readonly SyncVar<float> AbilityTwoCooldownEndsAtSync = new SyncVar<float>();
    public readonly SyncVar<int> CurrentAmmoSync = new SyncVar<int>();
    public readonly SyncVar<int> MaxAmmoSync = new SyncVar<int>();
    public readonly SyncVar<bool> HasAmmoSync = new SyncVar<bool>();

    private readonly Dictionary<int, WeaponBase> _weaponInstances = new Dictionary<int, WeaponBase>();
    private readonly Dictionary<int, float[]> _serverSlotCooldowns = new Dictionary<int, float[]>();

    private PlayerCamera _playerCamera;
    private NetworkPlayerMotor _playerMotor;
    private IDamageable _selfDamageable;
    private PlayerStyleController _styleController;
    private WeaponBase _currentWeaponInstance;
    private Transform _runtimeWeaponRoot;
    private float _primaryFireDamageBonus;

    public WeaponDefinition CurrentWeapon { get { return GetWeaponAt(CurrentWeaponIndexSync.Value); } }
    public WeaponBase CurrentWeaponInstance { get { return _currentWeaponInstance; } }
    public IDamageable SelfDamageable { get { return _selfDamageable; } }
    public NetworkObject OwnerNetworkObject { get { return NetworkObject; } }
    public PlayerStyleController StyleController { get { return ResolveStyleController(); } }
    public float PrimaryFireDamageBonus { get { return _primaryFireDamageBonus; } }

    private void Awake()
    {
        _playerCamera = GetComponent<PlayerCamera>();
        _playerMotor = GetComponent<NetworkPlayerMotor>();
        DamageResolver.TryGetDamageable(this, out _selfDamageable);
        ResolveStyleController();
        CurrentWeaponIndexSync.OnChange += CurrentWeaponIndexSync_OnChange;
        RefreshEquippedWeapon();
    }

    private void OnDestroy()
    {
        CurrentWeaponIndexSync.OnChange -= CurrentWeaponIndexSync_OnChange;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetCurrentWeaponIndexServer(_startingWeaponIndex);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshEquippedWeapon();
    }

    public void OnAttack(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            RequestPrimaryFire();
    }

    public void OnSecondaryFire(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            RequestSecondaryFire();
    }

    public void OnAbilityOne(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            RequestAbilityOne();
    }

    public void OnAbilityTwo(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            RequestAbilityTwo();
    }

    public void OnReload(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            RequestReload();
    }

    public void OnNext(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            CycleNextWeapon();
    }

    public void OnPrevious(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value != null && value.isPressed)
            CyclePreviousWeapon();
    }

    public void RequestPrimaryFire()
    {
        RequestUse(WeaponActionSlot.PrimaryFire);
    }

    public void RequestSecondaryFire()
    {
        RequestUse(WeaponActionSlot.SecondaryFire);
    }

    public void RequestAbilityOne()
    {
        RequestUse(WeaponActionSlot.AbilityOne);
    }

    public void RequestAbilityTwo()
    {
        RequestUse(WeaponActionSlot.AbilityTwo);
    }

    public void RequestReload()
    {
        if (!IsOwner)
            return;
        if (CurrentWeaponInstance == null)
            return;

        ReloadServerRpc();
    }

    public void CycleNextWeapon()
    {
        if (!IsOwner)
            return;

        CycleWeaponServerRpc(1);
    }

    public void CyclePreviousWeapon()
    {
        if (!IsOwner)
            return;

        CycleWeaponServerRpc(-1);
    }

    public bool TryUseSlotServer(WeaponActionSlot slot, Vector3 origin, Vector3 direction)
    {
        return TryUseSlotServer(slot, origin, direction, PlayerMovementState.Idle);
    }

    public bool TryUseSlotServer(WeaponActionSlot slot, Vector3 origin, Vector3 direction, PlayerMovementState movementState)
    {
        if (!IsServerInitialized)
            return false;

        return TryUseCurrentWeapon(slot, origin, direction, movementState);
    }

    [ServerRpc]
    private void UseCurrentWeaponServerRpc(WeaponActionSlot slot, Vector3 origin, Vector3 direction, PlayerMovementState movementState)
    {
        TryUseCurrentWeapon(slot, origin, direction, movementState);
    }

    [ServerRpc]
    private void CycleWeaponServerRpc(int direction)
    {
        if (_loadout == null || _loadout.Length == 0)
            return;

        int nextIndex = WrapIndex(CurrentWeaponIndexSync.Value + direction, _loadout.Length);
        if (nextIndex == CurrentWeaponIndexSync.Value)
            return;

        SetCurrentWeaponIndexServer(nextIndex);
        if (StyleController != null)
            StyleController.ServerNotifyWeaponSwapped();
    }

    [ServerRpc]
    private void ReloadServerRpc()
    {
        if (_currentWeaponInstance != null)
            _currentWeaponInstance.Reload();
    }

    private bool TryUseCurrentWeapon(WeaponActionSlot slot, Vector3 origin, Vector3 direction, PlayerMovementState movementState)
    {
        WeaponDefinition weapon = CurrentWeapon;
        if (weapon == null)
            return false;

        WeaponBase runtimeWeapon = GetOrCreateWeaponInstance(CurrentWeaponIndexSync.Value, weapon);
        if (runtimeWeapon == null || !runtimeWeapon.CanUse(slot))
            return false;

        float[] cooldowns = GetOrCreateServerCooldowns(CurrentWeaponIndexSync.Value);
        int slotIndex = (int)slot;
        if (slotIndex < 0 || slotIndex >= cooldowns.Length)
            return false;
        if (Time.time < cooldowns[slotIndex])
            return false;

        int actionInstanceId = 0;
        string styleFamilyId = null;
        StyleTag styleTags = StyleTag.None;
        if (StyleController != null)
        {
            StyleActionProfile styleProfile;
            if (runtimeWeapon.TryGetStyleProfile(slot, out styleProfile))
            {
                actionInstanceId = StyleController.ServerPrepareAction(styleProfile, slot, movementState, out styleFamilyId, out styleTags);
            }
        }

        WeaponUseContext context = new WeaponUseContext(
            this,
            runtimeWeapon,
            weapon,
            slot,
            NetworkObject,
            runtimeWeapon.gameObject,
            origin,
            direction,
            _selfDamageable,
            actionInstanceId,
            styleFamilyId,
            styleTags);

        if (!runtimeWeapon.ServerUse(context))
        {
            if (StyleController != null)
                StyleController.ServerCancelPreparedAction(actionInstanceId);
            return false;
        }

        if (StyleController != null)
            StyleController.ServerCommitPreparedAction(actionInstanceId, slot);

        float cooldownDuration = Mathf.Max(0f, runtimeWeapon.GetCooldown(slot));
        if (StyleController != null)
            cooldownDuration *= StyleController.GetCooldownMultiplier();

        cooldowns[slotIndex] = Time.time + cooldownDuration;
        PushHudStateServer();

        if (_debugLogs)
            Debug.Log($"{name} used {slot} on {weapon.DisplayName}.");

        return true;
    }

    private void SetCurrentWeaponIndexServer(int index)
    {
        if (_loadout == null || _loadout.Length == 0)
        {
            CurrentWeaponIndexSync.Value = 0;
            RefreshEquippedWeapon();
            PushHudStateServer();
            return;
        }

        int nextIndex = WrapIndex(index, _loadout.Length);
        CurrentWeaponIndexSync.Value = nextIndex;
        RefreshEquippedWeapon();
        PushHudStateServer();
    }

    private bool TryGetAimPose(out Vector3 origin, out Vector3 direction)
    {
        Transform source = _aimOrigin;
        if (_preferCameraAim && _playerCamera != null && _playerCamera.CameraHolderTransform != null)
            source = _playerCamera.CameraHolderTransform;
        if (source == null)
            source = transform;

        origin = source.position;
        direction = source.forward.sqrMagnitude > 0.0001f ? source.forward.normalized : transform.forward;
        return true;
    }

    private WeaponDefinition GetWeaponAt(int index)
    {
        if (_loadout == null || _loadout.Length == 0)
            return null;

        int safeIndex = Mathf.Clamp(index, 0, _loadout.Length - 1);
        return _loadout[safeIndex];
    }

    private void RequestUse(WeaponActionSlot slot)
    {
        if (!IsOwner)
            return;
        if (CurrentWeapon == null)
            return;
        if (!TryGetAimPose(out Vector3 origin, out Vector3 direction))
            return;

        PlayerMovementState movementState = _playerMotor != null ? _playerMotor.State : PlayerMovementState.Idle;
        UseCurrentWeaponServerRpc(slot, origin, direction, movementState);
    }

    private void CurrentWeaponIndexSync_OnChange(int previous, int next, bool asServer)
    {
        RefreshEquippedWeapon();
    }

    private void RefreshEquippedWeapon()
    {
        WeaponBase previousWeapon = _currentWeaponInstance;
        WeaponDefinition currentDefinition = CurrentWeapon;
        WeaponBase nextWeapon = currentDefinition != null
            ? GetOrCreateWeaponInstance(CurrentWeaponIndexSync.Value, currentDefinition)
            : null;

        if (previousWeapon != null && previousWeapon != nextWeapon)
            previousWeapon.OnUnequip();

        _currentWeaponInstance = nextWeapon;

        foreach (KeyValuePair<int, WeaponBase> pair in _weaponInstances)
        {
            WeaponBase instance = pair.Value;
            if (instance == null)
                continue;

            bool isCurrent = instance == _currentWeaponInstance;
            if (instance.gameObject.activeSelf != isCurrent)
                instance.gameObject.SetActive(isCurrent);
        }

        if (_currentWeaponInstance != null && _currentWeaponInstance != previousWeapon)
            _currentWeaponInstance.OnEquip();
    }

    private WeaponBase GetOrCreateWeaponInstance(int index, WeaponDefinition weapon)
    {
        if (weapon == null)
            return null;
        if (_weaponInstances.TryGetValue(index, out WeaponBase existing) && existing != null)
            return existing;

        WeaponBase created = CreateWeaponInstance(weapon);
        if (created == null)
            return null;

        created.Initialize(this, weapon);
        created.gameObject.SetActive(false);
        _weaponInstances[index] = created;
        return created;
    }

    private WeaponBase CreateWeaponInstance(WeaponDefinition weapon)
    {
        Transform parent = GetRuntimeWeaponRoot();
        if (parent == null)
            return null;

        if (weapon.WeaponPrefab == null)
        {
            Debug.LogWarning($"Weapon '{weapon.DisplayName}' is missing a runtime weapon prefab.", this);
            return null;
        }

        GameObject instanceObject = Instantiate(weapon.WeaponPrefab, parent, false);
        WeaponBase runtimeWeapon = instanceObject.GetComponent<WeaponBase>();
        if (runtimeWeapon != null)
            return runtimeWeapon;

        Debug.LogWarning($"Weapon prefab '{weapon.WeaponPrefab.name}' on '{weapon.DisplayName}' is missing a WeaponBase component.", this);
        Destroy(instanceObject);
        return null;
    }

    private Transform GetRuntimeWeaponRoot()
    {
        if (_runtimeWeaponRoot != null)
            return _runtimeWeaponRoot;

        Transform parent = _weaponMount != null ? _weaponMount : transform;
        GameObject runtimeRoot = new GameObject("RuntimeWeapons");
        runtimeRoot.transform.SetParent(parent, false);
        _runtimeWeaponRoot = runtimeRoot.transform;
        return _runtimeWeaponRoot;
    }

    private float[] GetOrCreateServerCooldowns(int weaponIndex)
    {
        if (_serverSlotCooldowns.TryGetValue(weaponIndex, out float[] cooldowns) && cooldowns != null && cooldowns.Length == 4)
            return cooldowns;

        cooldowns = new float[4];
        _serverSlotCooldowns[weaponIndex] = cooldowns;
        return cooldowns;
    }

    public void SpawnServer(NetworkObject networkObject)
    {
        if (!IsServerInitialized || networkObject == null)
            return;

        Spawn(networkObject);
    }

    public void DespawnServer(NetworkObject networkObject)
    {
        if (!IsServerInitialized || networkObject == null)
            return;

        Despawn(networkObject.gameObject);
    }

    public bool IsSlotEnabled(WeaponActionSlot slot)
    {
        return CurrentWeapon != null && CurrentWeapon.IsSlotEnabled(slot);
    }

    public float GetCooldownRemaining(WeaponActionSlot slot)
    {
        return Mathf.Max(0f, GetCooldownEndsAt(slot) - Time.time);
    }

    public float GetCooldownDuration(WeaponActionSlot slot)
    {
        float duration = CurrentWeapon != null ? CurrentWeapon.GetCooldown(slot) : 0f;
        if (StyleController != null)
            duration *= StyleController.GetCooldownMultiplier();

        return duration;
    }

    public float GetCooldownNormalized(WeaponActionSlot slot)
    {
        float duration = GetCooldownDuration(slot);
        if (duration <= 0f)
            return GetCooldownRemaining(slot) > 0f ? 0f : 1f;

        return 1f - Mathf.Clamp01(GetCooldownRemaining(slot) / duration);
    }

    public bool TryGetAmmo(out int current, out int max)
    {
        if (!HasAmmoSync.Value)
        {
            current = 0;
            max = 0;
            return false;
        }

        current = Mathf.Max(0, CurrentAmmoSync.Value);
        max = Mathf.Max(current, MaxAmmoSync.Value);
        return true;
    }

    public void NotifyRuntimeStateChanged()
    {
        if (!IsServerInitialized)
            return;

        PushHudStateServer();
    }

    private void PushHudStateServer()
    {
        if (!IsServerInitialized)
            return;

        WeaponDefinition weapon = CurrentWeapon;
        if (weapon == null)
        {
            SetCooldownSync(WeaponActionSlot.PrimaryFire, 0f);
            SetCooldownSync(WeaponActionSlot.SecondaryFire, 0f);
            SetCooldownSync(WeaponActionSlot.AbilityOne, 0f);
            SetCooldownSync(WeaponActionSlot.AbilityTwo, 0f);
            HasAmmoSync.Value = false;
            CurrentAmmoSync.Value = 0;
            MaxAmmoSync.Value = 0;
            return;
        }

        float[] cooldowns = GetOrCreateServerCooldowns(CurrentWeaponIndexSync.Value);
        SetCooldownSync(WeaponActionSlot.PrimaryFire, cooldowns[(int)WeaponActionSlot.PrimaryFire]);
        SetCooldownSync(WeaponActionSlot.SecondaryFire, cooldowns[(int)WeaponActionSlot.SecondaryFire]);
        SetCooldownSync(WeaponActionSlot.AbilityOne, cooldowns[(int)WeaponActionSlot.AbilityOne]);
        SetCooldownSync(WeaponActionSlot.AbilityTwo, cooldowns[(int)WeaponActionSlot.AbilityTwo]);

        WeaponBase runtimeWeapon = _currentWeaponInstance;
        if (runtimeWeapon == null)
            runtimeWeapon = GetOrCreateWeaponInstance(CurrentWeaponIndexSync.Value, weapon);

        if (runtimeWeapon != null && runtimeWeapon.TryGetAmmo(out int currentAmmo, out int maxAmmo))
        {
            HasAmmoSync.Value = true;
            CurrentAmmoSync.Value = Mathf.Max(0, currentAmmo);
            MaxAmmoSync.Value = Mathf.Max(CurrentAmmoSync.Value, maxAmmo);
        }
        else
        {
            HasAmmoSync.Value = false;
            CurrentAmmoSync.Value = 0;
            MaxAmmoSync.Value = 0;
        }
    }

    private void SetCooldownSync(WeaponActionSlot slot, float endsAt)
    {
        switch (slot)
        {
            case WeaponActionSlot.PrimaryFire:
                PrimaryCooldownEndsAtSync.Value = endsAt;
                break;
            case WeaponActionSlot.SecondaryFire:
                SecondaryCooldownEndsAtSync.Value = endsAt;
                break;
            case WeaponActionSlot.AbilityOne:
                AbilityOneCooldownEndsAtSync.Value = endsAt;
                break;
            case WeaponActionSlot.AbilityTwo:
                AbilityTwoCooldownEndsAtSync.Value = endsAt;
                break;
        }
    }

    private float GetCooldownEndsAt(WeaponActionSlot slot)
    {
        switch (slot)
        {
            case WeaponActionSlot.PrimaryFire:
                return PrimaryCooldownEndsAtSync.Value;
            case WeaponActionSlot.SecondaryFire:
                return SecondaryCooldownEndsAtSync.Value;
            case WeaponActionSlot.AbilityOne:
                return AbilityOneCooldownEndsAtSync.Value;
            case WeaponActionSlot.AbilityTwo:
                return AbilityTwoCooldownEndsAtSync.Value;
            default:
                return 0f;
        }
    }

    private static int WrapIndex(int index, int length)
    {
        if (length <= 0)
            return 0;

        int wrapped = index % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }

    private PlayerStyleController ResolveStyleController()
    {
        if (_styleController != null)
            return _styleController;

        _styleController = GetComponent<PlayerStyleController>();
        return _styleController;
    }

    public void AddPrimaryFireDamageBonus(float bonus)
    {
        _primaryFireDamageBonus += bonus;
    }

    public void RemovePrimaryFireDamageBonus(float bonus)
    {
        _primaryFireDamageBonus -= bonus;
    }

    public int ApplyPrimaryFireDamageBonus(int amount)
    {
        if (_primaryFireDamageBonus <= 0f)
            return amount;

        return Mathf.RoundToInt(amount * (1f + _primaryFireDamageBonus));
    }
}
