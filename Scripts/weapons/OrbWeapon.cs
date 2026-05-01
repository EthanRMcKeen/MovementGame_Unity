using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class OrbWeapon : WeaponBase
{
    [Header("Orb")]
    [SerializeField] private NetworkObject _orbPrefab;
    [SerializeField] private int _maxCharges = 2;
    [SerializeField] private float _spawnOffset = 0.9f;
    [SerializeField] private float _outboundSpeed = 22f;
    [SerializeField] private float _returnSpeed = 30f;
    [SerializeField] private float _maxTravelDistance = 18f;
    [SerializeField] private float _hitRadius = 0.6f;
    [SerializeField] private float _catchRadius = 1f;
    [SerializeField] private LayerMask _damageMask = ~0;
    [SerializeField] private int _outboundDamage = 20;
    [SerializeField] private int _returnDamage = 20;

    [Header("Style")]
    [SerializeField] private StyleActionProfile _primaryStyleProfile;
    [SerializeField] private StyleActionProfile _recallStyleProfile;

    private readonly Collider[] _hitResults = new Collider[16];
    private readonly List<OrbRuntime> _activeOrbs = new List<OrbRuntime>(4);

    private CapsuleCollider _ownerCapsule;

    public int MaxCharges { get { return Mathf.Clamp(_maxCharges, 1, 8); } }
    public int ActiveOrbCount { get { return _activeOrbs.Count; } }
    public int AvailableCharges { get { return Mathf.Max(0, MaxCharges - _activeOrbs.Count); } }

    private enum OrbState
    {
        Outbound,
        Resting,
        Returning
    }

    private sealed class OrbRuntime
    {
        public NetworkObject Orb;
        public Rigidbody Body;
        public Collider HitCollider;
        public Vector3 ThrowDirection;
        public Vector3 ThrowStart;
        public OrbState State;
        public readonly HashSet<IDamageable> OutboundDamagedTargets = new HashSet<IDamageable>();
        public readonly HashSet<IDamageable> ReturnDamagedTargets = new HashSet<IDamageable>();
        public int OutboundActionInstanceId;
        public WeaponActionSlot OutboundActionSlot;
        public string OutboundActionFamilyId;
        public StyleTag OutboundStyleTags;
        public int ReturnActionInstanceId;
        public WeaponActionSlot ReturnActionSlot;
        public string ReturnActionFamilyId;
        public StyleTag ReturnStyleTags;
    }

    public override void Initialize(CombatController controller, WeaponDefinition definition)
    {
        base.Initialize(controller, definition);
        _ownerCapsule = controller != null ? controller.GetComponent<CapsuleCollider>() : null;
    }

    public override bool PrimaryFire(WeaponUseContext context)
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return false;
        if (_orbPrefab == null || _activeOrbs.Count >= MaxCharges)
            return false;

        ThrowSingleOrb(context);
        return true;
    }

    public override bool AbilityTwo(WeaponUseContext context)
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return false;
        if (_activeOrbs.Count == 0)
            return false;

        BeginRecallAll(context);
        return true;
    }

    public override bool TryGetAmmo(out int current, out int max)
    {
        current = AvailableCharges;
        max = MaxCharges;
        return true;
    }

    public override bool TryGetStyleProfile(WeaponActionSlot slot, out StyleActionProfile profile)
    {
        if (slot == WeaponActionSlot.PrimaryFire)
        {
            profile = _primaryStyleProfile.Resolve($"{GetType().Name}.{slot}", slot);
            return true;
        }

        if (slot == WeaponActionSlot.AbilityTwo)
        {
            profile = _recallStyleProfile.Resolve($"{GetType().Name}.{slot}", slot);
            return true;
        }

        return base.TryGetStyleProfile(slot, out profile);
    }

    public override void OnUnequip()
    {
        if (Controller != null && Controller.IsServerInitialized)
            DespawnAllOrbs();
    }

    private void OnDestroy()
    {
        if (Controller != null && Controller.IsServerInitialized)
            DespawnAllOrbs();
    }

    private void FixedUpdate()
    {
        if (Controller == null || !Controller.IsServerInitialized)
            return;

        for (int i = _activeOrbs.Count - 1; i >= 0; i--)
        {
            OrbRuntime orb = _activeOrbs[i];
            if (orb.Orb == null)
            {
                _activeOrbs.RemoveAt(i);
                Controller.NotifyRuntimeStateChanged();
                continue;
            }

            bool despawned = UpdateOrbMotion(orb, Time.fixedDeltaTime);
            if (despawned)
            {
                _activeOrbs.RemoveAt(i);
                Controller.NotifyRuntimeStateChanged();
                continue;
            }

            if (orb.State == OrbState.Outbound)
                ApplyOutboundDamage(orb);
            else if (orb.State == OrbState.Returning)
                ApplyReturningDamage(orb);
        }
    }

    private void ThrowSingleOrb(WeaponUseContext context)
    {
        if (_orbPrefab == null)
            return;
        Vector3 origin = context.Origin;
        Vector3 direction = context.Direction;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Controller != null ? Controller.transform.forward : Vector3.forward;

        Vector3 launchDirection = direction.normalized;
        Vector3 spawnPosition = origin + (launchDirection * _spawnOffset);
        NetworkObject orb = Instantiate(_orbPrefab, spawnPosition, Quaternion.LookRotation(launchDirection, Vector3.up));

        OrbRuntime runtime = new OrbRuntime
        {
            Orb = orb,
            ThrowStart = spawnPosition,
            ThrowDirection = launchDirection,
            State = OrbState.Outbound,
            OutboundActionInstanceId = context.ActionInstanceId,
            OutboundActionSlot = context.ActionSlot,
            OutboundActionFamilyId = context.StyleFamilyId,
            OutboundStyleTags = context.StyleTags
        };

        PrepareOrbPhysics(runtime);
        Controller.SpawnServer(orb);
        _activeOrbs.Add(runtime);
        Controller.NotifyRuntimeStateChanged();
    }

    private void PrepareOrbPhysics(OrbRuntime runtime)
    {
        if (runtime == null || runtime.Orb == null)
            return;

        runtime.Body = runtime.Orb.GetComponent<Rigidbody>();
        runtime.HitCollider = runtime.Orb.GetComponent<Collider>();
        SetOrbKinematic(runtime, true);
    }

    private bool UpdateOrbMotion(OrbRuntime orb, float deltaTime)
    {
        if (orb.State == OrbState.Outbound)
        {
            orb.Orb.transform.position += orb.ThrowDirection * (_outboundSpeed * deltaTime);
            orb.Orb.transform.rotation = Quaternion.LookRotation(orb.ThrowDirection, Vector3.up);

            float travelledDistance = Vector3.Distance(orb.ThrowStart, orb.Orb.transform.position);
            if (travelledDistance >= _maxTravelDistance)
                SetOrbResting(orb);

            return false;
        }

        if (orb.State == OrbState.Resting)
            return false;
        if (orb.State != OrbState.Returning)
            return false;

        Vector3 returnTarget = GetReturnTarget();
        Vector3 toTarget = returnTarget - orb.Orb.transform.position;
        float distance = toTarget.magnitude;
        if (distance <= _catchRadius)
        {
            DespawnOrb(orb);
            return true;
        }

        Vector3 returnDirection = toTarget / distance;
        orb.Orb.transform.position += returnDirection * (_returnSpeed * deltaTime);
        orb.Orb.transform.rotation = Quaternion.LookRotation(returnDirection, Vector3.up);
        return false;
    }

    private void ApplyOutboundDamage(OrbRuntime orb)
    {
        if (_outboundDamage <= 0 || orb == null || orb.Orb == null)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            orb.Orb.transform.position,
            _hitRadius,
            _hitResults,
            _damageMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _hitResults[i];
            if (hit == null)
                continue;
            if (Controller != null && hit.transform.IsChildOf(Controller.transform))
                continue;

            if (!DamageResolver.TryGetDamageable(hit, out IDamageable damageable))
                continue;
            if (ReferenceEquals(damageable, Controller.SelfDamageable))
                continue;
            if (!damageable.CanTakeDamage)
                continue;
            if (orb.OutboundDamagedTargets.Contains(damageable))
                continue;

            orb.OutboundDamagedTargets.Add(damageable);
            int finalDamage = _outboundDamage;
            if (Controller != null)
            {
                finalDamage = Controller.ApplyPrimaryFireDamageBonus(finalDamage);
                if (Controller.StyleController != null)
                    finalDamage = Controller.StyleController.ApplyOutgoingDamageBonus(finalDamage);
            }

            damageable.ServerReceiveDamage(new DamageRequest(
                finalDamage,
                Controller.OwnerNetworkObject,
                orb.Orb.gameObject,
                orb.Orb.transform.position,
                -orb.ThrowDirection,
                "OrbOutbound",
                false,
                false,
                0.5f,
                orb.OutboundActionInstanceId,
                orb.OutboundActionSlot,
                orb.OutboundActionFamilyId,
                orb.OutboundStyleTags,
                false,
                orb.OutboundDamagedTargets.Count));
        }
    }

    private void ApplyReturningDamage(OrbRuntime orb)
    {
        if (_returnDamage <= 0 || orb == null || orb.Orb == null)
            return;

        Vector3 toTarget = GetReturnTarget() - orb.Orb.transform.position;
        Vector3 returnDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : -orb.ThrowDirection;

        int hitCount = Physics.OverlapSphereNonAlloc(
            orb.Orb.transform.position,
            _hitRadius,
            _hitResults,
            _damageMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _hitResults[i];
            if (hit == null)
                continue;
            if (Controller != null && hit.transform.IsChildOf(Controller.transform))
                continue;

            if (!DamageResolver.TryGetDamageable(hit, out IDamageable damageable))
                continue;
            if (ReferenceEquals(damageable, Controller.SelfDamageable))
                continue;
            if (!damageable.CanTakeDamage)
                continue;
            if (orb.ReturnDamagedTargets.Contains(damageable))
                continue;

            orb.ReturnDamagedTargets.Add(damageable);
            int finalDamage = _returnDamage;
            if (Controller != null)
            {
                finalDamage = Controller.ApplyPrimaryFireDamageBonus(finalDamage);
                if (Controller.StyleController != null)
                    finalDamage = Controller.StyleController.ApplyOutgoingDamageBonus(finalDamage);
            }

            damageable.ServerReceiveDamage(new DamageRequest(
                finalDamage,
                Controller.OwnerNetworkObject,
                orb.Orb.gameObject,
                orb.Orb.transform.position,
                -returnDirection,
                "OrbReturn",
                false,
                false,
                0.5f,
                orb.ReturnActionInstanceId,
                orb.ReturnActionSlot,
                orb.ReturnActionFamilyId,
                orb.ReturnStyleTags,
                false,
                orb.ReturnDamagedTargets.Count));
        }
    }

    private void BeginRecallAll(WeaponUseContext context)
    {
        for (int i = 0; i < _activeOrbs.Count; i++)
        {
            OrbRuntime orb = _activeOrbs[i];
            if (orb == null)
                continue;

            orb.State = OrbState.Returning;
            orb.ReturnActionInstanceId = context.ActionInstanceId;
            orb.ReturnActionSlot = context.ActionSlot;
            orb.ReturnActionFamilyId = context.StyleFamilyId;
            orb.ReturnStyleTags = context.StyleTags;
            orb.ReturnDamagedTargets.Clear();
            SetOrbKinematic(orb, true);
        }
    }

    private Vector3 GetReturnTarget()
    {
        return GetPlayerCenter();
    }

    private Vector3 GetPlayerCenter()
    {
        if (_ownerCapsule != null)
            return _ownerCapsule.bounds.center;
        if (Controller != null)
            return Controller.transform.position;

        return transform.position;
    }

    private void DespawnOrb(OrbRuntime orb)
    {
        if (orb == null)
            return;
        if (orb.Orb != null && Controller != null)
            Controller.DespawnServer(orb.Orb);

        orb.Orb = null;
        orb.OutboundDamagedTargets.Clear();
        orb.ReturnDamagedTargets.Clear();
    }

    private void DespawnAllOrbs()
    {
        for (int i = 0; i < _activeOrbs.Count; i++)
            DespawnOrb(_activeOrbs[i]);

        _activeOrbs.Clear();
        if (Controller != null)
            Controller.NotifyRuntimeStateChanged();
    }

    private void SetOrbResting(OrbRuntime orb)
    {
        orb.State = OrbState.Resting;
        SetOrbKinematic(orb, false);
    }

    private void SetOrbKinematic(OrbRuntime orb, bool kinematic)
    {
        if (orb.Body != null)
        {
            orb.Body.isKinematic = kinematic;
            orb.Body.useGravity = !kinematic;

            if (kinematic)
            {
                orb.Body.linearVelocity = Vector3.zero;
                orb.Body.angularVelocity = Vector3.zero;
            }
        }

        if (orb.HitCollider != null)
            orb.HitCollider.isTrigger = kinematic;
    }
}
