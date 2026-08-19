namespace IronNestGunMod
{
    /// <summary>
    /// Tunable parameters — starting guesses, calibrate after playtesting.
    /// </summary>
    public static class ModConfig
    {
        public static float ExtractionStuckChanceAtMaxFouling = 0.35f; // 35% at 100 fouling
        public static float MisfireRiskThreshold = 60f;
        public static float PowderVentRecoveryPercent = 0.6f; // 60% of charge returned, 40% lost
    }
}
