using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;
using ElementalStarSea.Agent;

namespace ElementalStarSea.EditorTools
{
    public static class AgentAcceptance
    {
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static GameState State(string region = "dead_city_ruins")
        { var state = new GameState { currentRegion = region }; state.AddVisitedRegion(region); return state; }
        static string Gameplay(GameState state)
        {
            var clone = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            clone.agent = null;
            return JsonUtility.ToJson(clone);
        }
        public static void ProfileLookupAndEntries()
        {
            var catalog = AgentCatalog.Load();
            Require(catalog.profiles.Select(p => p.region_id).Distinct().Count() == 14 && catalog.generic_regions.Distinct().Count() == 3, "profile identity contract");
            foreach (var region in GameData.Regions.regions)
            {
                var profile = catalog.ForRegion(region.id);
                Require((profile != null) != catalog.generic_regions.Contains(region.id), "ambiguous/missing lookup " + region.id);
                var state = State(region.id);
                string before = Gameplay(state);
                var controller = new AgentController(state, catalog, () => new AgentEnvironmentFacts { region = region.id, ready = true,
                    has_temperature = region.id == "crystal_tundra" || region.id == "ember_rift",
                    temperature = region.id == "ember_rift" ? catalog.ember_authored_temperature : catalog.crystal_authored_temperature,
                    has_solution_availability = region.id == "salt_marsh", solution_availability = catalog.salt_authored_solution_availability,
                    has_oxygen = region.id == "fluorescent_grotto", oxygen = catalog.grotto_authored_oxygen, light = catalog.grotto_authored_light });
                var entry = controller.Publish("REGION_ENTERED", region.id, 10000);
                if (profile != null)
                {
                    Require(!entry.Silent && entry.reaction_id == profile.entry_reaction && controller.Queue.Current.text ==
                        catalog.responses.First(r => r.reaction_id == entry.reaction_id).text, "entry/profile/text not connected " + region.id);
                    Require(controller.Publish("REGION_ENTERED", region.id, 20000).reason == "already_shown", "entry repeated " + region.id);
                }
                else Require(entry.Silent, "invented generic region entry");
                Require(Gameplay(state) == before, "entry mutated gameplay " + region.id);
            }
        }
        public static void KnowledgeAndMilestones()
        {
            var catalog = AgentCatalog.Load();
            var state = State();
            var controller = new AgentController(state, catalog);
            const string sample = "item_dead_city_iron_bearing_debris_block";
            state.AddItem(sample);
            string before = Gameplay(state);
            Require(controller.Publish("MATERIAL_COLLECTED", sample, 1000, analyzable: true).reaction_id == "unknown_material_hint", "unknown sample hint");
            Require(controller.Publish("MATERIAL_ANALYZED", sample, 4000).reason == "unconfirmed_knowledge", "forged analysis accepted");
            Require(Gameplay(state) == before && !controller.Queue.Current.text.Contains("铁"), "unknown sample leaked identity / gameplay mutation");
            state.IdentifySample("resource_stone");
            Require(controller.Publish("MATERIAL_ANALYZED", "resource_stone", 4000).reason == "confirmed_facts_unavailable",
                "identification without authored composition invented facts");
            Require(new WorldChemistryService(state).AnalyzeDeadCityBlock(sample).success, "formal analysis fixture");
            before = Gameplay(state);
            Require(controller.Publish("MATERIAL_ANALYZED", sample, 4001).reaction_id == "first_iron_analysis", "confirmed analysis not explained");
            Require(Gameplay(state) == before, "analysis dialogue changed knowledge");
            var detached = new AgentContext(state, catalog);
            detached.known_materials[Array.IndexOf(detached.known_materials, sample)] = "changed_snapshot";
            Require(state.HasIdentifiedSample(sample), "context aliases gameplay arrays");
            var forged = State();
            Require(new AgentController(forged, catalog).Publish("DEVICE_CRAFTED", "device_chemical_workbench", 100).Silent, "forged device milestone accepted");
            var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
            foreach (var ingredient in devices.Devices.First(d => d.id == "device_chemical_workbench").crafting.inputs)
                state.AddItem(ingredient.id, ingredient.n); // manufacturing fixture, not a player-route claim
            Require(devices.Craft("device_chemical_workbench").success, "formal craft fixture");
            before = Gameplay(state);
            Require(controller.Publish("DEVICE_CRAFTED", "device_chemical_workbench", 5000).reaction_id == "workbench_crafted" && state.agent.bootstrap_complete,
                "workbench milestone not observed");
            Require(controller.Publish("MATERIAL_COLLECTED", sample, 10000, analyzable: true).reason == "bootstrap_guidance_complete", "mature onboarding not suppressed");
            Require(Gameplay(state) == before, "milestone agent changed gameplay");
            state.currentRegion = "planetary_core"; state.AddVisitedRegion("planetary_core");
            while (controller.Queue.Current != null) controller.Queue.Advance(100, false);
            Require(!controller.Publish("REGION_ENTERED", "planetary_core", 20000).Silent &&
                controller.Publish("REACTION_OBSERVED", "reaction_n2", 20001).reason == "player_autonomy_window", "unconfirmed final-region reaction inferred a route");
        }
        public static void BudgetsQueueAndRecall()
        {
            var catalog = AgentCatalog.Load(); var state = State(); var controller = new AgentController(state, catalog);
            Require(controller.Publish("PLAYER_AWAKENED", "player", 1000).reaction_id == "awakened_greeting", "awakening");
            Require(controller.Publish("MATERIAL_COLLECTED", "unknown", 1001, analyzable: true).reason == "global_cooldown", "global budget");
            Require(controller.Publish("EXPERIMENT_FAILED", "test", 1002, uiExplains: true).reason == "ui_already_explains", "UI duplicate suppression");
            Require(controller.Publish("EXPERIMENT_FAILED", "test", 1003).action == "WARN" && controller.Queue.Current.priority == 100, "warning did not interrupt hint");
            Require(controller.Publish("EXPERIMENT_FAILED", "test", 2000).reason == "cooldown", "warning cooldown");
            float remaining = controller.Queue.Current.remaining;
            controller.Queue.Advance(100, true);
            Require(controller.Queue.Current.remaining == remaining, "paused panel consumed dialogue");
            controller.Queue.Advance(5, false);
            Require(controller.Queue.Current.reaction_id == "awakened_greeting", "interrupted entry did not resume");
            var e = new AgentEvent { type = "UNHANDLED", subject = "x", region = state.currentRegion, event_id = "stable_event", order = 9, time_ms = 10000 };
            controller.Observe(e); string before = JsonUtility.ToJson(state);
            Require(controller.Observe(e).reason == "duplicate_event" && JsonUtility.ToJson(state) == before, "event dedup changed state");
            for (int i = 0; i < 100; i++) controller.Publish("UNHANDLED", "x", 10001 + i);
            Require(state.agent.recent_event_ids.Length == 64, "recent event bound");
            var policy = new AgentDecisionPolicy(catalog);
            before = JsonUtility.ToJson(state.agent);
            policy.Decide(e, new AgentContext(state, catalog), state.agent, 0);
            Require(JsonUtility.ToJson(state.agent) == before, "policy mutated supplied persistent state");
            // Source-only facts are insufficient without confirmed analysis and region discovery.
            var context = new AgentContext(state, catalog);
            Require(!CrossRegionRecall.HasRegionFact(context, catalog, "crystal_tundra", "carbonate"), "recall invented unvisited knowledge");
            var recallState = State();
            recallState.IdentifySample("item_dead_city_iron_bearing_debris_block");
            var known = new AgentContext(recallState, catalog);
            Require(known.confirmed_analyses.Single().facts.Contains("iron_bearing") &&
                !CrossRegionRecall.HasRegionFact(known, catalog, "crystal_tundra", "carbonate"), "recall copied facts across regions");
        }
        public static void Persistence()
        {
            var catalog = AgentCatalog.Load(); var state = State(); var controller = new AgentController(state, catalog);
            controller.Publish("REGION_ENTERED", state.currentRegion, 1000);
            controller.Publish("EXPERIMENT_FAILED", "test", 1001);
            string path = Path.GetFullPath(Path.Combine("Logs", "u16_agent_test_" + Guid.NewGuid().ToString("N"), "save.json"));
            var repository = new SaveRepository(path, "test", forceSnapshotHistory: true);
            string reason; Require(repository.Write(state, out reason), "agent snapshot save " + reason);
            var result = repository.Read(); Require(result.success, "agent snapshot load");
            var restored = result.state;
            Require(JsonUtility.ToJson(state.agent) == JsonUtility.ToJson(restored.agent), "agent attention/order/cooldown lost");
            var loaded = new AgentController(restored, catalog);
            Require(loaded.Publish("REGION_ENTERED", state.currentRegion, 1002).reason == "already_shown" &&
                loaded.Publish("EXPERIMENT_FAILED", "test", 1003).reason == "cooldown", "restart replay / cooldown lost");
            restored.agent.recent_event_ids = new[] { "duplicate", "duplicate" };
            Require(!repository.Write(restored, out reason) && repository.Read().success, "invalid agent snapshot committed");
            string legacyPath = Path.Combine(Path.GetDirectoryName(path), "pre_agent.json");
            File.WriteAllText(legacyPath, "{\"inventoryIds\":[],\"inventoryCounts\":[],\"currentRegion\":\"prologue_lab\",\"energy\":100,\"hp\":100}");
            var legacy = new SaveRepository(legacyPath, "legacy").Read();
            Require(legacy.success && new AgentController(legacy.state, catalog).Publish("PLAYER_AWAKENED", "player", 0).reaction_id == "awakened_greeting",
                "pre-Agent Unity save no longer loads / initializes");
        }
    }
}
