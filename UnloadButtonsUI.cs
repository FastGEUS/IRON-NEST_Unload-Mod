using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using MelonLoader;
using Il2Cpp;

namespace IronNestGunMod
{
    /// <summary>
    /// UI overhaul: no clickable GUI.Button elements. Mouse clicks on an
    /// IMGUI overlay silently fail whenever Cursor.lockState == Locked
    /// (confirmed via diagnostic: Unity reports the mouse position as
    /// (-10000, -10000) while the cursor is locked, e.g. during normal
    /// turret aiming). Unload is triggered purely by keyboard hotkeys
    /// (Keyboard.current, Unity's new Input System), which work
    /// identically regardless of cursor lock state.
    ///
    /// Hotkeys are user-configurable via HotkeyConfig, which is backed by
    /// MelonPreferences and creates an editable section in
    /// UserData/MelonPreferences.cfg. Defaults: 8 = left gun, 9 = right
    /// gun, 0 = toggle the on-screen hint.
    /// </summary>
    public class UnloadButtonsUI
    {
        private static int s_instanceCounter = 0;
        private static int s_activeInstanceId = -1;
        private readonly int _instanceId;
        private bool _loggedSuperseded = false;

        private readonly List<GunUnloadHandler> _handlers = new List<GunUnloadHandler>();
        private bool _resolved = false;
        private int _resolveRequestId = 0;

        private GunUnloadHandler _leftHandler;
        private GunUnloadHandler _rightHandler;
        private readonly List<GunUnloadHandler> _unassignedHandlers = new List<GunUnloadHandler>();

        private bool _hintVisible = true;

        // Built lazily on first OnGUI() call — IL2CPP's GUIStyle interop
        // wrapper only exposes the parameterless/IntPtr constructors, not
        // GUIStyle(GUIStyle other), so we build a fresh style and set the
        // properties we need by hand instead of copying from
        // GUI.skin.label.
        private GUIStyle _hintStyle;

        public UnloadButtonsUI()
        {
            _instanceId = ++s_instanceCounter;
            s_activeInstanceId = _instanceId;
            MelonLogger.Msg($"[UnloadButtonsUI] Instance #{_instanceId} created and is now the active instance.");

            // FIX: previously HotkeyConfig's static constructor only ran
            // whenever OnGUI() first happened to read a hotkey value —
            // which only happens AFTER guns are resolved on a scene. If
            // the player checked UserData/MelonPreferences.cfg before
            // ever loading into a scene with guns, the section simply
            // didn't exist yet ("mod isn't updating MelonPreferences").
            // Forcing initialization here, at mod construction time,
            // guarantees the config file section exists immediately on
            // game start, regardless of whether any guns are ever found.
            HotkeyConfig.EnsureInitialized();
        }

        private bool IsActiveInstance => _instanceId == s_activeInstanceId;

        public void OnSceneWasLoaded()
        {
            s_activeInstanceId = _instanceId;

            _resolved = false;
            _handlers.Clear();
            _leftHandler = null;
            _rightHandler = null;
            _unassignedHandlers.Clear();
            _resolveRequestId++;
            MelonCoroutines.Start(DelayedResolve(_resolveRequestId));
        }

        private System.Collections.IEnumerator DelayedResolve(int requestId)
        {
            yield return new WaitForSeconds(2f);
            if (requestId != _resolveRequestId)
            {
                MelonLogger.Msg("[UnloadButtonsUI] Skipping stale resolve request (a newer scene load superseded it).");
                yield break;
            }
            ResolveGuns();
        }

        private void ResolveGuns()
        {
            try
            {
                _handlers.Clear();
                _leftHandler = null;
                _rightHandler = null;
                _unassignedHandlers.Clear();

                var guns = UnityEngine.Object.FindObjectsOfType<GunController>();
                var powderControllers = UnityEngine.Object.FindObjectsOfType<PowderChargeController>();
                var shellSelectors = UnityEngine.Object.FindObjectsOfType<CylinderShellSelector>();

                MelonLogger.Msg($"[UnloadButtonsUI] Found {guns?.Length ?? 0} gun(s), {powderControllers?.Length ?? 0} powder controller(s), {shellSelectors?.Length ?? 0} shell selector(s).");

                if (guns == null || guns.Length == 0)
                {
                    MelonLogger.Msg("[UnloadButtonsUI] No GunController found in scene.");
                    return;
                }

                int index = 0;
                foreach (var gun in guns)
                {
                    string gunName;
                    try { gunName = gun.gunName; }
                    catch { gunName = null; }

                    var handler = new GunUnloadHandler
                    {
                        Label = string.IsNullOrEmpty(gunName) ? $"Gun {index + 1}" : gunName,
                        Gun = gun,
                    };

                    try { handler.ReloadController = gun.artilleryReloadController; }
                    catch (Exception e)
                    {
                        MelonLogger.Msg($"[UnloadButtonsUI] Reading gun.artilleryReloadController threw: {e}");
                    }

                    if (handler.ReloadController != null)
                    {
                        if (powderControllers != null)
                        {
                            foreach (var pc in powderControllers)
                            {
                                if (pc.reloadController == handler.ReloadController)
                                {
                                    handler.PowderController = pc;
                                    break;
                                }
                            }
                        }

                        if (shellSelectors != null)
                        {
                            foreach (var sel in shellSelectors)
                            {
                                if (sel.artilleryReloadController == handler.ReloadController)
                                {
                                    handler.ShellSelector = sel;
                                    break;
                                }
                            }
                        }
                    }

                    _handlers.Add(handler);
                    index++;
                }

                // Assign hotkeys by NAME, not by array order — Unity does
                // not guarantee FindObjectsOfType() ordering, so relying
                // on index would risk silently swapping which key
                // unloads which gun between scene loads.
                foreach (var handler in _handlers)
                {
                    string labelLower = handler.Label?.ToLowerInvariant() ?? "";
                    if (labelLower.Contains("left") && _leftHandler == null)
                    {
                        _leftHandler = handler;
                    }
                    else if (labelLower.Contains("right") && _rightHandler == null)
                    {
                        _rightHandler = handler;
                    }
                    else
                    {
                        _unassignedHandlers.Add(handler);
                    }
                }

                _resolved = true;
                MelonLogger.Msg($"[UnloadButtonsUI] Resolved {_handlers.Count} gun(s). Left='{_leftHandler?.Label ?? "NONE"}' (key {HotkeyConfig.LeftGunKey}), Right='{_rightHandler?.Label ?? "NONE"}' (key {HotkeyConfig.RightGunKey}).");

                if (_unassignedHandlers.Count > 0)
                {
                    var names = string.Join(", ", _unassignedHandlers.ConvertAll(h => h.Label));
                    MelonLogger.Msg($"[UnloadButtonsUI] Warning: {_unassignedHandlers.Count} gun(s) have no 'Left'/'Right' in their name and got no hotkey assigned: {names}.");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[UnloadButtonsUI] ResolveGuns() threw a top-level exception: {e}");
            }
        }

        public void OnGUI()
        {
            if (!IsActiveInstance)
            {
                if (!_loggedSuperseded)
                {
                    _loggedSuperseded = true;
                    MelonLogger.Msg($"[UnloadButtonsUI] Instance #{_instanceId} is no longer the active instance (active is #{s_activeInstanceId}). This instance will stop drawing/handling input.");
                }
                return;
            }

            if (!_resolved || _handlers.Count == 0)
                return;

            bool isRepaint = Event.current != null && Event.current.type == EventType.Repaint;
            var keyboard = Keyboard.current;

            // Hotkeys are re-read from HotkeyConfig every frame (cheap:
            // MelonPreferences_Entry.Value is just a cached field read,
            // no file I/O or re-parsing happens here) so a config file
            // edit takes effect on the very next frame without needing a
            // mod/scene reload.
            Key leftKey = HotkeyConfig.LeftGunKey;
            Key rightKey = HotkeyConfig.RightGunKey;
            Key toggleKey = HotkeyConfig.ToggleHintKey;

            // Poll hotkeys once per rendered frame (Repaint pass only),
            // independent of Cursor.lockState — this is what makes the
            // action work even while the player is actively aiming.
            if (isRepaint && keyboard != null)
            {
                var toggleControl = keyboard[toggleKey];
                if (toggleControl != null && toggleControl.wasPressedThisFrame)
                {
                    _hintVisible = !_hintVisible;
                    MelonLogger.Msg($"[UnloadButtonsUI] Hint visibility toggled: {_hintVisible}.");
                }

                TryTriggerFromHotkey(_leftHandler, leftKey, keyboard);
                TryTriggerFromHotkey(_rightHandler, rightKey, keyboard);
            }

            if (!_hintVisible)
                return;

            string hintText = BuildHintText(leftKey, rightKey, toggleKey);

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle();
                _hintStyle.fontSize = 16;
                _hintStyle.richText = false;
                _hintStyle.wordWrap = false;
                _hintStyle.normal.textColor = Color.white;
            }

            var rect = new Rect(20, 20, 480, 24 * (_handlers.Count + 1) + 10);

            // Plain background box for legibility only — not
            // interactive, does not consume input events.
            UnityEngine.GUI.Box(rect, GUIContent.none);
            UnityEngine.GUI.Label(rect, hintText, _hintStyle);
        }

        private void TryTriggerFromHotkey(GunUnloadHandler handler, Key key, Keyboard keyboard)
        {
            if (handler == null)
                return;

            var control = keyboard[key];
            if (control == null || !control.wasPressedThisFrame)
                return;

            bool ready = false;
            bool canFire = false;
            int powderCharges = -1;
            string stateKey = "?";

            try
            {
                ready = handler.IsReady();
                if (handler.Gun != null)
                {
                    canFire = handler.Gun.CanFire;
                    powderCharges = handler.Gun.PowderCharges;
                }
                if (handler.ReloadController != null)
                {
                    stateKey = handler.ReloadController.CurrentState?.stateKey ?? "NULL";
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[UnloadButtonsUI] [{handler.Label}] Reading live diagnostic values threw: {e}");
            }

            MelonLogger.Msg($"[UnloadButtonsUI] Unload triggered for '{handler.Label}' via hotkey {key} (ready={ready}, CanFire={canFire}, Powder={powderCharges}, State='{stateKey}').");

            try
            {
                try { handler.SyncPowderCharges(); }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[UnloadButtonsUI] [{handler.Label}] SyncPowderCharges threw: {e}");
                }

                handler.TriggerUnload();
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[UnloadButtonsUI] [{handler.Label}] TriggerUnload() threw: {e}");
            }
        }

        private string BuildHintText(Key leftKey, Key rightKey, Key toggleKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine("UNLOAD MOD by SVET");
            sb.AppendLine($"Toggle this hint - [{toggleKey}]");
            AppendGunLine(sb, _leftHandler, leftKey);
            AppendGunLine(sb, _rightHandler, rightKey);

            foreach (var handler in _unassignedHandlers)
            {
                AppendGunLine(sb, handler, null);
            }

            return sb.ToString();
        }

        private void AppendGunLine(StringBuilder sb, GunUnloadHandler handler, Key? key)
        {
            if (handler == null)
                return;
            bool ready = false;
            bool canFire = false;
            int powderCharges = -1;

            try
            {
                try { handler.SyncPowderCharges(); }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[UnloadButtonsUI] [{handler.Label}] SyncPowderCharges threw: {e}");
                }

                ready = handler.IsReady();
                if (handler.Gun != null)
                {
                    canFire = handler.Gun.CanFire;
                    powderCharges = handler.Gun.PowderCharges;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[UnloadButtonsUI] [{handler.Label}] Reading live diagnostic values threw: {e}");
            }

            string keyTag = key.HasValue ? $"[{key.Value}]" : "[--]";
            if (handler.Label == "GunLeft") {sb.AppendLine($"Unload Left Gun - {keyTag}");}
            else {sb.AppendLine($"Unload Right Gun - {keyTag}");}
            //sb.AppendLine($"{keyTag} Unload {handler.Label}");
        }
    }
}
