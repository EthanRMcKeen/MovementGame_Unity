using UnityEngine;
using System.Collections;

public class Combat : MonoBehaviour
{
    [Header("References")]
    private PlayerMovementAdv pm;
    public GameObject weapon;

    public bool canAttack = true;
    public float attackCooldown = 0.5f;
    public bool isAttacking = false;

    private void Start()
    {
        pm = GetComponent<PlayerMovementAdv>();
    }

    private void Update()
    {
        // light attacking
        if (Input.GetMouseButtonDown(0) && canAttack)
        {
            Attack();
        }
    }

    public void Attack()
    {
        isAttacking = true;
        canAttack = false;
        pm.lightAttacking = true;
        StartCoroutine(ResetAttackCooldown());
    }

    private IEnumerator ResetAttackCooldown()
    {
        StartCoroutine(ResetAttackBool());
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
        pm.lightAttacking = false;
    }

    private IEnumerator ResetAttackBool()
    {
        yield return new WaitForSeconds(1.0f);//length of attack animation
        isAttacking = false;
    }
}
