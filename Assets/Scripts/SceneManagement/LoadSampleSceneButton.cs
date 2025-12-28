// File: UI/LoadSampleSceneButton.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LoadSampleSceneButton : MonoBehaviour
{
    [SerializeField] private string sceneName = "SampleScene";

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(LoadScene);
    }

    void OnDestroy()
    {
        GetComponent<Button>().onClick.RemoveListener(LoadScene);
    }

    private void LoadScene()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }
}
