using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ==================================================================
    // 战斗数据（程序B）：武器、怪物、刷怪、升级、经验。世界数据在 Models.World.cs（程序A）。
    // 配置数据：由 Unity 层的 TurnConfigReader 从 Inspector 参数转换而来，运行时只读。
    // 新策划案（11/24）用的是下面的 ArsenalConfig / HordeConfig / BuildConfig；
    // 旧方案的 WeaponConfig / WeaponFormConfig / MonsterConfig / SpawnConfig / PenaltyConfig / UpgradeConfig
    // 只为兼容场景与 Prefab 上已序列化的 Inspector 字段而保留，新系统不再读取（MonsterConfig 的岩浆伤害除外）。
    // 像素 → 世界单位：1 单位 = 40 像素。
    // ==================================================================

    // ---------------- 新策划案：三把武器 ----------------

    /// <summary>水枪。常态：水弹落地 / 命中敌人处留下水渍；爆发：全图水渍同时喷发喷泉。</summary>
    [Serializable]
    public sealed class WaterGunConfig
    {
        public int Ammo = 30;
        /// <summary>攻击间隔（秒 / 发）。</summary>
        public float FireInterval = 0.2f;
        /// <summary>水弹飞行速度（单位/秒）。策划书没给，暂定 10（400 像素/秒）。</summary>
        public float BulletSpeed = 10f;
        public float Damage = 5f;
        public float BulletRadius = 0.15f;
        /// <summary>自动索敌 / 射程（单位）。策划书没给，暂定 8。</summary>
        public float Range = 8f;
        /// <summary>水渍半径（单位，策划 40 像素 = 1）。</summary>
        public float PuddleRadius = 1f;
        /// <summary>单滩水渍的喷泉伤害。</summary>
        public float FountainDamage = 12f;
        /// <summary>重叠加成：同一敌人被 n 滩喷泉覆盖时，每滩伤害 ×(1 + OverlapBonus × (n − 1))。默认 0 = 每滩独立判定、直接相加。</summary>
        public float OverlapBonus = 0f;
        /// <summary>全图水渍上限（防止极端情况下卡顿）。</summary>
        public int MaxPuddles = 400;
        /// <summary>散射词条：两枚子弹之间的夹角（度）。</summary>
        public float SpreadDegrees = 12f;
    }

    /// <summary>投矛。常态：命中叠矛印记，击杀则穿透下一个并传递印记；爆发：全部长矛向玩家回收，路径穿刺。</summary>
    [Serializable]
    public sealed class SpearConfig
    {
        public int Ammo = 20;
        public float FireInterval = 0.4f;
        /// <summary>长矛飞行速度（单位/秒）。策划书没给，暂定 12。</summary>
        public float Speed = 12f;
        public float Damage = 8f;
        public float Radius = 0.18f;
        /// <summary>索敌 / 射程（单位）。暂定 9。</summary>
        public float Range = 9f;
        /// <summary>每穿透一次伤害衰减比例（策划 20%）。</summary>
        public float PierceFalloff = 0.2f;
        /// <summary>每根长矛的回收伤害。</summary>
        public float RecallDamage = 15f;
        /// <summary>回收路径判定半宽（单位）。暂定 0.5。</summary>
        public float RecallWidth = 0.5f;
        /// <summary>每多一层矛，这只敌人身上每根矛的回收伤害加成（默认 0，词条提升）。</summary>
        public float StackBonus = 0f;
        /// <summary>双矛词条：两根长矛之间的夹角（度）。</summary>
        public float SpreadDegrees = 14f;
        /// <summary>回收击杀时产生的普通矛数量与索敌范围。</summary>
        public int RecallKillSpearCount = 2;
        public float RecallKillTargetRange = 9f;
    }

    /// <summary>
    /// 陌刀。常态（11/27 起）：朝最近敌人发射一道月牙剑气弹，命中第一个敌人即消失（「破阵」每层 +1 穿透），命中叠 1 层剑气印记；
    /// 爆发：横贯屏幕的巨型斩击，按全场印记总层数增伤。
    /// </summary>
    [Serializable]
    public sealed class BladeConfig
    {
        public int Ammo = 25;
        public float FireInterval = 0.5f;
        public float Damage = 6f;
        /// <summary>剑气弹飞行速度（单位/秒）。11/27 剑气改弹幕后新增，暂定 12。</summary>
        public float WaveSpeed = 12f;
        /// <summary>剑气弹判定半宽（单位，月牙的宽度一半）；「开阖」「长刀」按比例加宽。暂定 0.6。</summary>
        public float WaveHalfWidth = 0.6f;
        /// <summary>旧扇形剑气的总角度（度）。11/27 剑气改弹幕后不再使用，字段保留以兼容存档。</summary>
        public float Angle = 60f;
        /// <summary>剑气射程（单位，策划 300 像素 = 7.5）。</summary>
        public float Range = 7.5f;
        /// <summary>旧弧形剑气的厚度。11/27 剑气改弹幕后不再使用，字段保留以兼容存档。</summary>
        public float RingThickness = 0.9f;
        /// <summary>一刀斩基础伤害。</summary>
        public float SlashDamage = 25f;
        /// <summary>每层印记增伤（策划 8%）。</summary>
        public float MarkBonus = 0.08f;
        /// <summary>横向斩击的半高（单位，以玩家为中心上下各这么多）。暂定 1.5。</summary>
        public float SlashHalfHeight = 1.5f;
    }

    /// <summary>
    /// 掌（11/25 双武器策划案）。常态：朝面向方向扇形掌击，伤害 + 击退（冲锋中的敌人免疫）+ 叠 1 层掌印；
    /// 爆发：以自身为中心的圆形天降掌，每只敌人按自己的掌印层数增伤，结算后掌印清空。策划书没给数值，以下均为暂定。
    /// </summary>
    [Serializable]
    public sealed class PalmConfig
    {
        public int Ammo = 25;
        public float FireInterval = 0.45f;
        public float Damage = 7f;
        /// <summary>扇形总角度（度）。</summary>
        public float Angle = 90f;
        /// <summary>掌击距离（单位）。</summary>
        public float Range = 3.5f;
        /// <summary>只命中最近一圈敌人（同陌刀），穿透词条每层多推进一圈。</summary>
        public float RingThickness = 0.9f;
        /// <summary>击退距离（单位）。</summary>
        public float Knockback = 1.2f;
        /// <summary>天降掌半径（单位）。</summary>
        public float SkyRadius = 6f;
        public float SkyDamage = 20f;
        /// <summary>每层掌印的增伤（该敌人的伤害 × (1 + 层数 × MarkBonus)）。</summary>
        public float MarkBonus = 0.15f;
    }

    /// <summary>
    /// 弩（11/25 双武器策划案）。常态：基础 1 发重型弩箭，穿透（每穿透一个衰减）+ 强制击退；
    /// 敌人被击退撞到障碍物 / 地图边缘：眩晕，玩家 +1 层杀气（只加弩的伤害，无限叠加，主武器切换时清零）。
    /// 翻滚：没有爆发。策划书没给数值，以下均为暂定。
    /// </summary>
    [Serializable]
    public sealed class CrossbowConfig
    {
        public int Ammo = 1;
        public float FireInterval = 0.3f;
        public float Speed = 24f;
        public float Damage = 30f;
        public float Radius = 0.66f;
        public float Range = 22.5f;
        /// <summary>玩家击杀累计成长：每段击杀增加基础尺寸比例，最终尺寸倍率封顶；本局保留。</summary>
        public int KillsPerSizeStep = 5;
        public float SizePerKillStep = 0.01f;
        public float MaxSizeMultiplier = 5f;
        public float SwordWaveRangeMultiplier = 3f;
        /// <summary>基础可穿透（可命中）的敌人数。</summary>
        public int Pierce = 3;
        /// <summary>每穿透一个敌人伤害衰减比例。</summary>
        public float PierceFalloff = 0.25f;
        /// <summary>击退距离（单位）。</summary>
        public float Knockback = 2.5f;
        /// <summary>撞墙眩晕（秒）。</summary>
        public float WallStun = 1f;
        /// <summary>每层杀气的弩伤害加成。</summary>
        public float IntentBonus = 0.05f;
        /// <summary>「杀气溢出」词条的溅射半径（单位）。</summary>
        public float SplashRadius = 1.2f;
    }

    /// <summary>副武器 · 浮游炮：环绕玩家自动攻击；每次翻滚盲盒 50% 分裂（×2）/ 50% 合体（÷2，保底 1）。数值暂定。</summary>
    [Serializable]
    public sealed class DronesConfig
    {
        public int StartCount = 1;
        /// <summary>分裂一次增加几台（翻滚分裂、「裂变」击杀分裂都按这个数加）。</summary>
        public int SplitAdd = 1;
        /// <summary>合体一次减少几台。</summary>
        public int MergeRemove = 1;
        /// <summary>合体最少保留几台（「双核」词条至少 2 台）。</summary>
        public int MinCount = 1;
        /// <summary>台数上限；0 = 不限。11/27 定为 50 台封顶。</summary>
        public int MaxCount = 50;
        /// <summary>单台每发伤害。</summary>
        public float Damage = 4f;
        public float FireInterval = 0.8f;
        public float Range = 7f;
        public float BulletSpeed = 12f;
        public float BulletRadius = 0.12f;
        /// <summary>环绕半径（单位）与角速度（弧度 / 秒）。</summary>
        public float OrbitRadius = 1.3f;
        public float OrbitSpeed = 2f;
        /// <summary>同时真正开火的台数上限（性能）。超出的台数折算进这些炮的伤害里，逻辑数量本身没有上限。</summary>
        public int MaxActive = 32;
        /// <summary>翻滚时分裂（+台数）的基础概率。11/27 调为 70%。</summary>
        public float SplitChance = 0.7f;
    }

    /// <summary>副武器 · 引力黑洞：翻滚时以翻滚前的站位为中心，把周围怪物拉向中心（无基础伤害）。数值暂定。</summary>
    [Serializable]
    public sealed class GravityWellConfig
    {
        public float Radius = 5f;
        /// <summary>持久黑洞每秒伤害（旧实现无基础伤害，首版暂定6）。</summary>
        public float DamagePerSecond = 6f;
        public float MergeSpeed = 8f;
        public float PickupRadius = 5f;
        public float PickupSpeed = 8f;
        /// <summary>持续牵引速度（单位/秒）。</summary>
        public float PullDistance = 3f;
        /// <summary>拉到离中心这么近就停（单位）。</summary>
        public float MinGap = 0.6f;
        /// <summary>次级引力点：半径、拉扯距离、离主中心的距离。</summary>
        public float SecondaryRadius = 2.5f;
        public float SecondaryPull = 1.2f;
        public float SecondaryOffset = 3f;
    }

    /// <summary>副武器 · 空间锚：每次翻滚记录锚点；下一次翻滚回溯到锚点，连线斩击。数值暂定。</summary>
    [Serializable]
    public sealed class SpaceAnchorConfig
    {
        public float SlashDamage = 30f;
        /// <summary>斩击路径半宽（单位）。</summary>
        public float Width = 0.6f;
    }

    /// <summary>
    /// 主武器 · 扑克牌（11/25 第二批策划案）。常态不攻击，每隔 DrawInterval 秒抽 1 张牌到头顶（弹药 = 手牌上限，抽满停）；
    /// 翻滚爆发时打出全部手牌，每张按花色 + 点数独立结算。数值策划书没给，以下均为暂定。
    /// </summary>
    [Serializable]
    public sealed class PokerConfig
    {
        /// <summary>手牌上限（= 弹药）。</summary>
        public int HandSize = 5;
        public float DrawInterval = 1.5f;
        public float JokerChanceMultiplier = 0.5f;
        public float ShieldMultiplier = 0.5f;
        public int SpadeChildCount = 4;
        public float SpadeChildDamageRatio = 0.375f;
        public float SpadeChildRadiusRatio = 0.5f;
        public float SpadeChildOffsetRatio = 0.5f;
        public float SpadeChildDelay = 0.15f;
        /// <summary>眼状裂隙短轴/长轴比例。</summary>
        public float ClubAspectRatio = 0.35f;
        /// <summary>红桃：每点几发爱心弹（策划 5）；单发伤害、速度、半径、射程、可反弹次数、扇形张角、一次爆发最多几发（性能）。</summary>
        public int HeartsPerPoint = 10;
        public float HeartDamage = 3f;
        public float HeartSpeed = 10f;
        public float HeartRadius = 0.15f;
        public float HeartRange = 14f;
        public int HeartBounces = 3;
        public float HeartSpread = 50f;
        /// <summary>♥ 追踪：转向速度（度 / 秒，0 = 不追踪）、索敌半径（以爱心弹自己为圆心）。</summary>
        public float HeartHomingTurn = 360f;
        public float HeartHomingRange = 6f;
        /// <summary>♥ 命中时的小范围爆炸：半径、伤害 = 爱心弹伤害 × 倍率（打到范围内所有敌人，含被直接命中的那只）。</summary>
        public float HeartBlastRadius = 0.9f;
        public float HeartBlastRatio = 0.6f;
        public int MaxHearts = 600;
        /// <summary>黑桃：伤害 = 每点伤害 × 点数；半径 = 基础 + 每点 × 点数。</summary>
        public float SpadeDamagePerPoint = 6f;
        public float SpadeRadius = 1.2f;
        public float SpadeRadiusPerPoint = 0.12f;
        /// <summary>方片：护盾层数 = 点数（× 加成）；最多叠几层。</summary>
        public int ShieldMax = 40;
        /// <summary>梅花：裂隙半径、每秒伤害、吸附速度（单位/秒）、持续 = 基础 + 每点 × 点数（秒）。</summary>
        public float ClubRadius = 1.875f;
        public float ClubDps = 9f;
        public float ClubPullSpeed = 2.5f;
        public float ClubDuration = 1f;
        public float ClubDurationPerPoint = 0.4f;
        /// <summary>大小王：每张永久提升所有卡牌效果的比例（切换主武器时清零）。</summary>
        public float SmallJokerBonus = 0.08f;
        public float BigJokerBonus = 0.15f;
        /// <summary>找黑桃落点 / 梅花目标的搜索范围。</summary>
        public float TargetRange = 12f;
    }

    /// <summary>
    /// 主武器 · 手里剑（11/25 第二批策划案）。常态朝最近敌人飞出 FlyDistance 后停住旋转，持续伤害范围内敌人（永久留存）；
    /// 爆发：全部回收，路径穿刺；方案 A 回收后环绕玩家，直到下一次切换主武器。数值暂定。
    /// </summary>
    [Serializable]
    public sealed class ShurikenConfig
    {
        public int Ammo = 12;
        public float FireInterval = 0.6f;
        public float Speed = 9f;
        public float FlyDistance = 5f;
        /// <summary>旋转：判定半径、每次伤害、伤害间隔（秒）。</summary>
        public float SpinRadius = 0.9f;
        public float SpinDamage = 4f;
        public float SpinInterval = 0.3f;
        /// <summary>回收：每枚路径伤害、路径半宽。</summary>
        public float RecallDamage = 14f;
        public float RecallWidth = 0.5f;
        /// <summary>双飞词条：两枚之间的夹角（度）。</summary>
        public float DoubleAngle = 20f;
        /// <summary>环绕（方案 A）：半径、角速度（弧度 / 秒）。</summary>
        public float OrbitRadius = 1.6f;
        public float OrbitSpeed = 4f;
        /// <summary>场上最多留几枚（性能兜底，超出时最早的消失）。</summary>
        public int MaxOnField = 60;
        public float TargetRange = 8f;
    }

    /// <summary>
    /// 主武器 · 订书机（11/25 第三批）：订书钉命中造成伤害并击退；被击退的敌人撞上其他怪物时把沿途撞到的钉合成一组（叠钉痕 + 减速）。
    /// 爆发：全图带钉痕的怪各向四周散射 SecondaryCount 枚次级钉（和常态钉逻辑一致）。数值暂定。
    /// </summary>
    [Serializable]
    public sealed class StaplerConfig
    {
        public int Ammo = 25;
        public float FireInterval = 0.4f;
        public float Speed = 14f;
        public float Damage = 6f;
        public float Radius = 0.15f;
        public float Range = 9f;
        /// <summary>可命中敌人数（1 = 不穿透）。</summary>
        public int Pierce = 1;
        public float Knockback = 1f;
        /// <summary>一组最多钉合几只（含被击退的那只）。</summary>
        public int GroupMax = 3;
        /// <summary>钉合减速：移速 × (1 − GroupSlow)，持续 GroupSlowTime 秒。</summary>
        public float GroupSlow = 0.3f;
        public float GroupSlowTime = 4f;
        public int SecondaryCount = 5;
        public float SecondaryDamage = 8f;
        /// <summary>每层钉痕的散射伤害加成。</summary>
        public float MarkBonus = 0.2f;
        public float SecondaryRange = 5f;
        /// <summary>一次爆发最多散射几枚（性能）。</summary>
        public int MaxSecondary = 400;
    }

    /// <summary>
    /// 主武器 · 剑（11/25 第三批）：朝面向方向突进刺击（突进中无敌），路径留下剑痕；
    /// 爆发：影子沿全部剑痕复刻刺击，影刺每击杀 1 名敌人永久 +1 突进次数上限（切换主武器清零）。数值暂定。
    /// </summary>
    [Serializable]
    public sealed class SwordConfig
    {
        public int Ammo = 8;
        public float FireInterval = 0.9f;
        public float DashDistance = 4.5f;
        public float DashTime = 0.15f;
        public float Width = 0.6f;
        public float Damage = 14f;
        /// <summary>面向方向这么远、这么大夹角内有敌人才突进。</summary>
        public float TargetRange = 5f;
        public float TargetAngle = 120f;
        public float ShadowDamage = 22f;
        /// <summary>每条剑痕给影刺的伤害加成。</summary>
        public float TrailBonus = 0.05f;
        /// <summary>影刺击杀几名敌人 +1 突进次数上限（策划 1）。</summary>
        public float KillsPerCharge = 1f;
        /// <summary>剑痕最多保留几条（性能）。</summary>
        public int MaxTrails = 40;
        /// <summary>影子复刻的倍速（表现用）。</summary>
        public float ShadowSpeed = 5f;
    }

    /// <summary>
    /// 副武器 · 强磁铁（11/25 第三批）：奇数次翻滚在原地留正极，偶数次留负极，两极沿直线对撞（路径穿刺 + 中点爆炸眩晕）后消失。
    /// 在主武器爆发之前结算。数值暂定。
    /// </summary>
    [Serializable]
    public sealed class MagnetConfig
    {
        public float PathDamage = 20f;
        public float PathWidth = 0.6f;
        public float BlastDamage = 60f;
        public float BlastRadius = 2.5f;
        public float StunTime = 0.8f;
        /// <summary>「磁场」词条：磁极生成时吸附这么远内的敌人；「磁流」词条：把路径两侧这么远内的敌人拉向中点（拉近距离 = 词条数值）。</summary>
        public float SpawnPullRadius = 2.5f;
        public float PathPullRadius = 1.5f;
    }

    /// <summary>
    /// 副武器 · 光标（11/25 第三批）：翻滚 = 按下 / 松开。第一次翻滚开始绘制（身后拖出选区边界），
    /// 之后每次翻滚：选区（轨迹围成的多边形 + 边界厚度）内敌人受到降维打击并定身，然后以当前站位重新开始绘制。数值暂定。
    /// </summary>
    [Serializable]
    public sealed class CursorConfig
    {
        public float Damage = 35f;
        public float PinTime = 1.5f;
        /// <summary>轨迹最长多少单位（选区的最大覆盖范围），超过后丢掉最早的点。</summary>
        public float MaxLength = 30f;
        /// <summary>边界厚度：离轨迹这么近的也算在选区里（避免漏怪）。</summary>
        public float Thickness = 0.6f;
        /// <summary>轨迹采样间距。</summary>
        public float SampleSpacing = 0.3f;
    }

    /// <summary>副武器 · 时间沙漏：常态敌人移速按 e^(−DecayRate × t) 趋近 FloorSpeed；翻滚时重置为 1。数值暂定。</summary>
    [Serializable]
    public sealed class HourglassConfig
    {
        public float DecayRate = 0.15f;
        public float SecondsToFloor = 5f;
        /// <summary>移速下限（策划 1/2）。</summary>
        public float FloorSpeed = 0.5f;
    }

    /// <summary>
    /// 副武器 · WASD：按住移动键时在对应方向生成判定区（每只敌人 HitCooldown 秒最多吃 1 次），最多 2 个方向；
    /// 翻滚时四向齐发。数值暂定。
    /// </summary>
    [Serializable]
    public sealed class WasdConfig
    {
        public float Damage = 8f;
        /// <summary>判定区中心离玩家的距离、判定区半径。</summary>
        public float Distance = 1.4f;
        public float Radius = 1.1f;
        public float HitCooldown = 3f;
        /// <summary>判定间隔（秒，性能）。</summary>
        public float CheckInterval = 0.1f;
        public float BurstDamage = 20f;
        public float BurstDistance = 1.6f;
        public float BurstRadius = 1.6f;
    }

    /// <summary>
    /// 主武器 · 闪电（11/27 定稿）。常态：发射一道闪电，在怪物之间超快速连锁跳跃，命中的怪物各叠 1 层雷痕；
    /// 同一条链里同一只怪不会被重复命中，每一跳选离上一只最近的。
    /// 翻滚：逐个引爆全场雷痕（伤害 = 每层引爆伤害 × 层数）；因引爆而死的怪物向最近的敌人再发一道规格完全相同的闪电（传染），
    /// 传染打上的雷痕在同一次引爆里接着结算（每只怪每次翻滚最多被引爆一次），结算后全场雷痕清空。数值策划书没给，以下均为暂定。
    /// </summary>
    [Serializable]
    public sealed class LightningConfig
    {
        public int Ammo = 20;
        public float FireInterval = 0.5f;
        /// <summary>闪电每命中一只怪的伤害。</summary>
        public float Damage = 6f;
        /// <summary>第一下的索敌 / 射程（单位）。</summary>
        public float Range = 8f;
        /// <summary>一条闪电最多命中几只怪（含第一下；策划「五次连锁跳跃」暂按 5 只算）。</summary>
        public int ChainCount = 5;
        /// <summary>每一跳最远能跳多远（单位）。</summary>
        public float JumpRange = 3.5f;
        /// <summary>每层雷痕的引爆伤害。</summary>
        public float DetonateDamage = 12f;
        /// <summary>传染：引爆致死的怪向这么远内最近的敌人发闪电（单位）。</summary>
        public float InfectionRange = 8f;
        /// <summary>一次翻滚最多引爆几只 / 最多传染几次（性能兜底）。</summary>
        public int MaxDetonations = 400;
        public int MaxInfections = 150;
    }

    /// <summary>五把主武器 + 三把副武器 + 共用参数。</summary>
    [Serializable]
    public sealed class ArsenalConfig
    {
        public float FixedDamageMultiplier = 1.25f;
        public WaterGunConfig WaterGun = new WaterGunConfig();
        public SpearConfig Spear = new SpearConfig();
        public BladeConfig Blade = new BladeConfig();
        public PalmConfig Palm = new PalmConfig();
        public CrossbowConfig Crossbow = new CrossbowConfig();
        public DronesConfig Drones = new DronesConfig();
        public GravityWellConfig Gravity = new GravityWellConfig();
        public SpaceAnchorConfig Anchor = new SpaceAnchorConfig();
        public PokerConfig Poker = new PokerConfig();
        public ShurikenConfig Shuriken = new ShurikenConfig();
        public HourglassConfig Hourglass = new HourglassConfig();
        public WasdConfig Wasd = new WasdConfig();
        public StaplerConfig Stapler = new StaplerConfig();
        public SwordConfig Sword = new SwordConfig();
        public MagnetConfig Magnet = new MagnetConfig();
        public CursorConfig Cursor = new CursorConfig();
        public LightningConfig Lightning = new LightningConfig();

        /// <summary>旧存档 / 旧场景里缺的分组补成默认值（JsonUtility 读不到的嵌套对象可能是 null）。</summary>
        public ArsenalConfig Normalize()
        {
            if (WaterGun == null) WaterGun = new WaterGunConfig();
            if (Spear == null) Spear = new SpearConfig();
            if (Blade == null) Blade = new BladeConfig();
            if (Palm == null || Palm.Ammo <= 0) Palm = new PalmConfig();
            if (Crossbow == null || Crossbow.Ammo <= 0) Crossbow = new CrossbowConfig();
            if (Drones == null || Drones.FireInterval <= 0f) Drones = new DronesConfig();
            if (Gravity == null || Gravity.Radius <= 0f) Gravity = new GravityWellConfig();
            if (Anchor == null || Anchor.Width <= 0f) Anchor = new SpaceAnchorConfig();
            if (Poker == null || Poker.HandSize <= 0) Poker = new PokerConfig();
            if (Shuriken == null || Shuriken.Ammo <= 0) Shuriken = new ShurikenConfig();
            if (Hourglass == null || Hourglass.FloorSpeed <= 0f) Hourglass = new HourglassConfig();
            if (Wasd == null || Wasd.Radius <= 0f) Wasd = new WasdConfig();
            if (Stapler == null || Stapler.Ammo <= 0) Stapler = new StaplerConfig();
            if (Sword == null || Sword.Ammo <= 0) Sword = new SwordConfig();
            if (Magnet == null || Magnet.PathWidth <= 0f) Magnet = new MagnetConfig();
            if (Cursor == null || Cursor.MaxLength <= 0f) Cursor = new CursorConfig();
            if (Lightning == null || Lightning.Ammo <= 0) Lightning = new LightningConfig();
            return this;
        }
        /// <summary>暴击伤害倍率（暴击词条共用）。</summary>
        public float CritMultiplier = 2f;
        /// <summary>「翻滚爆发时对周身一圈敌人造成百分比伤害」的半径（单位）。</summary>
        public float BurstRingRadius = 3f;
        /// <summary>「受到伤害时给附近敌人叠印记」的搜索半径（单位）。</summary>
        public float HurtMarkRange = 8f;
    }

    // ---------------- 新策划案：五种怪物 + 波次 ----------------

    /// <summary>一种怪物的基础数值（= 它首次出现那一波的数值）。</summary>
    [Serializable]
    public sealed class HordeKindConfig
    {
        public float MaxHp;
        /// <summary>移动速度（单位/秒）。</summary>
        public float Speed;
        /// <summary>碰撞 / 弹幕 / 冲锋 / 激光伤害（按种类）。</summary>
        public float Damage;
        /// <summary>攻击间隔或接触伤害间隔（秒）。</summary>
        public float AttackInterval;
        /// <summary>蓄力时间（秒）。</summary>
        public float Windup;
        /// <summary>进入攻击的距离（冲锋范围 / 狙击射程，单位）。</summary>
        public float Range;
        public int Xp;
        /// <summary>第几波开始出现。</summary>
        public int DebutWave;
        /// <summary>刷新权重。</summary>
        public float Weight;
    }

    /// <summary>怪物与波次（新策划案）。Kinds 按 MonsterKind.Walker..Sniper 的顺序。</summary>
    [Serializable]
    public sealed class HordeConfig
    {
        public HordeKindConfig[] Kinds = Turn.Monsters.MonsterCatalog.CreateHordeDefaults();
        /// <summary>每推进 1 波：血量 +10%、伤害 +5%（从该怪物首次出现的波次算起）。</summary>
        public float HpPerWave = 0.10f;
        public float DamagePerWave = 0.05f;

        /// <summary>常规刷怪间隔（秒）。</summary>
        public float SpawnInterval = 1.5f;
        /// <summary>第 1 波每批刷几只。</summary>
        public float BaseCount = 2f;
        /// <summary>每推进 1 波，每批多刷几只（线性）。</summary>
        public float CountPerWave = 0.5f;
        public int MaxAlive = 160;
        /// <summary>从地图边缘往里缩多少刷新（单位）。</summary>
        public float EdgeInset = 0.6f;
        /// <summary>刷新点离玩家的最小距离（太近就重新随机）。</summary>
        public float MinPlayerDistance = 6f;
        public float Radius = 0.4f;

        // 弹幕孢
        /// <summary>已停用（11/26 起弹幕孢只朝玩家发一枚）；字段保留，旧的调参存档才能正常读取。</summary>
        public int SporeBullets = 8;
        public float SporeBulletSpeed = 3.75f;
        public float SporeBulletRadius = 0.2f;
        public float SporeBulletLifetime = 6f;
        // 疾冲兽
        public float ChargeSpeed = 7.5f;
        public float ChargeMaxDistance = 9f;
        // 毒腐体
        public float PoolRadius = 1.5f;
        public float PoolDuration = 5f;
        public float PoolDamage = 2f;
        public float PoolInterval = 0.5f;
        // 狙击械
        public float SniperMinRange = 6f;
        public float SniperMaxRange = 10f;
        public float LaserWidth = 0.3f;

        // 精英 / Boss（策划书没给规则，暂定：第 2 波起每波开始时刷 1 只精英；每 10 波刷 1 只 Boss）
        public int EliteFromWave = 2;
        public float EliteHp = 5f, EliteDamage = 1.5f, EliteScale = 1.5f;
        public int EliteXp = 5;
        public int BossEveryWaves = 10;
        public float BossHp = 25f, BossDamage = 2f, BossScale = 2.2f;
        public float BossHpMultiplier = 2f;
        public float NormalChaseSpeedMultiplier = .85f;
        public int BossXp = 20;
    }

    // ---------------- 新策划案：词条与宝箱 ----------------

    /// <summary>一条词条的数值：每层效果 Value（含义见 BuildCatalog 的说明），Value2 为附加参数（持续时间 / 半径等），MaxLevel 最大层数。</summary>
    [Serializable]
    public sealed class BuildDef
    {
        public int MaxLevel;
        public float Value;
        public float Value2;
    }

    [Serializable]
    public sealed class BuildConfig
    {
        /// <summary>升级 / 宝箱的候选数。</summary>
        public int OptionCount = 3;
        /// <summary>按 (int)BuildId 下标。</summary>
        public BuildDef[] Builds = Turn.Weapons.BuildCatalog.CreateDefaults();
        /// <summary>宝箱的强化选项一次给几层。</summary>
        public int ChestLevels = 1;
        /// <summary>宝箱代价选项：选择后随机获得几层强化。</summary>
        public int CurseBonusLevels = 3;
        /// <summary>每层代价：最大生命 −15 / 怪物伤害 +15% / 怪物移速 +10%。</summary>
        public float FrailMaxHp = 15f;
        public float FuryDamage = 0.15f;
        public float HasteSpeed = 0.10f;
    }

    /// <summary>两种武器形态共用的弹匣与射击节奏（全局一份，升级直接作用于它）。</summary>
    public sealed class WeaponConfig
    {
        public int MagazineSize = 6;
        /// <summary>每次翻滚补足当前武器弹药；完整六位面可各自覆盖容量。</summary>
        public int[] PlaneCapacities = { 8, 8, 8, 8, 1, 1 };
        public int AmmoPerShot = 1;
        /// <summary>开局弹药；&lt; 0 表示满弹匣。</summary>
        public int InitialAmmo = -1;
        public float FireInterval = 0.6f;
        public float[] PlaneFireIntervals = { .7f, .9f, .45f, .6f, 2f, 2f };
        public float[] PlaneTargetRanges = { 6f, 8f, 5f, 6f, 10f, 12f };
        public float[] PlaneAttackRanges = { 6f, 8f, 5f, 6f, 10f, 12f };
        /// <summary>原始与未来每次成功翻滚只能重新获得一发。</summary>
        public int SingleShotCapacity = 1;
        /// <summary>自动索敌半径。</summary>
        public float TargetRange = 8f;
        /// <summary>攻击范围：子弹最远飞行距离。</summary>
        public float AttackRange = 11f;
        /// <summary>投射物最长存活秒数；射程上限仍由 AttackRange 独立控制。</summary>
        public float ProjectileLifetime = 3f;
        /// <summary>首次切离发射位面时剩余寿命的倍率。</summary>
        public float PlaneSwitchLifetimeMultiplier = 1.6f;
    }

    /// <summary>某张地图绑定的武器形态（火山 = 火球，海洋 = 鱼叉）。</summary>
    public sealed class WeaponFormConfig
    {
        public PlaneId Map;
        public string Name = "";
        public float ProjectileSpeed = 10f;
        public float Damage = 10f;
        public float Radius = 0.2f;
        /// <summary>可击中的怪物数（≥ 1）。</summary>
        public int Pierce = 1;
        public float SplashRadius = 1.2f;
        public float BurnDps = 6f, BurnDuration = 2f;
        public float PoisonDps = 5f, PoisonDuration = 3f;
        public int PoisonMaxStacks = 3;
        public float HarpoonFalloff = .30f;
    }

    /// <summary>怪物原型；数值稳定用于跨位面一一映射，不修改 PlaneId 的序列化值。</summary>
    public enum MonsterKind
    {
        StoneGolem, Anglerfish, NullPointer, Bug, Buglet, Alarm, BlackPot,
        RockThrower, VineDemon, Boar, FutureSoldier, RepairRobot, DataWorm,
        /// <summary>新策划案：近战炮灰 · 游荡尸。</summary>
        Walker,
        /// <summary>新策划案：远程弹幕 · 弹幕孢。</summary>
        Spore,
        /// <summary>新策划案：冲锋 · 疾冲兽。</summary>
        Charger,
        /// <summary>新策划案：亡语 · 毒腐体。</summary>
        Rotter,
        /// <summary>新策划案：激光枪 · 狙击械。</summary>
        Sniper,
    }

    public static class MonsterKinds
    {
        /// <summary>新策划案的五种怪物（HordeConfig.Kinds 的下标 = Kind − Walker）。</summary>
        public const MonsterKind First = MonsterKind.Walker;
        public const int HordeCount = 5;

        public static int HordeIndex(MonsterKind kind) { return (int)kind - (int)First; }

        public static string DisplayName(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Walker: return "游荡尸";
                case MonsterKind.Spore: return "弹幕孢";
                case MonsterKind.Charger: return "疾冲兽";
                case MonsterKind.Rotter: return "毒腐体";
                case MonsterKind.Sniper: return "狙击械";
                default: return kind.ToString();
            }
        }
    }

    /// <summary>纯数据怪物原型；MonsterSystem 只读，Inspector/数值面板在开局写入配置。</summary>
    [Serializable]
    public sealed class MonsterArchetype
    {
        public float MaxHp;
        public float Speed;
        public float Damage;
        public float AttackInterval;
        public float Windup;
        public float Range;
        public int Xp;
    }

    public sealed class MonsterConfig
    {
        public float MaxHp = 30f;
        public float Speed = 2f;
        /// <summary>火山石头人的最大生命（Inspector：MonsterPrefab 的 MonsterSettings.volcanoMaxHp；F1 面板可调）。</summary>
        public float VolcanoMaxHp = 60f;
        /// <summary>海洋安康鱼的最大生命（Inspector：MonsterPrefab 的 MonsterSettings.oceanMaxHp；F1 面板可调）。</summary>
        public float OceanMaxHp = 45f;

        public float ContactDamage = 8f;
        public float ContactInterval = 0.6f;
        public float Radius = 0.4f;
        /// <summary>切到火山地图时，站在岩浆范围内的怪物受到的一次性伤害（之后岩浆消失）。</summary>
        public float LavaHitDamage = 20f;
        public int Xp = 1;
        /// <summary>各形态数值：MonsterCatalog.CreateDefaults 按设计表建立，改动需通过 Inspector/面板透传。</summary>
        public MonsterArchetype[] Archetypes = Turn.Monsters.MonsterCatalog.CreateDefaults();
        public float SplitInterval = 3f;
        public int BugletLimitPerParent = 6;
        public float BubbleSpeed = 3f;
        public float EnemyProjectileLifetime = 3f;
        public float EnemyProjectileRadius = .22f;
        public float StoneRecovery = .5f;
        public float ChargeSpeed = 5.5f;
        public float ChargeDuration = .8f;
        public float RobotChargeSpeed = 4.8f;
        public float RobotKnockbackDistance = 1.5f;
        public float BoarChargeSpeed = 5f;
        public float BoarStunDuration = 2f;
        public float DataWormExplosionRadius = 1f;
        public float LaserDuration = 1f;
        public float LaserWarning = .4f;
        public float LaserWidth = .18f;
        public float CodeShotInterval = .18f;
        public float RockWarning = .3f;
        public float RockImpactRadius = .7f;
        public float AlarmAuraRadius = 2f;
        public int CodeBurstShots = 3;
    }

    public sealed class SpawnConfig
    {
        public int InitialCount = 6;
        public float Interval = 2.5f;
        public int CountPerSpawn = 2;
        /// <summary>每隔多少秒每批多刷 RampAmount 只。</summary>
        public float RampInterval = 30f;
        public int RampAmount = 1;
        /// <summary>常规刷怪的存活上限（翻滚惩罚不受此限制）。</summary>
        public int MaxAlive = 120;
        public float MinDistance = 7f;
        public float MaxDistance = 11f;
        /// <summary>骨甲野猪/数据蠕虫 P2 进入常规刷新池的时间。</summary>
        public float P2StartSeconds = 90f;
        /// <summary>每张位面的怪物候选权重；负值表示禁用，0 表示采用默认等权。</summary>
        public float[] KindWeights = new float[13];
    }

    public sealed class PenaltyConfig
    {
        /// <summary>每次成功翻滚固定新增的怪物数量。</summary>
        public int Count = 3;
        public float MinDistance = 4f;
        public float MaxDistance = 8f;
        public PenaltyArea Area = PenaltyArea.AroundPlayer;
    }

    public sealed class UpgradeConfig
    {
        public int OptionCount = 3;
        /// <summary>按 B01..B19 顺序配置的最大叠层。B10 地形增伤暂设 0（不进池）：岩浆喷发即消失，等策划定危险地形规则后再开（原值 2）。</summary>
        public int[] BuildMaxLevels = { 5, 4, 2, 3, 3, 2, 3, 2, 3, 0, 2, 3, 3, 2, 2, 1, 2, 1, 2 };
        /// <summary>词条稀有度权重：普通、稀有、史诗。</summary>
        public int CommonWeight = 70, RareWeight = 25, EpicWeight = 5;
        public float DamagePerLevel = 0.20f;
        public float FireIntervalReductionPerLevel = 0.12f;
        public float MinFireInterval = 0.2f;
        public int ExtraProjectilePerLevel = 1;
        public float MultiShotSpreadDegrees = 8f;
        public float ProjectileSpeedPerLevel = 0.40f;
        public float TargetRangePerLevel = 0.25f;
        public float SlowOnHit = 0.30f, SlowDuration = 2f;
        public float LifestealPerLevel = 0.05f;
        public float CriticalChancePerLevel = 0.15f, CriticalMultiplier = 2f;
        public float AreaRadiusPerLevel = 0.30f;
        public float HazardDamagePerLevel = 0.50f;
        public int KillAmmoRefundPerLevel = 1;
        public float ExperienceMagnetPerLevel = 0.50f;
        public float RollCooldownReductionPerLevel = 0.15f;
        public float ShockwaveKnockback = 1f;
        public float PlaneSwitchProjectileDamageBonus = 0.30f, PlaneSwitchBonusDuration = 0.5f;
        public float PenaltyReductionPerLevel = 0.40f;
        public float DoubleRollWindow = 0.8f, DoubleRollCooldown = 10f;
        public float ProjectileLifetimeBonusPerLevel = 0.60f;
        /// <summary>按 (int)UpgradeId 下标保留旧配置，仅供旧场景的序列化字段兼容。</summary>
        public readonly int[] MaxLevels = { 5, 5, 5, 5, 5 };

        public int MagazinePerLevel = 2;
        public float BurnDpsPerLevel = 4f;
        public float BurnDuration = 3f;
        public float RollCooldownPerLevel = 0.4f;
        public float RollInvinciblePerLevel = 0.15f;
        public float ShockwaveBaseRadius = 2.5f;
        public float ShockwaveRadiusPerLevel = 0.5f;
        public float ShockwaveBaseDamage = 20f;
        public float ShockwaveDamagePerLevel = 10f;
    }

    public sealed class ExperienceConfig
    {
        /// <summary>新策划案：1 级升 2 级所需经验（策划 50）。</summary>
        public int FirstLevelXp = 50;
        /// <summary>新策划案：每级所需经验递增比例（策划 20%）。</summary>
        public float XpGrowthRate = 0.2f;
        /// <summary>新策划案：经验拾取范围（单位，策划 100 像素 = 2.5）。进入后经验球飞向玩家。</summary>
        public float PickupRange = 2.5f;
        /// <summary>宝箱：玩家靠近到这个距离自动打开（单位）。</summary>
        public float ChestOpenRadius = 1f;
        /// <summary>1 级升 2 级所需经验。</summary>
        public int BaseXpToLevel = 4;
        /// <summary>每升一级所需经验每级增长 4。</summary>
        public int XpGrowthPerLevel = 4;
        public float PickupRadius = 0.5f;
        /// <summary>
        /// 生命成长（11/27）：1 级生命 = PlayerConfig.MaxHp（20），每升 1 级 +MaxHpPerLevel（4），最多到 MaxHpCap（100）。
        /// ProgressionSystem 升级时写入；LevelOneMaxHp 是旧方案字段，不再读取（1 级生命以 PlayerConfig.MaxHp 为准）。
        /// </summary>
        public float LevelOneMaxHp = 20f, MaxHpPerLevel = 4f;
        /// <summary>生命上限封顶（11/27，100）；≤ 0 = 不封顶。</summary>
        public float MaxHpCap = 100f;
        public float LevelOneMoveSpeed = 4.2f, MoveSpeedPerLevel = .08f;
        public int MaxLevel = 12;
        public float MagnetRadius = 2f;
        public float MagnetSpeed = 10f;
    }

    /// <summary>一局的全部配置（战斗部分）。世界部分在 Models.World.cs 的同名 partial 里。</summary>
    public sealed partial class TurnConfig
    {
        public WeaponConfig Weapon = new WeaponConfig();
        /// <summary>按 (int)PlaneId 下标：每个位面绑定的武器形态（没有的为 null）。</summary>
        public readonly WeaponFormConfig[] Forms = new WeaponFormConfig[PlaneIds.Count];
        public MonsterConfig Monster = new MonsterConfig();
        public SpawnConfig Spawn = new SpawnConfig();
        public PenaltyConfig Penalty = new PenaltyConfig();
        public UpgradeConfig Upgrades = new UpgradeConfig();
        public ExperienceConfig Experience = new ExperienceConfig();
        /// <summary>新策划案：三把武器。</summary>
        public ArsenalConfig Arsenal = new ArsenalConfig();
        /// <summary>新策划案：五种怪物 + 波次 + 精英 / Boss。</summary>
        public HordeConfig Horde = new HordeConfig();
        /// <summary>新策划案：词条表 + 宝箱。</summary>
        public BuildConfig Builds = new BuildConfig();
    }

    // ==================================================================
    // 运行时实体：字段公开是为了让表现层零开销地读取。
    // 每个字段只有注释里写明的那个系统可以写；表现层只读。
    // ==================================================================

    /// <summary>普通怪物：跨地图保留的实体。地图切换不会碰这里的任何字段。</summary>
    public sealed class MonsterEntity
    {
        public int Id;
        /// <summary>MonsterSystem 写。</summary>
        public Vec2 Position;
        /// <summary>MonsterSystem 写：本步的追踪速度（表现层据此决定朝向）。</summary>
        public Vec2 Velocity;
        /// <summary>CombatSystem 写伤害与治疗；MonsterSystem 仅换形态时按血量比例重标定。</summary>
        public float Hp;
        /// <summary>MonsterSystem 写：怪物原型，位面转换时按成对表换型。</summary>
        public MonsterKind Kind;
        /// <summary>MonsterSystem 写：攻击阶段 0 空闲、1 前摇、2 冲锋/硬直。</summary>
        public int AiPhase;
        /// <summary>MonsterSystem 写：攻击或冲锋计时。</summary>
        public float AiTimer;
        /// <summary>MonsterSystem 写：冲锋方向。</summary>
        public Vec2 AttackDirection;
        /// <summary>MonsterSystem 写：投石野人预警时锁定的落点，不随玩家移动。</summary>
        public Vec2 TargetPosition;
        /// <summary>MonsterSystem 写：虫崽分裂父怪 ID，其他怪为零。</summary>
        public int ParentId;
        /// <summary>MonsterSystem 写：Bug 代码弹连射剩余数量。</summary>
        public int BurstShotsLeft;
        /// <summary>MonsterSystem 写：虫崽分裂计时。</summary>
        public float SplitTimer;
        /// <summary>MonsterSystem 写：减速剩余时间。</summary>
        public float SlowLeft;
        /// <summary>MonsterSystem 写：减速乘数，1 为正常速度。</summary>
        public float SlowFactor = 1f;
        /// <summary>MonsterSystem 写：击退/眩晕剩余秒数。</summary>
        public float StunLeft;
        /// <summary>MonsterSystem 写：经验值在生成时取怪物原型配置。</summary>
        public int XpValue;
        /// <summary>MonsterSystem 写：出生时是否被惩罚波生成。</summary>
        public PlaneId SpawnPlane;
        /// <summary>MonsterSystem 写：怪物当前行为形态；不依赖位面事件提前改变表现。</summary>
        public PlaneId Form;
        /// <summary>MonsterSystem 写：生成时赋值，切位面时按形态更新。</summary>
        public float MaxHp;
        /// <summary>MonsterSystem 写（死亡由 CombatSystem 通过 IMonsterSystem.Despawn 发起）。</summary>
        public bool Alive;
        /// <summary>是否为翻滚惩罚生成的（仅供调试显示）。</summary>
        public bool Penalty;

        /// <summary>MonsterSystem 写：接触伤害冷却。</summary>
        public float ContactTimer;
        /// <summary>MonsterSystem 写：脚下地形。</summary>
        public TerrainType Terrain;

        /// <summary>WeaponSystem 写：身上的矛印记层数（= 插着几根矛）。爆发后 / 回到常态空间时清零。</summary>
        public int SpearMarks;
        /// <summary>WeaponSystem 写：剑气印记层数。回到常态空间时清零。</summary>
        public int BladeMarks;
        /// <summary>WeaponSystem 写：掌印层数。天降掌结算后 / 回到常态空间时清零。</summary>
        public int PalmMarks;
        /// <summary>SubWeaponSystem 写（引力黑洞「失衡」/ 强磁铁「磁暴」）：本次翻滚爆发伤害的额外倍率（0 = 没有）。CombatSystem 对 Burst 来源读取。回到常态空间时清零。</summary>
        public float BurstVulnerability;
        /// <summary>WeaponSystem 写（订书机）：钉痕层数。翻滚爆发散射时清零（爆发后次级钉打出的新钉痕留到下一次爆发）。</summary>
        public int StapleMarks;
        /// <summary>WeaponSystem 写（闪电，11/27）：雷痕层数。闪电每次命中 +1 层；翻滚引爆后全场清零。</summary>
        public int LightningMarks;
        /// <summary>WeaponSystem 写（订书机）：钉合组编号（0 = 没被钉合）。同一组不会再被钉合；和钉痕一起在爆发时清零。</summary>
        public int StapleGroup;
        /// <summary>SubWeaponSystem 写（光标）：「降维打击」定身剩余秒数（表现层压扁贴地；「定身易伤」读它）。</summary>
        public float PinnedLeft;
        /// <summary>MonsterSystem 写（生成时）：0 普通 / 1 精英 / 2 Boss。精英与 Boss 死亡掉落宝箱。</summary>
        public int Rank;
        /// <summary>MonsterSystem 写（生成时）：碰撞半径（精英 / Boss 更大）。</summary>
        public float Radius = 0.4f;
        /// <summary>MonsterSystem 写（生成时）：伤害倍率（波次成长 × 精英 / Boss）。</summary>
        public float DamageScale = 1f;
        /// <summary>MonsterSystem 写：疾冲兽本次冲锋已经走过的距离。</summary>
        public float ChargeTravelled;

        /// <summary>MonsterSystem 写：灼烧（持续 Buff）剩余时间。</summary>
        public float BurnLeft;
        /// <summary>MonsterSystem 写：灼烧每秒伤害。</summary>
        public float BurnDps;
        /// <summary>MonsterSystem 写：灼烧结算计时。</summary>
        public float BurnTimer;

        public bool Burning { get { return BurnLeft > 0f && BurnDps > 0f; } }
    }

    /// <summary>敌方弹幕表现类别；命中与伤害仍由 ProjectileSystem 统一结算。</summary>
    public enum EnemyProjectileKind { Normal, Bubble, Code, Rock, Laser, Explosion }

    /// <summary>子弹。ProjectileSystem 写全部字段。</summary>
    public sealed class ProjectileEntity
    {
        public int Id;
        /// <summary>ProjectileSystem 写：发射时所属位面，切换后不改变。</summary>
        public PlaneId Form;
        /// <summary>ProjectileSystem 写：剩余存活秒数，独立于射程。</summary>
        public float LifetimeLeft;
        /// <summary>ProjectileSystem 写：B16 传送余波剩余秒数，命中时仅加成一次。</summary>
        public float AftershockLeft;
        /// <summary>ProjectileSystem 写：每枚弹幕只允许首次切走时延寿。</summary>
        public bool LifetimeExtended;
        /// <summary>ProjectileSystem 写：敌方弹幕不伤怪物，玩家弹幕不伤玩家。</summary>
        public bool Enemy;
        /// <summary>ProjectileSystem 写：敌方弹幕的视觉和命中特效类型。</summary>
        public EnemyProjectileKind EnemyKind;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius;
        public float Damage;
        public int PierceLeft;
        /// <summary>ProjectileSystem 写：已经命中的怪物数，用于鱼叉穿透后伤害衰减。</summary>
        public int HitCount;
        public float DistanceLeft;
        public bool Alive;
        public readonly List<int> HitIds = new List<int>();
        /// <summary>ProjectileSystem 写（生成时）：玩家子弹属于哪把武器（敌方弹幕忽略）。</summary>
        public WeaponKind Weapon;
        /// <summary>ProjectileSystem 写（生成时）：副武器的子弹（例：浮游炮）。≠ None 时命中 / 结束交给 ISubWeaponSystem。</summary>
        public SubWeaponKind SubOwner;
        /// <summary>WeaponSystem 写：长矛携带的矛印记层数（击杀穿透时累加被击杀者身上的矛）。</summary>
        public int CarriedMarks;
        /// <summary>WeaponSystem 写：水弹是否已经留下过水渍（穿透词条：只在首个命中处留一滩）。</summary>
        public bool MarkLeft;
        /// <summary>WeaponSystem 写（生成时）、ProjectileSystem 递减：碰到遮挡物 / 地图边缘还能反弹几次（扑克牌爱心弹）。</summary>
        public int BouncesLeft;
        /// <summary>WeaponSystem 写（生成时）：追踪转向速度（弧度 / 秒，0 = 不追踪）与索敌半径（扑克牌爱心弹）。</summary>
        public float HomingTurnRate;
        public float HomingRange;
        /// <summary>ProjectileSystem 写：当前追踪的目标与重新索敌倒计时。</summary>
        public MonsterEntity HomingTarget;
        public float HomingRetarget;
    }

    /// <summary>扑克牌的花色。大小王没有花色（None）。</summary>
    public enum CardSuit { None = 0, Heart = 1, Spade = 2, Diamond = 3, Club = 4 }

    /// <summary>一张扑克牌：Rank 1–13（J/Q/K = 11/12/13），14 = 小王，15 = 大王。</summary>
    public struct PlayingCard
    {
        public const int SmallJoker = 14;
        public const int BigJoker = 15;
        public int Rank;
        public CardSuit Suit;
        public bool IsJoker { get { return Rank >= SmallJoker; } }

        public string Label
        {
            get
            {
                if (Rank == SmallJoker) return "小王";
                if (Rank == BigJoker) return "大王";
                string suit = Suit == CardSuit.Heart ? "♥" : Suit == CardSuit.Spade ? "♠" : Suit == CardSuit.Diamond ? "♦" : "♣";
                string rank = Rank == 1 ? "A" : Rank == 11 ? "J" : Rank == 12 ? "Q" : Rank == 13 ? "K" : Rank.ToString();
                return suit + rank;
            }
        }
    }

    /// <summary>武器留在场上的东西（表现层只读）：手里剑（停住旋转 / 环绕）、梅花裂隙、剑痕（线段 Position → End）。</summary>
    public enum WeaponFieldKind { Shuriken = 0, OrbitShuriken = 1, Rift = 2, SwordTrail = 3, GroundSpear = 4 }

    public struct WeaponField
    {
        public WeaponFieldKind Kind;
        public Vec2 Position;
        /// <summary>线段类（剑痕）的终点。</summary>
        public Vec2 End;
        public float Radius;
        /// <summary>剩余时间比例 0–1（裂隙）；没有时限的为 1。</summary>
        public float Life;
    }

    /// <summary>水枪的水渍（铺垫痕迹）。永久存在，直到翻滚爆发后回到常态空间时清空。WeaponSystem 写。</summary>
    public struct WaterMark
    {
        public Vec2 Position;
        public float Radius;
    }

    /// <summary>毒腐体死亡留下的毒潭。MonsterSystem 写全部字段。</summary>
    public sealed class PoisonPool
    {
        public int Id;
        public Vec2 Position;
        public float Radius;
        public float TimeLeft;
        public float Duration;
        /// <summary>每次结算对玩家的伤害（已含波次成长）。</summary>
        public float Damage;
    }

    /// <summary>精英 / Boss 掉落的宝箱。ExperienceSystem 写全部字段。玩家靠近自动打开。</summary>
    public sealed class ChestEntity
    {
        public int Id;
        public Vec2 Position;
        /// <summary>掉落者的等级（1 精英 / 2 Boss），表现层据此区分外观。</summary>
        public int Rank;
        public bool Alive;
    }

    /// <summary>经验球。ExperienceSystem 写全部字段。</summary>
    public sealed class OrbEntity
    {
        public int Id;
        public Vec2 Position;
        public int Value;
        public bool Magnetized;
        public bool Alive;
    }

    /// <summary>升级 / 宝箱面板上的一个候选项。ProgressionSystem 生成。</summary>
    public sealed class UpgradeOption
    {
        public BuildId Build;
        public WeaponKind Weapon;
        public int FromLevel;
        public int ToLevel;
        public string Name = "";
        public string Description = "";
        /// <summary>标签：「强化」「宝箱」「代价」。</summary>
        public string Tag = "强化";
        /// <summary>宝箱代价选项：Curse ≠ None 时，选择后该代价 +1 层，并随机获得 BonusLevels 层当前武器的强化（此时 Build 无意义）。</summary>
        public ChestCurse Curse;
        public int BonusLevels;
    }
}