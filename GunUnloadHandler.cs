using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;

namespace IronNestGunMod
{
    /// <summary>
    /// Full "abort and reset to pre-load state":
    ///  - Reload FSM back to its initial state via ForceResetStateToInitial().
    ///  - Animators re-synced via ResetAnimators() — this is what plays the
    ///    ~12 second reset animation.
    ///  - Barrel elevation reset to 0 via ResetElevation().
    ///  - All powder dispenser levers reset via PowderController.ResetAll().
    ///  - AFTER the ~12s animation finishes, the shell is returned to the
    ///    cylinder and powder charges are restored (43% full / else -1).
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

            return Gun.CanFire && Gun.PowderCharges > 0;
        }

        public void TriggerUnload()
        {
            if (!IsReady())
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Not ready — gun must be loaded and fireable.");
                return;
            }

            // Capture everything BEFORE resetting anything.
            int chargesBeforeUnload = Gun.PowderCharges;
            ShellBlueprint chamberedBlueprint = null;
            try { chamberedBlueprint = Gun.ChamberedShellBlueprint; }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Reading ChamberedShellBlueprint threw: {e}");
            }
            ShellDefinition shellDef = ExtractShellDefinition(chamberedBlueprint);

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

            try { Gun.SetPowderCharge(0); }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] SetPowderCharge(0) threw: {e}");
            }

            try { Gun.ForcePendingReload(); }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ForcePendingReload threw: {e}");
            }

            try { Gun.ResetElevation(); }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetElevation threw: {e}");
            }

            if (PowderController != null)
            {
                try { PowderController.currentSelectedCharges = 0; } catch { }
                try { PowderController.ResetAllUsedDispensers(); }
                catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAllUsedDispensers threw: {e}"); }
                try { PowderController.ResetAll(); }
                catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAll threw: {e}"); }
            }

            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Reset triggered. Waiting {ResetAnimationDuration}s for the animation before returning shell/powder...");

            MelonCoroutines.Start(FinishUnloadAfterAnimation(chargesBeforeUnload, shellDef));
        }

        private IEnumerator FinishUnloadAfterAnimation(int chargesBeforeUnload, ShellDefinition shellDef)
        {
            yield return new WaitForSeconds(ResetAnimationDuration);

            string afterKey = ReloadController.CurrentState?.stateKey ?? "NULL";
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Animation finished. state='{afterKey}' CanFire={Gun.CanFire} elevation={Gun.CurrentElevation}");

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

            if (PowderChargeInventory.Instance != null)
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
