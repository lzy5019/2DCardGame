using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class testCS : MonoBehaviour
{
    public Text debugText;
    public PlayerEndTurn endTurn;

    private void Update()
    {
        if (endTurn.localPlayer == null)
        {
            debugText.text = "null";
            return;
        }

        debugText.text = "normal";
    }
}
