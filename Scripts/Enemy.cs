using UnityEngine.UI;
using UnityEngine;

public class Enemy : MonoBehaviour, ICombat, IDamageable
{
    [Header("References")]
    private Animator anim;
    private BoxCollider hitbox;
    public AnimationClip attackAnimation;
    public GameObject attackHitbox;
    public Toggle debugMode;
    public GameObject parryEffect;
    public GameObject hitEffect;

    [Header("Settings")]
    public float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    [Header("Attack")]
    public float attackDamage = 10f;
    public float attackCooldown = 5f;
    private float attackAnimationDuration;
    public float attackStartupTime = 0.5f;
    public float attackHitboxDuration = 0.5f;
    
    private float animationTimer = 0f;
    private float attackTimer = 0f;

    public bool isParryable = false;

    private void Start()
    {
        currentHealth = maxHealth;
        anim = GetComponent<Animator>();
        hitbox = GetComponent<BoxCollider>();
        attackAnimationDuration = attackAnimation.length;
    }

    private void Update()
    {
        if (isDead)
            return;
        //attack every attackCooldown seconds
        attackTimer -= Time.deltaTime;
        animationTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            Attack();
        }

        if (debugMode.isOn)
        {
            // DEBUG: show hitbox while active
            if (IsHitboxActive())
                attackHitbox.GetComponent<MeshRenderer>().enabled = true;
            else
                attackHitbox.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (debugMode.isOn)
            Debug.Log("Enemy took " + damage + " damage. Current health: " + currentHealth);

        anim.SetTrigger("isHit");
        Instantiate(hitEffect, transform.position + Vector3.up * 1.4f, transform.rotation);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        anim.SetBool("isDead", true);
        hitbox.enabled = false;
        isDead = true;
        //Destroy(gameObject, 2f); // delete after 2 seconds
    }

    private void Attack()
    {
        isParryable = true;
        anim.SetTrigger("isAttacking");
        animationTimer = attackAnimationDuration;
        attackTimer = attackCooldown;
    }

    public bool IsHitboxActive()
    {
        float timeSinceAttackStart = attackAnimationDuration - animationTimer;
        if (timeSinceAttackStart >= attackStartupTime && timeSinceAttackStart < attackStartupTime + attackHitboxDuration)
        {
            attackHitbox.GetComponent<SphereCollider>().enabled = true;
            
            isParryable = false;
            return true;
        }
        attackHitbox.GetComponent<SphereCollider>().enabled = false;
        return false;
    }

    private void CancelAttack()
    {
        //anim.ResetTrigger("isAttacking");
        animationTimer = 0f;
        isParryable = false;
    }

    // ICombat
    public float AttackDamage => attackDamage;
    public bool IsParrying => false;
    public bool IsParryable => isParryable;
    public bool IsBlocking => false; // Enemy does not block (might change in future for different enemy types)

    public void OnParried()
    {
        // Enemy gets staggered when parried
        anim.SetTrigger("isHit");
        CancelAttack();
        if (debugMode.isOn)
            Debug.Log("Enemy was parried!");
        //particle effect
        Instantiate(parryEffect, attackHitbox.transform.position, attackHitbox.transform.rotation);
    }

    public void Parry()
    {
        // Enemy does not parry (might change in future for different enemy types)
        return;
    }

}
