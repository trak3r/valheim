namespace TeflonTed.Common
{
    /// <summary>Hardcoded search radii — no config knobs.</summary>
    public static class Radii
    {
        /// <summary>Craft / sort: workstation-scale reach.</summary>
        public const float Workstation = 20f;

        /// <summary>
        /// Fuel pull: tight enough to avoid draining general storage, wide enough for
        /// large pieces like the charcoal kiln (~4m footprint).
        /// </summary>
        public const float FuelAdjacency = 4f;
    }
}
