using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ManualBlinkingMod
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    [BepInProcess("CasualtiesUnknown.exe")]
    [BepInDependency("me.danimineiro.modsettings", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        private Harmony _harmony = null!;

        // ─── CONFIGURATION ENTRIES ──────────────────────────────────────────
        public static ConfigEntry<bool> ConfigBlinkingEnabled = null!;
        public static ConfigEntry<KeyCode> ConfigBlinkKey = null!;
        public static ConfigEntry<float> ConfigBaseDrySpeed = null!;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            // Bind configuration settings
            ConfigBlinkingEnabled = Config.Bind(
                "General",
                "BlinkingEnabled",
                true,
                "If true, manual blinking mechanics are active. If false, vanilla blinking behavior is restored."
            );

            ConfigBlinkKey = Config.Bind(
                "Controls",
                "BlinkKey",
                KeyCode.CapsLock,
                "The key you hold down to close eyes or tap to blink."
            );

            ConfigBaseDrySpeed = Config.Bind(
                "Realism",
                "BaseDrySpeed",
                0.035f,
                "The base rate at which eyes dry per second. Lower values make eyes dry slower. Default is 0.035 (approx 28 seconds to dry)."
            );

            // Apply Harmony patches
            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Check if ModSettingsLib is installed and register if so
            if (IsModSettingsInstalled())
            {
                try
                {
                    RegisterModSettings();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to register custom settings page in ModSettingsLib: {ex}");
                }
            }
            else
            {
                Logger.LogInfo("ModSettingsLib was not found. Mod-specific configurations will only be managed via the BepInEx config file.");
            }

            Logger.LogInfo($"[{PluginInfo.NAME} v{PluginInfo.VERSION}] Loaded! Blink / Hold Eyes Closed with [{ConfigBlinkKey.Value}]. Keep your eyes moist!");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private static bool IsModSettingsInstalled()
        {
            return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("me.danimineiro.modsettings");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterModSettings()
        {
            Logger.LogInfo("ModSettingsLib is present. Initializing and registering custom settings page...");

            // Ensure our strings are in the dictionary if locale is already loaded
            AddTranslations();

            // Native setting wrappers representing our BepInEx configs
            SettingBool enabledSetting = null!;
            enabledSetting = new SettingBool
            {
                name = "manualblinking.enabled",
                value = ConfigBlinkingEnabled.Value,
                apply = () =>
                {
                    ConfigBlinkingEnabled.Value = enabledSetting.value;
                    Instance.Config.Save();
                }
            };

            SettingKeybind blinkKeySetting = null!;
            blinkKeySetting = new SettingKeybind
            {
                name = "manualblinking.blinkkey",
                value = ConfigBlinkKey.Value,
                apply = () =>
                {
                    ConfigBlinkKey.Value = blinkKeySetting.value;
                    Instance.Config.Save();
                }
            };

            // Register default settings page with title key and settings array
            CU_ModSettings.ModSettingsPlugin.AddModSettingsPageDefaults(
                "manualblinking.title",
                new Setting[] { enabledSetting, blinkKeySetting }
            );

            Logger.LogInfo("Successfully registered settings page in ModSettingsLib!");
        }

        public static void AddTranslations()
        {
            if (Locale.currentLang?.other == null) return;
            var other = Locale.currentLang.other;

            other["manualblinking.title"] = "Manual Blinking";

            other["gamesetmanualblinking.enabled"] = "Manual Blinking Mod";
            other["gamesetmanualblinking.enableddsc"] = "Enable or disable all manual blinking mechanics, dry eye vignettes, and blackouts.";

            other["gamesetmanualblinking.blinkkey"] = "Blink Key";
            other["gamesetmanualblinking.blinkkeydsc"] = "The key you tap to blink, or hold down to keep your eyes closed.";
        }
    }

    public static class PluginInfo
    {
        public const string GUID = "com.antigravity.manualblinking";
        public const string NAME = "Manual Blinking Mod";
        public const string VERSION = "1.0.0.0";
    }

    // ─── HARMONY PATCHES ──────────────────────────────────────────────────
    [HarmonyPatch(typeof(Body), nameof(Body.HandleCirculation))]
    public static class Patch_HandleCirculation
    {
        public static void Prefix(Body __instance)
        {
            if (PlayerCamera.main != null && __instance == PlayerCamera.main.body)
            {
                var ctrl = __instance.GetComponent<BlinkingController>();
                if (ctrl == null && __instance.alive)
                {
                    ctrl = __instance.gameObject.AddComponent<BlinkingController>();
                }
            }
        }
    }

    [HarmonyPatch(typeof(Locale), nameof(Locale.LoadLanguage))]
    public static class Patch_LocaleLoadLanguage
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Plugin.Logger.LogInfo("Locale.LoadLanguage Postfix: Injecting ModSettings translation keys...");
            Plugin.AddTranslations();
        }
    }
}
