using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PlayerCam : MonoBehaviour
{
    public float sensX;
    public float sensY;

    public Transform orientation;
    public Transform camHolder;
    public Slider slider;

    float xRotation;
    float yRotation;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // lock cursror in middle of screen
        Cursor.visible = false;
        sensX = slider.value;
        sensY = slider.value;
    }

    private void Update()
    {
        // get mouse input
        float mouseX = Input.GetAxisRaw("Mouse X") * sensX * Time.fixedDeltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensY * Time.fixedDeltaTime;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //rotate cam and orientation
        camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    public void DoFov(float endVal)
    {
        GetComponent<Camera>().DOFieldOfView(endVal, .25f);
    }

    public void DoTilt(float zTilt)
    {
        transform.DOLocalRotate(new Vector3(0,0,zTilt), .25f);
    }

    public void AdjustSpeed(float val)
    {
        sensX = val;
        sensY = val;
    }
}
