using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 挂在特效 Prefab 上：进入亚空间、画面变成灰白时，这个物体（默认连同子物体）保持原色高亮。
    /// 策划：「画面整体切换为灰白色调，仅爆发特效、敌人受击特效保留原色高亮」—— 程序B 的爆发 / 受击特效 Prefab 挂这个就行。
    /// 原理：Awake 时把物体放到 SubspaceColor Layer，由 SubspaceView 的 Overlay 相机（不做后处理）渲染。
    /// 项目里没有 SubspaceColor Layer 时什么都不做（Console 会提示一次）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeepColorInSubspace : MonoBehaviour
    {
        [Tooltip("连同所有子物体一起保留原色")]
        [SerializeField] private bool includeChildren = true;

        private void Awake()
        {
            SubspaceView.KeepColor(gameObject, includeChildren);
        }
    }
}
