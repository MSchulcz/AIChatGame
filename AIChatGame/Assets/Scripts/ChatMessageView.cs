using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Отображение одного сообщения чата (пузырь с заголовком и текстом).</summary>
public class ChatMessageView : MonoBehaviour
{
    public enum Kind { User, Assistant, Error }

    public HorizontalLayoutGroup row;
    public Image bubble;
    public TMP_Text header;
    public TMP_Text body;

    [Header("Цвета")]
    public Color userColor = new Color(0.20f, 0.38f, 0.75f);
    public Color assistantColor = new Color(0.22f, 0.24f, 0.28f);
    public Color errorColor = new Color(0.55f, 0.18f, 0.18f);

    [Tooltip("Отступ с противоположной стороны, чтобы пузыри пользователя и ассистента различались")]
    public int sideIndent = 120;

    public void Setup(Kind kind, string text, string modelName = null)
    {
        body.text = text;

        switch (kind)
        {
            case Kind.User:
                header.text = "Вы";
                bubble.color = userColor;
                row.padding = new RectOffset(sideIndent, 0, 4, 4);
                break;
            case Kind.Assistant:
                header.text = string.IsNullOrEmpty(modelName) ? "Ассистент" : $"Ассистент · <alpha=#88>{modelName}";
                bubble.color = assistantColor;
                row.padding = new RectOffset(0, sideIndent, 4, 4);
                break;
            default:
                header.text = "Ошибка";
                bubble.color = errorColor;
                row.padding = new RectOffset(sideIndent / 2, sideIndent / 2, 4, 4);
                break;
        }
    }
}
