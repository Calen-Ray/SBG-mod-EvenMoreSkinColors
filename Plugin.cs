using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;

namespace EvenMoreSkinColors
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "cray.evenmoreskincolors";
        public const string ModName = "EvenMoreSkinColors";
        public const string ModVersion = "0.1.3";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            Compatibility.DetectInstalledMods();
            SkinToneState.Initialize(Config, Logger);
            SkinToneNetworkSerialization.Register();
            NetworkClient.OnConnectedEvent += SkinToneNetwork.OnClientConnected;

            new Harmony(ModGuid).PatchAll();

            Logger.LogInfo($"{ModName} v{ModVersion} loaded.");
        }

        private void OnDestroy()
        {
            NetworkClient.OnConnectedEvent -= SkinToneNetwork.OnClientConnected;
        }
    }
}
