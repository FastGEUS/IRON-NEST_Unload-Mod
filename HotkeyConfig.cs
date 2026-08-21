using System;
using UnityEngine.InputSystem;
using MelonLoader;

namespace IronNestGunMod
{
    /// <summary>
    /// User-configurable hotkeys, backed by MelonLoader's MelonPreferences
    /// system. This automatically creates/updates a human-editable section
    /// in the game's UserData/MelonPreferences.cfg file, so players can
    /// remap the unload hotkeys without touching any code.
    ///
    /// IMPORTANT: this class's static constructor (which creates the
    /// category and writes the file) only runs on first access to one of
    /// the static members below — standard C# lazy static init. Callers
    /// MUST call EnsureInitialized() as early as possible (e.g. from the
    /// UnloadButtonsUI constructor, which runs right at mod load) rather
    /// than relying on it being touched incidentally later (e.g. only
    /// after guns are resolved on a scene) — otherwise the config section
    /// won't exist in the file until much later than players expect,
    /// which is exactly the bug reported: "the file doesn't update".
    /// </summary>
    public static class HotkeyConfig
    {
        private const string CategoryId = "UnloadButtons";
        private const string CategoryDisplayName = "Per-Gun Unload Buttons - Hotkeys";

        private const string DefaultLeftKey = "Digit8";
        private const string DefaultRightKey = "Digit9";
        private const string DefaultToggleKey = "Digit0";

        private static readonly MelonPreferences_Category s_category;
        private static readonly MelonPreferences_Entry<string> s_leftKeyEntry;
        private static readonly MelonPreferences_Entry<string> s_rightKeyEntry;
        private static readonly MelonPreferences_Entry<string> s_toggleKeyEntry;

        static HotkeyConfig()
        {
            MelonLogger.Msg("[HotkeyConfig] Static initializer running — creating/loading preferences category.");

            s_category = MelonPreferences.CreateCategory(CategoryId, CategoryDisplayName);

            s_leftKeyEntry = s_category.CreateEntry(
                "LeftGunKey",
                DefaultLeftKey,
                "Left gun unload key",
                "Key that unloads the gun whose name contains \"Left\". Must match a UnityEngine.InputSystem.Key name, e.g. Digit8, F6, Numpad8.");

            s_rightKeyEntry = s_category.CreateEntry(
                "RightGunKey",
                DefaultRightKey,
                "Right gun unload key",
                "Key that unloads the gun whose name contains \"Right\". Must match a UnityEngine.InputSystem.Key name, e.g. Digit9, F7, Numpad9.");

            s_toggleKeyEntry = s_category.CreateEntry(
                "ToggleHintKey",
                DefaultToggleKey,
                "Toggle hint text key",
                "Key that shows/hides the on-screen hotkey hint. Must match a UnityEngine.InputSystem.Key name, e.g. Digit0, F8, Numpad0.");

            try
            {
                s_category.SaveToFile(false);
                MelonLogger.Msg("[HotkeyConfig] SaveToFile() completed without throwing.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[HotkeyConfig] Saving default preferences to file threw: {e}");
            }

            MelonLogger.Msg($"[HotkeyConfig] Loaded hotkeys: LeftGunKey='{s_leftKeyEntry.Value}', RightGunKey='{s_rightKeyEntry.Value}', ToggleHintKey='{s_toggleKeyEntry.Value}'.");
        }

        /// <summary>
        /// No-op body — its only purpose is to be called from somewhere
        /// that runs early (mod load), so that touching this type forces
        /// the static constructor above to run immediately instead of
        /// whenever some unrelated code first happens to read a hotkey
        /// value. Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
        }

        public static Key LeftGunKey => ParseOrDefault(s_leftKeyEntry.Value, Key.Digit8, "LeftGunKey");
        public static Key RightGunKey => ParseOrDefault(s_rightKeyEntry.Value, Key.Digit9, "RightGunKey");
        public static Key ToggleHintKey => ParseOrDefault(s_toggleKeyEntry.Value, Key.Digit0, "ToggleHintKey");

        private static Key ParseOrDefault(string raw, Key fallback, string entryNameForLogging)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                MelonLogger.Msg($"[HotkeyConfig] '{entryNameForLogging}' is empty in the config file. Using default: {fallback}.");
                return fallback;
            }

            if (Enum.TryParse<Key>(raw.Trim(), ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            MelonLogger.Msg($"[HotkeyConfig] '{entryNameForLogging}' value '{raw}' in the config file is not a valid key name. Using default: {fallback}. Valid names match UnityEngine.InputSystem.Key, e.g. Digit8, F6, Numpad8, LeftShift.");
            return fallback;
        }
    }
}
