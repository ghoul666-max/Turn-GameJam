using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 表现特效（节点 VFXContainer）。只订阅事件触发表现，不改任何状态：
    ///   · RollPerformedEvent → 玩家周围临时传送门（跟随玩家，寿命 = 翻滚时长）+ HUD 全屏闪色（地图切换特效）；
    ///   · ShockwaveTriggeredEvent → 落地冲击波（缩放到实际半径）。
    /// 地图切换是 Space 输入驱动的（RollSystem.TryRoll），传送门只是事后播放的画面，没有任何触发器。
    /// </summary>
    public sealed class VfxController : MonoBehaviour
    {
        [Header("特效 Prefab（生成器自动拖好）")]
        [SerializeField] private TimedVfx portalPrefab;
        [SerializeField] private TimedVfx shockwavePrefab;
        [Tooltip("冲击波 Prefab 在 scale = 1 时的半径（世界单位）")]
        [SerializeField] private float shockwaveUnitRadius = 0.5f;

        [Header("引用")]
        [SerializeField] private Transform player;
        [SerializeField] private HudUI hud;

        private ITurnContext _ctx;

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            _ctx = ctx;
            ctx.Events.Subscribe<RollPerformedEvent>(OnRoll);
            ctx.Events.Subscribe<ShockwaveTriggeredEvent>(OnShockwave);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<RollPerformedEvent>(OnRoll);
            _ctx.Events.Unsubscribe<ShockwaveTriggeredEvent>(OnShockwave);
            _ctx = null;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnRoll(RollPerformedEvent evt)
        {
            if (portalPrefab != null)
            {
                TimedVfx portal = Instantiate(portalPrefab, transform);
                portal.SetLifetime(Mathf.Max(0.3f, _ctx.Config.Roll.Duration + 0.15f));
                portal.Play(ViewMath.ToWorld(evt.Position), 1f, player);
            }
            if (hud != null) hud.FlashScreen(ViewMath.MapColor(evt.ToPlane));
        }

        private void OnShockwave(ShockwaveTriggeredEvent evt)
        {
            if (shockwavePrefab == null) return;
            TimedVfx wave = Instantiate(shockwavePrefab, transform);
            float scale = evt.Radius / Mathf.Max(0.01f, shockwaveUnitRadius);
            wave.Play(ViewMath.ToWorld(evt.Position), scale, null);
        }
    }
}
