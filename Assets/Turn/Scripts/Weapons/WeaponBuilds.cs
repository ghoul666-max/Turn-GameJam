using System;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 三把武器的定稿词条池（新策划案 11/24）：名字、说明、默认每层数值与最大层数。纯数据，不引用 UnityEngine。
    /// 每层效果 = Value（百分比类按 1 = 100% 记），Value2 = 附加参数（持续秒数 / 半径 / 敌人数）。
    /// 「可重复获取叠加」：总效果 = 层数 × Value。最大层数策划书没给，这里是暂定值，Inspector（UpgradeManager → 词条表）与 F1 面板可调。
    /// </summary>
    public static class BuildCatalog
    {
        public static BuildDef[] CreateDefaults()
        {
            var d = new BuildDef[BuildIds.Count];
            d[0] = B(5, 0.15f, 0f); // 扩容水箱
            d[1] = B(5, 0.1f, 0f); // 急射
            d[2] = B(2, 1f, 0f); // 散射
            d[3] = B(5, 0.15f, 0f); // 高压水流
            d[4] = B(1, 1f, 0f); // 贯穿水弹
            d[5] = B(5, 0.1f, 0f); // 水渍扩散
            d[6] = B(5, 0.15f, 0f); // 涌泉
            d[7] = B(5, 0.1f, 0f); // 喷涌
            d[8] = B(3, 0.15f, 2f); // 寒泉
            d[9] = B(3, 0.15f, 0f); // 激流
            d[10] = B(4, 0.25f, 0f); // 汇流
            d[11] = B(2, 0.4f, 0f); // 水压冲击
            d[12] = B(3, 0.08f, 3f); // 潮汐
            d[13] = B(3, 0.25f, 2.5f); // 余波
            d[14] = B(3, 0.03f, 0f); // 甘泉
            d[15] = B(2, 1f, 0f); // 应激
            d[16] = B(2, 0.3f, 0f); // 回流
            d[17] = B(5, 0.15f, 0f); // 矛囊
            d[18] = B(5, 0.1f, 0f); // 疾投
            d[19] = B(2, 1f, 0f); // 双矛
            d[20] = B(4, 0.05f, 0f); // 锋锐
            d[21] = B(3, 0.25f, 0f); // 连锁
            d[22] = B(5, 0.15f, 0f); // 倒刺
            d[23] = B(4, 0.1f, 0f); // 矛林
            d[24] = B(5, 0.1f, 0f); // 横扫回收
            d[25] = B(3, 0.15f, 0f); // 贯心
            d[26] = B(2, 0.4f, 0f); // 钉刺
            d[27] = B(3, 0.1f, 0f); // 串刺
            d[28] = B(3, 0.08f, 3f); // 矛阵
            d[29] = B(3, 0.25f, 2.5f); // 回旋
            d[30] = B(3, 0.03f, 0f); // 饮血
            d[31] = B(2, 2f, 0f); // 反刺
            d[32] = B(5, 0.15f, 0f); // 刀意
            d[33] = B(5, 0.1f, 0f); // 快刀
            d[34] = B(5, 0.15f, 0f); // 开阖
            d[35] = B(5, 0.1f, 0f); // 远斩
            d[36] = B(2, 1f, 0f); // 破阵
            d[37] = B(5, 0.1f, 0f); // 长刀
            d[38] = B(4, 0.02f, 0f); // 蓄势
            d[39] = B(5, 0.1f, 0f); // 横贯
            d[40] = B(3, 0.15f, 0f); // 惊鸿
            d[41] = B(2, 0.4f, 0f); // 震慑
            d[42] = B(1, 0.2f, 0f); // 断魂
            d[43] = B(3, 0.08f, 3f); // 刀圈
            d[44] = B(3, 0.25f, 2.5f); // 刀势
            d[45] = B(2, 1f, 3f); // 反斩
            return d;
        }

        private static BuildDef B(int maxLevel, float value, float value2)
        {
            return new BuildDef { MaxLevel = maxLevel, Value = value, Value2 = value2 };
        }

        /// <summary>按 BuildIds.Count 补齐（Inspector 里数组长度不对或某项 MaxLevel &lt; 0 时用默认值）。</summary>
        public static BuildDef[] Normalize(BuildDef[] configured)
        {
            BuildDef[] defaults = CreateDefaults();
            if (configured == null) return defaults;
            var result = new BuildDef[defaults.Length];
            for (int i = 0; i < result.Length; i++)
            {
                BuildDef c = i < configured.Length ? configured[i] : null;
                result[i] = c != null && c.MaxLevel >= 0 && (c.Value != 0f || c.MaxLevel == 0)
                    ? new BuildDef { MaxLevel = c.MaxLevel, Value = c.Value, Value2 = c.Value2 }
                    : defaults[i];
            }
            return result;
        }

        public static string Name(BuildId id)
        {
            switch (id)
            {
                case BuildId.WaterAmmo: return "扩容水箱";
                case BuildId.WaterRate: return "急射";
                case BuildId.WaterSpread: return "散射";
                case BuildId.WaterBulletSpeed: return "高压水流";
                case BuildId.WaterPierce: return "贯穿水弹";
                case BuildId.WaterPuddleSize: return "水渍扩散";
                case BuildId.WaterFountainDamage: return "涌泉";
                case BuildId.WaterFountainArea: return "喷涌";
                case BuildId.WaterFountainSlow: return "寒泉";
                case BuildId.WaterFountainCrit: return "激流";
                case BuildId.WaterOverlap: return "汇流";
                case BuildId.WaterFountainStun: return "水压冲击";
                case BuildId.WaterBurstRing: return "潮汐";
                case BuildId.WaterPostHaste: return "余波";
                case BuildId.WaterLifesteal: return "甘泉";
                case BuildId.WaterHurtPuddle: return "应激";
                case BuildId.WaterKillRefund: return "回流";
                case BuildId.SpearAmmo: return "矛囊";
                case BuildId.SpearRate: return "疾投";
                case BuildId.SpearDouble: return "双矛";
                case BuildId.SpearFalloff: return "锋锐";
                case BuildId.SpearChainMark: return "连锁";
                case BuildId.SpearRecallDamage: return "倒刺";
                case BuildId.SpearStackBonus: return "矛林";
                case BuildId.SpearRecallWidth: return "横扫回收";
                case BuildId.SpearCrit: return "贯心";
                case BuildId.SpearStun: return "钉刺";
                case BuildId.SpearPathRamp: return "串刺";
                case BuildId.SpearBurstRing: return "矛阵";
                case BuildId.SpearPostHaste: return "回旋";
                case BuildId.SpearLifesteal: return "饮血";
                case BuildId.SpearHurtMarks: return "反刺";
                case BuildId.BladeAmmo: return "刀意";
                case BuildId.BladeRate: return "快刀";
                case BuildId.BladeAngle: return "开阖";
                case BuildId.BladeRange: return "远斩";
                case BuildId.BladePierce: return "破阵";
                case BuildId.BladeLength: return "长刀";
                case BuildId.BladeMarkBonus: return "蓄势";
                case BuildId.BladeSlashWidth: return "横贯";
                case BuildId.BladeCrit: return "惊鸿";
                case BuildId.BladeStun: return "震慑";
                case BuildId.BladeExecute: return "断魂";
                case BuildId.BladeBurstRing: return "刀圈";
                case BuildId.BladePostHaste: return "刀势";
                case BuildId.BladeHurtMarks: return "反斩";
                default: return id.ToString();
            }
        }

        /// <summary>说明模板：{p} = 本层百分比、{n} = 本层数值、{v2} = 附加参数。</summary>
        private static string Template(BuildId id)
        {
            switch (id)
            {
                case BuildId.WaterAmmo: return "弹药上限 +{p}";
                case BuildId.WaterRate: return "自动攻击攻速 +{p}";
                case BuildId.WaterSpread: return "每次多发射 {n} 枚散射水弹";
                case BuildId.WaterBulletSpeed: return "子弹飞行速度 +{p}";
                case BuildId.WaterPierce: return "子弹穿透敌人，只在首个命中的单位 / 地面留下水渍";
                case BuildId.WaterPuddleSize: return "水渍基础范围 +{p}";
                case BuildId.WaterFountainDamage: return "喷泉单次伤害 +{p}";
                case BuildId.WaterFountainArea: return "喷泉爆炸范围 +{p}";
                case BuildId.WaterFountainSlow: return "喷泉命中的敌人减速 {p}，持续 {v2} 秒";
                case BuildId.WaterFountainCrit: return "喷泉伤害有 {p} 概率暴击";
                case BuildId.WaterOverlap: return "重叠水渍的伤害加成：每多一滩重叠 +{p}";
                case BuildId.WaterFountainStun: return "喷泉击中的敌人眩晕 {n} 秒";
                case BuildId.WaterBurstRing: return "翻滚爆发时，对周围 {v2} 单位内敌人造成 {p} 最大生命的伤害";
                case BuildId.WaterPostHaste: return "翻滚结束后 {v2} 秒内攻速 +{p}";
                case BuildId.WaterLifesteal: return "喷泉造成伤害的 {p} 转为生命";
                case BuildId.WaterHurtPuddle: return "受到伤害时，在脚下生成 {n} 滩水渍";
                case BuildId.WaterKillRefund: return "常态击杀敌人有 {p} 概率补充 1 颗子弹";
                case BuildId.SpearAmmo: return "弹药上限 +{p}";
                case BuildId.SpearRate: return "投掷攻速 +{p}";
                case BuildId.SpearDouble: return "每次多投 {n} 根散射长矛，可分别命中不同敌人叠层";
                case BuildId.SpearFalloff: return "穿透后的伤害衰减比例 −{p}";
                case BuildId.SpearChainMark: return "命中时有 {p} 概率在相邻 1 名敌人身上也叠 1 层矛";
                case BuildId.SpearRecallDamage: return "长矛回收基础伤害 +{p}";
                case BuildId.SpearStackBonus: return "敌人身上每多一层矛，其每根矛的回收伤害 +{p}";
                case BuildId.SpearRecallWidth: return "回收路径判定宽度 +{p}";
                case BuildId.SpearCrit: return "回收伤害有 {p} 概率暴击";
                case BuildId.SpearStun: return "回收长矛命中的敌人眩晕 {n} 秒";
                case BuildId.SpearPathRamp: return "单根长矛回收路径上每多命中 1 名敌人，伤害 +{p}（递增）";
                case BuildId.SpearBurstRing: return "翻滚爆发时，对周身 {v2} 单位内敌人造成 {p} 最大生命的穿刺伤害";
                case BuildId.SpearPostHaste: return "翻滚结束后 {v2} 秒内投掷攻速 +{p}";
                case BuildId.SpearLifesteal: return "回收伤害的 {p} 转为生命";
                case BuildId.SpearHurtMarks: return "受到伤害时，在最近的敌人身上叠加 {n} 层矛";
                case BuildId.BladeAmmo: return "弹药上限 +{p}";
                case BuildId.BladeRate: return "剑气发射攻速 +{p}";
                case BuildId.BladeAngle: return "扇形剑气的展开角度 +{p}";
                case BuildId.BladeRange: return "扇形剑气的攻击距离 +{p}";
                case BuildId.BladePierce: return "剑气可多穿透 {n} 名敌人（向纵深多推进一圈）";
                case BuildId.BladeLength: return "武器长度 +{p}：同时提升扇形角度与攻击距离";
                case BuildId.BladeMarkBonus: return "每层剑气印记的增伤 +{p}";
                case BuildId.BladeSlashWidth: return "横向斩击的有效宽度 +{p}";
                case BuildId.BladeCrit: return "斩击伤害有 {p} 概率暴击";
                case BuildId.BladeStun: return "一刀斩命中的敌人眩晕 {n} 秒";
                case BuildId.BladeExecute: return "一刀斩直接斩杀生命低于 {p} 的敌人";
                case BuildId.BladeBurstRing: return "翻滚爆发时，对周身 {v2} 单位内敌人造成 {p} 最大生命的斩击伤害";
                case BuildId.BladePostHaste: return "翻滚结束后 {v2} 秒内剑气发射攻速 +{p}";
                case BuildId.BladeHurtMarks: return "受到伤害时，在最近的 {v2} 名敌人身上各叠加 {n} 层剑气印记";
                default: return "";
            }
        }

        /// <summary>选到这一层后的效果说明（总效果 = 层数 × 每层数值）。</summary>
        public static string Describe(BuildId id, BuildDef def, int toLevel)
        {
            if (def == null) return Template(id);
            float total = def.Value * Math.Max(1, toLevel);
            string percent = MathF.Round(total * 100f).ToString("0") + "%";
            string number = MathF.Abs(total - MathF.Round(total)) < 0.01f ? MathF.Round(total).ToString("0") : total.ToString("0.0");
            string v2 = MathF.Abs(def.Value2 - MathF.Round(def.Value2)) < 0.01f ? MathF.Round(def.Value2).ToString("0") : def.Value2.ToString("0.0");
            return Template(id).Replace("{p}", percent).Replace("{n}", number).Replace("{v2}", v2);
        }
    }
}
