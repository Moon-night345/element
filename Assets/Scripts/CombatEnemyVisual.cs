using UnityEngine;
using ElementalStarSea.Data;
namespace ElementalStarSea.Game
{
    // Procedural silhouettes mirror SandboxWorld._draw_creature, including source colors/scale.
    public sealed class CombatEnemyVisual : MonoBehaviour
    {
        static Sprite circle;
        Transform body, health;
        LineRenderer orbit;
        Color accent;
        static Sprite Circle()
        {
            if (circle != null) return circle;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Point;
            var colors = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) colors[y * 64 + x] = new Vector2(x - 31.5f, y - 31.5f).sqrMagnitude <= 1024 ? Color.white : Color.clear;
            tex.SetPixels(colors); tex.Apply(); circle = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64); return circle;
        }
        static Vector3 P(float x, float y) { return new Vector3(x / 32f, -y / 32f, 0); }
        void Disc(float x, float y, float radius, Color color)
        {
            var go = new GameObject("circle"); go.transform.SetParent(body, false); go.transform.localPosition = P(x, y);
            go.transform.localScale = Vector3.one * (radius * 2 / 32f); var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Circle(); sr.color = color; sr.sortingOrder = 7;
        }
        Transform Box(string name, Transform parent, Vector3 center, Vector2 size, Color color, int sort = 7)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center;
            SpriteUtil.Rect(go, size, color, sort); return go.transform;
        }
        void Line(float x1, float y1, float x2, float y2, float width, Color color)
        {
            Vector3 a = P(x1, y1), b = P(x2, y2); var line = Box("line", body, (a + b) / 2, new Vector2(Vector3.Distance(a, b), width / 32f), color, 8);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var go = new GameObject("triangle"); go.transform.SetParent(body, false);
            var mesh = new Mesh { vertices = new[] { a, b, c }, triangles = new[] { 0, 1, 2 }, colors = new[] { color, color, color } }; mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")); mr.sortingOrder = 7;
        }
        LineRenderer Arc(float x, float y, float radius, float from, float to, float width, Color color)
        {
            var go = new GameObject("arc"); go.transform.SetParent(body, false); go.transform.localPosition = P(x, y);
            var lr = go.AddComponent<LineRenderer>(); lr.useWorldSpace = false; lr.positionCount = 19; lr.startWidth = lr.endWidth = width / 32f;
            lr.material = new Material(Shader.Find("Sprites/Default")); lr.startColor = lr.endColor = color; lr.sortingOrder = 8;
            SetArc(lr, radius, from, to); return lr;
        }
        static void SetArc(LineRenderer lr, float radius, float from, float to)
        { for (int i = 0; i < lr.positionCount; i++) { float angle = Mathf.Lerp(from, to, i / (float)(lr.positionCount - 1)); lr.SetPosition(i, P(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius)); } }
        public static CombatEnemyVisual Create(CombatEnemyState state, EnemyDefinition def)
        {
            var go = new GameObject("combat_" + state.id); var v = go.AddComponent<CombatEnemyVisual>(); v.Build(def); v.Sync(state); return v;
        }
        void Build(EnemyDefinition def)
        {
            Color color; ColorUtility.TryParseHtmlString(def.body, out color); ColorUtility.TryParseHtmlString(def.accent, out accent);
            body = new GameObject("body").transform; body.SetParent(transform, false); body.localScale = Vector3.one * def.scale;
            var glow = accent; glow.a = .14f; Disc(0, 0, 30, glow);
            switch (def.shape)
            {
                case "scavenger": Box("body", body, Vector3.zero, new Vector2(36, 40) / 32, color); Line(-12, -16, 22, -30, 5, accent); break;
                case "vine": Disc(0, -4, 17, color); for (int i = 0; i < 4; i++) Line(-12 + i * 8, 8, -22 + i * 14, 30, 4, Color.Lerp(color, Color.white, .16f)); break;
                case "crystal": Triangle(P(-22, 18), P(-4, -28), P(18, 18), color); Line(-4, -26, -4, 16, 3, accent); break;
                case "shell": Disc(0, 0, 22, color); Arc(0, 0, 16, Mathf.PI, Mathf.PI * 2, 4, accent); break;
                case "crawler": case "burrower": for (int i = 0; i < 3; i++) Disc(-18 + i * 18, 0, 15 - i * 2, color); Line(-20, 14, 22, 14, 3, accent); break;
                case "orb": case "echo": Disc(0, 0, 18, color); orbit = Arc(0, 0, 28, 0, Mathf.PI * 1.5f, 3, accent); break;
                case "rat": Disc(-4, 2, 16, color); Disc(12, -10, 9, color); Line(-18, 10, -34, 20, 3, accent); break;
                case "skeleton": Disc(0, -14, 12, color); Line(0, -2, 0, 22, 6, color); Line(-18, 4, 18, 4, 4, color); break;
                case "fungus": Box("stem", body, P(0, 12), new Vector2(24, 28) / 32, color); Arc(0, -8, 25, Mathf.PI, Mathf.PI * 2, 8, accent); break;
                case "drone": Box("body", body, Vector3.zero, new Vector2(40, 28) / 32, color); Disc(0, 0, 7, accent); Line(-25, -20, 25, -20, 3, accent); break;
                case "mutant": Disc(0, 0, 20, color); for (int i = 0; i < 5; i++) Disc(Mathf.Cos(i * 1.25f) * 24, Mathf.Sin(i * 1.25f) * 24, 5, accent); break;
                case "colossus": Disc(0, -4, 28, color); Line(-24, 18, 24, 18, 8, accent); break;
                case "wraith": Triangle(P(-22, 22), P(0, -30), P(22, 22), color); Disc(0, -8, 6, accent); break;
                default: Disc(0, 0, 22, color); Disc(8, -6, 4, accent); break;
            }
            Box("health_background", transform, P(0, -33.5f), new Vector2(48, 5) / 32, new Color(.227f, .145f, .157f), 9);
            health = Box("health", transform, P(0, -33.5f), new Vector2(48, 5) / 32, new Color(.859f, .482f, .420f), 10);
        }
        public void Sync(CombatEnemyState e)
        {
            gameObject.SetActive(e.alive); if (!e.alive) return; transform.position = new Vector3(e.x, e.y, 0);
            body.localPosition = Vector3.up * Mathf.Sin(Time.time * 3 + transform.GetSiblingIndex()) * 2 / 32f;
            float ratio = Mathf.Clamp01(e.health / Mathf.Max(1, e.max_health)); health.localScale = new Vector3(48 / 32f * ratio, 5 / 32f, 1);
            health.localPosition = P(-24 + 24 * ratio, -33.5f);
            if (orbit != null) SetArc(orbit, 28, Time.time, Time.time + Mathf.PI * 1.5f);
        }
    }
}
