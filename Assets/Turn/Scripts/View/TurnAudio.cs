using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>一个音效槽：拖一个 AudioClip 进来就会在对应时机播放；空着就是静音。</summary>
    [Serializable]
    public sealed class SoundCue
    {
        public AudioClip clip;
        [UnityEngine.Range(0f, 1f)] public float volume = 1f;
        [Tooltip("每次播放随机偏移音高的幅度（0.05 = ±5%），避免重复感")]
        [UnityEngine.Range(0f, 0.5f)] public float pitchJitter = 0.05f;
        [Tooltip("两次播放的最短间隔（秒），防止同一帧几十只怪死亡时叠爆")]
        [Min(0f)] public float minInterval;

        [NonSerialized] public float LastPlayed = -999f;

        public SoundCue() { }

        public SoundCue(float minInterval)
        {
            this.minInterval = minInterval;
        }
    }

    /// <summary>
    /// 音效接口（程序A）。只订阅模拟层事件播放声音，不做任何玩法判断；所有槽位为空时完全静音。
    /// GameManager 会自动挂一个空的；要配音效，请在编辑器里选中 GameManager → Add Component → Turn Audio，
    /// 然后把音频拖进对应槽位（场景保存后生效）。
    ///   · 一次性音效：翻滚、冷却中按翻滚、受伤、无敌挡伤、死亡、开火、打空、怪物死亡、捡经验、升级、
    ///     岩浆喷发、鲨鱼出现、落地冲击波、来电预警、电话砸落、翻页、胜利；
    ///   · 背景音乐：每个位面一首，切位面时交叉淡入淡出；
    ///   · 工位困意：循环音（心跳 / 呼吸），音量随困意加深。
    ///   · 亚空间版（2026-11-26）：进入 / 回到常态、按武器区分的开火与爆发、怪物受击、狙击械激光、毒潭、
    ///     精英 / Boss 出场、副武器、宝箱、新一波、强化确认、失败短曲；亚空间期间背景音乐低通 + 降调；
    ///     battleMusic 不为空时所有位面都用它（单地图），场上有 Boss 时换 bossMusic。
    /// </summary>
    public sealed class TurnAudio : MonoBehaviour
    {
        private const int VoiceCount = 16;

        [Header("总音量")]
        [UnityEngine.Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [UnityEngine.Range(0f, 1f)] [SerializeField] private float musicVolume = 0.6f;
        [Tooltip("切位面时背景音乐交叉淡入淡出的秒数")]
        [Min(0f)] [SerializeField] private float musicCrossfade = 0.6f;

        [Header("背景音乐（每个位面一首，空着 = 不换）")]
        [SerializeField] private AudioClip volcanoMusic;
        [SerializeField] private AudioClip oceanMusic;
        [SerializeField] private AudioClip officeMusic;
        [SerializeField] private AudioClip dreamMusic;
        [SerializeField] private AudioClip primalMusic;
        [SerializeField] private AudioClip futureMusic;
        [Tooltip("标题页音乐：标题页显示时播放，封面翻开后换回当前位面的曲子；空着 = 标题页沿用位面曲")]
        [SerializeField] private AudioClip titleMusic;
        [Tooltip("战斗曲：不为空时所有位面都用这一首（单地图版）；空着 = 按上面的位面曲")]
        [SerializeField] private AudioClip battleMusic;
        [Tooltip("Boss 曲：场上有 Boss（Rank 2）时切换，Boss 全部死亡后切回；空着 = 不换")]
        [SerializeField] private AudioClip bossMusic;

        [Header("亚空间")]
        [SerializeField] private SoundCue subspaceEnter = new SoundCue(0.2f);
        [Tooltip("回到常态的渐强音：进入亚空间时就开始播，自动对齐让音频的结尾落在回到常态那一刻（按事件里的持续时间）")]
        [SerializeField] private AudioClip subspaceExitSwell;
        [UnityEngine.Range(0f, 1f)] [SerializeField] private float subspaceExitSwellVolume = 0.8f;
        [Tooltip("回到常态时再放的一次性音效（和上面的渐强音二选一或叠加）")]
        [SerializeField] private SoundCue subspaceExit = new SoundCue(0.2f);
        [Tooltip("亚空间里背景音乐低通的截止频率（Hz），越低越闷")]
        [UnityEngine.Range(200f, 22000f)] [SerializeField] private float subspaceMusicCutoff = 900f;
        [Tooltip("亚空间里背景音乐的音高（1 = 不变；会同时变慢）")]
        [UnityEngine.Range(0.5f, 1f)] [SerializeField] private float subspaceMusicPitch = 0.88f;
        [Tooltip("亚空间里背景音乐的音量倍率")]
        [UnityEngine.Range(0f, 1f)] [SerializeField] private float subspaceMusicDuck = 0.7f;
        [Tooltip("进出亚空间时背景音乐处理的过渡秒数（现实时间）")]
        [Min(0.01f)] [SerializeField] private float subspaceMusicRamp = 0.08f;

        [Header("翻滚 / 玩家")]
        [SerializeField] private SoundCue roll = new SoundCue();
        [SerializeField] private SoundCue rollRejected = new SoundCue(0.2f);
        [SerializeField] private SoundCue playerHurt = new SoundCue(0.15f);
        [SerializeField] private SoundCue damageBlocked = new SoundCue(0.15f);
        [SerializeField] private SoundCue playerDeath = new SoundCue();

        [Header("战斗")]
        [SerializeField] private SoundCue shot = new SoundCue(0.05f);
        [Tooltip("火山 · 火球专用的发射音效；空着时火球也用上面的通用开火音效")]
        [SerializeField] private SoundCue fireballShot = new SoundCue(0.05f);
        [SerializeField] private SoundCue ammoEmpty = new SoundCue(0.5f);
        [SerializeField] private SoundCue monsterDeath = new SoundCue(0.05f);
        [SerializeField] private SoundCue orbPickup = new SoundCue(0.05f);
        [SerializeField] private SoundCue levelUp = new SoundCue();

        [Header("位面 / 环境")]
        [SerializeField] private SoundCue lavaEruption = new SoundCue();
        [SerializeField] private SoundCue sharkAppear = new SoundCue();
        [SerializeField] private SoundCue shockwave = new SoundCue();
        [SerializeField] private SoundCue phoneWarning = new SoundCue();
        [SerializeField] private SoundCue phoneLand = new SoundCue();
        [Tooltip("工位困意的循环音（心跳 / 呼吸），音量 = 这里的音量 × 困意进度")]
        [SerializeField] private AudioClip drowsyLoop;
        [UnityEngine.Range(0f, 1f)] [SerializeField] private float drowsyVolume = 0.8f;

        [Header("流程")]
        [SerializeField] private SoundCue pageTurn = new SoundCue();
        [SerializeField] private SoundCue victory = new SoundCue();
        [SerializeField] private SoundCue defeat = new SoundCue();
        [Tooltip("失败短曲在玩家死亡音效之后延迟多少秒播放")]
        [Min(0f)] [SerializeField] private float defeatDelay = 0.8f;
        [SerializeField] private SoundCue waveStart = new SoundCue(1f);
        [SerializeField] private SoundCue upgradeConfirm = new SoundCue(0.1f);
        [SerializeField] private SoundCue chestOpen = new SoundCue(0.2f);

        [Header("主武器 · 常态开火（空着 = 用上面的通用开火；水枪用 fireballShot）")]
        [SerializeField] private SoundCue spearShot = new SoundCue(0.05f);
        [SerializeField] private SoundCue bladeShot = new SoundCue(0.05f);
        [SerializeField] private SoundCue palmShot = new SoundCue(0.05f);
        [SerializeField] private SoundCue crossbowShot = new SoundCue(0.05f);
        [Tooltip("扑克牌不攻击，每抽一张牌触发一次")]
        [SerializeField] private SoundCue pokerShot = new SoundCue(0.05f);
        [SerializeField] private SoundCue shurikenShot = new SoundCue(0.05f);
        [SerializeField] private SoundCue staplerShot = new SoundCue(0.05f);
        [Tooltip("剑的常态攻击就是突进")]
        [SerializeField] private SoundCue swordShot = new SoundCue(0.05f);
        [Tooltip("闪电（11/27）：空着 = 用通用开火")]
        [SerializeField] private SoundCue lightningShot = new SoundCue(0.05f);

        [Header("主武器 · 爆发（同一次爆发里多次触发只响一次，靠最短间隔）")]
        [SerializeField] private SoundCue waterBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue spearBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue bladeBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue palmBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue pokerBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue shurikenBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue staplerBurst = new SoundCue(0.5f);
        [SerializeField] private SoundCue swordBurst = new SoundCue(0.5f);
        [Tooltip("闪电（11/27）：雷痕引爆，一次翻滚里多次引爆靠最短间隔限制")]
        [SerializeField] private SoundCue lightningBurst = new SoundCue(0.25f);

        [Header("主武器 · 其它")]
        [SerializeField] private SoundCue crossbowWallSlam = new SoundCue(0.1f);
        [SerializeField] private SoundCue spadeBomb = new SoundCue(0.15f);
        [SerializeField] private SoundCue heartBlast = new SoundCue(0.06f);

        [Header("怪物")]
        [Tooltip("怪物受击：最高频的音效，音量放低、间隔短、音高随机大一点")]
        [SerializeField] private SoundCue monsterHit = new SoundCue(0.04f);
        [SerializeField] private SoundCue sniperWarn = new SoundCue(0.2f);
        [SerializeField] private SoundCue sniperFire = new SoundCue(0.1f);
        [SerializeField] private SoundCue rotterPoison = new SoundCue(0.15f);
        [SerializeField] private SoundCue eliteAppear = new SoundCue(0.5f);
        [SerializeField] private SoundCue bossAppear = new SoundCue(1f);

        [Header("副武器")]
        [SerializeField] private SoundCue gravityWell = new SoundCue(0.3f);
        [SerializeField] private SoundCue anchorSlash = new SoundCue(0.3f);
        [SerializeField] private SoundCue hourglassReset = new SoundCue(0.3f);
        [SerializeField] private SoundCue wasdKey = new SoundCue(0.08f);
        [SerializeField] private SoundCue wasdBurst = new SoundCue(0.3f);
        [SerializeField] private SoundCue cursorStrike = new SoundCue(0.3f);
        [SerializeField] private SoundCue magnetCollide = new SoundCue(0.3f);

        private ITurnContext _ctx;
        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _drowsy;
        private bool _musicOnA = true;
        private float _musicFade = 1f;
        private bool _titleMode; // 标题页期间：开局 / 切位面事件不换曲，等 PlayPlaneMusic
        private AudioSource _swell;
        private AudioLowPassFilter _lowA;
        private AudioLowPassFilter _lowB;
        private float _subspaceTarget; // 0 = 常态，1 = 亚空间
        private float _subspaceMix;
        private int _bossAlive;
        private float _defeatAt = -1f;

        // ---------------- 绑定 ----------------

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            EnsureSources();
            _ctx = ctx;
            IEventBus e = ctx.Events;
            e.Subscribe<RunStartedEvent>(OnRunStarted);
            e.Subscribe<PlaneSwitchedEvent>(OnPlaneSwitched);
            e.Subscribe<RollPerformedEvent>(OnRoll);
            e.Subscribe<RollRejectedEvent>(OnRollRejected);
            e.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            e.Subscribe<DamageBlockedEvent>(OnDamageBlocked);
            e.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            e.Subscribe<ShotFiredEvent>(OnShot);
            e.Subscribe<AmmoEmptiedEvent>(OnAmmoEmpty);
            e.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            e.Subscribe<OrbCollectedEvent>(OnOrb);
            e.Subscribe<LeveledUpEvent>(OnLevelUp);
            e.Subscribe<LavaEruptedEvent>(OnLava);
            e.Subscribe<SharksSpawnedEvent>(OnSharks);
            e.Subscribe<ShockwaveTriggeredEvent>(OnShockwave);
            e.Subscribe<HazardWarnedEvent>(OnHazardWarned);
            e.Subscribe<HazardLandedEvent>(OnHazardLanded);
            e.Subscribe<LevelClearedEvent>(OnLevelCleared);
            // 亚空间版
            e.Subscribe<SubspaceEnteredEvent>(OnSubspaceEntered);
            e.Subscribe<SubspaceExitedEvent>(OnSubspaceExited);
            e.Subscribe<WaveStartedEvent>(OnWaveStarted);
            e.Subscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
            e.Subscribe<ChestOpenedEvent>(OnChestOpened);
            e.Subscribe<FountainBurstEvent>(OnFountainBurst);
            e.Subscribe<SpearRecalledEvent>(OnSpearRecalled);
            e.Subscribe<BladeSlashedEvent>(OnBladeSlashed);
            e.Subscribe<PalmSkyFellEvent>(OnPalmSkyFell);
            e.Subscribe<CardPlayedEvent>(OnCardPlayed);
            e.Subscribe<ShurikenRecalledEvent>(OnShurikenRecalled);
            e.Subscribe<StapleScatterEvent>(OnStapleScatter);
            e.Subscribe<ShadowStrikeEvent>(OnShadowStrike);
            e.Subscribe<LightningDetonatedEvent>(OnLightningDetonated);
            e.Subscribe<WallSlammedEvent>(OnWallSlammed);
            e.Subscribe<SpadeBombEvent>(OnSpadeBomb);
            e.Subscribe<HeartBlastEvent>(OnHeartBlast);
            e.Subscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            e.Subscribe<MonsterSpawnedEvent>(OnMonsterSpawned);
            e.Subscribe<LaserAttackWarnedEvent>(OnLaserWarned);
            e.Subscribe<LaserFiredEvent>(OnLaserFired);
            e.Subscribe<PoisonPoolCreatedEvent>(OnPoisonPool);
            e.Subscribe<GravityPulledEvent>(OnGravityPulled);
            e.Subscribe<AnchorSlashedEvent>(OnAnchorSlashed);
            e.Subscribe<HourglassResetEvent>(OnHourglassReset);
            e.Subscribe<WasdStrikeEvent>(OnWasdStrike);
            e.Subscribe<CursorStrikeEvent>(OnCursorStrike);
            e.Subscribe<MagnetCollideEvent>(OnMagnetCollide);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            IEventBus e = _ctx.Events;
            e.Unsubscribe<RunStartedEvent>(OnRunStarted);
            e.Unsubscribe<PlaneSwitchedEvent>(OnPlaneSwitched);
            e.Unsubscribe<RollPerformedEvent>(OnRoll);
            e.Unsubscribe<RollRejectedEvent>(OnRollRejected);
            e.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            e.Unsubscribe<DamageBlockedEvent>(OnDamageBlocked);
            e.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            e.Unsubscribe<ShotFiredEvent>(OnShot);
            e.Unsubscribe<AmmoEmptiedEvent>(OnAmmoEmpty);
            e.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            e.Unsubscribe<OrbCollectedEvent>(OnOrb);
            e.Unsubscribe<LeveledUpEvent>(OnLevelUp);
            e.Unsubscribe<LavaEruptedEvent>(OnLava);
            e.Unsubscribe<SharksSpawnedEvent>(OnSharks);
            e.Unsubscribe<ShockwaveTriggeredEvent>(OnShockwave);
            e.Unsubscribe<HazardWarnedEvent>(OnHazardWarned);
            e.Unsubscribe<HazardLandedEvent>(OnHazardLanded);
            e.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
            e.Unsubscribe<SubspaceEnteredEvent>(OnSubspaceEntered);
            e.Unsubscribe<SubspaceExitedEvent>(OnSubspaceExited);
            e.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            e.Unsubscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
            e.Unsubscribe<ChestOpenedEvent>(OnChestOpened);
            e.Unsubscribe<FountainBurstEvent>(OnFountainBurst);
            e.Unsubscribe<SpearRecalledEvent>(OnSpearRecalled);
            e.Unsubscribe<BladeSlashedEvent>(OnBladeSlashed);
            e.Unsubscribe<PalmSkyFellEvent>(OnPalmSkyFell);
            e.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
            e.Unsubscribe<ShurikenRecalledEvent>(OnShurikenRecalled);
            e.Unsubscribe<StapleScatterEvent>(OnStapleScatter);
            e.Unsubscribe<ShadowStrikeEvent>(OnShadowStrike);
            e.Unsubscribe<LightningDetonatedEvent>(OnLightningDetonated);
            e.Unsubscribe<WallSlammedEvent>(OnWallSlammed);
            e.Unsubscribe<SpadeBombEvent>(OnSpadeBomb);
            e.Unsubscribe<HeartBlastEvent>(OnHeartBlast);
            e.Unsubscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            e.Unsubscribe<MonsterSpawnedEvent>(OnMonsterSpawned);
            e.Unsubscribe<LaserAttackWarnedEvent>(OnLaserWarned);
            e.Unsubscribe<LaserFiredEvent>(OnLaserFired);
            e.Unsubscribe<PoisonPoolCreatedEvent>(OnPoisonPool);
            e.Unsubscribe<GravityPulledEvent>(OnGravityPulled);
            e.Unsubscribe<AnchorSlashedEvent>(OnAnchorSlashed);
            e.Unsubscribe<HourglassResetEvent>(OnHourglassReset);
            e.Unsubscribe<WasdStrikeEvent>(OnWasdStrike);
            e.Unsubscribe<CursorStrikeEvent>(OnCursorStrike);
            e.Unsubscribe<MagnetCollideEvent>(OnMagnetCollide);
            _ctx = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>翻页（标题封面翻开 / 过关翻页）时由 GameManager 调用。</summary>
        public void PlayPageTurn()
        {
            Play(pageTurn);
        }

        /// <summary>显示标题页时由 GameManager 调用（在开新局之前调，避免先响一下位面曲）。titleMusic 为空时什么都不做。</summary>
        public void PlayTitleMusic()
        {
            if (titleMusic == null) return;
            _titleMode = true;
            PlayMusic(titleMusic);
        }

        /// <summary>标题封面翻开、进入关卡时由 GameManager 调用：换回当前位面的曲子。</summary>
        public void PlayPlaneMusic()
        {
            _titleMode = false;
            if (_ctx != null) PlayMusicFor(_ctx.Plane.Active);
        }

        // ---------------- 事件 → 声音 ----------------

        private void OnRunStarted(RunStartedEvent evt)
        {
            _bossAlive = 0;
            _subspaceTarget = 0f;
            _subspaceMix = 0f;
            _defeatAt = -1f;
            if (_swell != null) _swell.Stop();
            PlayMusicFor(evt.Plane);
        }
        private void OnPlaneSwitched(PlaneSwitchedEvent evt) { PlayMusicFor(evt.To); }
        private void OnRoll(RollPerformedEvent evt) { Play(roll); }
        private void OnRollRejected(RollRejectedEvent evt) { Play(rollRejected); }
        private void OnPlayerDamaged(PlayerDamagedEvent evt) { Play(playerHurt); }
        private void OnDamageBlocked(DamageBlockedEvent evt) { Play(damageBlocked); }
        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            Play(playerDeath);
            _subspaceTarget = 0f;
            if (defeat != null && defeat.clip != null) _defeatAt = Time.unscaledTime + defeatDelay;
        }
        private void OnShot(ShotFiredEvent evt)
        {
            SoundCue own = ShotFor(evt.Weapon);
            Play(own != null && own.clip != null ? own : shot);
        }
        private void OnAmmoEmpty(AmmoEmptiedEvent evt) { Play(ammoEmpty); }
        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            Play(monsterDeath);
            if (evt.Rank >= 2 && _bossAlive > 0)
            {
                _bossAlive--;
                if (_bossAlive == 0 && bossMusic != null && _ctx != null) PlayMusicFor(_ctx.Plane.Active);
            }
        }
        private void OnOrb(OrbCollectedEvent evt) { Play(orbPickup); }
        private void OnLevelUp(LeveledUpEvent evt) { Play(levelUp); }
        private void OnLava(LavaEruptedEvent evt) { Play(lavaEruption); }
        private void OnSharks(SharksSpawnedEvent evt) { Play(sharkAppear); }
        private void OnShockwave(ShockwaveTriggeredEvent evt) { Play(shockwave); }
        private void OnHazardWarned(HazardWarnedEvent evt) { Play(phoneWarning); }
        private void OnHazardLanded(HazardLandedEvent evt) { Play(phoneLand); }
        private void OnLevelCleared(LevelClearedEvent evt) { if (evt.Victory) Play(victory); }

        // ---- 亚空间版 ----

        private void OnSubspaceEntered(SubspaceEnteredEvent evt)
        {
            Play(subspaceEnter);
            _subspaceTarget = 1f;
            if (subspaceExitSwell != null && _swell != null)
            {
                // 让渐强音的结尾正好落在回到常态那一刻
                float offset = Mathf.Clamp(subspaceExitSwell.length - Mathf.Max(0f, evt.Duration), 0f, Mathf.Max(0f, subspaceExitSwell.length - 0.01f));
                _swell.Stop();
                _swell.clip = subspaceExitSwell;
                _swell.pitch = 1f;
                _swell.volume = subspaceExitSwellVolume * masterVolume * TurnVolume.SfxGain;
                _swell.time = offset;
                _swell.Play();
            }
        }

        private void OnSubspaceExited(SubspaceExitedEvent evt)
        {
            Play(subspaceExit);
            _subspaceTarget = 0f;
        }

        private void OnWaveStarted(WaveStartedEvent evt) { if (evt.Wave > 1) Play(waveStart); }
        private void OnUpgradeApplied(UpgradeAppliedEvent evt) { Play(upgradeConfirm); }
        private void OnChestOpened(ChestOpenedEvent evt) { Play(chestOpen); }

        private void OnFountainBurst(FountainBurstEvent evt) { Play(waterBurst); }
        private void OnSpearRecalled(SpearRecalledEvent evt) { Play(spearBurst); }
        private void OnBladeSlashed(BladeSlashedEvent evt) { Play(bladeBurst); }
        private void OnPalmSkyFell(PalmSkyFellEvent evt) { Play(palmBurst); }
        private void OnCardPlayed(CardPlayedEvent evt) { Play(pokerBurst); }
        private void OnShurikenRecalled(ShurikenRecalledEvent evt) { Play(shurikenBurst); }
        private void OnStapleScatter(StapleScatterEvent evt) { Play(staplerBurst); }
        private void OnShadowStrike(ShadowStrikeEvent evt) { Play(swordBurst); }
        private void OnLightningDetonated(LightningDetonatedEvent evt) { Play(lightningBurst); }
        private void OnWallSlammed(WallSlammedEvent evt) { Play(crossbowWallSlam); }
        private void OnSpadeBomb(SpadeBombEvent evt) { Play(spadeBomb); }
        private void OnHeartBlast(HeartBlastEvent evt) { Play(heartBlast); }

        private void OnMonsterDamaged(MonsterDamagedEvent evt) { Play(monsterHit); }
        private void OnMonsterSpawned(MonsterSpawnedEvent evt)
        {
            if (evt.Rank >= 2)
            {
                _bossAlive++;
                Play(bossAppear);
                if (bossMusic != null && !_titleMode) PlayMusic(bossMusic);
            }
            else if (evt.Rank == 1)
            {
                Play(eliteAppear);
            }
        }
        private void OnLaserWarned(LaserAttackWarnedEvent evt) { Play(sniperWarn); }
        private void OnLaserFired(LaserFiredEvent evt) { Play(sniperFire); }
        private void OnPoisonPool(PoisonPoolCreatedEvent evt) { Play(rotterPoison); }

        private void OnGravityPulled(GravityPulledEvent evt) { if (!evt.Secondary) Play(gravityWell); }
        private void OnAnchorSlashed(AnchorSlashedEvent evt) { Play(anchorSlash); }
        private void OnHourglassReset(HourglassResetEvent evt) { Play(hourglassReset); }
        private void OnWasdStrike(WasdStrikeEvent evt) { Play(evt.Burst ? wasdBurst : wasdKey); }
        private void OnCursorStrike(CursorStrikeEvent evt) { Play(cursorStrike); }
        private void OnMagnetCollide(MagnetCollideEvent evt) { Play(magnetCollide); }

        private SoundCue ShotFor(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.WaterGun: return fireballShot;
                case WeaponKind.Spear: return spearShot;
                case WeaponKind.Blade: return bladeShot;
                case WeaponKind.Palm: return palmShot;
                case WeaponKind.Crossbow: return crossbowShot;
                case WeaponKind.Poker: return pokerShot;
                case WeaponKind.Shuriken: return shurikenShot;
                case WeaponKind.Stapler: return staplerShot;
                case WeaponKind.Sword: return swordShot;
                case WeaponKind.Lightning: return lightningShot;
                default: return null;
            }
        }

        // ---------------- 播放 ----------------

        private void Play(SoundCue cue)
        {
            if (cue == null || cue.clip == null || _voices == null) return;
            float now = Time.unscaledTime;
            if (now - cue.LastPlayed < cue.minInterval) return;
            cue.LastPlayed = now;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.pitch = 1f + UnityEngine.Random.Range(-cue.pitchJitter, cue.pitchJitter);
            voice.PlayOneShot(cue.clip, cue.volume * masterVolume * TurnVolume.SfxGain);
        }

        /// <summary>音量设置界面拖动「音效」/「总音量」时的试听：放一个已配好的短音效（没配音效就什么都不放）。</summary>
        public void PlayPreview()
        {
            EnsureSources();
            SoundCue[] candidates = { orbPickup, levelUp, shot, pageTurn, roll };
            for (int i = 0; i < candidates.Length; i++)
            {
                SoundCue cue = candidates[i];
                if (cue == null || cue.clip == null) continue;
                float saved = cue.LastPlayed;
                cue.LastPlayed = -999f; // 试听不受最小间隔限制
                Play(cue);
                if (cue.LastPlayed < 0f) cue.LastPlayed = saved;
                return;
            }
        }

        private AudioClip MusicFor(PlaneId plane)
        {
            if (_bossAlive > 0 && bossMusic != null) return bossMusic;
            if (battleMusic != null) return battleMusic;
            switch (plane)
            {
                case PlaneId.Volcano: return volcanoMusic;
                case PlaneId.Ocean: return oceanMusic;
                case PlaneId.Office: return officeMusic;
                case PlaneId.Dream: return dreamMusic;
                case PlaneId.Primal: return primalMusic;
                case PlaneId.Future: return futureMusic;
                default: return null;
            }
        }

        private void PlayMusicFor(PlaneId plane)
        {
            if (_titleMode) return;
            PlayMusic(MusicFor(plane));
        }

        private void PlayMusic(AudioClip clip)
        {
            EnsureSources();
            if (clip == null) return;
            AudioSource current = _musicOnA ? _musicA : _musicB;
            if (current.clip == clip && current.isPlaying) return;

            AudioSource next = _musicOnA ? _musicB : _musicA;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _musicOnA = !_musicOnA;
            _musicFade = 0f;
        }

        private void LateUpdate()
        {
            if (_musicA == null) return;

            // 背景音乐交叉淡入淡出
            float target = musicVolume * masterVolume * TurnVolume.MusicGain;
            if (_musicFade < 1f)
            {
                _musicFade = musicCrossfade > 0f ? Mathf.Min(1f, _musicFade + Time.unscaledDeltaTime / musicCrossfade) : 1f;
            }
            // 亚空间：背景音乐低通 + 降调 + 压低音量（按现实时间过渡）
            float rampStep = Time.unscaledDeltaTime / Mathf.Max(0.01f, subspaceMusicRamp);
            _subspaceMix = Mathf.MoveTowards(_subspaceMix, _subspaceTarget, rampStep);
            float cutoff = 22000f * Mathf.Pow(Mathf.Max(200f, subspaceMusicCutoff) / 22000f, _subspaceMix); // 按对数插值，听感更均匀
            float pitch = Mathf.Lerp(1f, subspaceMusicPitch, _subspaceMix);
            target *= Mathf.Lerp(1f, subspaceMusicDuck, _subspaceMix);
            if (_lowA != null) _lowA.cutoffFrequency = cutoff;
            if (_lowB != null) _lowB.cutoffFrequency = cutoff;
            _musicA.pitch = pitch;
            _musicB.pitch = pitch;

            AudioSource current = _musicOnA ? _musicA : _musicB;
            AudioSource previous = _musicOnA ? _musicB : _musicA;
            current.volume = target * _musicFade;
            previous.volume = target * (1f - _musicFade);
            if (_musicFade >= 1f && previous.isPlaying) previous.Stop();

            // 失败短曲（死亡音效之后延迟播放）
            if (_defeatAt >= 0f && Time.unscaledTime >= _defeatAt)
            {
                _defeatAt = -1f;
                Play(defeat);
            }

            // 工位困意循环音
            float drowsiness = _ctx != null && _ctx.Plane.Active == PlaneId.Office ? _ctx.Environment.Drowsiness : 0f;
            if (drowsyLoop != null && drowsiness > 0.001f)
            {
                if (_drowsy.clip != drowsyLoop) _drowsy.clip = drowsyLoop;
                if (!_drowsy.isPlaying) _drowsy.Play();
                _drowsy.volume = drowsyVolume * masterVolume * TurnVolume.SfxGain * drowsiness;
            }
            else if (_drowsy.isPlaying)
            {
                _drowsy.Stop();
            }
        }

        private void EnsureSources()
        {
            if (_voices != null) return;
            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) _voices[i] = MakeSource("Sfx_" + i, false);
            _musicA = MakeSource("MusicA", true);
            _musicB = MakeSource("MusicB", true);
            _drowsy = MakeSource("DrowsyLoop", true);
            _swell = MakeSource("SubspaceSwell", false);
            _lowA = _musicA.gameObject.AddComponent<AudioLowPassFilter>();
            _lowB = _musicB.gameObject.AddComponent<AudioLowPassFilter>();
            _lowA.cutoffFrequency = 22000f;
            _lowB.cutoffFrequency = 22000f;
        }

        private AudioSource MakeSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D 声音
            return source;
        }
    }
}
