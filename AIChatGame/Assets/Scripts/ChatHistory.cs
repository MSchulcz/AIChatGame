using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChatMessage
{
    public string role;
    public string content;

    public ChatMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

/// <summary>
/// Хранит системный промпт и историю диалога, ограничивает её длину.
/// Системный промпт хранится отдельно и никогда не обрезается.
/// </summary>
public class ChatHistory : MonoBehaviour
{
    [TextArea(4, 12)]
    public string systemPrompt =
        "Ты — дружелюбный ИИ-ассистент внутри учебного приложения на Unity.\n" +
        "Отвечай на русском языке.\n" +
        "Пиши понятно и структурированно.\n" +
        "Для вопросов о программировании давай практические примеры.\n" +
        "Если не уверен в ответе, прямо скажи об этом.\n" +
        "Не утверждай, что выполнил действия в Unity, если ты только объясняешь, как их выполнить.";

    [Tooltip("Сколько последних сообщений user/assistant хранить (без системного промпта)")]
    [Min(2)]
    public int maxMessages = 10;

    private readonly List<ChatMessage> messages = new List<ChatMessage>();

    public int Count => messages.Count;

    /// <summary>
    /// Собирает список для запроса: системный промпт + история + новый вопрос.
    /// Сам вопрос в историю не добавляется, пока не получен успешный ответ.
    /// </summary>
    public List<ChatMessage> BuildRequestMessages(string pendingUserMessage)
    {
        var result = new List<ChatMessage>(messages.Count + 2);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            result.Add(new ChatMessage("system", systemPrompt));
        result.AddRange(messages);
        result.Add(new ChatMessage("user", pendingUserMessage));
        return result;
    }

    /// <summary>Сохраняет пару вопрос/ответ после успешного запроса и обрезает историю.</summary>
    public void CommitExchange(string userMessage, string assistantMessage)
    {
        messages.Add(new ChatMessage("user", userMessage));
        messages.Add(new ChatMessage("assistant", assistantMessage));
        Trim();
    }

    public void Clear()
    {
        messages.Clear();
    }

    private void Trim()
    {
        int excess = messages.Count - maxMessages;
        if (excess <= 0)
            return;
        // Удаляем парами, чтобы история не начиналась с ответа ассистента.
        if (excess % 2 != 0)
            excess++;
        messages.RemoveRange(0, Mathf.Min(excess, messages.Count));
    }
}
