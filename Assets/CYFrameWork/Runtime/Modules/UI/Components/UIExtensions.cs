// ============================================================================
// CYFramework 2.2 - UI 扩展方法
// 功能：常用 UI 操作的扩展方法
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CYFramework.Modules.UI.Components
{
    /// <summary>
    /// UI 扩展方法
    /// </summary>
    public static class UIExtensions
    {
        #region Button 扩展
        
        /// <summary>
        /// 添加点击监听（自动清理旧监听）
        /// </summary>
        public static void OnClick(this Button button, UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        
        /// <summary>
        /// 添加点击监听（带点击音效）
        /// </summary>
        public static void OnClickWithSound(this Button button, UnityAction action, string soundName = "click")
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                // 播放点击音效
                // ServiceLocator.Get<IAudioService>()?.PlaySFX(soundName);
                action?.Invoke();
            });
        }
        
        /// <summary>
        /// 设置按钮可交互状态
        /// </summary>
        public static void SetInteractable(this Button button, bool interactable, float disabledAlpha = 0.5f)
        {
            button.interactable = interactable;
            
            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = interactable ? 1f : disabledAlpha;
        }
        
        #endregion
        
        #region Text 扩展
        
        /// <summary>
        /// 设置文本（支持 Text 和 TextMeshPro）
        /// </summary>
        public static void SetText(this GameObject go, string text)
        {
            var textComponent = go.GetComponent<Text>();
            if (textComponent != null)
            {
                textComponent.text = text;
                return;
            }
            
            // 尝试 TextMeshPro
            var tmpText = go.GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = text;
            }
        }
        
        /// <summary>
        /// 数字渐变动画
        /// </summary>
        public static IEnumerator AnimateNumber(this Text text, int from, int to, float duration, string format = "{0}")
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                text.text = string.Format(format, current);
                yield return null;
            }
            text.text = string.Format(format, to);
        }
        
        #endregion
        
        #region Image 扩展
        
        /// <summary>
        /// 设置精灵（从 Resources 加载）
        /// </summary>
        public static void SetSprite(this Image image, string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                image.sprite = sprite;
            }
        }
        
        /// <summary>
        /// 设置填充值动画
        /// </summary>
        public static IEnumerator AnimateFill(this Image image, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                image.fillAmount = Mathf.Lerp(from, to, t);
                yield return null;
            }
            image.fillAmount = to;
        }
        
        /// <summary>
        /// 设置透明度
        /// </summary>
        public static void SetAlpha(this Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
        
        #endregion
        
        #region RectTransform 扩展
        
        /// <summary>
        /// 设置锚点位置
        /// </summary>
        public static void SetAnchoredPosition(this RectTransform rect, float x, float y)
        {
            rect.anchoredPosition = new Vector2(x, y);
        }
        
        /// <summary>
        /// 设置大小
        /// </summary>
        public static void SetSize(this RectTransform rect, float width, float height)
        {
            rect.sizeDelta = new Vector2(width, height);
        }
        
        /// <summary>
        /// 设置为全屏拉伸
        /// </summary>
        public static void SetFullStretch(this RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        
        /// <summary>
        /// 移动动画
        /// </summary>
        public static IEnumerator AnimateMove(this RectTransform rect, Vector2 from, Vector2 to, float duration, Func<float, float> easing = null)
        {
            float elapsed = 0f;
            rect.anchoredPosition = from;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (easing != null) t = easing(t);
                rect.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            
            rect.anchoredPosition = to;
        }
        
        #endregion
        
        #region CanvasGroup 扩展
        
        /// <summary>
        /// 淡入动画
        /// </summary>
        public static IEnumerator FadeIn(this CanvasGroup canvasGroup, float duration)
        {
            yield return Fade(canvasGroup, 0f, 1f, duration);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        
        /// <summary>
        /// 淡出动画
        /// </summary>
        public static IEnumerator FadeOut(this CanvasGroup canvasGroup, float duration)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            yield return Fade(canvasGroup, 1f, 0f, duration);
        }
        
        /// <summary>
        /// 淡入淡出
        /// </summary>
        public static IEnumerator Fade(this CanvasGroup canvasGroup, float from, float to, float duration)
        {
            float elapsed = 0f;
            canvasGroup.alpha = from;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            
            canvasGroup.alpha = to;
        }
        
        #endregion
        
        #region 缓动函数
        
        /// <summary>
        /// EaseOutQuad 缓动
        /// </summary>
        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        
        /// <summary>
        /// EaseOutCubic 缓动
        /// </summary>
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        
        /// <summary>
        /// EaseOutBack 缓动（带回弹）
        /// </summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        
        /// <summary>
        /// EaseOutElastic 缓动（弹性）
        /// </summary>
        public static float EaseOutElastic(float t)
        {
            if (t == 0f) return 0f;
            if (t == 1f) return 1f;
            
            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
        
        #endregion
    }
}
