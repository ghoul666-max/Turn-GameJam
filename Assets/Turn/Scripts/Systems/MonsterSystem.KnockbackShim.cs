using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// <see cref="MonsterSystem"/> 的临时补丁文件（程序A 维护），只为 B15「翻滚冲击」提供
    /// <see cref="IMonsterSystem.Knockback"/> 的实现，避免把 A 的代码写进 B 的 MonsterSystem.cs。
    ///
    /// 程序B 在自己文件里实现同名方法后，**直接删掉本文件（含 .meta）** 即可，不需要改别处。
    /// Knockback 属于 IMonsterSystem，两边都实现会编译冲突（重复定义），只能留一份。
    /// </summary>
    public sealed partial class MonsterSystem
    {
        /// <summary>沿 direction 推开 distance；分小步经过遮挡物修正，最后 Clamp 进边界。</summary>
        public void Knockback(MonsterEntity monster, Vec2 direction, float distance)
        {
            if (monster == null || !monster.Alive || distance <= 0f || direction.SqrMagnitude < 0.0001f) return;

            Vec2 dir = direction.Normalized;
            int steps = Math.Max(1, (int)MathF.Ceiling(distance / 0.25f));
            float step = distance / steps;

            Vec2 p = monster.Position;
            for (int i = 0; i < steps; i++)
            {
                p = _ctx.Obstacles.ResolveMove(p, p + dir * step, monster.Radius, ObstacleMask.Units);
            }

            monster.Position = _ctx.Bounds.Clamp(p, monster.Radius);
        }
    }
}
