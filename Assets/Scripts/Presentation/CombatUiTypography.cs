using TMPro;
using UnityEngine;

namespace PF2e.Presentation
{
    public static class CombatUiTypography
    {
        private const string RegularFontResourcePath = "Fonts & Materials/Source Sans 3 SDF";
        private const string LightFontResourcePath = "Fonts & Materials/Source Sans 3 Light SDF";

        private static TMP_FontAsset regularFont;
        private static TMP_FontAsset lightFont;
        private static bool regularLoaded;
        private static bool lightLoaded;

        public static TMP_FontAsset RegularFont => LoadRegularFont();

        public static TMP_FontAsset LightFont => LoadLightFontOrFallback();

        public static bool ApplyBody(
            TMP_Text text,
            float fontSize,
            float characterSpacing,
            Color color,
            float lineSpacing = float.NaN,
            FontStyles style = FontStyles.Normal)
        {
            return Apply(text, LoadLightFontOrFallback(), fontSize, characterSpacing, color, style, lineSpacing);
        }

        public static bool ApplySecondary(
            TMP_Text text,
            float fontSize,
            float characterSpacing,
            Color color,
            float lineSpacing = float.NaN,
            FontStyles style = FontStyles.Normal)
        {
            return Apply(text, LoadLightFontOrFallback(), fontSize, characterSpacing, color, style, lineSpacing);
        }

        public static bool ApplyTitle(
            TMP_Text text,
            float fontSize,
            float characterSpacing,
            Color color,
            FontStyles style = FontStyles.Normal,
            float lineSpacing = float.NaN)
        {
            return Apply(text, LoadRegularFont(), fontSize, characterSpacing, color, style, lineSpacing);
        }

        public static bool ApplyButton(
            TMP_Text text,
            float fontSize,
            float characterSpacing,
            Color color,
            FontStyles style = FontStyles.Normal)
        {
            return Apply(text, LoadRegularFont(), fontSize, characterSpacing, color, style, float.NaN);
        }

        public static bool ApplyWithFont(
            TMP_Text text,
            TMP_FontAsset font,
            float fontSize,
            float characterSpacing,
            Color color,
            FontStyles style = FontStyles.Normal,
            float lineSpacing = float.NaN)
        {
            return Apply(text, font, fontSize, characterSpacing, color, style, lineSpacing);
        }

        private static TMP_FontAsset LoadRegularFont()
        {
            if (!regularLoaded)
            {
                regularFont = Resources.Load<TMP_FontAsset>(RegularFontResourcePath);
                regularLoaded = true;
            }

            return regularFont;
        }

        private static TMP_FontAsset LoadLightFontOrFallback()
        {
            if (!lightLoaded)
            {
                lightFont = Resources.Load<TMP_FontAsset>(LightFontResourcePath);
                lightLoaded = true;
            }

            return lightFont != null ? lightFont : LoadRegularFont();
        }

        private static bool Apply(
            TMP_Text text,
            TMP_FontAsset font,
            float fontSize,
            float characterSpacing,
            Color color,
            FontStyles style,
            float lineSpacing)
        {
            if (text == null)
                return false;

            bool changed = false;

            if (font != null && text.font != font)
            {
                text.font = font;
                changed = true;
            }

            if (font != null && font.material != null && text.fontSharedMaterial != font.material)
            {
                text.fontSharedMaterial = font.material;
                changed = true;
            }

            if (!Mathf.Approximately(text.fontSize, fontSize))
            {
                text.fontSize = fontSize;
                changed = true;
            }

            if (!Mathf.Approximately(text.characterSpacing, characterSpacing))
            {
                text.characterSpacing = characterSpacing;
                changed = true;
            }

            if (!float.IsNaN(lineSpacing) && !Mathf.Approximately(text.lineSpacing, lineSpacing))
            {
                text.lineSpacing = lineSpacing;
                changed = true;
            }

            if (!Approximately(text.color, color))
            {
                text.color = color;
                changed = true;
            }

            if (text.fontStyle != style)
            {
                text.fontStyle = style;
                changed = true;
            }

            if (!text.enableKerning)
            {
                text.enableKerning = true;
                changed = true;
            }

            if (changed)
            {
                text.UpdateMeshPadding();
                text.SetAllDirty();
            }

            return changed;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= 0.001f
                && Mathf.Abs(a.g - b.g) <= 0.001f
                && Mathf.Abs(a.b - b.b) <= 0.001f
                && Mathf.Abs(a.a - b.a) <= 0.001f;
        }
    }
}
