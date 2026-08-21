using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;


namespace IronNestGunMod
{
    /// <summary>
    /// FIX HISTORY:
    /// 1) SyncPowderCharges() was fighting the game's own internal logic
    ///    every frame during the unload/reset window (log spam of
    ///    hundreds of "Synced Gun.PowderCharges 0 -> 1" per second).
    ///    Fixed by gating it behind IsUnloading.
    /// 2) REGRESSION (found via log at 22:07:18): after IsUnloading flips
    ///    back to false, the spam came back — because
    ///    PowderController.currentSelectedCharges was never reset to 0
    ///    during TriggerUnload(). The game clears Gun.PowderCharges to 0
    ///    on its own, but our sync kept reading the STALE
    ///    currentSelectedCharges (still 1) and setting Gun.PowderCharges
    ///    back to 1, forever, once IsUnloading stopped blocking the sync.
    ///    Fixed by explicitly zeroing PowderController.currentSelectedCharges
    ///    and calling ResetAll() inside TriggerUnload(), same as the very
    ///    first working version did before it was dropped in a refactor.
    /// 3) LOGGING: every step of TriggerUnload/FinishUnloadAfterAnimation
    ///    now logs on success too (not just on exception), and IsUnloading
    ///    transitions are logged explicitly, so a single click's full log
    ///    trail can be reconstructed end-to-end without needing per-frame
    ///    spam. SyncPowderCharges' "skipped because unloading" is logged
    ///    only once per skip window, not every frame.
    /// </summary>
    public class GunUnloadHandler
    {
        public string Label;
        public GunController Gun;
        public ArtilleryReloadController ReloadController;
        public PowderChargeController PowderController;
        public CylinderShellSelector ShellSelector;


        public bool IsUnloading { get; private set; }

        private bool _loggedSyncSkipThisWindow = false;


        public bool IsReady()
        {
            if (Gun == null || ReloadController == null)
                return false;


            return Gun.CanFire;
        }


        /// <summary>
        /// Keeps Gun.PowderCharges in sync with
        /// PowderController.currentSelectedCharges — but ONLY while no
        /// unload sequence is in progress for this gun, to avoid fighting
        /// the game's own reset logic (see class remarks).
        /// </summary>
        public void SyncPowderCharges()
        {
            if (IsUnloading)
            {
                if (!_loggedSyncSkipThisWindow)
                {
                    _loggedSyncSkipThisWindow = true;
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] SyncPowderCharges skipped for the duration of the unload window (IsUnloading=true).");
                }
                return;
            }
            _loggedSyncSkipThisWindow = false;


            if (Gun == null || PowderController == null)
                return;


            int desired = PowderController.currentSelectedCharges;
            int current = Gun.PowderCharges;


            if (desired != current)
            {
                try
                {
                    Gun.SetPowderCharge(desired);
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] Synced Gun.PowderCharges {current} -> {desired}.");
                }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] SyncPowderCharges SetPowderCharge threw: {e}");
                }
            }
        }


        public void TriggerUnload()
        {
            // Full state snapshot BEFORE doing anything — the single most
            // useful line for reconstructing "what the gun looked like at
            // the moment of the click".
            string preStateKey = ReloadController?.CurrentState?.stateKey ?? "NULL";
            int preGunCharges = Gun != null ? Gun.PowderCharges : -1;
            int preControllerCharges = PowderController != null ? PowderController.currentSelectedCharges : -1;
            bool preCanFire = Gun != null && Gun.CanFire;
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] TriggerUnload() called. Snapshot: CanFire={preCanFire}, Gun.PowderCharges={preGunCharges}, PowderController.currentSelectedCharges={preControllerCharges}, ReloadState='{preStateKey}', IsUnloading={IsUnloading}.");


            if (!IsReady())
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Not ready — gun must be able to fire. Aborting TriggerUnload().");
                return;
            }


            if (IsUnloading)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Already unloading — ignoring duplicate click.");
                return;
            }


            IsUnloading = true;
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] IsUnloading: False -> True.");


            int chargesBeforeUnload = GetBestChargeCount();
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] GetBestChargeCount() = {chargesBeforeUnload} (fromController={preControllerCharges}, fromGun={preGunCharges}).");


            ShellBlueprint chamberedBlueprint = null;
            try
            {
                chamberedBlueprint = Gun.ChamberedShellBlueprint;
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ChamberedShellBlueprint read: {(chamberedBlueprint != null ? chamberedBlueprint.ToString() : "null")}.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Reading ChamberedShellBlueprint threw: {e}");
            }
            ShellDefinition shellDef = ExtractShellDefinition(chamberedBlueprint);
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] ExtractShellDefinition() = {(shellDef != null ? shellDef.ToString() : "null")}.");


            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Unloading with chargesBeforeUnload={chargesBeforeUnload}.");


            try
            {
                ReloadController.ForceResetStateToInitial();
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ForceResetStateToInitial() succeeded.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ForceResetStateToInitial threw: {e}");
            }


            try
            {
                ReloadController.ResetAnimators();
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAnimators() succeeded.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAnimators threw: {e}");
            }


            try
            {
                ReloadController.chamberedShell = null;
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] chamberedShell cleared.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Clearing chamberedShell threw: {e}");
            }


            try
            {
                Gun.pendingReload = true;
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] pendingReload set to true.");
            }
            catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] Setting pendingReload=true threw: {e}"); }

            try
            {
                Gun.hasFired = false;
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] hasFired set to false.");
            }
            catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] Setting hasFired=false threw: {e}"); }


            try
            {
                Gun.ResetElevation();
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetElevation() succeeded.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetElevation threw: {e}");
            }


            if (PowderController != null)
            {
                // --- REGRESSION FIX (see class remarks, item 2) ---
                try
                {
                    PowderController.currentSelectedCharges = 0;
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] PowderController.currentSelectedCharges reset to 0.");
                }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] Resetting currentSelectedCharges threw: {e}");
                }

                try
                {
                    PowderController.ResetAllUsedDispensers();
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] PowderController.ResetAllUsedDispensers() succeeded.");
                }
                catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAllUsedDispensers threw: {e}"); }

                try
                {
                    PowderController.ResetAll();
                    MelonLogger.Msg($"[GunUnloadHandler:{Label}] PowderController.ResetAll() succeeded.");
                }
                catch (Exception e) { MelonLogger.Msg($"[GunUnloadHandler:{Label}] ResetAll threw: {e}"); }
            }
            else
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] No PowderController reference — skipped dispenser reset.");
            }


            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Reset triggered. pendingReload={Gun.pendingReload}. Waiting {ModConfig.ResetAnimationDuration}s...");


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
            yield return new WaitForSeconds(ModConfig.ResetAnimationDuration);


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


            bool fullRecovery = UnityEngine.Random.Range(0f, 1f) < ModConfig.FullPowderRecoveryChance;
            int chargesToRestore = fullRecovery ? chargesBeforeUnload : Mathf.Max(0, chargesBeforeUnload - 1);
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] Powder recovery roll: fullRecovery={fullRecovery}, chargesBeforeUnload={chargesBeforeUnload}, chargesToRestore={chargesToRestore}.");


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
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] No powder charges to restore (chargesBeforeUnload={chargesBeforeUnload}, InventoryInstance={(PowderChargeInventory.Instance != null ? "present" : "null")}).");
            }


            try
            {
                Gun.pendingReload = false;
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] pendingReload set to false.");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Resetting pendingReload=false threw: {e}");
            }


            // Double-check right before we resume syncing: if
            // currentSelectedCharges somehow drifted again during the 12s
            // window, log it loudly so we can tell "our reset didn't
            // stick" apart from any other cause.
            if (PowderController != null && Gun != null)
            {
                MelonLogger.Msg($"[GunUnloadHandler:{Label}] Pre-resume check: PowderController.currentSelectedCharges={PowderController.currentSelectedCharges}, Gun.PowderCharges={Gun.PowderCharges}.");
            }


            IsUnloading = false;
            MelonLogger.Msg($"[GunUnloadHandler:{Label}] IsUnloading: True -> False.");


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
