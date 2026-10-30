using System;
using UnityEngine;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class PlayerMotorAcceptance
    {
        static void Require(bool valid, string why) { if (!valid) throw new InvalidOperationException(why); }
        static bool Near(float a, float b) { return Mathf.Abs(a - b) < .0001f; }
        public static void Motion()
        {
            var c = PlayerMotorCatalog.Load(); var motor = new PlayerMotorService(c);
            Require(c.schema_version == 1 && c.pixels_per_unit == 32 && c.duration_seconds == 1.2f, "source parameters");
            var f = motor.Step(Vector2.zero, .1f, 2, true, true, true, true, 100, .8f);
            Require(Near(f.velocity.x, 220 / 32f) && Near(f.velocity.y, 520 / 32f) && f.hover_elapsed == 0 && f.energy_spent == 0, "jump/axis clamp/ground reset");
            f = motor.Step(Vector2.zero, .2f, 0, false, true, false, false, 100, 0);
            Require(Near(f.velocity.y, -1500 / 32f * .2f) && f.energy_spent == 0, "locked ability altered gravity");
            f = motor.Step(Vector2.zero, .2f, 0, false, true, false, true, 100, 0);
            Require(Near(f.velocity.y, -1500 / 32f * .25f * .2f) && Near(f.energy_spent, 3) && f.motion_state == "HOVER", "hover gravity/cost");
            f = motor.Step(Vector2.zero, .2f, 0, false, true, false, true, 1.5f, 0);
            Require(Near(f.energy_spent, 1.5f) && Near(f.hover_elapsed, .1f) && Near(f.velocity.y, -1500 / 32f * .125f), "partial energy budget must integrate ordinary remainder");
            f = motor.Step(Vector2.zero, .2f, 0, false, true, false, true, 100, 1.1f);
            Require(Near(f.energy_spent, 1.5f) && Near(f.hover_elapsed, 1.2f) && f.motion_state == "AIRBORNE", "duration remainder");
            f = motor.Step(new Vector2(0, -100), .1f, 0, false, false, false, true, 0, 1.2f);
            Require(Near(f.velocity.y, -900 / 32f) && f.energy_spent == 0, "terminal speed/empty energy");
            f = motor.Step(Vector2.one, .2f, 1, true, true, false, true, 100, .3f, true);
            Require(f.velocity == Vector2.zero && f.energy_spent == 0 && f.hover_elapsed == .3f, "interaction lock spent energy/moved");
        }
        public static void UnlockAndSave()
        {
            var s = new GameState(); var reactions = new ReactionService(s);
            Require(!s.HasAbility("ability_h2_hover") && !reactions.Execute("reaction_h2_synthesis", new[] { "synthesis_device" }).success && !s.HasAbility("ability_h2_hover"), "failed reaction granted hover");
            s.AddItem("element_h", 2); Require(reactions.Execute("reaction_h2_synthesis", new[] { "synthesis_device" }).success && s.HasAbility("ability_h2_hover") && s.unlockedAbilityIds.Length == 1, "actual H2 commit did not unlock");
            s.GrantAbility("ability_h2_hover"); Require(s.unlockedAbilityIds.Length == 1, "duplicate ability");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u32_player_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true); Require(repo.Write(s, out error), "ability save " + error); var read = repo.Read();
            Require(read.success && read.state.HasAbility("ability_h2_hover"), "ability lost on disk");
            var old = new GameState { discoveredReactions = new[] { "reaction_h2_synthesis" }, unlockedAbilityIds = null }; Require(old.HasAbility("ability_h2_hover"), "existing completed H2 save lost earned hover");
            var heldOnly = new GameState(); heldOnly.AddItem("compound_h2"); Require(!heldOnly.HasAbility("ability_h2_hover"), "owning H2 invented ability unlock");
        }
    }
}
