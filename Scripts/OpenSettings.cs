using UnityEngine;

public class OpenSettings : MonoBehaviour
{
    public GameObject setting;
    public bool isOpen = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isOpen)
                Pause();
            else
                Resume();
        }
    }

    public void Pause()
    {
        setting.SetActive(true);
        isOpen = true;
        this.GetComponent<PlayerCam>().enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        setting.SetActive(false);
        isOpen = false;
        this.GetComponent<PlayerCam>().enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
