using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AIGameChat
{
    /// <summary>Режимы для проверки ротации без траты реального лимита (честно помечаются в логе и в интерфейсе).</summary>
    public enum ClientTestMode
    {
        None,
        SimulateRateLimitOnFirstModel, // первая модель «отвечает» 429, остальные — реальные запросы
        SimulateAllModelsDown,         // все модели «отвечают» 503, сеть не используется
        InvalidFirstModelId            // первая модель заменяется несуществующей — реальный запрос вернёт 404
    }

    public class OpenRouterResult
    {
        public bool success;
        public bool fatal;                 // true — ротация остановлена из-за ошибки конфигурации/сети
        public string text;                // «сырой» текст ответа модели
        public string model;               // какая модель ответила
        public string error;               // сообщение для игрока
        public List<string> failedAttempts = new List<string>(); // «модель: причина» для отладки
    }

    /// <summary>
    /// Отправляет историю в OpenRouter и перебирает модели из ModelRotator.
    /// Переключение на следующую модель: 429, 5xx, 404 (модель не найдена), 403, 408, таймаут, пустой ответ, ошибка в теле ответа.
    /// Остановка без переключения: 401 (ключ), 402 (кредиты/лимит аккаунта), 400 (некорректный запрос), нет сети.
    /// </summary>
    public class OpenRouterClient : MonoBehaviour
    {
        const string Url = "https://openrouter.ai/api/v1/chat/completions";

        [SerializeField] ModelRotator rotator;
        [SerializeField, Range(5, 120)] int timeoutSeconds = 30;
        [SerializeField, Range(0f, 1.5f)] float temperature = 0.8f;
        [SerializeField] int maxTokens = 600;
        [SerializeField] ClientTestMode testMode = ClientTestMode.None;

        public ClientTestMode TestMode
        {
            get { return testMode; }
            set { testMode = value; }
        }

        enum Outcome { Success, Rotate, Fatal }

        public IEnumerator Send(List<ChatMessage> messages, Action<OpenRouterResult> onDone)
        {
            var result = new OpenRouterResult();

            string key = OpenRouterConfig.GetApiKey();
            if (string.IsNullOrEmpty(key))
            {
                result.fatal = true;
                result.error = "API-ключ OpenRouter не задан. Откройте «Настройки» и вставьте ключ.";
                onDone(result);
                yield break;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                result.fatal = true;
                result.error = "Нет подключения к интернету.";
                onDone(result);
                yield break;
            }

            if (rotator == null)
            {
                result.fatal = true;
                result.error = "Не назначен ModelRotator в OpenRouterClient.";
                onDone(result);
                yield break;
            }

            OpenRouterConfig.FileData file = OpenRouterConfig.LoadFile();
            List<string> order = rotator.GetAttemptOrder(file != null ? file.models : null);
            if (order.Count == 0)
            {
                result.fatal = true;
                result.error = "Список моделей пуст. Добавьте модели в ModelRotator.";
                onDone(result);
                yield break;
            }

            // Один проход по списку: попыток не больше, чем моделей.
            for (int i = 0; i < order.Count; i++)
            {
                string model = order[i];

                if (testMode == ClientTestMode.InvalidFirstModelId && i == 0)
                {
                    model = "invalid-provider/does-not-exist:free";
                }
                if (testMode == ClientTestMode.SimulateRateLimitOnFirstModel && i == 0)
                {
                    NoteFailure(result, model, "ТЕСТ: имитация 429 (лимит)");
                    continue;
                }
                if (testMode == ClientTestMode.SimulateAllModelsDown)
                {
                    NoteFailure(result, model, "ТЕСТ: имитация 503 (недоступна)");
                    continue;
                }

                var payload = new ChatRequest
                {
                    model = model,
                    messages = messages,
                    temperature = temperature,
                    max_tokens = maxTokens
                };
                string body = JsonUtility.ToJson(payload);

                Outcome outcome;
                string detail;
                string text;
                string fatalMessage = null;

                using (var req = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", "Bearer " + key);
                    req.SetRequestHeader("X-Title", "AI GameChat");
                    req.timeout = timeoutSeconds;

                    yield return req.SendWebRequest();

                    outcome = Evaluate(req, out detail, out text);
                    if (outcome == Outcome.Fatal)
                    {
                        fatalMessage = BuildFatalMessage(req, detail);
                    }
                }

                if (outcome == Outcome.Success)
                {
                    rotator.MarkSuccess(model);
                    result.success = true;
                    result.text = text;
                    result.model = model;
                    onDone(result);
                    yield break;
                }

                if (outcome == Outcome.Fatal)
                {
                    Debug.LogWarning("[AIGameChat] " + model + " → " + detail);
                    result.fatal = true;
                    result.error = fatalMessage;
                    onDone(result);
                    yield break;
                }

                NoteFailure(result, model, detail);
            }

            result.error = "Все бесплатные модели сейчас недоступны (попыток: " + order.Count + "). Попробуйте позже.";
            onDone(result);
        }

        static void NoteFailure(OpenRouterResult result, string model, string reason)
        {
            result.failedAttempts.Add(model + ": " + reason);
            Debug.Log("[AIGameChat] Переключение с " + model + " — " + reason);
        }

        static Outcome Evaluate(UnityWebRequest req, out string detail, out string text)
        {
            text = null;
            detail = "";

            long code = req.responseCode;
            string raw = req.downloadHandler != null ? req.downloadHandler.text : "";
            string apiMessage = ExtractApiMessage(raw);

            switch (req.result)
            {
                case UnityWebRequest.Result.Success:
                    if (!string.IsNullOrEmpty(apiMessage))
                    {
                        detail = "ошибка в теле ответа: " + apiMessage;
                        return Outcome.Rotate;
                    }
                    text = ExtractContent(raw);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        detail = "пустой ответ";
                        return Outcome.Rotate;
                    }
                    return Outcome.Success;

                case UnityWebRequest.Result.ProtocolError:
                    detail = code + (string.IsNullOrEmpty(apiMessage) ? "" : " " + apiMessage);
                    if (code == 400 || code == 401 || code == 402)
                    {
                        if (code == 400)
                        {
                            Debug.LogWarning("[AIGameChat] 400, тело ответа: " + Truncate(raw, 500));
                            // Часть бесплатных моделей не принимает системный промпт или отдельные параметры —
                            // это проблема конкретной модели, а не запроса в целом, поэтому пробуем следующую.
                            if (IsModelSpecific400(apiMessage)) return Outcome.Rotate;
                        }
                        return Outcome.Fatal;
                    }
                    if (code == 403 || code == 404 || code == 408 || code == 429 || code >= 500)
                    {
                        return Outcome.Rotate;
                    }
                    return Outcome.Fatal;

                case UnityWebRequest.Result.ConnectionError:
                    if (req.error != null && req.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        detail = "таймаут";
                        return Outcome.Rotate;
                    }
                    detail = "нет соединения: " + req.error;
                    return Outcome.Fatal;

                default:
                    detail = "ошибка обработки ответа: " + req.error;
                    return Outcome.Rotate;
            }
        }

        static bool IsModelSpecific400(string apiMessage)
        {
            if (string.IsNullOrEmpty(apiMessage)) return false;
            string[] markers = { "developer instruction", "system message", "system prompt", "system role", "does not support", "not supported", "not enabled" };
            foreach (string m in markers)
            {
                if (apiMessage.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        static string BuildFatalMessage(UnityWebRequest req, string detail)
        {
            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                return "Нет соединения с сервером OpenRouter. Проверьте интернет.";
            }
            switch (req.responseCode)
            {
                case 401: return "Ошибка авторизации (401). Проверьте API-ключ в «Настройках».";
                case 402: return "Недостаточно кредитов или превышен лимит аккаунта OpenRouter (402).";
                case 400: return "Некорректный запрос (400): " + detail;
                default: return "Ошибка запроса: " + detail;
            }
        }

        static string ExtractApiMessage(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            try
            {
                ChatResponse r = JsonUtility.FromJson<ChatResponse>(raw);
                if (r != null && r.error != null && !string.IsNullOrEmpty(r.error.message)) return r.error.message;
            }
            catch (Exception) { }
            return "";
        }

        static string ExtractContent(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                ChatResponse r = JsonUtility.FromJson<ChatResponse>(raw);
                if (r != null && r.choices != null && r.choices.Count > 0 && r.choices[0].message != null)
                {
                    return r.choices[0].message.content;
                }
            }
            catch (Exception) { }
            return null;
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }
    }
}
