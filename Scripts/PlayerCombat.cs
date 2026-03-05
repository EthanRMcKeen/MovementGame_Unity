using UnityEngine.UI;
using UnityEngine;

public class PlayerCombat : MonoBehaviour, ICombat, IDamageable
{
    [Header("References")]
    private PlayerScript ps;
    public GameObject weapon;
    public MeshRenderer hitboxMesh;
    public Toggle debugMode;
    private CapsuleCollider weaponCollider;

    [Header("Attack Settings")]
    public float attackDamage = 25f;
    public float attackCooldown = 1.5f;
    public float parryDamage = 10f;
    public bool isParrying = false;
    
    private float cooldownTimer = 0f;

    [Header("Player Settings")]
    public float maxHealth = 100f;
    private float currentHealth;
    public float parryIFramesDuration = 0.5f;

    public bool isDamageable;
    private bool hitboxActive = false;
    private bool attackInProgress = false;

    private void Start()
    {
        ps = GetComponent<PlayerScript>();
        currentHealth = maxHealth;
        isDamageable = true;
        weaponCollider = weapon.GetComponent<CapsuleCollider>();
        weaponCollider.enabled = false;
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        HandleInput();
        HandleDebug();
    }

    private void HandleInput()
    {
        // Light attack input
        if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f && !attackInProgress)
        {
            Attack();
        }

        // Blocking (disabled while attacking)
        if (!attackInProgress)
            ps.blocking = Input.GetMouseButton(1);
    }

    private void HandleDebug()
    {
        if (!debugMode) return;

        hitboxMesh.enabled = debugMode.isOn && weaponCollider.enabled;
    }

    public void Parry()
    {
        isDamageable = false;
        ps.parrying = true;
        Invoke(nameof(ResetParry), parryIFramesDuration);
    }

    private void ResetParry()
    {
        isDamageable = true;
        isParrying = false;
        ps.parrying = false;
        //add parry effect here
    }

    private void Attack()
    {
        ps.lightAttacking = true;
        attackInProgress = true;

        cooldownTimer = attackCooldown;
        Invoke(nameof(CancelAttack), attackCooldown);
    }

    public void EnableHitbox()
    {
        weaponCollider.enabled = true;
        isParrying = true;
        hitboxActive = true;
    }

    public void DisableHitbox()
    {
        weaponCollider.enabled = false;
        isParrying = false;
        hitboxActive = false;
    }

    public bool IsHitboxActive()
    {
        return hitboxActive;
    }

    public void TakeDamage(float damage)
    {
        if (!isDamageable)
            return;
        currentHealth -= damage;
        if(debugMode.isOn)
            Debug.Log("Player took " + damage + " damage. Current health: " + currentHealth);
    }

    public void CancelAttack()
    {
        attackInProgress = false;
        ps.lightAttacking = false;

        weaponCollider.enabled = false;
        isParrying = false;
    }

    // ICombat
    public float AttackDamage => attackDamage;
    public bool IsParrying => isParrying;//whether player is attempting to parry
    public bool IsParryable => false;
    public bool IsBlocking => ps.blocking;

    public void OnParried()
    {
        //player cant be parried
        return;
    }
}