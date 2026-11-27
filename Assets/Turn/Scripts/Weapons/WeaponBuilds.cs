using System;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 五把主武器 + 三把副武器的定稿词条池（11/24 亚空间策划案 + 11/25 双武器策划案）：名字、说明、默认每层数值与最大层数。纯数据，不引用 UnityEngine。
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
            d[46] = B(5, 0.15f, 0f); // 厚掌
            d[47] = B(5, 0.1f, 0f); // 连掌
            d[48] = B(5, 0.15f, 0f); // 开掌
            d[49] = B(5, 0.1f, 0f); // 推掌
            d[50] = B(3, 1f, 0f); // 印心
            d[51] = B(5, 0.15f, 0f); // 排山
            d[52] = B(2, 1f, 0f); // 透劲
            d[53] = B(5, 0.15f, 0f); // 天降
            d[54] = B(4, 0.02f, 0f); // 叠劲
            d[55] = B(5, 0.1f, 0f); // 覆掌
            d[56] = B(3, 0.15f, 0f); // 破军
            d[57] = B(2, 0.4f, 0f); // 镇压
            d[58] = B(1, 0.5f, 0.3f); // 二重掌
            d[59] = B(3, 0.08f, 3f); // 掌风
            d[60] = B(3, 0.25f, 2.5f); // 余势
            d[61] = B(3, 0.03f, 0f); // 纳气
            d[62] = B(1, 1f, 0f); // 反掌
            d[63] = B(2, 0.3f, 0f); // 回气
            d[64] = B(5, 0.15f, 0f); // 重矢
            d[65] = B(5, 0.15f, 0f); // 破甲
            d[66] = B(3, 0.15f, 0f); // 贯穿：穿透已为固有能力，改为击退距离强化
            d[67] = B(4, 0.05f, 0f); // 锐镞
            d[68] = B(3, 0.3f, 0f); // 钉墙
            d[69] = B(2, 1f, 0f); // 嗜杀
            d[70] = B(4, 0.02f, 0f); // 杀意
            d[71] = B(3, 0.15f, 0f); // 夺命
            d[72] = B(2, 0.3f, 10f); // 杀气溢出
            d[73] = B(3, 1f, 0f); // 饮恨
            d[74] = B(1, 1f, 0f); // 反击
            d[75] = B(2, 0.2f, 0f); // 连弩
            d[76] = B(5, 0.15f, 0f); // 火力强化
            d[77] = B(5, 0.1f, 0f); // 速射
            d[78] = B(1, 1f, 0f); // 贯通弹
            d[79] = B(3, 0.1f, 0f); // 增殖
            d[80] = B(2, 0.5f, 2.5f); // 超频
            d[81] = B(1, 0.2f, 8f); // 蜂群
            d[82] = B(1, 1f, 0f); // 双核
            d[83] = B(2, 0.1f, 0f); // 裂变
            d[84] = B(3, 15f, 3f); // 分裂冲击
            d[85] = B(5, 0.15f, 0f); // 视界
            d[86] = B(5, 0.2f, 0f); // 潮汐力
            d[87] = B(1, 1f, 0f); // 锁定
            d[88] = B(3, 0.05f, 0f); // 撕扯
            d[89] = B(3, 20f, 2f); // 坍缩
            d[90] = B(3, 0.15f, 0f); // 失衡
            d[91] = B(2, 1f, 0f); // 次级奇点
            d[92] = B(5, 0.15f, 0f); // 锋线
            d[93] = B(5, 0.1f, 0f); // 宽刃
            d[94] = B(1, 1f, 0f); // 虚空斩
            d[95] = B(2, 0.3f, 2f); // 轻身
            d[96] = B(2, 0.4f, 0f); // 断空
            d[97] = B(1, 1f, 0f); // 双锚
            d[98] = B(3, 0.05f, 1f); // 时隙
            // ---- 11/25 第二 / 三批：扑克牌、手里剑、时间沙漏、WASD、强磁铁、光标、订书机、剑（数值暂定） ----
            d[99] = B(6, 8f, 0f); // 精简牌库
            d[100] = B(3, 0.1f, 0f); // 鬼牌
            d[101] = B(5, 0.1f, 0f); // 加注
            d[102] = B(5, 0.15f, 0f); // 热恋
            d[103] = B(3, 1f, 0f); // 心心相印
            d[104] = B(1, 1f, 0f); // 穿心
            d[105] = B(5, 0.15f, 0f); // 黑桃尖
            d[106] = B(5, 0.1f, 0f); // 大爆炸
            d[107] = B(2, 0.5f, 0f); // 震慑
            d[108] = B(3, 15f, 2.5f); // 碎钻
            d[109] = B(5, 0.15f, 0f); // 梅开
            d[110] = B(5, 0.1f, 0f); // 深渊
            d[111] = B(3, 0.25f, 0f); // 长夜
            d[112] = B(3, 0.15f, 0f); // 王炸
            d[113] = B(3, 0.03f, 0f); // 抽成
            d[114] = B(5, 0.1f, 0f); // 疾风
            d[115] = B(1, 1f, 0f); // 双飞
            d[116] = B(5, 0.1f, 0f); // 远掷
            d[117] = B(5, 0.15f, 0f); // 飞旋
            d[118] = B(5, 0.1f, 0f); // 旋风
            d[119] = B(5, 0.15f, 0f); // 归刃
            d[120] = B(5, 0.1f, 0f); // 宽径
            d[121] = B(3, 0.1f, 0f); // 会心
            d[122] = B(3, 0.08f, 3f); // 影袭
            d[123] = B(2, 0.5f, 2.5f); // 连掷
            d[124] = B(3, 0.25f, 0f); // 护身
            d[125] = B(3, 0.03f, 0f); // 饮刃
            d[126] = B(5, 0.15f, 0f); // 流沙
            d[127] = B(1, 0.1f, 0f); // 凝滞
            d[128] = B(3, 0.04f, 3f); // 时之刃
            d[129] = B(1, 0.3f, 3f); // 余韵
            d[130] = B(3, 0.15f, 0f); // 迟滞
            d[131] = B(5, 0.15f, 0f); // 重击键
            d[132] = B(5, 0.1f, 0f); // 大键帽
            d[133] = B(3, 0.1f, 0f); // 连击
            d[134] = B(1, 1f, 0f); // 斜键
            d[135] = B(5, 0.15f, 0f); // 四向强化
            d[136] = B(5, 0.1f, 0f); // 全键盘
            d[137] = B(2, 0.5f, 0f); // 卡键
            d[138] = B(3, 0.1f, 0f); // 暴击键
            d[139] = B(1, 1f, 0f); // 防御键
            d[140] = B(5, 0.15f, 0f); // 磁暴线
            d[141] = B(5, 0.1f, 0f); // 宽磁场
            d[142] = B(5, 0.15f, 0f); // 湮灭
            d[143] = B(5, 0.1f, 0f); // 冲击波
            d[144] = B(3, 0.3f, 0f); // 磁晕
            d[145] = B(3, 0.08f, 0f); // 连锁
            d[146] = B(3, 0.5f, 0f); // 磁流
            d[147] = B(3, 0.15f, 0f); // 磁暴
            d[148] = B(3, 0.5f, 0f); // 磁场
            d[149] = B(1, 1f, 0f); // 穿壁
            d[150] = B(5, 0.15f, 0f); // 降维
            d[151] = B(5, 0.1f, 0f); // 大选区
            d[152] = B(3, 0.5f, 0f); // 压扁
            d[153] = B(5, 0.1f, 0f); // 加厚
            d[154] = B(3, 0.03f, 0f); // 全选
            d[155] = B(3, 0.1f, 0f); // 暴击框
            d[156] = B(3, 0.15f, 3f); // 拖拽
            d[157] = B(3, 0.08f, 0f); // 快速框选
            d[158] = B(3, 0.15f, 0f); // 贴地
            d[159] = B(5, 0.15f, 0f); // 大容量
            d[160] = B(5, 0.1f, 0f); // 速钉
            d[161] = B(1, 1f, 0f); // 双钉
            d[162] = B(5, 0.15f, 0f); // 重压
            d[163] = B(3, 1f, 0f); // 贯穿钉
            d[164] = B(3, 0.1f, 0f); // 紧固
            d[165] = B(3, 2f, 0f); // 长钉
            d[166] = B(5, 0.15f, 0f); // 散钉
            d[167] = B(5, 0.05f, 0f); // 深钉
            d[168] = B(3, 1f, 0f); // 多钉
            d[169] = B(3, 0.1f, 0f); // 暴钉
            d[170] = B(2, 0.4f, 0f); // 钉住
            d[171] = B(3, 0.08f, 3f); // 钉阵
            d[172] = B(2, 0.5f, 2.5f); // 连发
            d[173] = B(3, 0.03f, 0f); // 吸钉
            d[174] = B(5, 0.15f, 0f); // 剑匣
            d[175] = B(5, 0.1f, 0f); // 疾刺
            d[176] = B(5, 0.1f, 0f); // 长驱
            d[177] = B(5, 0.1f, 0f); // 剑幅
            d[178] = B(3, 0.1f, 0f); // 破竹
            d[179] = B(5, 0.15f, 0f); // 影刃
            d[180] = B(3, 0.03f, 0f); // 剑意
            d[181] = B(3, 0.1f, 0f); // 影杀
            d[182] = B(2, 0.4f, 0f); // 定影
            d[183] = B(3, 0.2f, 0f); // 嗜杀
            d[184] = B(3, 0.08f, 3f); // 剑阵
            d[185] = B(2, 0.5f, 2.5f); // 剑势
            d[186] = B(3, 0.03f, 0f); // 饮血
            d[187] = B(1, 1f, 0f); // 回身
            d[188] = B(1, 0.05f, 0f); // 补剑
            // ---- 闪电（11/27；策划书没给词条，以下为暂定）----
            d[189] = B(5, 0.15f, 0f); // 电容
            d[190] = B(5, 0.1f, 0f); // 疾电
            d[191] = B(3, 1f, 0f); // 链跃
            d[192] = B(5, 0.15f, 0f); // 电弧
            d[193] = B(5, 0.15f, 0f); // 高压
            d[194] = B(5, 0.15f, 0f); // 雷爆
            d[195] = B(3, 0.15f, 0f); // 落雷
            d[196] = B(2, 0.4f, 0f); // 麻痹
            d[197] = B(3, 0.25f, 0f); // 重雷
            d[198] = B(3, 0.25f, 2.5f); // 余电
            d[199] = B(3, 0.03f, 0f); // 汲电
            // 保留旧 ID / 数组索引兼容存档，但这些追加免费伤害词条不再进入升级池。
            DisableRollDamageBuilds(d);
            return d;
        }

        private static void DisableRollDamageBuilds(BuildDef[] builds)
        {
            BuildId[] removed = { BuildId.WaterBurstRing, BuildId.SpearBurstRing, BuildId.BladeBurstRing,
                BuildId.PalmBurstRing, BuildId.ShurikenBurstRing, BuildId.StaplerBurstRing, BuildId.SwordBurstRing };
            for (int i = 0; i < removed.Length; i++) builds[(int)removed[i]] = B(0, 0f, 0f);
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
            DisableRollDamageBuilds(result);
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
                case BuildId.PalmAmmo: return "厚掌";
                case BuildId.PalmRate: return "连掌";
                case BuildId.PalmAngle: return "开掌";
                case BuildId.PalmRange: return "推掌";
                case BuildId.PalmExtraMark: return "印心";
                case BuildId.PalmKnockback: return "排山";
                case BuildId.PalmPierce: return "透劲";
                case BuildId.PalmSkyDamage: return "天降";
                case BuildId.PalmMarkBonus: return "叠劲";
                case BuildId.PalmSkyArea: return "覆掌";
                case BuildId.PalmCrit: return "破军";
                case BuildId.PalmStun: return "镇压";
                case BuildId.PalmDoubleDrop: return "二重掌";
                case BuildId.PalmBurstRing: return "掌风";
                case BuildId.PalmPostHaste: return "余势";
                case BuildId.PalmLifesteal: return "纳气";
                case BuildId.PalmHurtStrike: return "反掌";
                case BuildId.PalmKillRefund: return "回气";
                case BuildId.BowDamage: return "重矢";
                case BuildId.BowKnockback: return "破甲";
                case BuildId.BowPierce: return "贯穿";
                case BuildId.BowFalloff: return "锐镞";
                case BuildId.BowStunTime: return "钉墙";
                case BuildId.BowExtraIntent: return "嗜杀";
                case BuildId.BowIntentBonus: return "杀意";
                case BuildId.BowCrit: return "夺命";
                case BuildId.BowSplash: return "杀气溢出";
                case BuildId.BowWallHeal: return "饮恨";
                case BuildId.BowHurtShot: return "反击";
                case BuildId.BowKillReload: return "连弩";
                case BuildId.DroneDamage: return "火力强化";
                case BuildId.DroneRate: return "速射";
                case BuildId.DronePierce: return "贯通弹";
                case BuildId.DroneSplitChance: return "增殖";
                case BuildId.DronePostHaste: return "超频";
                case BuildId.DroneSwarmBonus: return "蜂群";
                case BuildId.DroneMinTwo: return "双核";
                case BuildId.DroneKillSplit: return "裂变";
                case BuildId.DroneSplitBlast: return "分裂冲击";
                case BuildId.GravityRadius: return "视界";
                case BuildId.GravityPull: return "潮汐力";
                case BuildId.GravityPullCharging: return "锁定";
                case BuildId.GravityPullDamage: return "撕扯";
                case BuildId.GravityBlast: return "坍缩";
                case BuildId.GravityVulnerable: return "失衡";
                case BuildId.GravitySecondary: return "次级奇点";
                case BuildId.AnchorDamage: return "锋线";
                case BuildId.AnchorWidth: return "宽刃";
                case BuildId.AnchorThroughWalls: return "虚空斩";
                case BuildId.AnchorSpeedBoost: return "轻身";
                case BuildId.AnchorStun: return "断空";
                case BuildId.AnchorDoubleAnchor: return "双锚";
                case BuildId.AnchorCooldownCut: return "时隙";
                case BuildId.PokerTrimDeck: return "精简牌库";
                case BuildId.PokerJokerChance: return "鬼牌";
                case BuildId.PokerNumberBoost: return "加注";
                case BuildId.PokerHeartDamage: return "热恋";
                case BuildId.PokerHeartCount: return "心心相印";
                case BuildId.PokerHeartPierce: return "穿心";
                case BuildId.PokerSpadeDamage: return "黑桃尖";
                case BuildId.PokerSpadeArea: return "大爆炸";
                case BuildId.PokerSpadeStun: return "震慑";
                case BuildId.PokerShieldBurst: return "碎钻";
                case BuildId.PokerClubDamage: return "梅开";
                case BuildId.PokerClubPull: return "深渊";
                case BuildId.PokerClubDuration: return "长夜";
                case BuildId.PokerJokerBoost: return "王炸";
                case BuildId.PokerLifesteal: return "抽成";
                case BuildId.ShurikenRate: return "疾风";
                case BuildId.ShurikenDouble: return "双飞";
                case BuildId.ShurikenRange: return "远掷";
                case BuildId.ShurikenSpinDamage: return "飞旋";
                case BuildId.ShurikenSpinArea: return "旋风";
                case BuildId.ShurikenRecallDamage: return "归刃";
                case BuildId.ShurikenRecallWidth: return "宽径";
                case BuildId.ShurikenCrit: return "会心";
                case BuildId.ShurikenBurstRing: return "影袭";
                case BuildId.ShurikenPostHaste: return "连掷";
                case BuildId.ShurikenOrbitBonus: return "护身";
                case BuildId.ShurikenLifesteal: return "饮刃";
                case BuildId.HourglassDecay: return "流沙";
                case BuildId.HourglassFloor: return "凝滞";
                case BuildId.HourglassResetBlast: return "时之刃";
                case BuildId.HourglassPostSlow: return "余韵";
                case BuildId.HourglassVulnerable: return "迟滞";
                case BuildId.WasdDamage: return "重击键";
                case BuildId.WasdArea: return "大键帽";
                case BuildId.WasdSpeedBonus: return "连击";
                case BuildId.WasdDiagonal: return "斜键";
                case BuildId.WasdBurstDamage: return "四向强化";
                case BuildId.WasdBurstArea: return "全键盘";
                case BuildId.WasdBurstStun: return "卡键";
                case BuildId.WasdBurstCrit: return "暴击键";
                case BuildId.WasdHurtStrike: return "防御键";
                case BuildId.MagnetPathDamage: return "磁暴线";
                case BuildId.MagnetPathWidth: return "宽磁场";
                case BuildId.MagnetBlastDamage: return "湮灭";
                case BuildId.MagnetBlastArea: return "冲击波";
                case BuildId.MagnetStun: return "磁晕";
                case BuildId.MagnetHitRamp: return "连锁";
                case BuildId.MagnetPathPull: return "磁流";
                case BuildId.MagnetVulnerable: return "磁暴";
                case BuildId.MagnetSpawnPull: return "磁场";
                case BuildId.MagnetThroughWalls: return "穿壁";
                case BuildId.CursorDamage: return "降维";
                case BuildId.CursorArea: return "大选区";
                case BuildId.CursorPinTime: return "压扁";
                case BuildId.CursorThickness: return "加厚";
                case BuildId.CursorCrowdBonus: return "全选";
                case BuildId.CursorCrit: return "暴击框";
                case BuildId.CursorAfterSlow: return "拖拽";
                case BuildId.CursorDrawHaste: return "快速框选";
                case BuildId.CursorPinVulnerable: return "贴地";
                case BuildId.StaplerAmmo: return "大容量";
                case BuildId.StaplerRate: return "速钉";
                case BuildId.StaplerDouble: return "双钉";
                case BuildId.StaplerKnockback: return "重压";
                case BuildId.StaplerPierce: return "贯穿钉";
                case BuildId.StaplerSlow: return "紧固";
                case BuildId.StaplerGroupSize: return "长钉";
                case BuildId.StaplerScatterDamage: return "散钉";
                case BuildId.StaplerMarkBonus: return "深钉";
                case BuildId.StaplerScatterCount: return "多钉";
                case BuildId.StaplerCrit: return "暴钉";
                case BuildId.StaplerStun: return "钉住";
                case BuildId.StaplerBurstRing: return "钉阵";
                case BuildId.StaplerPostHaste: return "连发";
                case BuildId.StaplerLifesteal: return "吸钉";
                case BuildId.SwordAmmo: return "剑匣";
                case BuildId.SwordRate: return "疾刺";
                case BuildId.SwordRange: return "长驱";
                case BuildId.SwordWidth: return "剑幅";
                case BuildId.SwordHitRamp: return "破竹";
                case BuildId.SwordShadowDamage: return "影刃";
                case BuildId.SwordTrailBonus: return "剑意";
                case BuildId.SwordCrit: return "影杀";
                case BuildId.SwordStun: return "定影";
                case BuildId.SwordKillThreshold: return "嗜杀";
                case BuildId.SwordBurstRing: return "剑阵";
                case BuildId.SwordPostHaste: return "剑势";
                case BuildId.SwordLifesteal: return "饮血";
                case BuildId.SwordHurtDash: return "回身";
                case BuildId.SwordKillRefund: return "补剑";
                case BuildId.LightningAmmo: return "电容";
                case BuildId.LightningRate: return "疾电";
                case BuildId.LightningJumps: return "链跃";
                case BuildId.LightningJumpRange: return "电弧";
                case BuildId.LightningDamage: return "高压";
                case BuildId.LightningDetonateDamage: return "雷爆";
                case BuildId.LightningCrit: return "落雷";
                case BuildId.LightningStun: return "麻痹";
                case BuildId.LightningExtraMark: return "重雷";
                case BuildId.LightningPostHaste: return "余电";
                case BuildId.LightningLifesteal: return "汲电";
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
                case BuildId.BladeAngle: return "月牙剑气的宽度 +{p}";
                case BuildId.BladeRange: return "扇形剑气的攻击距离 +{p}";
                case BuildId.BladePierce: return "剑气可多穿透 {n} 名敌人";
                case BuildId.BladeLength: return "武器长度 +{p}：同时提升剑气宽度与射程";
                case BuildId.BladeMarkBonus: return "每层剑气印记的增伤 +{p}";
                case BuildId.BladeSlashWidth: return "横向斩击的有效宽度 +{p}";
                case BuildId.BladeCrit: return "斩击伤害有 {p} 概率暴击";
                case BuildId.BladeStun: return "一刀斩命中的敌人眩晕 {n} 秒";
                case BuildId.BladeExecute: return "一刀斩直接斩杀生命低于 {p} 的敌人";
                case BuildId.BladeBurstRing: return "翻滚爆发时，对周身 {v2} 单位内敌人造成 {p} 最大生命的斩击伤害";
                case BuildId.BladePostHaste: return "翻滚结束后 {v2} 秒内剑气发射攻速 +{p}";
                case BuildId.BladeHurtMarks: return "受到伤害时，在最近的 {v2} 名敌人身上各叠加 {n} 层剑气印记";
                case BuildId.PalmAmmo: return "弹药上限 +{p}";
                case BuildId.PalmRate: return "掌击释放攻速 +{p}";
                case BuildId.PalmAngle: return "扇形掌击的展开角度 +{p}";
                case BuildId.PalmRange: return "扇形掌击的有效攻击距离 +{p}";
                case BuildId.PalmExtraMark: return "每次掌击命中额外叠加 {n} 层掌印";
                case BuildId.PalmKnockback: return "掌击退力度 +{p}（冲锋中的敌人仍免疫击退）";
                case BuildId.PalmPierce: return "掌击可多穿透 {n} 名敌人（向纵深多推进一圈）";
                case BuildId.PalmSkyDamage: return "天降掌基础伤害 +{p}";
                case BuildId.PalmMarkBonus: return "每层掌印的伤害加成 +{p}";
                case BuildId.PalmSkyArea: return "天降掌的圆形作用范围 +{p}";
                case BuildId.PalmCrit: return "天降掌伤害有 {p} 概率暴击";
                case BuildId.PalmStun: return "天降掌命中的敌人眩晕 {n} 秒";
                case BuildId.PalmDoubleDrop: return "天降掌分两段落下，第二段伤害为第一段的 {p}";
                case BuildId.PalmBurstRing: return "翻滚爆发时，对周身 {v2} 单位内敌人造成 {p} 最大生命的伤害";
                case BuildId.PalmPostHaste: return "翻滚结束后 {v2} 秒内掌击攻速 +{p}";
                case BuildId.PalmLifesteal: return "天降掌伤害的 {p} 转为生命";
                case BuildId.PalmHurtStrike: return "受到伤害时，自动对周身释放一次扇形掌击并叠加掌印";
                case BuildId.PalmKillRefund: return "常态掌击击杀敌人有 {p} 概率补充 1 颗弹药";
                case BuildId.BowDamage: return "弩箭基础伤害 +{p}";
                case BuildId.BowKnockback: return "弩箭击退力度 +{p}";
                case BuildId.BowPierce: return "弩箭击退距离 +{p}";
                case BuildId.BowFalloff: return "弩箭穿透后的伤害衰减比例 −{p}";
                case BuildId.BowStunTime: return "敌人撞墙的眩晕时长 +{n} 秒";
                case BuildId.BowExtraIntent: return "每次击退敌人撞墙，额外增加 {n} 点撞墙计数（用于杀气溢出）";
                case BuildId.BowIntentBonus: return "击杀尺寸成长速率 +{p}（尺寸上限不变）";
                case BuildId.BowCrit: return "弩箭伤害有 {p} 概率暴击";
                case BuildId.BowSplash: return "撞墙计数 ≥ {v2} 时，弩箭命中额外造成 {p} 伤害的小范围溅射（切枪清空计数）";
                case BuildId.BowWallHeal: return "击退敌人撞墙时回复 {n} 点生命";
                case BuildId.BowHurtShot: return "受到伤害时，自动向身前发射一发弩箭并击退敌人";
                case BuildId.BowKillReload: return "弩箭常态击杀敌人有 {p} 概率直接补满弹药";
                case BuildId.DroneDamage: return "单台浮游炮伤害 +{p}";
                case BuildId.DroneRate: return "浮游炮攻击攻速 +{p}";
                case BuildId.DronePierce: return "浮游炮子弹可穿透 {n} 个敌人";
                case BuildId.DroneSplitChance: return "翻滚分裂概率 +{p}";
                case BuildId.DronePostHaste: return "翻滚后 {v2} 秒内浮游炮攻速 +{p}";
                case BuildId.DroneSwarmBonus: return "浮游炮数量 ≥ {v2} 台时，所有浮游炮伤害 +{p}";
                case BuildId.DroneMinTwo: return "合体时至少保留 2 台";
                case BuildId.DroneKillSplit: return "浮游炮击杀敌人时有 {p} 概率立即分裂一次（台数加法，同翻滚分裂）";
                case BuildId.DroneSplitBlast: return "分裂到新的数量时，对周身 {v2} 单位内敌人造成 {n} 点伤害";
                case BuildId.GravityRadius: return "引力场作用范围 +{p}";
                case BuildId.GravityPull: return "怪物拉扯力度 +{p}";
                case BuildId.GravityPullCharging: return "引力场可以拉扯冲锋中的敌人";
                case BuildId.GravityPullDamage: return "引力拉扯时造成 {p} 最大生命的伤害";
                case BuildId.GravityBlast: return "聚拢结束时，中心点 {v2} 单位内爆炸，造成 {n} 点伤害";
                case BuildId.GravityVulnerable: return "被拉扯过的敌人受到本次主武器爆发伤害 +{p}";
                case BuildId.GravitySecondary: return "每次翻滚额外生成 {n} 个次级引力点，小幅拉扯周边敌人";
                case BuildId.AnchorDamage: return "锚点连线斩击伤害 +{p}";
                case BuildId.AnchorWidth: return "斩击路径的判定宽度 +{p}";
                case BuildId.AnchorThroughWalls: return "斩击穿透障碍物命中敌人";
                case BuildId.AnchorSpeedBoost: return "回溯完成后 {v2} 秒内移动速度 +{p}";
                case BuildId.AnchorStun: return "斩击命中的敌人眩晕 {n} 秒";
                case BuildId.AnchorDoubleAnchor: return "可同时储存 2 个锚点，回溯时沿路径触发两段斩击";
                case BuildId.AnchorCooldownCut: return "斩击每命中 1 名敌人，本次翻滚冷却 −{n} 秒（最多 −{v2} 秒）";
                case BuildId.PokerTrimDeck: return "永久从牌库中移除点数最小的 {n} 张牌，提升大牌出现概率（牌库剩 6 张时不再出现）";
                case BuildId.PokerJokerChance: return "大小王出现概率 +{p}";
                case BuildId.PokerNumberBoost: return "每张点数牌的基础效果系数 +{p}";
                case BuildId.PokerHeartDamage: return "红桃爱心弹基础伤害 +{p}";
                case BuildId.PokerHeartCount: return "每点牌面对应的爱心弹数量 +{n}";
                case BuildId.PokerHeartPierce: return "爱心弹可穿透 {n} 个敌人";
                case BuildId.PokerSpadeDamage: return "黑桃炸弹基础伤害 +{p}";
                case BuildId.PokerSpadeArea: return "黑桃爆炸范围 +{p}";
                case BuildId.PokerSpadeStun: return "黑桃炸弹命中的敌人眩晕 {n} 秒";
                case BuildId.PokerShieldBurst: return "方片护盾每碎一片，对周身 {v2} 单位内敌人造成 {n} 点伤害";
                case BuildId.PokerClubDamage: return "梅花裂隙基础伤害 +{p}";
                case BuildId.PokerClubPull: return "裂隙吸附范围与拉扯力度 +{p}";
                case BuildId.PokerClubDuration: return "牌面点数对裂隙持续时间的加成倍率 +{p}";
                case BuildId.PokerJokerBoost: return "大小王提供的永久加成幅度 +{p}";
                case BuildId.PokerLifesteal: return "卡牌造成伤害时吸血 {p}";
                case BuildId.ShurikenRate: return "手里剑发射攻速 +{p}";
                case BuildId.ShurikenDouble: return "一次发射两枚散射手里剑";
                case BuildId.ShurikenRange: return "手里剑飞行距离 +{p}";
                case BuildId.ShurikenSpinDamage: return "手里剑旋转的基础伤害 +{p}";
                case BuildId.ShurikenSpinArea: return "手里剑旋转的判定范围 +{p}";
                case BuildId.ShurikenRecallDamage: return "手里剑回收基础伤害 +{p}";
                case BuildId.ShurikenRecallWidth: return "回收路径的判定宽度 +{p}";
                case BuildId.ShurikenCrit: return "回收伤害有 {p} 概率暴击";
                case BuildId.ShurikenBurstRing: return "翻滚爆发时，对周围 {v2} 单位内敌人造成 {p} 最大生命的穿刺伤害";
                case BuildId.ShurikenPostHaste: return "翻滚结束后 {v2} 秒内手里剑发射攻速 +{p}";
                case BuildId.ShurikenOrbitBonus: return "环绕状态下手里剑旋转伤害 +{p}";
                case BuildId.ShurikenLifesteal: return "回收爆发造成伤害时吸血 {p}";
                case BuildId.HourglassDecay: return "敌人移速的衰减速率 +{p}";
                case BuildId.HourglassFloor: return "敌人最低移速下限降低 {p}（基础 50% → 40%）";
                case BuildId.HourglassResetBlast: return "翻滚重置移速的瞬间，对周身 {v2} 单位内敌人造成 {p} 最大生命的伤害";
                case BuildId.HourglassPostSlow: return "翻滚结束后 {v2} 秒内敌人移速额外降低 {p}";
                case BuildId.HourglassVulnerable: return "敌人移速越低受到的伤害越高（降到一半移速时 +{p}）";
                case BuildId.WasdDamage: return "单方向按键基础伤害 +{p}";
                case BuildId.WasdArea: return "按键判定的作用范围 +{p}";
                case BuildId.WasdSpeedBonus: return "移动速度越快按键伤害越高（基础移速时 +{p}）";
                case BuildId.WasdDiagonal: return "斜向移动时额外触发对角线方向的伤害判定";
                case BuildId.WasdBurstDamage: return "四向齐发的基础伤害 +{p}";
                case BuildId.WasdBurstArea: return "四向齐发的作用范围 +{p}";
                case BuildId.WasdBurstStun: return "四向齐发命中的敌人眩晕 {n} 秒";
                case BuildId.WasdBurstCrit: return "四向齐发有 {p} 概率暴击";
                case BuildId.WasdHurtStrike: return "受到伤害时，自动在四个方向各触发一次按键伤害";
                case BuildId.MagnetPathDamage: return "磁极对撞的路径基础伤害 +{p}";
                case BuildId.MagnetPathWidth: return "对撞路径的判定宽度 +{p}";
                case BuildId.MagnetBlastDamage: return "中心爆炸基础伤害 +{p}";
                case BuildId.MagnetBlastArea: return "中心爆炸作用范围 +{p}";
                case BuildId.MagnetStun: return "爆炸眩晕时长 +{n} 秒";
                case BuildId.MagnetHitRamp: return "对撞路径上每命中 1 名敌人，爆炸伤害 +{p}";
                case BuildId.MagnetPathPull: return "对撞时把路径两侧敌人向中心拉近 {n} 单位";
                case BuildId.MagnetVulnerable: return "被爆炸命中的敌人受到本次主武器爆发伤害 +{p}";
                case BuildId.MagnetSpawnPull: return "磁极生成时，把周身敌人向磁极拉近 {n} 单位";
                case BuildId.MagnetThroughWalls: return "对撞与爆炸穿透障碍物，不受地形遮挡";
                case BuildId.CursorDamage: return "降维打击基础伤害 +{p}";
                case BuildId.CursorArea: return "光标选区的最大覆盖范围 +{p}";
                case BuildId.CursorPinTime: return "定身时长 +{n} 秒";
                case BuildId.CursorThickness: return "打击判定的边界厚度 +{p}（避免漏怪）";
                case BuildId.CursorCrowdBonus: return "选区内每有 1 名敌人，打击伤害 +{p}";
                case BuildId.CursorCrit: return "降维打击有 {p} 概率暴击";
                case BuildId.CursorAfterSlow: return "打击结束后选区内敌人移速降低 {p}，持续 {v2} 秒";
                case BuildId.CursorDrawHaste: return "光标绘制过程中玩家移动速度 +{p}";
                case BuildId.CursorPinVulnerable: return "定身期间敌人受到的所有伤害 +{p}";
                case BuildId.StaplerAmmo: return "弹药上限 +{p}";
                case BuildId.StaplerRate: return "订书钉发射攻速 +{p}";
                case BuildId.StaplerDouble: return "一次发射两枚散射订书钉";
                case BuildId.StaplerKnockback: return "订书钉击退力度 +{p}（冲锋中的敌人仍免疫）";
                case BuildId.StaplerPierce: return "订书钉可穿透敌人数量 +{n}";
                case BuildId.StaplerSlow: return "钉合后的怪物减速幅度 +{p}";
                case BuildId.StaplerGroupSize: return "单次钉合的最大怪物数量 +{n}";
                case BuildId.StaplerScatterDamage: return "次级钉基础伤害 +{p}";
                case BuildId.StaplerMarkBonus: return "每层钉痕提供的散射伤害加成 +{p}";
                case BuildId.StaplerScatterCount: return "每只钉痕怪散射出的次级钉数量 +{n}";
                case BuildId.StaplerCrit: return "散射伤害有 {p} 概率暴击";
                case BuildId.StaplerStun: return "次级钉命中的敌人眩晕 {n} 秒";
                case BuildId.StaplerBurstRing: return "翻滚爆发时，对周围 {v2} 单位内敌人造成 {p} 最大生命的穿刺伤害";
                case BuildId.StaplerPostHaste: return "翻滚结束后 {v2} 秒内订书钉发射攻速 +{p}";
                case BuildId.StaplerLifesteal: return "散射爆发造成伤害时吸血 {p}";
                case BuildId.SwordAmmo: return "弹药上限 +{p}";
                case BuildId.SwordRate: return "突进刺击攻速 +{p}";
                case BuildId.SwordRange: return "单次突进的有效距离 +{p}";
                case BuildId.SwordWidth: return "突进刺击的判定宽度 +{p}";
                case BuildId.SwordHitRamp: return "突进路径上每多命中 1 名敌人，伤害 +{p}";
                case BuildId.SwordShadowDamage: return "影子复刻基础伤害 +{p}";
                case BuildId.SwordTrailBonus: return "每条剑痕对应的影刺伤害加成 +{p}";
                case BuildId.SwordCrit: return "影刺伤害有 {p} 概率暴击";
                case BuildId.SwordStun: return "影刺命中的敌人眩晕 {n} 秒";
                case BuildId.SwordKillThreshold: return "影刺击杀叠加突进次数的阈值 −{p}";
                case BuildId.SwordBurstRing: return "翻滚爆发时，对周围 {v2} 单位内敌人造成 {p} 最大生命的斩击伤害";
                case BuildId.SwordPostHaste: return "翻滚结束后 {v2} 秒内突进刺击攻速 +{p}";
                case BuildId.SwordLifesteal: return "影刺爆发造成伤害时吸血 {p}";
                case BuildId.SwordHurtDash: return "受到伤害时，自动向后突进一段距离并留下剑痕";
                case BuildId.SwordKillRefund: return "常态突进击杀敌人有 {p} 概率补充 1 颗弹药";
                case BuildId.LightningAmmo: return "弹药上限 +{p}";
                case BuildId.LightningRate: return "闪电发射攻速 +{p}";
                case BuildId.LightningJumps: return "每道闪电多连锁 {n} 只敌人";
                case BuildId.LightningJumpRange: return "连锁跳跃距离 +{p}";
                case BuildId.LightningDamage: return "闪电命中伤害 +{p}";
                case BuildId.LightningDetonateDamage: return "雷痕引爆伤害 +{p}";
                case BuildId.LightningCrit: return "引爆伤害有 {p} 概率暴击";
                case BuildId.LightningStun: return "引爆后没死的敌人眩晕 {n} 秒";
                case BuildId.LightningExtraMark: return "闪电命中有 {p} 概率多叠 1 层雷痕";
                case BuildId.LightningPostHaste: return "翻滚结束后 {v2} 秒内闪电攻速 +{p}";
                case BuildId.LightningLifesteal: return "雷痕引爆造成伤害时吸血 {p}";
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