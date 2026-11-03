using System.Collections.Generic;

namespace AIGameChat
{
    /// <summary>
    /// Системный промпт + последние N сообщений диалога.
    /// Системный промпт хранится отдельно и никогда не обрезается.
    /// Сообщение игрока попадает в историю только вместе с успешным ответом (Commit),
    /// поэтому при ошибке запроса в истории нет ни фиктивных ответов, ни дублей.
    /// </summary>
    public class ChatHistory
    {
        readonly List<ChatMessage> turns = new List<ChatMessage>();
        string systemPrompt;
        readonly int maxMessages;

        public ChatHistory(string systemPrompt, int maxMessages = 10)
        {
            this.systemPrompt = systemPrompt;
            this.maxMessages = maxMessages < 2 ? 2 : maxMessages;
        }

        public string SystemPrompt { get { return systemPrompt; } }
        public int TurnCount { get { return turns.Count; } }
        public IReadOnlyList<ChatMessage> Turns { get { return turns; } }

        public void SetSystemPrompt(string prompt)
        {
            systemPrompt = prompt;
        }

        public void Clear()
        {
            turns.Clear();
        }

        /// <summary>Сообщения для запроса: system + история + ещё не закоммиченное сообщение игрока.</summary>
        public List<ChatMessage> BuildRequest(string pendingUserText)
        {
            var list = new List<ChatMessage>(turns.Count + 2);
            list.Add(new ChatMessage("system", systemPrompt));
            list.AddRange(turns);
            list.Add(new ChatMessage("user", pendingUserText));
            return list;
        }

        /// <summary>Вызывается только после успешного ответа.</summary>
        public void Commit(string userText, string assistantText)
        {
            turns.Add(new ChatMessage("user", userText));
            turns.Add(new ChatMessage("assistant", assistantText));
            Trim();
        }

        void Trim()
        {
            while (turns.Count > maxMessages)
            {
                turns.RemoveAt(0);
            }
            // История всегда начинается с реплики игрока.
            while (turns.Count > 0 && turns[0].role != "user")
            {
                turns.RemoveAt(0);
            }
        }
    }
}
