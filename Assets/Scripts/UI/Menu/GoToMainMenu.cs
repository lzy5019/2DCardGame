using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToMainMenu : MonoBehaviour
{

    void Start()
    {
        SceneManager.LoadScene("MainMenu");
    }

}
