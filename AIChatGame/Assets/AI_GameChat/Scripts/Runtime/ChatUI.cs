using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace AIGameChat
{
    /// <summary>
    /// Правая половина экрана: лента сообщений, статус и строка ввода.
    /// Только отображение — логика запросов находится в ChatController.
    /// Пузырьки сообщений создаются из кода, отдельный префаб не нужен.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;
        [SerializeField] UnityEngine.UI.Button sendButton;
        [SerializeField] TMP_Text statusText;
        [SerializeField] UnityEngine.UI.ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] TMP_FontAsset font;

        [Header("Оформление")]
        [SerializeField] Color playerBubble = new Color(0.18f, 0.34f, 0.60f, 1f);
        [SerializeField] Color characterBubble = new Color(0.21f, 0.21f, 0.27f, 1f);
        [SerializeField] int messageFontSize = 28;

        bool busy;

        /// <summary>Игрок отправил непустое сообщение.</summary>
        public event Action<string> Submitted;

        void Awake()
        {
            sendButton.onClick.AddListener(Submit);
            input.onSubmit.AddListener(delegate { Submit(); });
            SetStatus("");
        }

        void Submit()
        {
            if (busy) return; // защита от повторной отправки, пока идёт запрос

            string text = input.text.Trim();
            if (text.Length == 0)
            {
                SetStatus("Введите сообщение.");
                input.ActivateInputField();
                return;
            }

            input.text = "";
            busy = true;
            sendButton.interactable = false;
            input.interactable = false;
            if (Submitted != null) Submitted(text);
        }

        public void SetBusy(bool value, string status = "")
        {
            busy = value;
            sendButton.interactable = !value;
            input.interactable = !value;
            SetStatus(value ? status : "");
            if (!value) input.ActivateInputField();
        }

        public void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        public void ClearMessages()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        /// <summary>Пузырёк сообщения. footnote — мелкая отладочная подпись (например, какая модель ответила).</summary>
        public void AddMessage(string speaker, string text, bool fromPlayer, string footnote = null)
        {
            var row = new GameObject(fromPlayer ? "PlayerRow" : "CharacterRow", typeof(RectTransform));
            row.transform.SetParent(content, false);

            var rowLayout = row.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(fromPlayer ? 110 : 0, fromPlayer ? 0 : 110, 0, 0);
            ConfigureLayout(rowLayout);

            var bubble = new GameObject("Bubble", typeof(RectTransform));
            bubble.transform.SetParent(row.transform, false);
            var image = bubble.AddComponent<UnityEngine.UI.Image>();
            image.color = fromPlayer ? playerBubble : characterBubble;

            var bubbleLayout = bubble.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(22, 22, 14, 14);
            bubbleLayout.spacing = 4f;
            ConfigureLayout(bubbleLayout);

            MakeText(bubble.transform, speaker, messageFontSize - 6, new Color(1f, 1f, 1f, 0.6f), FontStyles.Bold);
            MakeText(bubble.transform, text, messageFontSize, Color.white, FontStyles.Normal);
            if (!string.IsNullOrEmpty(footnote))
            {
                MakeText(bubble.transform, footnote, messageFontSize - 10, new Color(1f, 1f, 1f, 0.45f), FontStyles.Italic);
            }

            ScrollToBottom();
        }

        /// <summary>Служебная строка по центру: ошибки, подсказки.</summary>
        public void AddSystemNote(string text, bool isError = false)
        {
            var row = new GameObject("SystemNote", typeof(RectTransform));
            row.transform.SetParent(content, false);
            var layout = row.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 4, 4);
            ConfigureLayout(layout);

            Color color = isError ? new Color(1f, 0.55f, 0.5f, 1f) : new Color(1f, 1f, 1f, 0.6f);
            TMP_Text t = MakeText(row.transform, text, messageFontSize - 6, color, FontStyles.Italic);
            t.alignment = TextAlignmentOptions.Center;

            ScrollToBottom();
        }

        static void ConfigureLayout(UnityEngine.UI.VerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        TMP_Text MakeText(Transform parent, string text, int size, Color color, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
#if UNITY_6000_0_OR_NEWER
            t.textWrappingMode = TextWrappingModes.Normal;
#else
            t.enableWordWrapping = true;
#endif
            return t;
        }

        void ScrollToBottom()
        {
            if (isActiveAndEnabled) StartCoroutine(ScrollToBottomRoutine());
        }

        IEnumerator ScrollToBottomRoutine()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 0f;
        }
    }
}
