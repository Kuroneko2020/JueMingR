namespace NativeWorldTextProbe
{
    // Ownership-only research has its explicit diagnostic scope. This complete
    // formal responsibility has no inherited selector or successful early exit.
    internal static class NativeCombatObservationSuite
    {
        internal static void Run(object context)
        {
            NativeCombatObservationChecks.Run(context);
            NativeCombatBodyChecks.Run(context);
            NativeCombatTerrainChecks.Run(context);
            NativeCombatPredictionChecks.Run(context);
            NativeCombatEventChecks.Run(context);
            NativeCombatGeometryChecks.Run(context);
        }
    }
}
