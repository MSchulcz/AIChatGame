using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Упорядоченный список бесплатных моделей OpenRouter.
/// Каждая отправка начинается с последней модели, которая успешно ответила,
/// и делает не больше одного прохода по списку.
/// </summary>
public class ModelRotator : MonoBehaviour
{
    [Tooltip("Точные бесплатные model slug из каталога OpenRouter (https://openrouter.ai/models?max_price=0)")]
    public List<string> models = new List<string>
    {
        "nvidia/nemotron-3.5-super-vl:free",
        "dots-studio/dots-3-note-preview:free",
        "poolside/laguna-s-2.1:free",
        "recursive/flint-v1.0:free",
        "nvidia/nemotron-3-ultra-550b-a55b:free",
        "google/gemma-4-31b-it:free"
    };

    [Header("Тестовый режим")]
    [Tooltip("Имитировать HTTP 429 для первой модели в каждом проходе, чтобы проверить ротацию без расхода лимита")]
    public bool simulate429OnFirstAttempt = false;

    private int currentIndex;

    public int MaxAttempts => models.Count;

    public string CurrentModel => models.Count > 0 ? models[currentIndex % models.Count] : null;

    /// <summary>Порядок моделей для одного прохода, начиная с текущей.</summary>
    public List<string> GetAttemptOrder()
    {
        var order = new List<string>(models.Count);
        for (int i = 0; i < models.Count; i++)
        {
            string model = models[(currentIndex + i) % models.Count];
            if (!string.IsNullOrWhiteSpace(model))
                order.Add(model.Trim());
        }
        return order;
    }

    /// <summary>Запоминает модель, которая ответила, чтобы следующий запрос начинался с неё.</summary>
    public void MarkSuccess(string model)
    {
        int index = models.IndexOf(model);
        if (index >= 0)
            currentIndex = index;
    }
}
