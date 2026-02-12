using UnityEngine.UI;
using UnityEngine;

public class PlayerCombat : MonoBehaviour, ICombat, IDamageable
{
    [Header("References")]
    private PlayerMovementAdv pm;
    public GameObject weapon;
    public MeshRenderer hitboxMesh;
    public AnimationClip attackAnimation;
    public Toggle debugMode;

    [Header("Attack Settings")]
    public float attackDamage = 25f;
    public float attackCooldown = 1.5f;
    private float attackAnimationDuration;
    public float attackStartupTime = 0.5f;
    public float attackHitboxDuration = 0.5f;
    public float parryDamage = 10f;
    public bool isParrying = false;
    
    private float cooldownTimer = 0f;
    private float animationTimer = 0f;

    [Header("Player Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        pm = GetComponent<PlayerMovementAdv>();
        attackAnimationDuration = attackAnimation.length;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        // Tick down timers
        cooldownTimer -= Time.deltaTime;
        animationTimer -= Time.deltaTime;

        // Light attacking
        if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0)
        {
            Attack();
        }

        // Reset attack state after animation duration
        if (animationTimer <= 0 && pm.lightAttacking)
        {
            pm.lightAttacking = false;
        }

        if (debugMode.isOn)
        {
            // DEBUG: show hitbox while active
            if (IsHitboxActive())
                hitboxMesh.enabled = true;
            else
                hitboxMesh.enabled = false;
        }
    }

    private void Attack()
    {
        pm.lightAttacking = true;
        cooldownTimer = attackCooldown;
        animationTimer = attackAnimationDuration;
    }

    public bool IsHitboxActive()
    {
        float timeSinceAttackStart = attackAnimationDuration - animationTimer;
        if (timeSinceAttackStart >= attackStartupTime && timeSinceAttackStart < attackStartupTime + attackHitboxDuration)
        {
            weapon.GetComponent<CapsuleCollider>().enabled = true;
            isParrying = true;
            return true;
        }
        weapon.GetComponent<CapsuleCollider>().enabled = false;
        isParrying = false;
        return false;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if(debugMode.isOn)
            Debug.Log("Player took " + damage + " damage. Current health: " + currentHealth);
    }

    public void CancelAttack()
    {
        pm.lightAttacking = false;
        cooldownTimer = 0f;
        animationTimer = 0f;
    }

    // ICombat
    public float AttackDamage => attackDamage;
    public bool IsParrying => isParrying;
    public bool IsParryable => false;

    public void OnParried()
    {
        //player cant be parried
        return;
    }
}