using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Turn.View
{
    /// <summary>
    /// 过关翻页过场（程序A）。由 GameManager 自动挂上，场景里不需要摆任何东西。
    /// 流程：一张“纸”从右往左盖住整个屏幕 → 显示“第 N 页 完成” → onCovered（GameManager 在纸后面开下一关）
    ///       → 显示下一关标题 → 纸继续往左翻走，露出新关卡 → onFinished。
    /// 全程用不受 timeScale 影响的时间；模拟在翻页期间由 GameManager 暂停。
    /// </summary>
    public sealed class PageTurnView : MonoBehaviour
    {
        public const float CoverTime = 0.55f;
        public const float HoldTime = 0.9f;
        public const float RevealTime = 0.55f;

        private static readonly Color PaperColor = new Color(0.96f, 0.92f, 0.82f, 1f);
        private static readonly Color InkColor = new Color(0.22f, 0.17f, 0.12f, 1f);

        private Canvas _canvas;
        private RectTransform _page;
        private Image _shadow;
        private Text _title;
        private Coroutine _running;

        public bool IsPlaying { get { return _running != null; } }

        /// <summary>播放一次翻页。clearedTitle = 盖住时显示的文字；nextTitle = onCovered 之后显示的文字。</summary>
        public void Play(string clearedTitle, string nextTitle, Action onCovered, Action onFinished)
        {
            EnsureCanvas();
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(clearedTitle, nextTitle, onCovered, onFinished));
        }

        private IEnumerator Run(string clearedTitle, string nextTitle, Action onCovered, Action onFinished)
        {
            _canvas.enabled = true;
            _title.text = "";

            // 1) 纸从右边翻进来盖住屏幕
            for (float t = 0f; t < CoverTime; t += Time.unscaledDeltaTime)
            {
                SetPage(1f - Ease(t / CoverTime), 1f);
                yield return null;
            }
            SetPage(0f, 1f);
            _title.text = clearedTitle;
            yield return Wait(HoldTime * 0.6f);

            // 2) 在纸后面换关
            if (onCovered != null) onCovered();
            _title.text = nextTitle;
            yield return Wait(HoldTime);

            // 3) 纸往左翻走
            _title.text = "";
            for (float t = 0f; t < RevealTime; t += Time.unscaledDeltaTime)
            {
                SetPage(0f, 1f - Ease(t / RevealTime));
                yield return null;
            }
            _canvas.enabled = false;
            _running = null;
            if (onFinished != null) onFinished();
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>纸覆盖屏幕横向 [xMin, xMax]；阴影贴在纸的左边缘。</summary>
        private void SetPage(float xMin, float xMax)
        {
            _page.anchorMin = new Vector2(xMin, 0f);
            _page.anchorMax = new Vector2(Mathf.Max(xMin, xMax), 1f);
            _page.offsetMin = Vector2.zero;
            _page.offsetMax = Vector2.zero;
            float width = xMax - xMin;
            _shadow.color = new Color(0f, 0f, 0f, width > 0.001f && width < 0.999f ? 0.35f : 0f);
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("PageTurnOverlay");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // 盖在 HUD 上面
            _canvas.enabled = false;

            var pageGo = new GameObject("Page", typeof(RectTransform), typeof(Image));
            pageGo.transform.SetParent(go.transform, false);
            _page = pageGo.GetComponent<RectTransform>();
            Image paper = pageGo.GetComponent<Image>();
            paper.color = PaperColor;
            paper.raycastTarget = false;

            var shadowGo = new GameObject("EdgeShadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(pageGo.transform, false);
            var shadowRt = shadowGo.GetComponent<RectTransform>();
            shadowRt.anchorMin = new Vector2(0f, 0f);
            shadowRt.anchorMax = new Vector2(0f, 1f);
            shadowRt.pivot = new Vector2(1f, 0.5f);
            shadowRt.sizeDelta = new Vector2(24f, 0f);
            shadowRt.anchoredPosition = Vector2.zero;
            _shadow = shadowGo.GetComponent<Image>();
            _shadow.raycastTarget = false;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _title = titleGo.GetComponent<Text>();
            _title.font = UIFont.Chinese;
            _title.fontSize = 44;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.color = InkColor;
            _title.raycastTarget = false;

            SetPage(1f, 1f);
        }
    }
}
