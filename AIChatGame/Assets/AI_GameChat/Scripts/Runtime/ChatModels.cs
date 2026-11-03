using System;
using System.Collections.Generic;

namespace AIGameChat
{
    // Простые сериализуемые классы для JsonUtility (формат OpenAI/OpenRouter Chat Completions).

    [Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage() { }

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    public class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public float temperature;
        public int max_tokens;
    }

    [Serializable]
    public class ChatChoice
    {
        public ChatMessage message;
        public string finish_reason;
    }

    [Serializable]
    public class ApiError
    {
        public string message;
    }

    [Serializable]
    public class ChatResponse
    {
        public List<ChatChoice> choices;
        public ApiError error;
    }
}
