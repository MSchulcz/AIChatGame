using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AIGameChat
{
    /// <summary>
    /// Связывает всё вместе: ввод игрока → история → OpenRouter (с ротацией) → разбор эмоции → портрет и текст.
    /// </summary>
    public class ChatController : MonoBehaviour
    {
        [SerializeField] ChatUI ui;
        [SerializeField] CharacterView characterView;
        [SerializeField] OpenRouterClient client;
        [SerializeField] SettingsPanel settings;
        [SerializeField] List<CharacterProfile> profiles = new List<CharacterProfile>();
        [SerializeField, Range(2, 40)] int historyLimit = 10;
        [SerializeField] bool showDebugInfo = true;

        ChatHistory history;
        CharacterProfile current;
        bool busy;

        void Start()
        {
            if (profiles == null || profiles.Count == 0)
            {
                Debug.LogError("[AIGameChat] В ChatController нет профилей персонажей.");
                return;
            }

            var names = new List<string>();
            foreach (CharacterProfile p in profiles) names.Add(p != null ? p.displayName : "—");

            int index = Mathf.Clamp(PlayerPrefs.GetInt(SettingsPanel.ProfileKey, 0), 0, profiles.Count - 1);
            string custom = PlayerPrefs.GetString(SettingsPanel.CustomKey, "");

            settings.Initialize(names, index, custom);
            settings.Applied += OnSettingsApplied;
            ui.Submitted += OnSubmitted;

            StartConversation(index, custom);

            if (string.IsNullOrEmpty(OpenRouterConfig.GetApiKey()))
            {
                ui.AddSystemNote("Нужен API-ключ OpenRouter: откройте «Настройки» и вставьте его.", true);
            }
        }

        void OnDestroy()
        {
            if (settings != null) settings.Applied -= OnSettingsApplied;
            if (ui != null) ui.Submitted -= OnSubmitted;
        }

        void OnSettingsApplied(int index, string custom)
        {
            if (busy)
            {
                ui.AddSystemNote("Дождитесь ответа персонажа — настройки применятся при следующем открытии.", false);
                return;
            }
            StartConversation(Mathf.Clamp(index, 0, profiles.Count - 1), custom);
        }

        /// <summary>Новый разговор: новый системный промпт, пустая история, приветствие.</summary>
        void StartConversation(int profileIndex, string custom)
        {
            current = profiles[profileIndex];
            history = new ChatHistory(PromptBuilder.Build(current, custom), historyLimit);

            ui.ClearMessages();
            characterView.SetProfile(current);
            ui.AddMessage(current.displayName, current.greeting, false);
        }

        void OnSubmitted(string text)
        {
            if (busy) return;
            StartCoroutine(Run(text));
        }

        IEnumerator Run(string playerText)
        {
            busy = true;
            ui.AddMessage("Вы", playerText, true);
            ui.SetBusy(true, current.displayName + " думает…");

            List<ChatMessage> request = history.BuildRequest(playerText);
            OpenRouterResult result = null;
            yield return client.Send(request, r => result = r);

            if (result != null && result.success)
            {
                ParsedReply reply = EmotionParser.Parse(result.text, playerText);
                if (string.IsNullOrWhiteSpace(reply.text))
                {
                    ui.AddSystemNote("Модель вернула пустую реплику. Попробуйте ещё раз.", true);
                }
                else
                {
                    characterView.SetEmotion(reply.emotion);
                    ui.AddMessage(current.displayName, reply.text, false, showDebugInfo ? BuildFootnote(result, reply) : null);
                    history.Commit(playerText, EmotionParser.ToJson(reply));
                }
            }
            else
            {
                string error = result != null && !string.IsNullOrEmpty(result.error) ? result.error : "Неизвестная ошибка.";
                if (showDebugInfo && result != null && result.failedAttempts.Count > 0)
                {
                    error += "\n(" + string.Join("; ", result.failedAttempts) + ")";
                }
                // Сообщение осталось на экране, но в историю не попало: можно просто отправить его ещё раз.
                ui.AddSystemNote(error + "\nСообщение не отправлено в историю — отправьте его ещё раз.", true);
            }

            ui.SetBusy(false);
            busy = false;
        }

        static string BuildFootnote(OpenRouterResult result, ParsedReply reply)
        {
            var sb = new StringBuilder();
            sb.Append(result.model);
            if (result.failedAttempts.Count > 0) sb.Append(" · переключений: ").Append(result.failedAttempts.Count);
            sb.Append(" · эмоция: ").Append(reply.emotion.ToString().ToLowerInvariant());
            sb.Append(reply.emotionFromModel ? " (от модели, " : " (по ключевым словам, ").Append(reply.method).Append(")");
            return sb.ToString();
        }
    }
}
