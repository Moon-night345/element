using System;
namespace ElementalStarSea.Data
{
    // Godot SurvivalState: medkit ownership is a dedicated counter, not an invented inventory item.
    public sealed class SurvivalRuntimeService
    {
        readonly GameState state;
        public SurvivalRuntimeService(GameState state) { this.state = state ?? throw new ArgumentNullException(nameof(state)); }
        public static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        public bool ApplyDamage(float amount)
        { if (!Finite(amount) || amount <= 0 || !Finite(state.hp)) return false; state.hp = Math.Max(0, state.hp - amount); return true; }
        public bool Heal(float amount)
        { if (!Finite(amount) || amount <= 0 || !Finite(state.hp)) return false; state.hp = Math.Min(100, state.hp + amount); return true; }
        public bool AddRadiation(float amount)
        { if (!Finite(amount) || amount <= 0 || !Finite(state.radiation)) return false; state.radiation = Math.Min(100, state.radiation + amount); return true; }
        public bool ReduceRadiation(float amount)
        { if (!Finite(amount) || amount <= 0 || !Finite(state.radiation)) return false; state.radiation = Math.Max(0, state.radiation - amount); return true; }
        public bool UseMedkit()
        {
            if (state.radiationMedkits <= 0 || !Finite(state.hp) || !Finite(state.radiation)) return false;
            state.radiationMedkits--; Heal(20); ReduceRadiation(15); return true;
        }
        public float Tick(float delta, float environmentRadiation, float protectiveFactor = 0)
        {
            if (!Finite(delta) || delta <= 0 || !Finite(environmentRadiation) || !Finite(protectiveFactor) || !Finite(state.radiationProtection) || !Finite(state.radiation) || !Finite(state.hp)) return 0;
            float exposure = Math.Max(0, environmentRadiation) * Math.Max(0, 1 - state.radiationProtection - protectiveFactor);
            float dose = exposure * delta * 10;
            if (!Finite(dose)) return 0;
            float nextRadiation = Math.Min(100, state.radiation + dose);
            float damage = nextRadiation > 35 ? (nextRadiation - 35) / 65 * delta * 4 : 0;
            if (!Finite(damage)) return 0;
            state.radiation = nextRadiation; ApplyDamage(damage); return damage;
        }
        public bool IsDead { get { return state.hp <= 0; } }
    }
}
