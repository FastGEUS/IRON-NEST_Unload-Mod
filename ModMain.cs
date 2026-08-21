using MelonLoader;

[assembly: MelonInfo(typeof(IronNestGunMod.ModMain), "Unload Mod", "2.0-beta", "Svet")]
[assembly: MelonGame("Iron Nest", "Iron Nest Heavy Turret Simulator")]

namespace IronNestGunMod
{
    public class ModMain : MelonMod
    {
        private readonly UnloadButtonsUI _ui = new UnloadButtonsUI();

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Unload mod loaded.");
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
