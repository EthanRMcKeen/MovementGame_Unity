using UnityEngine;

public class DamageBoostItem : ItemBase
{
    [SerializeField] private float _damageBonusPercent = 0.1f;

    public override void ApplyBuff()
    {
        var combatController = Controller.GetComponent<CombatController>();
        if (combatController != null)
        {
            combatController.AddPrimaryFireDamageBonus(_damageBonusPercent);
        }
    }

    public override void RemoveBuff()
    {
        var combatController = Controller.GetComponent<CombatController>();
        if (combatController != null)
        {
            combatController.RemovePrimaryFireDamageBonus(_damageBonusPercent);
        }
    }
}