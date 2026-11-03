using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AIGameChat
{
    /// <summary>
    /// Окно настроек: выбор готового персонажа, свой характер и API-ключ.
    /// Выбор и текст характера сохраняются в PlayerPrefs. Ключ тоже (только для локального запуска, см. README).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public const string ProfileKey = "agc_profile";
        public const string CustomKey = "agc_custom";

        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_Dropdown profileDropdown;
        [SerializeField] TMP_InputField customInput;
        [SerializeField] TMP_InputField apiKeyInput;
        [SerializeField] UnityEngine.UI.Button openButton;
        [SerializeField] UnityEngine.UI.Button closeButton;
        [SerializeField] UnityEngine.UI.Button applyButton;
        [SerializeField] TMP_Text hintText;

        /// <summary>Игрок нажал «Применить»: индекс профиля и свой характер (может быть пустым).</summary>
        public event Action<int, string> Applied;

        void Awake()
        {
            openButton.onClick.AddListener(Show);
            closeButton.onClick.AddListener(Hide);
            applyButton.onClick.AddListener(Apply);
            customInput.characterLimit = PromptBuilder.MaxCustomLength;
            panelRoot.SetActive(false);
        }

        public void Initialize(List<string> profileNames, int selected, string custom)
        {
            profileDropdown.ClearOptions();
            profileDropdown.AddOptions(profileNames);
            profileDropdown.SetValueWithoutNotify(selected);
            profileDropdown.RefreshShownValue();
            customInput.text = custom ?? "";
            RefreshHint();
        }

        public void Show()
        {
            RefreshHint();
            panelRoot.SetActive(true);
        }

        public void Hide()
        {
            panelRoot.SetActive(false);
        }

        void Apply()
        {
            string key = apiKeyInput.text.Trim();
            if (key.Length > 0)
            {
                PlayerPrefs.SetString(OpenRouterConfig.ApiKeyPref, key);
                apiKeyInput.text = "";
            }

            int index = profileDropdown.value;
            string custom = customInput.text.Trim();
            PlayerPrefs.SetInt(ProfileKey, index);
            PlayerPrefs.SetString(CustomKey, custom);
            PlayerPrefs.Save();

            Hide();
            if (Applied != null) Applied(index, custom);
        }

        void RefreshHint()
        {
            if (hintText != null)
            {
                hintText.text = "API-ключ: " + OpenRouterConfig.DescribeKeySource()
                    + ". Новый ключ вводится в поле ниже. После применения настроек диалог начнётся заново.";
            }
        }
    }
}
