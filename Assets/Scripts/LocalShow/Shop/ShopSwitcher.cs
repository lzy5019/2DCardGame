using UnityEngine;

public class ShopSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject baseShopPanel;
    [SerializeField] private GameObject centerShopPanel;

    private bool isShowingBase = false;

    private void Start()
    {
        centerShopPanel.SetActive(true);
        baseShopPanel.SetActive(false);
        isShowingBase = false;
    }

    public void SwitchShop()
    {
        isShowingBase = !isShowingBase;

        baseShopPanel.SetActive(isShowingBase);
        centerShopPanel.SetActive(!isShowingBase);
    }
}