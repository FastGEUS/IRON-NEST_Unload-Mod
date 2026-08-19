using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;

namespace IronNestGunMod
{
    /// <summary>
    /// CONFIRMED via diagnostic log: PowderController.currentSelectedCharges
    /// correctly tracks what the player physically dispenses via the levers
    /// (it read 3 after a real reload), while Gun.PowderCharges — the field
    /// ballistics actually reads at fire time — stayed stuck at 0 and never
    /// re-synced on its own after our unload sequence.
    ///
    /// Fix: continuously (but cheaply — only on mismatch) sync
    /// Gun.PowderCharges to match PowderController.currentSelectedCharges,
    /// via the public SyncPowderCharges() method called every frame from
    /// UnloadButtonsUI.OnGUI().
    /// </summary>
    public class GunUnloadHandler
    {
        public string Label;
        public GunController Gun;
        public ArtilleryReloadController ReloadController;
        public PowderChargeController PowderController;
        public CylinderShellSelector ShellSelector;

        private const float ResetAnimationDuration = 12f;

        public bool IsReady()
        {
            if (Gun == null || ReloadController == null)
                return false;

            return Gun.CanFire;
        }

        /// <summary>
        /// Keeps Gun.PowderCharges (what ballistics reads) in sync with
        /// PowderController.currentSelectedCharges (what the player actually
        /// dispensed via the levers). Only writes when there's a real
        /// mismatch, to avoid spamming SetPowderCharge() every frame.
        /// </summary>
        public void SyncPowderCharges()
        {
            if (Gun == null || PowderController == null)
                return;

            int desired = PowderController.currentSelectedCharges;
            int current = Gun.PowderCharges;

            if (desired != current)
            {
                try
                {
                    Gun.SetPowderCharge(desired);
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] Synced Gun.PowderCharges {current} -> {desired} (from PowderController.currentSelectedCharges).");
                }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] SyncPowderCharges SetPowderCharge threw: {e}");
                }
            }
        }

        public void TriggerUnload()
        {
            if (!IsReady())
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Not ready — gun must be able to fire.");
                return;
            }

            int chargesBeforeUnload = GetBestChargeCount();

            ShellBlueprint chamberedBlueprint = null;
            try { chamberedBlueprint = Gun.ChamberedShellBlueprint; }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Reading ChamberedShellBlueprint threw: {e}");
            }
            ShellDefinition shellDef = ExtractShellDefinition(chamberedBlueprint);

            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Unloading with chargesBeforeUnload={chargesBeforeUnload}.");

            try { ReloadController.ForceResetStateToInitial(); }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ForceResetStateToInitial threw: {e}");
            }

            try { ReloadController.ResetAnimators(); }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAnimators threw: {e}");
            }

            try { ReloadController.chamberedShell = null; }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Clearing chamberedShell threw: {e}");
            }

            try { Gun.pendingReload = true; } catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] Setting pendingReload=true threw: {e}"); }
            try { Gun.hasFired = false; } catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] Setting hasFired=false threw: {e}"); }

            try { Gun.ResetElevation(); }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetElevation threw: {e}");
            }

            if (PowderController != null)
            {
                try { PowderController.ResetAllUsedDispensers(); }
                catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAllUsedDispensers threw: {e}"); }
            }

            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Reset triggered. pendingReload={Gun.pendingReload}. Waiting {ResetAnimationDuration}s...");

            MelonCoroutines.Start(FinishUnloadAfterAnimation(chargesBeforeUnload, shellDef));
        }

        private int GetBestChargeCount()
        {
            int fromController = PowderController != null ? PowderController.currentSelectedCharges : 0;
            int fromGun = Gun.PowderCharges;
            return Math.Max(fromController, fromGun);
        }

        private IEnumerator FinishUnloadAfterAnimation(int chargesBeforeUnload, ShellDefinition shellDef)
        {
            yield return new WaitForSeconds(ResetAnimationDuration);

            string afterKey = ReloadController.CurrentState?.stateKey ?? "NULL";
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Animation finished. state='{afterKey}' elevation={Gun.CurrentElevation}");

            if (ShellSelector != null && shellDef != null)
            {
                try
                {
                    bool inserted = ShellSelector.TryInsertShellRuntime(shellDef, ShellSlotPool.ShellSource.Mission, out int slotIndex);
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] TryInsertShellRuntime returned {inserted}, slot {slotIndex}.");
                }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] TryInsertShellRuntime threw: {e}.");
                }
            }
            else
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Warning: no ShellSelector or no ShellDefinition — shell not returned to cylinder.");
            }

            bool fullRecovery = UnityEngine.Random.Range(0f, 1f) < 0.43f;
            int chargesToRestore = fullRecovery ? chargesBeforeUnload : Mathf.Max(0, chargesBeforeUnload - 1);

            if (PowderChargeInventory.Instance != null && chargesBeforeUnload > 0)
            {
                if (fullRecovery)
                {
                    PowderChargeInventory.Instance.AddCharges(chargesBeforeUnload);
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] Powder fully recovered ({chargesBeforeUnload}).");
                }
                else
                {
                    PowderChargeInventory.Instance.AddCharges(chargesToRestore);
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] Lost 1 charge, recovered {chargesToRestore}/{chargesBeforeUnload}.");
                }
            }
            else
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] No powder charges to restore (chargesBeforeUnload={chargesBeforeUnload}).");
            }

            try { Gun.pendingReload = false; }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Resetting pendingReload=false threw: {e}");
            }

            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Unload fully complete. pendingReload={Gun.pendingReload} CanFire={Gun.CanFire}. Gun is reusable.");
        }

        private ShellDefinition ExtractShellDefinition(ShellBlueprint blueprint)
        {
            if (blueprint == null)
                return null;

            try
            {
                var type = blueprint.GetType();

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.PropertyType == typeof(ShellDefinition))
                    {
                        var value = prop.GetValue(blueprint) as ShellDefinition;
                        if (value != null)
                            return value;
                    }
                }

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(ShellDefinition))
                    {
                        var value = field.GetValue(blueprint) as ShellDefinition;
                        if (value != null)
                            return value;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ExtractShellDefinition threw: {e}");
                return null;
            }
        }
    }
}
