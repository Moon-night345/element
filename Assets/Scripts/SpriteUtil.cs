// 元素星海·裂核 — 程序化精灵与文字工具(无外部资源依赖)
using UnityEngine;

namespace ElementalStarSea.Game
{
    public static class SpriteUtil
    {
        static Sprite _square;

        /// <summary>1x1 白色方块精灵,所有几何体共用,通过 scale/color 变化。</summary>
        public static Sprite Square()
        {
            if (_square == null)
            {
                var tex = Texture2D.whiteTexture;
                _square = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                        new Vector2(0.5f, 0.5f), tex.width);
                _square.name = "ui_square";
            }
            return _square;
        }

        public static SpriteRenderer Rect(GameObject owner, Vector2 size, Color color, int sort = 0)
        {
            var sr = owner.AddComponent<SpriteRenderer>();
            sr.sprite = Square();
            owner.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.color = color;
            sr.sortingOrder = sort;
            return sr;
        }

        public static GameObject RectWorld(string name, Vector2 pos, Vector2 size, Color color, int sort = 0)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            Rect(go, size, color, sort);
            return go;
        }

        static Font _font;

        /// <summary>中文世界文字:系统微软雅黑(Windows 自带),失败回退内置字体。</summary>
        public static Font UiFont()
        {
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16); }
                catch { /* 忽略,走回退 */ }
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _font;
        }

        public static TextMesh WorldLabel(string name, Vector2 pos, string text,
                                          int size = 18, Color? color = null, int sort = 10)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UiFont();
            tm.fontSize = size;
            tm.characterSize = 0.18f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color ?? Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = sort;
            return tm;
        }
    }
}
