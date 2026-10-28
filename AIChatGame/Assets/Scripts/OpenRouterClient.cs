using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Отправляет запросы к OpenRouter Chat Completions и перебирает модели из ModelRotator,
/// если ошибка похожа на лимит или недоступность модели.
/// </summary>
public class OpenRouterClient : MonoBehaviour
{
    private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";
    private const string ConfigFileName = "openrouter_config.json";

    public ModelRotator rotator;

    [Range(0f, 2f)]
    public float temperature = 0.7f;

    [Tooltip("Таймаут одного HTTP-запроса, секунд")]
    [Min(5)]
    public int timeoutSeconds = 60;

    public bool IsBusy { get; private set; }

    private string apiKey;

    #region JSON DTO

    [Serializable]
    private class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public float temperature;
    }

    [Serializable]
    private class ChatResponse
    {
        public string model;
        public Choice[] choices;
        public ApiError error;
    }

    [Serializable]
    private class Choice
    {
        public ChatMessage message;
    }

    [Serializable]
    private class ApiError
    {
        public int code;
        public string message;
    }

    [Serializable]
    private class Config
    {
        public string apiKey;
    }

    #endregion

    private enum Outcome { Success, TryNextModel, Stop }

    private struct AttemptResult
    {
        public Outcome outcome;
        public string text;
        public string model;
        public string error;
    }

    private void Awake()
    {
        if (rotator == null)
            rotator = GetComponent<ModelRotator>();
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[OpenRouter] Не найден {path}. Скопируйте openrouter_config.example.json и вставьте свой ключ.");
            return;
        }

        try
        {
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(path));
            apiKey = config?.apiKey?.Trim();
        }
        catch (Exception e)
        {
            Debug.LogError($"[OpenRouter] Не удалось прочитать {ConfigFileName}: {e.Message}");
        }
    }

    public bool HasApiKey => !string.IsNullOrEmpty(apiKey) && !apiKey.StartsWith("ВСТАВЬТЕ");

    /// <summary>
    /// Один проход по списку моделей. onSuccess(текст, модель) или onError(сообщение для пользователя).
    /// onAttempt вызывается перед каждой попыткой, чтобы UI мог показать текущую модель.
    /// </summary>
    public IEnumerator SendWithRotation(
        List<ChatMessage> messages,
        Action<string, string> onSuccess,
        Action<string> onError,
        Action<string> onAttempt = null)
    {
        if (IsBusy)
        {
            onError?.Invoke("Запрос уже выполняется.");
            yield break;
        }

        if (!HasApiKey)
        {
            onError?.Invoke($"API-ключ не задан. Создайте Assets/StreamingAssets/{ConfigFileName} с полем \"apiKey\".");
            yield break;
        }

        List<string> order = rotator.GetAttemptOrder();
        if (order.Count == 0)
        {
            onError?.Invoke("Список моделей пуст. Добавьте модели в ModelRotator.");
            yield break;
        }

        IsBusy = true;
        var failures = new List<string>();

        try
        {
            for (int i = 0; i < order.Count; i++)
            {
                string model = order[i];
                onAttempt?.Invoke(model);

                AttemptResult result = default;
                if (rotator.simulate429OnFirstAttempt && i == 0)
                {
                    result = new AttemptResult
                    {
                        outcome = Outcome.TryNextModel,
                        model = model,
                        error = "HTTP 429 (имитация тестового режима)"
                    };
                }
                else
                {
                    yield return SendOnce(model, messages, r => result = r);
                }

                switch (result.outcome)
                {
                    case Outcome.Success:
                        rotator.MarkSuccess(model);
                        onSuccess?.Invoke(result.text, result.model ?? model);
                        yield break;

                    case Outcome.Stop:
                        Debug.LogWarning($"[OpenRouter] {model}: {result.error}. Ротация остановлена.");
                        onError?.Invoke(result.error);
                        yield break;

                    default:
                        Debug.LogWarning($"[OpenRouter] {model}: {result.error}. Пробую следующую модель.");
                        failures.Add($"{model}: {result.error}");
                        break;
                }
            }

            onError?.Invoke("Все бесплатные модели сейчас недоступны. Попробуйте позже.\n" +
                            string.Join("\n", failures));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IEnumerator SendOnce(string model, List<ChatMessage> messages, Action<AttemptResult> done)
    {
        var body = new ChatRequest { model = model, messages = messages, temperature = temperature };
        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("X-Title", "AIChatGame (Unity)");

            yield return request.SendWebRequest();

            done(Classify(model, request));
        }
    }

    private static AttemptResult Classify(string model, UnityWebRequest request)
    {
        long status = request.responseCode;
        string raw = request.downloadHandler?.text;
        ChatResponse response = TryParse(raw);
        string apiMessage = response?.error?.message;

        // Ответ не получен совсем: сеть или таймаут.
        if (request.result == UnityWebRequest.Result.ConnectionError || status == 0)
        {
            bool timeout = request.error != null &&
                           request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
            return timeout
                ? Fail(Outcome.TryNextModel, "превышено время ожидания ответа")
                : Fail(Outcome.Stop, "Нет соединения с OpenRouter. Проверьте интернет. (" + request.error + ")");
        }

        // OpenRouter может вернуть ошибку внутри тела даже с кодом 200.
        if (status >= 200 && status < 300 && response?.error != null && response.error.code != 0)
            status = response.error.code;

        if (status >= 200 && status < 300)
        {
            string text = response?.choices != null && response.choices.Length > 0
                ? response.choices[0].message?.content
                : null;

            if (string.IsNullOrWhiteSpace(text))
                return Fail(Outcome.TryNextModel, "модель вернула пустой ответ");

            return new AttemptResult { outcome = Outcome.Success, text = text.Trim(), model = response.model ?? model };
        }

        string details = $"HTTP {status}" + (string.IsNullOrEmpty(apiMessage) ? "" : $": {apiMessage}");
        Debug.Log($"[OpenRouter] {model} -> {details}\n{raw}");

        switch (status)
        {
            case 401:
                return Fail(Outcome.Stop, "Ошибка авторизации (401). Проверьте API-ключ OpenRouter.");
            case 402:
                return Fail(Outcome.Stop, "Недостаточно кредитов на аккаунте OpenRouter (402).");
            case 403:
                // Модерация или запрет для конкретной модели: другая модель может ответить.
                return Fail(Outcome.TryNextModel, details);
            case 400:
                // Неверный идентификатор модели — повод попробовать следующую; остальное — ошибка запроса.
                if (MentionsModel(apiMessage))
                    return Fail(Outcome.TryNextModel, details);
                return Fail(Outcome.Stop, "Некорректный запрос: " + details);
            case 404:
            case 408:
            case 429:
                return Fail(Outcome.TryNextModel, details);
            default:
                if (status >= 500)
                    return Fail(Outcome.TryNextModel, details);
                return Fail(Outcome.Stop, "Ошибка API: " + details);
        }
    }

    private static bool MentionsModel(string message)
    {
        return !string.IsNullOrEmpty(message) &&
               message.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ChatResponse TryParse(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return null;
        try
        {
            return JsonUtility.FromJson<ChatResponse>(raw);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static AttemptResult Fail(Outcome outcome, string error)
    {
        return new AttemptResult { outcome = outcome, error = error };
    }
}
