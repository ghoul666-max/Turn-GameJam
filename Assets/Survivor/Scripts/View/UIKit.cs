using System;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.View
{
    /// <summary>用代码创建 uGUI 元素的工具。所有元素默认锚定在父节点中心。</summary>
    public static class UIKit
    {
        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new string[] { "Microsoft YaHei", "SimHei", "PingFang SC", "Arial" }, 20);
                }
                return _font;
            }
        }

        /// <summary>
        /// 仅供编辑器生成 Prefab 时使用：系统字体不是资产，存不进 Prefab，
        /// 所以生成模板时临时换成内置字体；实例化后再由 ApplyFont 换回支持中文的系统字体。
        /// </summary>
        public static Font FontOverride;

        private static Font CurrentFont
        {
            get { return FontOverride != null ? FontOverride : DefaultFont; }
        }

        public static void ApplyFont(GameObject root)
        {
            if (root == null) return;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].font = DefaultFont;
        }

        public static RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>锚点与轴心都设为 anchor（例如 (0,1) = 左上角），再按 pos 偏移。</summary>
        public static void Pin(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image CreateImage(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color color)
        {
            RectTransform rt = CreateRect(name, parent, pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(string name, Transform parent, Vector2 pos, Vector2 size, string content,
            int fontSize, TextAnchor anchor, Color color)
        {
            RectTransform rt = CreateRect(name, parent, pos, size);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = CurrentFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static void AddOutline(Text text, float distance)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.06f, 0.05f, 0.09f, 1f);
            outline.effectDistance = new Vector2(distance, -distance);
        }

        public static Button MakeButton(Image image, Action onClick)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 0.75f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }
    }

    /// <summary>
    /// 用代码创建 SpriteRenderer 的工具 + 统一的无光照材质。
    /// 工程用的是 URP 2D 渲染器，默认的 Sprite-Lit 材质需要 2D 灯光；白模不打灯，统一换成 Sprite-Unlit。
    /// </summary>
    public static class SpriteKit
    {
        public const string UnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        public const string FallbackShaderName = "Sprites/Default";

        private static Material _unlit;

        /// <summary>仅供编辑器生成 Prefab 时使用：换成 URP 包里的材质资产，这样 Prefab 里存得下引用。</summary>
        public static Material MaterialOverride;

        public static Material UnlitMaterial
        {
            get
            {
                if (MaterialOverride != null) return MaterialOverride;
                if (_unlit == null)
                {
                    Shader shader = Shader.Find(UnlitShaderName);
                    if (shader == null) shader = Shader.Find(FallbackShaderName);
                    if (shader != null)
                    {
                        _unlit = new Material(shader);
                        _unlit.name = "SurvivorSpriteUnlit";
                    }
                }
                return _unlit;
            }
        }

        /// <summary>把物体下所有 SpriteRenderer 换成无光照材质（实例化后调用，和 ApplyFont 一样）。</summary>
        public static void ApplyMaterial(GameObject root)
        {
            if (root == null) return;
            Material material = UnlitMaterial;
            if (material == null) return;
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = material;
        }

        public static SpriteRenderer Create(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            Material material = UnlitMaterial;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
