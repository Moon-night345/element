namespace ElementalStarSea.Agent
{
    // U21 entry branch only. Remaining Sand material/hypothesis/failure/maturity logic is not implied.
    public static class SandDesertEntryBehavior
    {
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentCatalog catalog)
        {
            if (e.type != "REGION_ENTERED" || e.subject != "sand_desert" || e.region != "sand_desert" || c.region != "sand_desert") return null;
            if (!string.IsNullOrEmpty(e.behavior_profile) && e.behavior_profile != "sand_desert") return null;
            var p = catalog.ForRegion("sand_desert");
            string id = p.Reaction(CrossRegionRecall.HasRelevantOldKnowledge(c, catalog) ? "entry_recall" : "entry");
            return new AgentDecision { action = p.entry_action, reaction_id = id, cooldown_key = id, priority = 40,
                profile_id = "sand_desert", reason = "sand_entry_confirmed_context" };
        }
    }
}
