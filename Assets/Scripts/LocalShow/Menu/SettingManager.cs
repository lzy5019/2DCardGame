using UnityEngine;

public class SettingManager : MonoBehaviour
{
    public GameObject settingCanvas;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingCanvas.activeSelf)
            {
                CloseSetting();
            }
            else
            {
                OpenSetting();
            }
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