using System.Collections;
using TMPro;
using UnityEngine;

namespace AIGameChat
{
    /// <summary>Левая половина экрана: вертикальный портрет, который меняется вместе с эмоцией.</summary>
    public class CharacterView : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image portrait;
        [SerializeField] UnityEngine.UI.AspectRatioFitter fitter;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField, Range(1f, 1.1f)] float popScale = 1.03f;
        [SerializeField, Range(0.05f, 0.5f)] float popTime = 0.15f;

        CharacterProfile profile;
        Coroutine pop;

        public Emotion Current { get; private set; }

        public void SetProfile(CharacterProfile newProfile)
        {
            profile = newProfile;
            if (nameLabel != null && profile != null) nameLabel.text = profile.displayName;
            SetEmotion(Emotion.Neutral, false);
        }

        public void SetEmotion(Emotion emotion, bool animate = true)
        {
            Current = emotion;
            if (profile == null || portrait == null) return;

            Sprite sprite = profile.GetSprite(emotion);
            if (sprite == null) return;

            portrait.sprite = sprite;
            portrait.preserveAspect = true;
            if (fitter != null && sprite.rect.height > 0f)
            {
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            if (animate && isActiveAndEnabled)
            {
                if (pop != null) StopCoroutine(pop);
                pop = StartCoroutine(Pop());
            }
        }

        IEnumerator Pop()
        {
            RectTransform t = portrait.rectTransform;
            float elapsed = 0f;
            while (elapsed < popTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(elapsed / popTime) * Mathf.PI);
                t.localScale = Vector3.one * (1f + (popScale - 1f) * k);
                yield return null;
            }
            t.localScale = Vector3.one;
            pop = null;
        }
    }
}
