using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Поле ввода, кнопки, область сообщений и статус запроса.
/// Связывает ChatHistory и OpenRouterClient.
/// </summary>
public class ChatUI : MonoBehaviour
{
    [Header("Логика")]
    public ChatHistory history;
    public OpenRouterClient client;

    [Header("UI")]
    public TMP_InputField inputField;
    public Button sendButton;
    public Button clearButton;
    public ScrollRect scrollRect;
    public RectTransform messagesContainer;
    public ChatMessageView messageTemplate;
    public TMP_Text statusText;

    [Tooltip("Показывать в заголовке ответа модель, которая ответила")]
    public bool showModelName = true;

    private void Awake()
    {
        if (history == null) history = GetComponent<ChatHistory>();
        if (client == null) client = GetComponent<OpenRouterClient>();
        if (messageTemplate != null) messageTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        sendButton.onClick.AddListener(OnSendClicked);
        inputField.onSubmit.AddListener(OnSubmit);
        if (clearButton != null) clearButton.onClick.AddListener(OnClearClicked);
    }

    private void OnDisable()
    {
        sendButton.onClick.RemoveListener(OnSendClicked);
        inputField.onSubmit.RemoveListener(OnSubmit);
        if (clearButton != null) clearButton.onClick.RemoveListener(OnClearClicked);
    }

    private void Start()
    {
        SetStatus(client.HasApiKey
            ? "Готово. Введите сообщение."
            : "API-ключ не найден — см. README.");
        FocusInput();
    }

    private void OnSubmit(string _) => OnSendClicked();

    private void OnSendClicked()
    {
        if (client.IsBusy)
            return;

        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            FocusInput();
            return;
        }

        AddMessage(ChatMessageView.Kind.User, text);
        inputField.text = string.Empty;
        StartCoroutine(SendRoutine(text));
    }

    private IEnumerator SendRoutine(string userText)
    {
        SetBusy(true);
        SetStatus("Модель отвечает…");

        var messages = history.BuildRequestMessages(userText);
        string reply = null, replyModel = null, error = null;

        yield return client.SendWithRotation(
            messages,
            (text, model) => { reply = text; replyModel = model; },
            err => error = err,
            model => SetStatus($"Модель отвечает… ({model})"));

        if (reply != null)
        {
            history.CommitExchange(userText, reply);
            AddMessage(ChatMessageView.Kind.Assistant, reply, showModelName ? replyModel : null);
            SetStatus($"Ответила модель: {replyModel}");
        }
        else
        {
            // Вопрос не попал в историю; возвращаем его в поле ввода, чтобы можно было отправить повторно.
            AddMessage(ChatMessageView.Kind.Error, error ?? "Неизвестная ошибка.");
            if (string.IsNullOrEmpty(inputField.text))
                inputField.text = userText;
            SetStatus("Ответ не получен.");
        }

        SetBusy(false);
        FocusInput();
    }

    private void OnClearClicked()
    {
        if (client.IsBusy)
            return;

        history.Clear();
        for (int i = messagesContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = messagesContainer.GetChild(i);
            if (messageTemplate == null || child != messageTemplate.transform)
                Destroy(child.gameObject);
        }
        SetStatus("История очищена.");
        FocusInput();
    }

    private void AddMessage(ChatMessageView.Kind kind, string text, string model = null)
    {
        ChatMessageView view = Instantiate(messageTemplate, messagesContainer);
        view.gameObject.SetActive(true);
        view.Setup(kind, text, model);
        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(messagesContainer);
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void SetBusy(bool busy)
    {
        sendButton.interactable = !busy;
        if (clearButton != null) clearButton.interactable = !busy;
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    private void FocusInput()
    {
        inputField.ActivateInputField();
    }
}
