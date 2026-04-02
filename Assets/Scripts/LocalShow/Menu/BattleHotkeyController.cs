using UnityEngine;

public class BattleHotkeyController : MonoBehaviour
{
    [SerializeField] private ShopSwitcher shopSwitcher;
    [SerializeField] private SettingManager settingManager;
    [SerializeField] private PileBrowserUI pileBrowserUI;
    [SerializeField] private PlayerEndTurn playerEndTurn;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingManager.IsOpen)
            {
                settingManager.CloseSetting();
                return;
            }

            if (pileBrowserUI.IsOpen)
            {
                pileBrowserUI.ClosePile();
                return;
            }

            settingManager.OpenSetting();
            return;
        }

        if (settingManager.IsOpen)
            return;

        if (pileBrowserUI.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                pileBrowserUI.TogglePile("Draw", pileBrowserUI.OpenDrawPile);
                return;
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                pileBrowserUI.TogglePile("Discard", pileBrowserUI.OpenDiscardPile);
                return;
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                pileBrowserUI.TogglePile("Played", pileBrowserUI.OpenPlayedPile);
                return;
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                pileBrowserUI.TogglePile("Owned", pileBrowserUI.OpenOwnedPile);
                return;
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
            shopSwitcher.SwitchShop();

        if (Input.GetKeyDown(KeyCode.Space))
            playerEndTurn.TryEndTurnByHotkey();

        if (Input.GetKeyDown(KeyCode.A))
        {
            pileBrowserUI.TogglePile("Draw", pileBrowserUI.OpenDrawPile);
            return;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            pileBrowserUI.TogglePile("Discard", pileBrowserUI.OpenDiscardPile);
            return;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            pileBrowserUI.TogglePile("Played", pileBrowserUI.OpenPlayedPile);
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            pileBrowserUI.TogglePile("Owned", pileBrowserUI.OpenOwnedPile);
            return;
        }
    }
}
