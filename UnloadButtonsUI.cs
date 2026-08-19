using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using Il2Cpp;

namespace IronNestGunMod
{
    public class UnloadButtonsUI
    {
        private readonly List<GunUnloadHandler> _handlers = new List<GunUnloadHandler>();
        private bool _resolved = false;
        private bool _loggedGuiError = false;
        private int _resolveRequestId = 0;

        public void OnSceneWasLoaded()
        {
            _resolved = false;
            _loggedGuiError = false;
            _handlers.Clear();
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

                    MelonLogger.Msg($"[UnloadButtonsUI] Gun '{handler.Label}': Reload={(handler.ReloadController != null ? "OK" : "NULL")}, Powder={(handler.PowderController != null ? "OK" : "NULL")}, Selector={(handler.ShellSelector != null ? "OK" : "NULL")}");

                    _handlers.Add(handler);
                    index++;
                }

                _resolved = true;
                MelonLogger.Msg($"[UnloadButtonsUI] Resolved {_handlers.Count} gun(s). Buttons should now be visible top-left.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[UnloadButtonsUI] ResolveGuns() threw a top-level exception: {e}");
            }
        }

        public void OnGUI()
        {
            if (!_resolved || _handlers.Count == 0)
                return;

            try
            {
                int y = 20;
                const int width = 200;
                const int height = 36;
                const int spacing = 8;

                foreach (var handler in _handlers)
                {
                    bool ready;
                    try { ready = handler.IsReady(); }
                    catch (Exception e)
                    {
                        if (!_loggedGuiError)
                        {
                            MelonLogger.Msg($"[UnloadButtonsUI] handler.IsReady() threw: {e}");
                            _loggedGuiError = true;
                        }
                        ready = false;
                    }

                    UnityEngine.GUI.enabled = ready;
                    string label = ready ? $"Unload {handler.Label}" : $"{handler.Label} (not ready)";

                    bool clicked = UnityEngine.GUI.Button(new Rect(20, y, width, height), label);
                    if (clicked)
                    {
                        MelonLogger.Msg($"[UnloadButtonsUI] Button clicked for '{handler.Label}' (ready={ready}).");
                        try { handler.TriggerUnload(); }
                        catch (Exception e)
                        {
                            MelonLogger.Msg($"[UnloadButtonsUI] TriggerUnload() threw: {e}");
                        }
                    }

                    UnityEngine.GUI.enabled = true;
                    y += height + spacing;
                }
            }
            catch (Exception e)
            {
                if (!_loggedGuiError)
                {
                    MelonLogger.Msg($"[UnloadButtonsUI] OnGUI() threw a top-level exception: {e}");
                    _loggedGuiError = true;
                }
            }
        }
    }
}
