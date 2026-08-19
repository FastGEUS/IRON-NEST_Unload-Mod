namespace IronNestGunMod
{
    /// <summary>
    /// Shared runtime state for the unload mechanic and (later) the fouling mechanic.
    /// </summary>
    public static class ModState
    {
        public static float BreechFouling = 0f;
        public static float FiringPinFouling = 0f;

        public static bool ExtractionInProgress = false;
        public static bool ExtractionStuck = false;

        public static void ResetAll()
        {
            BreechFouling = 0f;
            FiringPinFouling = 0f;
            ExtractionInProgress = false;
            ExtractionStuck = false;
        }
    }
}
