using UnityEngine;

public class AvatarOrientaion : MonoBehaviour
{
    public Rigidbody playerRb;
    public Transform orientation;
    public bool faceMovementDirection = false;

    void Update()
    {
        if (faceMovementDirection)
        {
            transform.forward = new Vector3(playerRb.linearVelocity.x, 0, playerRb.linearVelocity.z).normalized;
        }
        else
        {
            transform.forward = new Vector3(orientation.forward.x, 0, orientation.forward.z).normalized;
        }
    }
}
