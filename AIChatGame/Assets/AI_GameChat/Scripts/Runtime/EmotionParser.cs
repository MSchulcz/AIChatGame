using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AIGameChat
{
    public class ParsedReply
    {
        public Emotion emotion = Emotion.Neutral;
        public string text = "";
        public bool emotionFromModel;   // false — эмоция определена локально по словам игрока
        public string method = "plain"; // json | regex | tag | plain
    }

    /// <summary>
    /// Достаёт из ответа модели пару (эмоция, реплика). Бесплатные модели часто нарушают формат,
    /// поэтому пробуем по очереди: чистый JSON → JSON внутри текста/блока кода → поля регулярками
    /// (в т.ч. обрезанный JSON) → тег [emotion: x] → просто текст.
    /// Неизвестная или отсутствующая эмоция заменяется локальной оценкой по тексту игрока.
    /// </summary>
    public static class EmotionParser
    {
        [Serializable]
        class Payload
        {
            public string emotion;
            public string reply;
        }

        static readonly Regex ReplyField = new Regex(@"""reply""\s*:\s*""((?:\\.|[^""\\])*)", RegexOptions.Singleline);
        static readonly Regex EmotionField = new Regex(@"""emotion""\s*:\s*""([^""]*)""");
        static readonly Regex Tag = new Regex(@"\[\s*(?:emotion|эмоция)\s*[:=]\s*([\p{L}]+)\s*\]", RegexOptions.IgnoreCase);

        public static ParsedReply Parse(string raw, string playerText)
        {
            var result = new ParsedReply();
            string s = StripFences(raw ?? "");
            string emotionName = null;

            Payload p = TryPayload(s);
            if (p == null)
            {
                int a = s.IndexOf('{');
                int b = s.LastIndexOf('}');
                if (a >= 0 && b > a) p = TryPayload(s.Substring(a, b - a + 1));
            }

            if (p != null)
            {
                result.method = "json";
                result.text = p.reply.Trim();
                emotionName = p.emotion;
            }
            else
            {
                Match rm = ReplyField.Match(s);
                if (rm.Success)
                {
                    result.method = "regex";
                    result.text = Unescape(rm.Groups[1].Value).Trim();
                    Match em = EmotionField.Match(s);
                    if (em.Success) emotionName = em.Groups[1].Value;
                }
                else
                {
                    Match tm = Tag.Match(s);
                    if (tm.Success)
                    {
                        result.method = "tag";
                        emotionName = tm.Groups[1].Value;
                        s = Tag.Replace(s, "").Trim();
                    }
                    result.text = s;
                }
            }

            Emotion? parsed = ParseEmotion(emotionName);
            if (parsed.HasValue)
            {
                result.emotion = parsed.Value;
                result.emotionFromModel = true;
            }
            else
            {
                result.emotion = ClassifyPlayerText(playerText);
                result.emotionFromModel = false;
            }
            return result;
        }

        /// <summary>Нормализованный JSON для записи в историю — модель видит свои прошлые ответы в нужном формате.</summary>
        public static string ToJson(ParsedReply reply)
        {
            var p = new Payload
            {
                emotion = reply.emotion.ToString().ToLowerInvariant(),
                reply = reply.text
            };
            return JsonUtility.ToJson(p);
        }

        // ---------- эмоция по названию ----------

        static readonly KeyValuePair<string, Emotion>[] Stems =
        {
            new KeyValuePair<string, Emotion>("neutr", Emotion.Neutral),
            new KeyValuePair<string, Emotion>("calm", Emotion.Neutral),
            new KeyValuePair<string, Emotion>("normal", Emotion.Neutral),
            new KeyValuePair<string, Emotion>("нейтр", Emotion.Neutral),
            new KeyValuePair<string, Emotion>("спокой", Emotion.Neutral),

            new KeyValuePair<string, Emotion>("happ", Emotion.Happy),
            new KeyValuePair<string, Emotion>("joy", Emotion.Happy),
            new KeyValuePair<string, Emotion>("glad", Emotion.Happy),
            new KeyValuePair<string, Emotion>("excit", Emotion.Happy),
            new KeyValuePair<string, Emotion>("cheer", Emotion.Happy),
            new KeyValuePair<string, Emotion>("smil", Emotion.Happy),
            new KeyValuePair<string, Emotion>("laugh", Emotion.Happy),
            new KeyValuePair<string, Emotion>("радост", Emotion.Happy),
            new KeyValuePair<string, Emotion>("счастл", Emotion.Happy),
            new KeyValuePair<string, Emotion>("весел", Emotion.Happy),
            new KeyValuePair<string, Emotion>("довол", Emotion.Happy),

            new KeyValuePair<string, Emotion>("sad", Emotion.Sad),
            new KeyValuePair<string, Emotion>("upset", Emotion.Sad),
            new KeyValuePair<string, Emotion>("sorrow", Emotion.Sad),
            new KeyValuePair<string, Emotion>("depress", Emotion.Sad),
            new KeyValuePair<string, Emotion>("груст", Emotion.Sad),
            new KeyValuePair<string, Emotion>("печал", Emotion.Sad),
            new KeyValuePair<string, Emotion>("расстр", Emotion.Sad),

            new KeyValuePair<string, Emotion>("angr", Emotion.Angry),
            new KeyValuePair<string, Emotion>("anger", Emotion.Angry),
            new KeyValuePair<string, Emotion>("mad", Emotion.Angry),
            new KeyValuePair<string, Emotion>("annoy", Emotion.Angry),
            new KeyValuePair<string, Emotion>("irrit", Emotion.Angry),
            new KeyValuePair<string, Emotion>("зл", Emotion.Angry),
            new KeyValuePair<string, Emotion>("серд", Emotion.Angry),
            new KeyValuePair<string, Emotion>("раздр", Emotion.Angry),

            new KeyValuePair<string, Emotion>("surpris", Emotion.Surprised),
            new KeyValuePair<string, Emotion>("shock", Emotion.Surprised),
            new KeyValuePair<string, Emotion>("amaz", Emotion.Surprised),
            new KeyValuePair<string, Emotion>("удивл", Emotion.Surprised),
            new KeyValuePair<string, Emotion>("шок", Emotion.Surprised),

            new KeyValuePair<string, Emotion>("scar", Emotion.Scared),
            new KeyValuePair<string, Emotion>("fear", Emotion.Scared),
            new KeyValuePair<string, Emotion>("afraid", Emotion.Scared),
            new KeyValuePair<string, Emotion>("fright", Emotion.Scared),
            new KeyValuePair<string, Emotion>("worr", Emotion.Scared),
            new KeyValuePair<string, Emotion>("anxi", Emotion.Scared),
            new KeyValuePair<string, Emotion>("страх", Emotion.Scared),
            new KeyValuePair<string, Emotion>("испуг", Emotion.Scared),
            new KeyValuePair<string, Emotion>("боя", Emotion.Scared),
            new KeyValuePair<string, Emotion>("тревож", Emotion.Scared),

            new KeyValuePair<string, Emotion>("embarr", Emotion.Embarrassed),
            new KeyValuePair<string, Emotion>("shy", Emotion.Embarrassed),
            new KeyValuePair<string, Emotion>("blush", Emotion.Embarrassed),
            new KeyValuePair<string, Emotion>("flust", Emotion.Embarrassed),
            new KeyValuePair<string, Emotion>("смущ", Emotion.Embarrassed),
            new KeyValuePair<string, Emotion>("стесн", Emotion.Embarrassed)
        };

        public static Emotion? ParseEmotion(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string v = name.Trim().Trim('"', '\'', '.', ',').ToLowerInvariant();
            if (v.Length == 0) return null;

            Emotion exact;
            if (Enum.TryParse(v, true, out exact) && Enum.IsDefined(typeof(Emotion), exact)) return exact;

            foreach (KeyValuePair<string, Emotion> kv in Stems)
            {
                if (v.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
            }
            return null;
        }

        // ---------- запасной вариант: эмоция по словам игрока ----------

        struct Rule
        {
            public Emotion emotion;
            public string[] words;
            public Rule(Emotion e, params string[] w) { emotion = e; words = w; }
        }

        // Порядок важен: комплименты проверяются раньше «люблю», оскорбления — до остального.
        // Слово с пробелом в начале (" ура") ищется только с начала слова, чтобы не срабатывать внутри
        // других слов («культура», «много», «процедура»).
        static readonly Rule[] Rules =
        {
            new Rule(Emotion.Embarrassed, "красив", "милая", "милый", "нравишься", "симпатичн", "обним", "поцел", "люблю тебя", "beautiful", " cute", "like you", " kiss"),
            new Rule(Emotion.Angry, "бесит", "ненавиж", "дурак", " дура ", " дура,", " дура!", "идиот", "тупиц", "заткнись", "отстань", " hate", "stupid", "idiot", "shut up"),
            new Rule(Emotion.Scared, "страшно", "боюсь", "испуга", "опасн", "монстр", "призрак", "scared", "afraid", "danger", "help me"),
            new Rule(Emotion.Sad, "грустн", "грусть", "печал", "плохо", "одинок", "плачу", "расстро", "скучаю", "потерял", " sad", "lonely", "depress", " cry"),
            new Rule(Emotion.Surprised, " ого ", " ого!", " ого,", " ого?", " ого.", " вау", "неужели", "не может быть", "серьёзно?", "серьезно?", "внезапно", " wow", " omg", "no way"),
            new Rule(Emotion.Happy, " ура", "круто", "класс", "отлично", "здорово", "люблю", "спасибо", "хаха", "ахах", "счастлив", " рад ", " рад!", "весел", "happy", "great", "awesome", "thank", "haha", ":)")
        };

        public static Emotion ClassifyPlayerText(string playerText)
        {
            if (string.IsNullOrWhiteSpace(playerText)) return Emotion.Neutral;
            string t = " " + playerText.ToLowerInvariant() + " ";
            foreach (Rule rule in Rules)
            {
                foreach (string w in rule.words)
                {
                    if (t.Contains(w)) return rule.emotion;
                }
            }
            return Emotion.Neutral;
        }

        // ---------- вспомогательное ----------

        static Payload TryPayload(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                Payload p = JsonUtility.FromJson<Payload>(json);
                if (p != null && !string.IsNullOrWhiteSpace(p.reply)) return p;
            }
            catch (Exception) { }
            return null;
        }

        static string StripFences(string s)
        {
            s = s.Trim();
            if (s.StartsWith("```", StringComparison.Ordinal))
            {
                int nl = s.IndexOf('\n');
                s = nl >= 0 ? s.Substring(nl + 1) : s.Substring(3);
                s = s.TrimEnd();
                if (s.EndsWith("```", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 3);
            }
            return s.Trim();
        }

        static string Unescape(string s)
        {
            try { return Regex.Unescape(s); }
            catch (ArgumentException) { return s; }
        }
    }
}
