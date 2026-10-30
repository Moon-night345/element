// 元素星海·裂核 — 环境粒子:按区域风格切换(雪/火星/孢子/沙尘/电花/辐射尘)
using System.Collections.Generic;
using UnityEngine;

namespace ElementalStarSea.Game
{
    public class AmbientParticles : MonoBehaviour
    {
        class Mote
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector3 vel;
            public float sway, phase;
        }

        readonly List<Mote> _motes = new List<Mote>();
        string _style = "";
        Transform _anchor;

        public static AmbientParticles Attach(Transform cameraTransform)
        {
            var go = new GameObject("AmbientParticles");
            go.transform.SetParent(cameraTransform, false);
            var ap = go.AddComponent<AmbientParticles>();
            ap._anchor = cameraTransform;
            return ap;
        }

        public void SetStyle(string style, Color accent)
        {
            if (style == _style) { Tint(accent); return; }
            _style = style;
            Rebuild(accent);
        }

        void Tint(Color accent)
        {
            foreach (var m in _motes)
                if (_style != "snow" && _style != "spore")
                    m.sr.color = WithAlpha(accent, m.sr.color.a);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Rebuild(Color accent)
        {
            foreach (var m in _motes)
                if (m.tr != null) Destroy(m.tr.gameObject);
            _motes.Clear();

            int count = _style == "none" ? 0 : 42;
            var rng = new System.Random(_style.GetHashCode());
            Color tint;
            float size0, size1;
            switch (_style)
            {
                case "snow":  tint = new Color(0.95f, 0.97f, 1f);      size0 = 0.10f; size1 = 0.22f; break;
                case "ember": tint = new Color(1f, 0.55f, 0.25f);      size0 = 0.06f; size1 = 0.16f; break;
                case "spore": tint = new Color(0.6f, 1f, 0.65f);       size0 = 0.08f; size1 = 0.18f; break;
                case "sand":  tint = new Color(0.9f, 0.8f, 0.55f);     size0 = 0.06f; size1 = 0.14f; break;
                case "spark": tint = new Color(0.7f, 0.9f, 1f);        size0 = 0.05f; size1 = 0.12f; break;
                case "mote":  tint = new Color(0.85f, 0.75f, 1f);      size0 = 0.06f; size1 = 0.14f; break;
                case "dust":  tint = new Color(0.8f, 0.75f, 0.68f);    size0 = 0.05f; size1 = 0.12f; break;
                default:      tint = Color.white;                      size0 = 0.06f; size1 = 0.10f; break;
            }
            if (_style == "ember" || _style == "spark" || _style == "mote") tint = Color.Lerp(tint, accent, 0.4f);

            var tex = TexGen.SoftCircle("amb", Color.white, 32);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("m" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = TexGen.SpriteOf(tex, 32f);
                var sc = size0 + (float)rng.NextDouble() * (size1 - size0);
                go.transform.localScale = new Vector3(sc, sc, 1f);
                float a = 0.25f + (float)rng.NextDouble() * 0.45f;
                sr.color = WithAlpha(tint, a);
                // 排布在摄像机可视范围附近
                float x = ((float)rng.NextDouble() - 0.5f) * 46f;
                float y = ((float)rng.NextDouble() - 0.5f) * 26f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                _motes.Add(new Mote
                {
                    tr = go.transform,
                    sr = sr,
                    vel = VelocityFor(_style, rng),
                    sway = 0.6f + (float)rng.NextDouble(),
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                });
            }
        }

        static Vector3 VelocityFor(string style, System.Random rng)
        {
            switch (style)
            {
                case "snow":  return new Vector3(-0.4f, -0.9f, 0f);
                case "ember": return new Vector3(0.25f, 1.1f, 0f);
                case "spore": return new Vector3(0.2f, -0.22f, 0f);
                case "sand":  return new Vector3(-2.4f, -0.12f, 0f);
                case "spark": return new Vector3(0.5f, -1.4f, 0f);
                case "mote":  return new Vector3(-0.15f, 0.35f, 0f);
                case "dust":  return new Vector3(-0.35f, -0.1f, 0f);
                default:      return new Vector3(0f, 0f, 0f);
            }
        }

        void Update()
        {
            if (_motes.Count == 0) return;
            float halfW = 24f, halfH = 14f;
            foreach (var m in _motes)
            {
                var lp = m.tr.localPosition;
                lp += m.vel * Time.deltaTime * 2.2f;
                lp.x += Mathf.Sin(Time.time * m.sway + m.phase) * Time.deltaTime * 0.4f;
                // 环绕
                if (lp.x > halfW) lp.x = -halfW;
                if (lp.x < -halfW) lp.x = halfW;
                if (lp.y > halfH) lp.y = -halfH;
                if (lp.y < -halfH) lp.y = halfH;
                m.tr.localPosition = lp;
            }
        }
    }
}
