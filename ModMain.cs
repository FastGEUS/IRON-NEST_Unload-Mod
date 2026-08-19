using MelonLoader;

[assembly: MelonInfo(typeof(IronNestGunMod.ModMain), "Per-Gun Unload Buttons", "0.1.0", "Svet")]
[assembly: MelonGame("Iron Nest", "Iron Nest Heavy Turret Simulator")]

namespace IronNestGunMod
{
    public class ModMain : MelonMod
    {
        private readonly UnloadButtonsUI _ui = new UnloadButtonsUI();

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Per-Gun Unload Buttons mod loaded.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _ui.OnSceneWasLoaded();
        }

        public override void OnGUI()
        {
            _ui.OnGUI();
        }

        public override void OnDeinitializeMelon()
        {
            ModState.ResetAll();
        }
    }
}
