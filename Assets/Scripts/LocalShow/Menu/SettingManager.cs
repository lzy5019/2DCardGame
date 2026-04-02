using UnityEngine;

public class SettingManager : MonoBehaviour
{
    public GameObject settingCanvas;
    public bool IsOpen
    {
        get
        {
            return settingCanvas != null && settingCanvas.gameObject.activeSelf;
        }
    }

    public void OpenSetting()
    {
        settingCanvas.SetActive(true);
    }

    public void CloseSetting()
    {
        settingCanvas.SetActive(false);
    }
}