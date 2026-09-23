namespace TeflonTed.Common
{
    /// <summary>Hardcoded search radii — no config knobs.</summary>
    public static class Radii
    {
        /// <summary>Craft / sort: workstation-scale reach.</summary>
        public const float Workstation = 20f;

        /// <summary>Fuel pull: intentionally tight so general storage is not drained.</summary>
        public const float FuelAdjacency = 2.5f;
    }
}
