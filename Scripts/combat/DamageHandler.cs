using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DamageHandler : NetworkBehaviour, IDamageable, IHealth
{
    [Header("Health")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private bool _autoRespawnOnDeath = true;
    [SerializeField] private float _respawnDelaySeconds = 2f;
    [SerializeField] private bool _debugLogs = true;

    public readonly SyncVar<int> CurrentHealthSync = new SyncVar<int>();
    public readonly SyncVar<bool> IsDeadSync = new SyncVar<bool>();

    public int CurrentHealth { get { return CurrentHealthSync.Value; } }
    public int MaxHealth { get { return _maxHealth; } }
    public bool IsDead { get { return IsDeadSync.Value; } }
    public IHealth Health { get { return this; } }
    public bool CanTakeDamage { get { return IsServerInitialized && !IsDeadSync.Value; } }

    private Coroutine _respawnRoutine;
    private CombatDefense _combatDefense;

    private void Awake()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        _respawnDelaySeconds = Mathf.Max(0f, _respawnDelaySeconds);
        _combatDefense = GetComponent<CombatDefense>();
        CurrentHealthSync.OnChange += CurrentHealth_OnChange;
        IsDeadSync.OnChange += IsDead_OnChange;
    }

    private void OnDestroy()
    {
        CurrentHealthSync.OnChange -= CurrentHealth_OnChange;
        IsDeadSync.OnChange -= IsDead_OnChange;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        CurrentHealthSync.Value = _maxHealth;
        IsDeadSync.Value = false;
    }

    public void ServerReceiveDamage(DamageRequest damage)
    {
        if (!CanTakeDamage)
            return;

        PlayerStyleController attackerStyle = damage.Attacker != null ? damage.Attacker.GetComponent<PlayerStyleController>() : null;

        if (_combatDefense != null)
        {
            DamageRequest resolvedDamage = damage;
            CombatDefenseOutcome outcome = _combatDefense.ResolveIncomingDamage(ref resolvedDamage);
            if (outcome == CombatDefenseOutcome.Ignored)
                return;
            if (outcome == CombatDefenseOutcome.Parried)
            {
                if (attackerStyle != null)
                    attackerStyle.ServerNotifyParry(resolvedDamage, this);

                return;
            }

            damage = resolvedDamage;
        }

        int amount = Mathf.Max(0, damage.Amount);
        if (amount == 0)
            return;

        int nextHealth = Mathf.Max(0, CurrentHealthSync.Value - amount);
        CurrentHealthSync.Value = nextHealth;
        bool killed = nextHealth <= 0;

        if (_debugLogs)
        {
            string attackerName = damage.Attacker != null ? damage.Attacker.name : "Unknown";
            Debug.Log($"{name} took {amount} {damage.DamageType} damage from {attackerName}. HP {CurrentHealthSync.Value}/{_maxHealth}");
        }

        if (attackerStyle != null)
            attackerStyle.ServerNotifyResolvedHit(damage, this, amount, killed);

        PlayerStyleController victimStyle = GetComponent<PlayerStyleController>();
        if (victimStyle != null)
        {
            bool heavyHit = killed || amount >= Mathf.CeilToInt(_maxHealth * victimStyle.HeavyHitHealthRatio);
            victimStyle.ServerNotifyOwnerDamaged(amount, heavyHit, killed);
        }

        if (nextHealth > 0)
            return;

        IsDeadSync.Value = true;

        if (_debugLogs)
        {
            string attackerName = damage.Attacker != null ? damage.Attacker.name : "Unknown";
            Debug.Log($"{name} was eliminated by {attackerName}.");
        }

        if (_autoRespawnOnDeath)
        {
            if (_respawnRoutine != null)
                StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(ServerRespawnAfterDelay());
        }
    }

    public void ServerApplyDamage(int amount, NetworkObject attacker)
    {
        ServerReceiveDamage(new DamageRequest(amount, attacker, damageType: "Legacy"));
    }

    public void ServerHeal(int amount)
    {
        if (!IsServerInitialized || IsDeadSync.Value)
            return;

        int healAmount = Mathf.Max(0, amount);
        if (healAmount == 0)
            return;

        CurrentHealthSync.Value = Mathf.Min(_maxHealth, CurrentHealthSync.Value + healAmount);
    }

    public void ServerRestoreFullHealth()
    {
        if (!IsServerInitialized)
            return;

        CurrentHealthSync.Value = _maxHealth;
        IsDeadSync.Value = false;
    }

    private IEnumerator ServerRespawnAfterDelay()
    {
        if (_respawnDelaySeconds > 0f)
            yield return new WaitForSeconds(_respawnDelaySeconds);

        if (!IsServerInitialized)
            yield break;

        CurrentHealthSync.Value = _maxHealth;
        IsDeadSync.Value = false;
        _respawnRoutine = null;
    }

    private void CurrentHealth_OnChange(int previous, int next, bool asServer)
    {
        if (asServer || !IsOwner || !_debugLogs)
            return;

        Debug.Log($"Health: {next}/{_maxHealth}");
    }

    private void IsDead_OnChange(bool previous, bool next, bool asServer)
    {
        if (asServer || !IsOwner || !_debugLogs)
            return;

        Debug.Log(next ? "You died." : "You respawned.");
    }
}
