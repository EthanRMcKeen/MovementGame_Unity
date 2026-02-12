using UnityEngine;
using System.Collections.Generic;

public class CollisionDetection : MonoBehaviour
{
    public MonoBehaviour combat;
    private HashSet<int> hitTargets = new HashSet<int>();
    private bool wasHitboxActive = false;

    private void OnTriggerEnter(Collider other)
    {
        var attackerCombat = combat as ICombat;
        if (attackerCombat == null)
            return;

        if (!attackerCombat.IsHitboxActive())
            return;

        if (other.TryGetComponent(out IDamageable defenderDamageable))
        {
            // prevent multiple hits on the same defender during one active hitbox window
            int defenderId = other.gameObject.GetInstanceID();
            if (hitTargets.Contains(defenderId))
                return;
            hitTargets.Add(defenderId);

            if (other.TryGetComponent(out ICombat defenderCombat))
            {
                if (defenderCombat.IsParrying && attackerCombat.IsParryable)
                {
                    attackerCombat.OnParried();
                    return;
                }
                else if (defenderCombat.IsParryable && attackerCombat.IsParrying)
                {
                    defenderCombat.OnParried();
                    return;
                }
            }
            
            defenderDamageable.TakeDamage(attackerCombat.AttackDamage);
            //Debug.Log("Hit detected for " + attackerCombat.AttackDamage + " damage.");
        }
    }

    private void Update()
    {
        var attackerCombat = combat as ICombat;
        if (attackerCombat == null)
            return;

        bool active = attackerCombat.IsHitboxActive();
        // clear tracked hits when the hitbox deactivates so the same targets can be hit by the next attack
        if (wasHitboxActive && !active)
        {
            hitTargets.Clear();
        }
        wasHitboxActive = active;
    }
}