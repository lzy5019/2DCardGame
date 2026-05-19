using UnityEngine;

public class FinalPileBrowserEscapeCloser : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private void Update()
    {
        if (!Input.GetKeyDown(closeKey))
            return;

        if (PileBrowserUI.Instance == null)
            return;

        if (!PileBrowserUI.Instance.IsOpen)
            return;

        PileBrowserUI.Instance.ClosePile();
    }
}
