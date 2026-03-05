using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    public PlayerCombat playerCombat;

    public void EnableHitbox()
    {
        playerCombat.EnableHitbox();
    }

    public void DisableHitbox()
    {
        playerCombat.DisableHitbox();
    }
}
