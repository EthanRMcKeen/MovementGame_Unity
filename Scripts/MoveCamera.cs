using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    public Transform cameraPosition;
    public Transform orientation;
    public bool thirdPersonView;

    private void Update()
    {
        if(!thirdPersonView)
        {
            transform.position = cameraPosition.position;
        }
        else
        {
            transform.position = cameraPosition.position - orientation.forward * 4f + Vector3.up * 1f;
        }
    }
}
