using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class SnapshotSaveAcceptance
    {
        static string TestPath(string parent)
        {
            string directory = Path.Combine(parent, "u15_snapshot_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "save.json");
        }
        static string[] Files(string path)
        { return Directory.GetFiles(path + ".history", "*.json").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray(); }
        static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        static void Write(SaveRepository repo, GameState state)
        { string reason; Require(repo.Write(state, out reason), "snapshot write: " + reason); }

        public static void Lifecycle()
        {
            string path = TestPath("Logs");
            var state = new GameState(); state.AddItem("element_h", 2);
            File.WriteAllText(path, JsonUtility.ToJson(state));
            string legacy = File.ReadAllText(path);
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            for (int i = 1; i <= 7; i++)
            {
                state.questStep = i; Write(repo, state);
                Require(repo.Read().success && repo.Read().state.questStep == i && repo.Read().recovery == "", "latest generation not authoritative");
            }
            Require(File.ReadAllText(path) == legacy && Files(path).Length == 7, "legacy / committed history overwritten");
            // Selection must persist when the forcing flag and EFS attribute are absent.
            var automatic = new SaveRepository(path, "test");
            state.questStep = 8; Write(automatic, state);
            Require(automatic.Read().state.questStep == 8 && Files(path).Length == 8 && File.ReadAllText(path) == legacy, "history routing reverted to legacy");
            var files = Files(path);
            File.WriteAllText(files[0], "{");
            Require(automatic.Read().success && automatic.Read().recovery == "history1" && automatic.Read().state.questStep == 7, "first generation recovery");
            File.WriteAllText(files[1], "{}");
            Require(automatic.Read().success && automatic.Read().recovery == "history2" && automatic.Read().state.questStep == 6, "second generation recovery");
            File.WriteAllText(files[2], "{}");
            Require(!automatic.Read().success && automatic.Read().exists, "silently fell back beyond two generations");
            Require(new SaveRepository(path, "wrong").Read().success == false, "history identity ignored");
        }

        public static void InterruptionAndVersion()
        {
            string path = TestPath("Logs");
            var state = new GameState { questStep = 1 };
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Write(repo, state);
            var committed = Files(path)[0];
            string original = File.ReadAllText(committed);
            var interrupted = new SaveRepository(path, "test", () => { throw new IOException("injected before snapshot commit"); }, true);
            state.questStep = 2; string reason;
            Require(!interrupted.Write(state, out reason) && Files(path).Length == 1 &&
                Directory.GetFiles(path + ".history", "*.tmp").Length == 1 &&
                repo.Read().state.questStep == 1 && File.ReadAllText(committed) == original, "interruption changed committed state / loaded temp");
            Write(repo, state);
            Require(repo.Read().state.questStep == 2, "retry failed after interruption");
            var newest = Files(path)[0];
            var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(newest));
            envelope.schema_version = 999;
            File.WriteAllText(newest, JsonUtility.ToJson(envelope));
            Require(repo.Read().reason == "unsupported_schema" && !repo.Write(state, out reason) && reason == "unsupported_schema" &&
                Files(path).Length == 2, "future history version obscured");

            string legacyPath = TestPath("Logs");
            File.WriteAllText(legacyPath, JsonUtility.ToJson(state));
            var migration = new SaveRepository(legacyPath, "test", forceSnapshotHistory: true);
            state.questStep = 3; Write(migration, state);
            File.WriteAllText(Files(legacyPath)[0], "{");
            Require(migration.Read().success && migration.Read().legacy && migration.Read().state.questStep == 2, "first snapshot cannot recover legacy base");
            var first = JsonUtility.FromJson<SaveEnvelope>(original); first.schema_version = 999;
            File.WriteAllText(legacyPath, JsonUtility.ToJson(first));
            Require(migration.Read().reason == "unsupported_schema" && !migration.Write(state, out reason), "future base version obscured");
        }

        public static void RealPersistentDirectory()
        {
            // Only new unique fixtures; do not inspect, alter or delete a player's save.
            string path = TestPath(Application.persistentDataPath);
            var repo = new SaveRepository(path, "real_directory_test");
            var state = new GameState();
            for (int i = 1; i <= 7; i++)
            {
                state.questStep = i; Write(repo, state);
                Require(repo.Read().success && repo.Read().state.questStep == i, "persistent directory repeated write/read");
            }
            bool encrypted = (File.GetAttributes(Path.GetDirectoryName(path)) & FileAttributes.Encrypted) != 0;
            Require(!encrypted || Files(path).Length == 7, "encrypted directory did not use immutable snapshots");
            Debug.Log("[U15_PERSISTENT_DIRECTORY] PASS: seven committed snapshots and reads; encrypted=" + encrypted + "; test_path=" + path);
        }
    }
}
