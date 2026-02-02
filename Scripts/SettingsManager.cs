using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    public Slider sensitivitySlider;
    public TextMeshProUGUI sensText;
    
    private void Start()
    {
        sensText.text = "Sensitivity: " + sensitivitySlider.value.ToString();

        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            sensText.text = "Sensitivity: " + value.ToString();
        });
    }
}
