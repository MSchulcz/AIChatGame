using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIGameChat
{
    [Serializable]
    public class EmotionSprite
    {
        public Emotion emotion;
        public Sprite sprite;
    }

    /// <summary>
    /// Профиль персонажа: имя, характер, манера речи, приветствие и портреты по эмоциям.
    /// Создаётся через Assets → Create → AI Chat → Character Profile.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "AI Chat/Character Profile")]
    public class CharacterProfile : ScriptableObject
    {
        public string displayName = "Персонаж";

        [TextArea(3, 8)]
        [Tooltip("Характер, история, отношение к игроку. Попадает в системный промпт.")]
        public string personality = "Дружелюбный собеседник.";

        [TextArea(2, 5)]
        [Tooltip("Как персонаж говорит: длина фраз, словечки, обращение на «ты»/«вы».")]
        public string speechStyle = "Говорит просто и тепло, обращается на «ты».";

        [TextArea(1, 4)]
        [Tooltip("Первая реплика при старте разговора.")]
        public string greeting = "Привет!";

        [Tooltip("Портреты по эмоциям (вертикальные, одного размера и с одинаковым pivot). Neutral обязателен.")]
        public List<EmotionSprite> sprites = new List<EmotionSprite>();

        public Sprite GetSprite(Emotion emotion)
        {
            Sprite neutral = null;
            Sprite any = null;
            if (sprites != null)
            {
                foreach (EmotionSprite es in sprites)
                {
                    if (es == null || es.sprite == null) continue;
                    if (es.emotion == emotion) return es.sprite;
                    if (es.emotion == Emotion.Neutral) neutral = es.sprite;
                    if (any == null) any = es.sprite;
                }
            }
            return neutral != null ? neutral : any;
        }
    }
}
