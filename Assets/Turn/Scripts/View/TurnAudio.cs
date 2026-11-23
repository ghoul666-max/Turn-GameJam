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
    /// </summary>
    public sealed class TurnAudio : MonoBehaviour
    {
        private const int VoiceCount = 10;

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

        [Header("翻滚 / 玩家")]
        [SerializeField] private SoundCue roll = new SoundCue();
        [SerializeField] private SoundCue rollRejected = new SoundCue(0.2f);
        [SerializeField] private SoundCue playerHurt = new SoundCue(0.15f);
        [SerializeField] private SoundCue damageBlocked = new SoundCue(0.15f);
        [SerializeField] private SoundCue playerDeath = new SoundCue();

        [Header("战斗")]
        [SerializeField] private SoundCue shot = new SoundCue(0.05f);
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

        private ITurnContext _ctx;
        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _drowsy;
        private bool _musicOnA = true;
        private float _musicFade = 1f;
        private bool _titleMode; // 标题页期间：开局 / 切位面事件不换曲，等 PlayPlaneMusic

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

        private void OnRunStarted(RunStartedEvent evt) { PlayMusicFor(evt.Plane); }
        private void OnPlaneSwitched(PlaneSwitchedEvent evt) { PlayMusicFor(evt.To); }
        private void OnRoll(RollPerformedEvent evt) { Play(roll); }
        private void OnRollRejected(RollRejectedEvent evt) { Play(rollRejected); }
        private void OnPlayerDamaged(PlayerDamagedEvent evt) { Play(playerHurt); }
        private void OnDamageBlocked(DamageBlockedEvent evt) { Play(damageBlocked); }
        private void OnPlayerDied(PlayerDiedEvent evt) { Play(playerDeath); }
        private void OnShot(ShotFiredEvent evt) { Play(shot); }
        private void OnAmmoEmpty(AmmoEmptiedEvent evt) { Play(ammoEmpty); }
        private void OnMonsterDied(MonsterDiedEvent evt) { Play(monsterDeath); }
        private void OnOrb(OrbCollectedEvent evt) { Play(orbPickup); }
        private void OnLevelUp(LeveledUpEvent evt) { Play(levelUp); }
        private void OnLava(LavaEruptedEvent evt) { Play(lavaEruption); }
        private void OnSharks(SharksSpawnedEvent evt) { Play(sharkAppear); }
        private void OnShockwave(ShockwaveTriggeredEvent evt) { Play(shockwave); }
        private void OnHazardWarned(HazardWarnedEvent evt) { Play(phoneWarning); }
        private void OnHazardLanded(HazardLandedEvent evt) { Play(phoneLand); }
        private void OnLevelCleared(LevelClearedEvent evt) { if (evt.Victory) Play(victory); }

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
            voice.PlayOneShot(cue.clip, cue.volume * masterVolume);
        }

        private AudioClip MusicFor(PlaneId plane)
        {
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
            float target = musicVolume * masterVolume;
            if (_musicFade < 1f)
            {
                _musicFade = musicCrossfade > 0f ? Mathf.Min(1f, _musicFade + Time.unscaledDeltaTime / musicCrossfade) : 1f;
            }
            AudioSource current = _musicOnA ? _musicA : _musicB;
            AudioSource previous = _musicOnA ? _musicB : _musicA;
            current.volume = target * _musicFade;
            previous.volume = target * (1f - _musicFade);
            if (_musicFade >= 1f && previous.isPlaying) previous.Stop();

            // 工位困意循环音
            float drowsiness = _ctx != null && _ctx.Plane.Active == PlaneId.Office ? _ctx.Environment.Drowsiness : 0f;
            if (drowsyLoop != null && drowsiness > 0.001f)
            {
                if (_drowsy.clip != drowsyLoop) _drowsy.clip = drowsyLoop;
                if (!_drowsy.isPlaying) _drowsy.Play();
                _drowsy.volume = drowsyVolume * masterVolume * drowsiness;
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
