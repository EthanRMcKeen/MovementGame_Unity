using UnityEngine;
using System.Collections.Generic;

public class CollisionDetection : MonoBehaviour
{
    public MonoBehaviour combat;
    private HashSet<int> hitTargets = new HashSet<int>();
    private bool wasHitboxActive = false;
    public float blockingDamageReduction = 0.5f; 

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
                    defenderCombat.Parry();
                    attackerCombat.OnParried();
                    return;
                }
                else if (defenderCombat.IsParryable && attackerCombat.IsParrying)
                {
                    attackerCombat.Parry();
                    defenderCombat.OnParried();
                    return;
                }
            }
            
            float damage = defenderDamageable.IsBlocking ? attackerCombat.AttackDamage * blockingDamageReduction : attackerCombat.AttackDamage;
            defenderDamageable.TakeDamage(damage);
            Debug.Log("Hit detected for " + damage + " damage.");
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