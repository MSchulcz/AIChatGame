using System.Collections.Generic;
using UnityEngine;

namespace AIGameChat
{
    /// <summary>
    /// Список бесплатных моделей и порядок их перебора.
    /// Один пользовательский запрос = не более одного прохода по списку (никаких бесконечных повторов).
    /// Последняя успешная модель ставится первой в следующем запросе, чтобы не тратить попытки на заведомо занятые модели.
    /// </summary>
    public class ModelRotator : MonoBehaviour
    {
        [Tooltip("Точные slug моделей OpenRouter с суффиксом :free. Список меняется — проверьте на openrouter.ai/models?max_price=0")]
        public List<string> models = new List<string>
        {
            "nvidia/nemotron-3.5-super-vl:free",
            "dots-studio/dots-3-note-preview:free",
            "poolside/laguna-s-2.1:free",
            "recursive/flint-v1.0:free",
            "nvidia/nemotron-3-ultra-550b-a55b:free",
            "google/gemma-4-31b-it:free"
        };

        string lastSuccessful;

        public int MaxAttempts
        {
            get { return models != null ? models.Count : 0; }
        }

        /// <summary>
        /// Возвращает порядок попыток для одного запроса.
        /// overrideModels (из openrouter_config.json) заменяет список из сцены, если он не пуст.
        /// </summary>
        public List<string> GetAttemptOrder(string[] overrideModels = null)
        {
            var source = new List<string>();
            IEnumerable<string> raw = (overrideModels != null && overrideModels.Length > 0)
                ? (IEnumerable<string>)overrideModels
                : models;

            if (raw != null)
            {
                foreach (string m in raw)
                {
                    if (string.IsNullOrWhiteSpace(m)) continue;
                    string t = m.Trim();
                    if (!source.Contains(t)) source.Add(t);
                }
            }

            var order = new List<string>();
            if (source.Count == 0) return order;

            int start = lastSuccessful != null ? source.IndexOf(lastSuccessful) : 0;
            if (start < 0) start = 0;

            for (int i = 0; i < source.Count; i++)
            {
                order.Add(source[(start + i) % source.Count]);
            }
            return order;
        }

        public void MarkSuccess(string model)
        {
            lastSuccessful = model;
        }

        public void ResetPreference()
        {
            lastSuccessful = null;
        }
    }
}
