namespace ElementalStarSea.Agent
{
    // U25 entry only; material, electrolysis and mature state remain explicitly pending.
    public static class MagneticPlateauEntryBehavior
    {
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentCatalog catalog)
        {
            if (e.type != "REGION_ENTERED" || e.subject != "magnetic_plateau" || e.region != "magnetic_plateau" || c.region != "magnetic_plateau") return null;
            if (!string.IsNullOrEmpty(e.behavior_profile) && e.behavior_profile != "magnetic_plateau") return null;
            var p = catalog.ForRegion("magnetic_plateau"); var env = c.environment;
            bool observed = env.ready && env.region == c.region && env.has_electrical_activity &&
                !float.IsNaN(env.electrical_activity) && !float.IsInfinity(env.electrical_activity) && env.electrical_activity >= p.minimum_electrical_activity;
            string id = p.Reaction(observed ? "environment_electricity" : "entry");
            return new AgentDecision { action = "FOLLOW_UP", priority = 40, reaction_id = id, cooldown_key = id,
                profile_id = "magnetic_plateau", reason = observed ? "authored_environment_electricity" : "visual_region_observation_only" };
        }
    }
}
