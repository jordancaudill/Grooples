using UnityEngine;

public class UIController : MonoBehaviour
{

    public GameObject levelPanel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ShowLevelPanel()
    {
        levelPanel.SetActive(true);
    }

    public void HideLevelPanel()
    {
        levelPanel.SetActive(false);
    }

    public void LoadScene(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }


    public void ExitGame()
    {
#if UNITY_STANDALONE
        Application.Quit();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

}
