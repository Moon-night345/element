// 元素星海·裂核 — 玩家角色(Godot 版女化学家帧动画:idle/walk/run/jump)
// 128px 单帧,ppu 64 → 2 单位高。方向翻转,行走/待机循环。
using UnityEngine;

namespace ElementalStarSea.Game
{
    public class CharacterSprite : MonoBehaviour
    {
        SpriteRenderer _sr;
        Sprite[] _idle, _walk, _jump;
        float _t;
        float _airTime;

        const int Sort = 6;

        public static CharacterSprite Attach(GameObject player)
        {
            var cs = player.AddComponent<CharacterSprite>();
            cs.Build();
            return cs;
        }

        void Build()
        {
            _idle = ArtLib.Frames("chem_idle", ArtLib.ChemPpu);
            _walk = ArtLib.Frames("chem_walk", ArtLib.ChemPpu);
            _jump = ArtLib.Frames("chem_jump", ArtLib.ChemPpu);
            if (_idle.Length == 0) _idle = ArtLib.Frames("chem_walk", ArtLib.ChemPpu);

            var shadowGo = new GameObject("shadow");
            shadowGo.transform.SetParent(transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, -0.92f, 0f);
            var ssr = shadowGo.AddComponent<SpriteRenderer>();
            ssr.sprite = SpriteUtil.Square();
            shadowGo.transform.localScale = new Vector3(0.8f, 0.16f, 1f);
            ssr.color = new Color(0f, 0f, 0f, 0.35f);
            ssr.sortingOrder = Sort - 3;

            var go = new GameObject("body");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = _idle.Length > 0 ? _idle[0] : SpriteUtil.Square();
            _sr.sortingOrder = Sort;
        }

        /// <summary>每帧驱动:speedX 移动方向,verticalMoving 时用跳跃帧。</summary>
        public void Tick(float speedX, bool verticalMoving)
        {
            if (_sr == null) return;
            float dt = Time.deltaTime;
            bool moving = Mathf.Abs(speedX) > 0.01f;
            var frames = _idle;
            float fps = 6f;
            if (verticalMoving && _jump.Length > 0) { frames = _jump; fps = 10f; _airTime = 0.4f; }
            else if (moving && _walk.Length > 0) { frames = _walk; fps = 12f; }
            if (_airTime > 0f && _jump.Length > 0) { frames = _jump; _airTime -= dt; }
            else if (!moving && !verticalMoving) _airTime = 0f;

            _t += dt * fps;
            _sr.sprite = frames[Mathf.FloorToInt(_t) % frames.Length];

            if (moving)
            {
                var s = transform.localScale;
                s.x = speedX > 0f ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
                transform.localScale = s;
            }
        }
    }
}
