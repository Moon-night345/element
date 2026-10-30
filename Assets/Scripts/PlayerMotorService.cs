using System;
using UnityEngine;
namespace ElementalStarSea.Data
{
    [Serializable] public sealed class PlayerMotorCatalog
    {
        public int schema_version, pixels_per_unit;
        public string ability_id, reaction_id;
        public float duration_seconds, gravity_multiplier, energy_cost_per_second, ground_speed, jump_velocity, gravity, terminal_velocity;
        public static PlayerMotorCatalog Load() { return JsonUtility.FromJson<PlayerMotorCatalog>(Resources.Load<TextAsset>("Data/player_motor").text); }
    }
    public sealed class PlayerMotionFrame
    {
        public Vector2 velocity;
        public float energy_spent, hover_elapsed;
        public string motion_state;
    }
    // Godot Y-down velocities converted once into Unity Y-up units; collision remains owned by Terrain.
    public sealed class PlayerMotorService
    {
        readonly PlayerMotorCatalog c;
        public PlayerMotorService(PlayerMotorCatalog catalog) { c = catalog; }
        public PlayerMotionFrame Step(Vector2 velocity, float delta, float axis, bool pressed, bool held, bool onFloor, bool unlocked, float energy, float elapsed, bool locked = false)
        {
            var f = new PlayerMotionFrame { hover_elapsed = elapsed, motion_state = locked ? "INTERACTION_LOCKED" : "AIRBORNE" };
            if (locked) return f;
            if (!SurvivalRuntimeService.Finite(delta) || delta < 0 || !SurvivalRuntimeService.Finite(axis) || !SurvivalRuntimeService.Finite(energy) || !SurvivalRuntimeService.Finite(elapsed) || !SurvivalRuntimeService.Finite(velocity.x) || !SurvivalRuntimeService.Finite(velocity.y)) return f;
            float horizontal = Mathf.Clamp(axis, -1, 1) * c.ground_speed / c.pixels_per_unit;
            if (onFloor)
            {
                f.velocity = new Vector2(horizontal, pressed ? c.jump_velocity / c.pixels_per_unit : 0); f.hover_elapsed = 0; f.motion_state = pressed ? "AIRBORNE" : "GROUND"; return f;
            }
            float used = Mathf.Clamp(elapsed, 0, c.duration_seconds), hover = 0;
            if (unlocked && held && used < c.duration_seconds && energy > 0)
                hover = Mathf.Min(delta, Mathf.Min(c.duration_seconds - used, energy / c.energy_cost_per_second));
            f.energy_spent = hover * c.energy_cost_per_second; f.hover_elapsed = used + hover;
            float y = velocity.y - c.gravity / c.pixels_per_unit * (c.gravity_multiplier * hover + delta - hover);
            f.velocity = new Vector2(horizontal, Mathf.Max(y, -c.terminal_velocity / c.pixels_per_unit));
            f.motion_state = hover > 0 && f.hover_elapsed < c.duration_seconds ? "HOVER" : "AIRBORNE"; return f;
        }
    }
}
