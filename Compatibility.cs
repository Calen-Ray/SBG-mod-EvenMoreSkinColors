using System;
using System.Linq;
using BepInEx.Bootstrap;

namespace EvenMoreSkinColors
{
    internal static class Compatibility
    {
        internal static bool IsMoreSkinColorsPresent { get; private set; }

        internal static void DetectInstalledMods()
        {
            IsMoreSkinColorsPresent = Chainloader.PluginInfos.Values.Any(info =>
            {
                string guid = info.Metadata.GUID ?? string.Empty;
                string name = info.Metadata.Name ?? string.Empty;
                return guid.Equals("ViViKo.MoreSkinColors", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("MoreSkinColors", StringComparison.OrdinalIgnoreCase)
                    || guid.IndexOf("MoreSkinColors", StringComparison.OrdinalIgnoreCase) >= 0;
            });

            if (IsMoreSkinColorsPresent)
            {
                Plugin.Log.LogInfo("Compatibility mode active: detected MoreSkinColors. Vanilla swatches remain untouched; custom tone UI stays additive.");
            }
        }
    }
}

