using System;
using System.IO;
using UnityEngine;

namespace AIGameChat
{
    /// <summary>
    /// Откуда берётся API-ключ (по приоритету):
    ///   1. PlayerPrefs (вводится в окне «Настройки» в игре);
    ///   2. файл Assets/StreamingAssets/openrouter_config.json (в .gitignore);
    ///   3. переменная окружения OPENROUTER_API_KEY.
    /// Ключ никогда не записывается в исходный код и не пишется в лог.
    /// ВАЖНО: ни PlayerPrefs, ни файл в сборке не являются надёжным хранилищем секрета.
    /// Для распространяемой игры нужен серверный посредник.
    /// </summary>
    public static class OpenRouterConfig
    {
        public const string ApiKeyPref = "agc_api_key";
        const string FileName = "openrouter_config.json";

        [Serializable]
        public class FileData
        {
            public string apiKey;
            public string[] models; // необязательно: переопределяет список моделей из сцены
        }

        public static string ConfigPath
        {
            get { return Path.Combine(Application.streamingAssetsPath, FileName); }
        }

        /// <summary>Читает конфиг-файл. Работает на десктопе и в редакторе (на Android StreamingAssets читается иначе).</summary>
        public static FileData LoadFile()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<FileData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIGameChat] Не удалось прочитать " + FileName + ": " + e.Message);
                return null;
            }
        }

        public static string GetApiKey()
        {
            string key = PlayerPrefs.GetString(ApiKeyPref, "");
            if (IsUsable(key)) return key.Trim();

            FileData file = LoadFile();
            if (file != null && IsUsable(file.apiKey)) return file.apiKey.Trim();

            string env = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (IsUsable(env)) return env.Trim();

            return "";
        }

        /// <summary>Короткое описание источника ключа для подсказки в настройках (сам ключ не раскрывается).</summary>
        public static string DescribeKeySource()
        {
            if (IsUsable(PlayerPrefs.GetString(ApiKeyPref, ""))) return "ключ задан в настройках игры";
            FileData file = LoadFile();
            if (file != null && IsUsable(file.apiKey)) return "ключ взят из openrouter_config.json";
            if (IsUsable(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"))) return "ключ взят из переменной OPENROUTER_API_KEY";
            return "ключ не задан";
        }

        static bool IsUsable(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            key = key.Trim();
            if (key.IndexOf(' ') >= 0) return false;
            if (key.IndexOf("ВСТАВЬТЕ", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (key.IndexOf("PASTE", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }
    }
}
