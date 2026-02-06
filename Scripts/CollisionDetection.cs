using UnityEngine;

public class CollisionDetection : MonoBehaviour
{
    public Combat combat;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") && combat.isAttacking)
        {
            Debug.Log("Hit " + other.name);
            other.GetComponent<Animator>().SetTrigger("isHit");
            //add particle effects
            //deal damage
        }
    }
}
