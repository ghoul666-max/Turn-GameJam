using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 玩家的音量设置（面向玩家的「音量设置」界面写，TurnAudio 每次播放 / 每帧读）。存在 PlayerPrefs 里，下次打开游戏还在。
    /// 三个通道：总音量、音乐、音效（含工位困意循环音），另有一个静音开关；实际音量 = Inspector 里的基准音量 × 这里的设置。
    /// </summary>
    public static class TurnVolume
    {
        public enum Channel { Master = 0, Music = 1, Sfx = 2 }

        public const int ChannelCount = 3;
        public const float DefaultMaster = 0.8f;
        public const float DefaultMusic = 0.7f;
        public const float DefaultSfx = 0.8f;

        private const string KeyMaster = "Turn.Volume.Master";
        private const string KeyMusic = "Turn.Volume.Music";
        private const string KeySfx = "Turn.Volume.Sfx";
        private const string KeyMuted = "Turn.Volume.Muted";

        private static bool _loaded;
        private static float _master = DefaultMaster;
        private static float _music = DefaultMusic;
        private static float _sfx = DefaultSfx;
        private static bool _muted;

        public static string DisplayName(Channel channel)
        {
            switch (channel)
            {
                case Channel.Master: return "总音量";
                case Channel.Music: return "音乐";
                default: return "音效";
            }
        }

        public static float Get(Channel channel)
        {
            EnsureLoaded();
            switch (channel)
            {
                case Channel.Master: return _master;
                case Channel.Music: return _music;
                default: return _sfx;
            }
        }

        public static void Set(Channel channel, float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            switch (channel)
            {
                case Channel.Master: _master = value; break;
                case Channel.Music: _music = value; break;
                default: _sfx = value; break;
            }
            Save();
        }

        public static bool Muted
        {
            get { EnsureLoaded(); return _muted; }
            set
            {
                EnsureLoaded();
                _muted = value;
                Save();
            }
        }

        /// <summary>音乐的最终倍率（静音时为 0）。</summary>
        public static float MusicGain { get { EnsureLoaded(); return _muted ? 0f : _master * _music; } }

        /// <summary>音效的最终倍率（静音时为 0）。</summary>
        public static float SfxGain { get { EnsureLoaded(); return _muted ? 0f : _master * _sfx; } }

        public static void ResetDefaults()
        {
            EnsureLoaded();
            _master = DefaultMaster;
            _music = DefaultMusic;
            _sfx = DefaultSfx;
            _muted = false;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, DefaultMaster));
            _music = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, DefaultMusic));
            _sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, DefaultSfx));
            _muted = PlayerPrefs.GetInt(KeyMuted, 0) != 0;
        }

        /// <summary>写进 PlayerPrefs（内存里）；拖滑条时每帧都会调，真正写盘在 Flush（关掉音量界面时）/ 退出游戏时由 Unity 自动完成。</summary>
        private static void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, _master);
            PlayerPrefs.SetFloat(KeyMusic, _music);
            PlayerPrefs.SetFloat(KeySfx, _sfx);
            PlayerPrefs.SetInt(KeyMuted, _muted ? 1 : 0);
        }

        /// <summary>写盘（关掉音量设置界面时调用）。</summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }
    }
}
