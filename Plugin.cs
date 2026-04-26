using BepInEx;
using BepInEx.Configuration;
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
        public const string ModVersion = "0.2.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;
        internal ConfigEntry<bool> verboseLoggingConfig;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            verboseLoggingConfig = Config.Bind(
                "Diagnostics",
                "VerboseLogging",
                false,
                "Emit per-event broadcast/receive/apply/revert logs. Off by default — flip on when reporting issues so the log shows the full skin-tone replication trace.");

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
