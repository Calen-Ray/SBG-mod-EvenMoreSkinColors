using System;
using UnityEngine;

namespace EvenMoreSkinColors
{
    internal struct SkinToneSelection : IEquatable<SkinToneSelection>
    {
        internal static readonly Color32 DefaultBaseColor = new Color32(216, 160, 116, 255);

        public bool Enabled;
        public Color BaseColor;

        public Color MouthColor
        {
            get
            {
                Color.RGBToHSV(BaseColor, out float hue, out float saturation, out float value);
                return Color.HSVToRGB(hue, Mathf.Clamp01(saturation * 0.7f), Mathf.Clamp(value * 0.48f, 0.06f, 0.95f));
            }
        }

        public bool Equals(SkinToneSelection other)
        {
            return Enabled == other.Enabled && BaseColor.Equals(other.BaseColor);
        }

        public override bool Equals(object obj)
        {
            return obj is SkinToneSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Enabled ? 1 : 0) * 397) ^ BaseColor.GetHashCode();
            }
        }

        public static string ToHtml(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
