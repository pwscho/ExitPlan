namespace ExitPlan.Core
{
    /// <summary>
    /// Guardrail decision made for a simulation year.
    /// A floor hit is a cut that stopped at <see cref="GuardrailSettings.MinSpendingFloor"/>,
    /// not a full <see cref="GuardrailSettings.LowerCut"/>.
    /// </summary>
    public enum GuardrailYearAction
    {
        None,
        Cut,
        FloorHit,
        Raise
    }
}
