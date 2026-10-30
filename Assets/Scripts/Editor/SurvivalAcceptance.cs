using System;
using UnityEngine;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class SurvivalAcceptance
    {
        static void Require(bool valid, string why) { if (!valid) throw new InvalidOperationException(why); }
        public static void RulesAndProtection()
        {
            var s = new GameState(); var survival = new SurvivalRuntimeService(s);
            string before = JsonUtility.ToJson(s);
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Require(!survival.Heal(bad) && !survival.ApplyDamage(bad) && !survival.AddRadiation(bad) && !survival.ReduceRadiation(bad) && JsonUtility.ToJson(s) == before, "invalid scalar changed survival");
            Require(survival.ApplyDamage(120) && survival.IsDead && s.hp == 0 && survival.Heal(120) && s.hp == 100, "health bounds");
            Require(survival.AddRadiation(120) && s.radiation == 100 && survival.ReduceRadiation(120) && s.radiation == 0, "radiation bounds");
            s.radiationProtection = .2f; survival.Tick(1, 1, .3f); Require(Math.Abs(s.radiation - 5) < .0001f && s.hp == 100, "additive protection factor");
            survival.Tick(1, 1, 1); Require(s.radiation == 5, "complete protection");
            s.radiation = 35; survival.Tick(1, 0); Require(s.hp == 100, "safe boundary");
            s.radiation = 100; Require(survival.Tick(1, 0) == 4 && s.hp == 96, "radiation damage rule");
            before = JsonUtility.ToJson(s);
            Require(survival.Tick(float.NaN, 1) == 0 && survival.Tick(1, float.PositiveInfinity) == 0 && survival.Tick(1, 1, float.NaN) == 0 && JsonUtility.ToJson(s) == before, "non-finite tick changed state");
            var env = new RegionEnvironmentService(s, GameData.Regions);
            Require(env.TickExposure(new RegionDef { id = "rust_jungle", radiation = float.NaN }, 1) == 0 && JsonUtility.ToJson(s) == before, "invalid region dose leaked corrosion damage");
            s.hp = 100; s.radiation = 0; env.TickExposure(new RegionDef { id = "rust_jungle", radiation = 0 }, 1);
            Require(Math.Abs(s.hp - 98.8f) < .0001f, "corrosion double applied or lost");
        }
        public static void MedkitAndDisk()
        {
            var s = new GameState { hp = 50, radiation = 40 }; var survival = new SurvivalRuntimeService(s);
            string before = JsonUtility.ToJson(s); Require(!survival.UseMedkit() && JsonUtility.ToJson(s) == before, "missing medkit free heal");
            s.radiationMedkits = 2; Require(survival.UseMedkit() && s.radiationMedkits == 1 && s.hp == 70 && s.radiation == 25, "medkit exact effect");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u31_survival_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true); Require(repo.Write(s, out error), "survival save " + error); var read = repo.Read();
            Require(read.success && read.state.radiationMedkits == 1 && read.state.hp == 70 && read.state.radiation == 25, "survival disk values");
            var restored = new SurvivalRuntimeService(read.state); Require(restored.UseMedkit() && read.state.radiationMedkits == 0 && !restored.UseMedkit(), "restored medkit reuse");
            s.hp = 95; s.radiation = 5; Require(survival.UseMedkit() && s.hp == 100 && s.radiation == 0, "medkit caps");
            s.radiationMedkits = -1; Require(!repo.Write(s, out error), "negative medkit saved");
        }
    }
}
