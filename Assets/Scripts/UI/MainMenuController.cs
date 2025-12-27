using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

namespace GameJam.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [System.Serializable]
        public struct LanguageTitle
        {
            public string languageName;
            [TextArea(1, 3)]
            public string titleText;
        }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Button startButton;

        [Header("Scene Settings")]
        [SerializeField] private string sceneToLoad = "SampleScene";

        [Header("Localization Settings")]
        [SerializeField] private List<LanguageTitle> localizedTitles;

        private void Start()
        {
            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged.AddListener(HandleLanguageChange);
                UpdateTitle(languageDropdown.value);
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(LoadGameScene);
            }
        }

        private void LoadGameScene()
        {
            SceneManager.LoadScene(sceneToLoad);
        }

        private void HandleLanguageChange(int index)
        {
            UpdateTitle(index);
        }

        private void UpdateTitle(int index)
        {
            if (titleText == null) return;

            if (index >= 0 && index < localizedTitles.Count)
            {
                titleText.text = localizedTitles[index].titleText;
            }
            else
            {
                Debug.LogWarning($"No localized title found for language index {index}");
            }
        }

        [ContextMenu("Sync Dropdown Options")]
        private void SyncDropdownOptions()
        {
            if (languageDropdown == null) return;

            languageDropdown.ClearOptions();
            List<string> options = new List<string>();
            foreach (var item in localizedTitles)
            {
                options.Add(item.languageName);
            }
            languageDropdown.AddOptions(options);
        }
    }
}
