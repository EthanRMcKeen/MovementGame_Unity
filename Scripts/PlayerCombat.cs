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
    private bool bufferedAttackInput = false;

    [Header("Combo Settings")]
    public float comboResetTime = 2f;
    public int comboStep = 0;
    private float lastAttackTime = 0f;

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
        
        // Reset combo if too much time has passed
        if (Time.time - lastAttackTime > comboResetTime && comboStep > 0)
        {
            comboStep = 0;
            ps.currentComboStep = 0;
        }

        HandleInput();
        HandleDebug();
    }

    private void HandleInput()
    {
        // Light attack input
        if (Input.GetMouseButtonDown(0))
        {
            if (cooldownTimer <= 0f)
            {
                Attack();
            }
            else if (attackInProgress)
            {
                // Buffer the attack input while attacking
                bufferedAttackInput = true;
            }
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
        // Update combo step
        comboStep++;
        if (comboStep > 3)
            comboStep = 1;
        
        lastAttackTime = Time.time;
        ps.lightAttacking = true;
        ps.currentComboStep = comboStep;
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

        // Check if player buffered an attack input
        if (bufferedAttackInput)
        {
            bufferedAttackInput = false;
            Attack();
        }
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