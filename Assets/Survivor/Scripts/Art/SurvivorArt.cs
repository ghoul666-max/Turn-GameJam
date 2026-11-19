using System.Collections.Generic;
using UnityEngine;

namespace Survivor.Art
{
    /// <summary>
    /// 全部美术素材都在这里用代码生成（像素风，Point 过滤，16 像素 = 1 世界单位）。
    /// 外部只通过 SurvivorArt.Get(key) / GetFrames / GetFlashFrames 取图。
    /// 取图顺序：Resources/Survivor/Art/&lt;key&gt;.png（编辑器导出的同一批图，Prefab 引用的就是它们）
    /// → 找不到时现场用代码生成。换正式美术：直接替换同名 PNG。
    ///
    /// 颜色约定（配合 Bloom 阈值 0.9）：普通颜色每个通道 ≤ 220，不发光；
    /// 只有刻意要发光的像素（眼睛、宝石、刀光、闪电）用接近 255 的颜色。
    /// </summary>
    public static class SurvivorArt
    {
        public const string ResourceDir = "Survivor/Art/";
        public const float PixelsPerUnit = 16f;
        /// <summary>待机第二帧（由第一帧自动压扁 1 像素得到）。</summary>
        public const string Frame2Suffix = "_f2";
        /// <summary>受击闪白用的纯白剪影。</summary>
        public const string FlashSuffix = "_flash";

        // 角色
        public const string Hunter = "hunter";
        public const string Bat = "bat";
        public const string Zombie = "zombie";
        public const string Skeleton = "skeleton";
        public const string Ghost = "ghost";
        public const string Golem = "golem";
        public const string Reaper = "reaper";
        // 武器图标
        public const string IconWhip = "icon_whip";
        public const string IconWand = "icon_wand";
        public const string IconKnife = "icon_knife";
        public const string IconGarlic = "icon_garlic";
        public const string IconBook = "icon_book";
        public const string IconBolt = "icon_bolt";
        // 被动图标
        public const string IconSpinach = "icon_spinach";
        public const string IconTome = "icon_tome";
        public const string IconCandle = "icon_candle";
        public const string IconWings = "icon_wings";
        public const string IconMagnet = "icon_magnet";
        public const string IconHeart = "icon_heart";
        public const string IconRing = "icon_ring";
        // 掉落物（"heal" 同时是升级里“烤鸡”选项的图标，ProgressionSystem 直接用这个字符串）
        public const string GemBlue = "gem_blue";
        public const string GemGreen = "gem_green";
        public const string GemRed = "gem_red";
        public const string Heal = "heal";
        // 投射物
        public const string Orb = "fx_orb";
        public const string Knife = "fx_knife";
        public const string Whip = "fx_whip";
        public const string Aura = "fx_aura";
        public const string Book = "fx_book";
        public const string Bolt = "fx_bolt";
        // 特效
        public const string Spark = "fx_spark";
        public const string Glow = "fx_glow";
        public const string Shadow = "fx_shadow";
        public const string Ring = "fx_ring";
        public const string Plus = "fx_plus";
        // 界面
        public const string Panel = "ui_panel";
        public const string Slot = "ui_slot";
        public const string ButtonFrame = "ui_button";
        public const string Pixel = "px";
        // 场景（灰度绘制，由 WorldView 按主题染色）
        public const string Ground0 = "ground_0";
        public const string Ground1 = "ground_1";
        public const string Ground2 = "ground_2";
        public const string Canopy0 = "canopy_0";
        public const string Canopy1 = "canopy_1";

        public static readonly string[] GroundKeys = { Ground0, Ground1, Ground2 };
        public static readonly string[] CanopyKeys = { Canopy0, Canopy1 };

        private static readonly string[] CharacterKeys = { Hunter, Bat, Zombie, Skeleton, Ghost, Golem, Reaper };
        /// <summary>有两帧动画的特效。</summary>
        private static readonly string[] AnimatedFxKeys = { Whip, Aura, Bolt };

        private static readonly string[] BaseKeys =
        {
            IconWhip, IconWand, IconKnife, IconGarlic, IconBook, IconBolt,
            IconSpinach, IconTome, IconCandle, IconWings, IconMagnet, IconHeart, IconRing,
            GemBlue, GemGreen, GemRed, Heal,
            Orb, Knife, Whip, Aura, Book, Bolt,
            Spark, Glow, Shadow, Ring, Plus,
            Panel, Slot, ButtonFrame, Pixel,
            Ground0, Ground1, Ground2, Canopy0, Canopy1,
        };

        /// <summary>所有可导出的素材键（含角色第二帧、闪白剪影、特效第二帧）。</summary>
        public static string[] AllKeys
        {
            get
            {
                var keys = new List<string>();
                for (int i = 0; i < CharacterKeys.Length; i++)
                {
                    string k = CharacterKeys[i];
                    keys.Add(k);
                    keys.Add(k + Frame2Suffix);
                    keys.Add(k + FlashSuffix);
                    keys.Add(k + Frame2Suffix + FlashSuffix);
                }
                keys.AddRange(BaseKeys);
                for (int i = 0; i < AnimatedFxKeys.Length; i++) keys.Add(AnimatedFxKeys[i] + Frame2Suffix);
                return keys.ToArray();
            }
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            { 'K', new Color32(22, 18, 30, 255) },    // 描边
            { 'S', new Color32(210, 170, 135, 255) }, // 皮肤
            { 'H', new Color32(120, 78, 52, 255) },   // 头发 / 兜帽
            { 'T', new Color32(130, 90, 55, 255) },   // 木头 / 皮革
            { 'R', new Color32(180, 48, 56, 255) },   // 红
            { 'E', new Color32(105, 30, 46, 255) },   // 暗红
            { 'B', new Color32(60, 92, 170, 255) },   // 蓝
            { 'b', new Color32(38, 52, 110, 255) },   // 深蓝
            { 'G', new Color32(88, 160, 80, 255) },   // 绿
            { 'g', new Color32(52, 100, 58, 255) },   // 深绿
            { 'Z', new Color32(130, 170, 110, 255) }, // 僵尸皮肤
            { 'Y', new Color32(215, 180, 70, 255) },  // 金
            { 'O', new Color32(205, 200, 180, 255) }, // 骨 / 蒜
            { 'o', new Color32(140, 135, 125, 255) }, // 骨阴影
            { 'P', new Color32(110, 62, 150, 255) },  // 紫
            { 'p', new Color32(62, 36, 92, 255) },    // 深紫
            { 'D', new Color32(82, 84, 100, 255) },   // 深灰
            { 'L', new Color32(150, 155, 170, 255) }, // 浅灰 / 石头
            { 'F', new Color32(170, 190, 215, 255) }, // 幽灵
            // ---- 发光色（越过 Bloom 阈值） ----
            { 'W', new Color32(255, 255, 255, 255) }, // 纯白
            { 'X', new Color32(255, 70, 70, 255) },   // 红眼
            { 'Q', new Color32(120, 235, 255, 255) }, // 青光
            { 'J', new Color32(110, 255, 140, 255) }, // 绿光
        };

        private static readonly Dictionary<char, Color32> GemGreenColors = new Dictionary<char, Color32>
        {
            { 'Q', new Color32(110, 255, 140, 255) },
            { 'q', new Color32(40, 150, 70, 255) },
        };

        private static readonly Dictionary<char, Color32> GemBlueColors = new Dictionary<char, Color32>
        {
            { 'Q', new Color32(110, 190, 255, 255) },
            { 'q', new Color32(50, 100, 200, 255) },
        };

        private static readonly Dictionary<char, Color32> GemRedColors = new Dictionary<char, Color32>
        {
            { 'Q', new Color32(255, 100, 100, 255) },
            { 'q', new Color32(170, 40, 50, 255) },
        };

        private static readonly Dictionary<char, Color32> TomeColors = new Dictionary<char, Color32>
        {
            { 'B', new Color32(110, 62, 150, 255) },
            { 'Y', new Color32(205, 200, 180, 255) },
        };

        // ---------------- 角色（面朝右） ----------------

        private static readonly string[] HunterRows =
        {
            "................",
            "......KKKK......",
            ".....KHHHHK.....",
            "....KHHHHHHK....",
            "....KHSSSSHK....",
            "....KSSKSKSK....",
            "....KSSSSSSK....",
            ".....KSSSSK.....",
            "....KRRRRRRK....",
            "...KbRRbbbbbK...",
            "..KSKbbbbbbKSK..",
            "..KK.KbbbbbK.KK.",
            ".....KTTTTTK....",
            ".....KbbKbbK....",
            ".....KDDKDDK....",
            ".....KKK.KKK....",
        };

        private static readonly string[] BatRows =
        {
            "................",
            "KK....K..K....KK",
            "KPK...KKKK...KPK",
            "KPPK.KPPPPK.KPPK",
            "KPPPKPXPPXPKPPPK",
            "KPPPPPPPPPPPPPPK",
            ".KPPKPPPPPPKPPK.",
            "..KK.KPPPPK.KK..",
            "......KPPK......",
            ".......KK.......",
        };

        private static readonly string[] ZombieRows =
        {
            "................",
            ".....KKKKK......",
            "....KZZZZZK.....",
            "....KZZZZZZK....",
            "....KZKZZKZK....",
            "....KZXZZXZK....",
            ".....KZEEZK.....",
            "....KggggKKKKKK.",
            "...KggggggZZZZZK",
            "...KggggggKKKKK.",
            "...KggggggK.....",
            "....KggggK......",
            "....KDDKDDK.....",
            "....KDDKDDK.....",
            "....KKK.KKK.....",
        };

        private static readonly string[] SkeletonRows =
        {
            ".....KKKKKK.....",
            "....KOOOOOOK....",
            "...KOOOOOOOOK...",
            "...KOKKOOKKOK...",
            "...KOKXOOKXOK...",
            "...KOOOOOOOOK...",
            "....KOKOKOKOK...",
            ".....KKKKKKK....",
            "......KOOK......",
            "....KKOOOOKK....",
            "...KOKOooOKOK...",
            "...KOKKOOKKOK...",
            "......KooK......",
            ".....KOKKOK.....",
            ".....KOK.KOK....",
            ".....KK...KK....",
        };

        private static readonly string[] GhostRows =
        {
            "................",
            ".....KKKKKK.....",
            "....KFFFFFFK....",
            "...KFFFFFFFFK...",
            "...KFFKWFFKWK...",
            "...KFFKKFFKKK...",
            "...KFFFFFFFFK...",
            "..KFFFFKKFFFFK..",
            "..KFFFFFFFFFFK..",
            "..KFFFFFFFFFFK..",
            "..KFFFFFFFFFFK..",
            "..KFKFFKKFFKFK..",
            "..KK.KK..KK.KK..",
        };

        private static readonly string[] GolemRows =
        {
            "....KKKKKKKK....",
            "...KLLLLLLLLK...",
            "..KLLDLLLLDLLK..",
            "..KLLQQLLQQLLK..",
            "..KLLLLLLLLLLK..",
            "KKKLLLDDDDLLLKKK",
            "KLLKLLLLLLLLKLLK",
            "KLLKDLLLLLLDKLLK",
            "KLLKLLLLLLLLKLLK",
            "KDDKLLLDDLLLKDDK",
            ".KK.KLLLLLLK.KK.",
            "....KLLKKLLK....",
            "...KLLLKKLLLK...",
            "...KDDDKKDDDK...",
            "...KKKKK.KKKKK..",
        };

        private static readonly string[] ReaperRows =
        {
            "......KKKK......",
            ".....KppppK...KK",
            "....KppppppK.KOK",
            "...KppKKKKppKKOK",
            "...KpKXKKXKpK.OK",
            "...KpKKKKKKpK.OK",
            "...KppKKKKppK.OK",
            "..KpppppppppK.OK",
            "..KpPppppppPpKOK",
            ".KpPPppppppPPpKK",
            ".KpPpppppppppPK.",
            ".KppppppppppppK.",
            "..KppppppppppK..",
            "..KpppKppKpppK..",
            "..KppK.KK.KppK..",
            "...KK......KK...",
        };

        // ---------------- 武器 / 投射物 ----------------

        private static readonly string[] WandRows =
        {
            "......KKK.",
            ".....KQWQK",
            ".....KWWWK",
            "....KKQWQK",
            "...KTKKKK.",
            "..KTK.....",
            ".KTK......",
            "KTK.......",
            "KK........",
        };

        private static readonly string[] WhipIconRows =
        {
            "..KKKK....",
            ".KTTTTK...",
            "KTK..KTK..",
            "KTK...KTK.",
            ".KK....KTK",
            ".KHK....KK",
            ".KHK......",
            ".KHK......",
            "..K.......",
        };

        private static readonly string[] KnifeRows =
        {
            ".KK.........",
            "KTTKKKKKKKK.",
            "KTTKLLWWWWWK",
            "KTTKKKKKKKK.",
            ".KK.........",
        };

        private static readonly string[] GarlicRows =
        {
            "....KK....",
            "...KGK....",
            "..KKOKK...",
            ".KOOOOOK..",
            "KOOoOOoOK.",
            "KOOoOOoOK.",
            "KOOoOOoOK.",
            ".KOOOOOK..",
            "..KKKKK...",
        };

        private static readonly string[] BookRows =
        {
            "KKKKKKKKK.",
            "KBBBBBBBKK",
            "KBYYYYYBKO",
            "KBYBBBYBKO",
            "KBBBYBBBKO",
            "KBBBYBBBKO",
            "KBBBBBBBKO",
            "KBBBBBBBKO",
            "KBBBBBBBKO",
            "KKKKKKKKKO",
            ".KOOOOOOOK",
            "..KKKKKKKK",
        };

        private static readonly string[] BoltIconRows =
        {
            "....KKK.",
            "...KWYK.",
            "..KWYK..",
            ".KWYKKK.",
            "KWYYYYK.",
            "KKKKWYK.",
            "...KWK..",
            "..KWK...",
            "..KK....",
        };

        // ---------------- 被动 ----------------

        private static readonly string[] SpinachRows =
        {
            "......KK..",
            "....KKGGK.",
            "...KGGGGGK",
            "..KGGgGGGK",
            ".KGGgGGGK.",
            ".KGgGGGGK.",
            "KGgGGGKK..",
            "KgKKKK....",
            "KK........",
        };

        private static readonly string[] CandleRows =
        {
            "...KK...",
            "..KYWK..",
            "..KYYK..",
            "...KK...",
            "..KOOK..",
            "..KOOK..",
            "..KOOK..",
            "..KOOK..",
            ".KKKKKK.",
            "KTTTTTTK",
            ".KKKKKK.",
        };

        private static readonly string[] WingsRows =
        {
            "KK........KK",
            "KOK......KOK",
            "KOOK....KOOK",
            ".KOOKKKKOOK.",
            ".KOOOKKOOOK.",
            "..KOOK.KOOK.",
            "...KK...KK..",
        };

        private static readonly string[] MagnetRows =
        {
            ".KKK..KKK.",
            "KLLK..KLLK",
            "KRRK..KRRK",
            "KRRK..KRRK",
            "KRRKKKKRRK",
            "KRRRRRRRRK",
            ".KRRRRRRK.",
            "..KKKKKK..",
        };

        private static readonly string[] HeartRows =
        {
            ".KK...KK.",
            "KRRK.KRRK",
            "KRWRKRRRK",
            "KRRRRRRRK",
            ".KRRRRRK.",
            "..KRRRK..",
            "...KRK...",
            "....K....",
        };

        private static readonly string[] RingRows =
        {
            "..KKKKK...",
            ".KYYYYYK..",
            "KYK...KYK.",
            "KYK...KYK.",
            "KYK...KYK.",
            ".KYYYYYK..",
            "..KKKKK...",
        };

        // ---------------- 掉落物 ----------------

        private static readonly string[] GemRows =
        {
            "...K...",
            "..KWK..",
            ".KWQQK.",
            "KWQQQqK",
            "KQQQQqK",
            ".KQQqK.",
            "..KqK..",
            "...K...",
        };

        private static readonly string[] HealRows =
        {
            "....KKKK....",
            "..KKTTTTKK..",
            ".KTTYTTTTTK.",
            "KTTTTTTTTTKK",
            "KTTTTTTTTKOK",
            ".KTTTTTTK.KO",
            "..KKKKKK...K",
        };

        private static readonly string[] PlusRows =
        {
            "..KKK..",
            "..KJK..",
            "KKKJKKK",
            "KJJJJJK",
            "KKKJKKK",
            "..KJK..",
            "..KKK..",
        };

        // 4x4 Bayer 有序抖动矩阵：让渐变保持像素感。
        private static readonly int[] Bayer =
        {
            0, 8, 2, 10,
            12, 4, 14, 6,
            3, 11, 1, 9,
            15, 7, 13, 5,
        };

        // ---------------- 对外接口 ----------------

        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "missing";

            Sprite sprite;
            if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;

            sprite = Resources.Load<Sprite>(ResourceDir + key);
            if (sprite == null) sprite = Generate(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>角色与部分特效有两帧；其余只返回一帧。</summary>
        public static Sprite[] GetFrames(string key)
        {
            if (HasSecondFrame(key)) return new Sprite[] { Get(key), Get(key + Frame2Suffix) };
            return new Sprite[] { Get(key) };
        }

        /// <summary>与 GetFrames 一一对应的纯白剪影（只有角色有）。</summary>
        public static Sprite[] GetFlashFrames(string key)
        {
            if (!IsCharacter(key)) return new Sprite[] { Get(key) };
            return new Sprite[] { Get(key + FlashSuffix), Get(key + Frame2Suffix + FlashSuffix) };
        }

        /// <summary>无视缓存与 PNG，直接用代码生成一张新图（编辑器导出 PNG 时使用）。</summary>
        public static Sprite Generate(string key)
        {
            Sprite sprite = Build(key);
            sprite.name = key;
            sprite.texture.name = key;
            return sprite;
        }

        /// <summary>手动登记一张图（编辑器生成 Prefab 时用它保证引用的是磁盘上的资产）。</summary>
        public static void Register(string key, Sprite sprite)
        {
            if (!string.IsNullOrEmpty(key) && sprite != null) Cache[key] = sprite;
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static bool IsCharacter(string key)
        {
            return System.Array.IndexOf(CharacterKeys, key) >= 0;
        }

        public static bool HasSecondFrame(string key)
        {
            return IsCharacter(key) || System.Array.IndexOf(AnimatedFxKeys, key) >= 0;
        }

        // ---------------- 分发 ----------------

        private static Sprite Build(string key)
        {
            if (key == null) key = "";

            // 闪白剪影：<角色>[_f2]_flash
            if (key.EndsWith(FlashSuffix))
            {
                string[] rows = CharacterAscii(key.Substring(0, key.Length - FlashSuffix.Length));
                if (rows != null) return FromAscii(rows, null, true);
            }

            string[] character = CharacterAscii(key);
            if (character != null) return FromAscii(character, null, false);

            switch (key)
            {
                case IconWhip: return FromAscii(WhipIconRows, null, false);
                case IconWand: return FromAscii(WandRows, null, false);
                case IconKnife: return FromAscii(KnifeRows, null, false);
                case IconGarlic: return FromAscii(GarlicRows, null, false);
                case IconBook: return FromAscii(BookRows, null, false);
                case IconBolt: return FromAscii(BoltIconRows, null, false);
                case IconSpinach: return FromAscii(SpinachRows, null, false);
                case IconTome: return FromAscii(BookRows, TomeColors, false);
                case IconCandle: return FromAscii(CandleRows, null, false);
                case IconWings: return FromAscii(WingsRows, null, false);
                case IconMagnet: return FromAscii(MagnetRows, null, false);
                case IconHeart: return FromAscii(HeartRows, null, false);
                case IconRing: return FromAscii(RingRows, null, false);
                case GemBlue: return FromAscii(GemRows, GemBlueColors, false);
                case GemGreen: return FromAscii(GemRows, GemGreenColors, false);
                case GemRed: return FromAscii(GemRows, GemRedColors, false);
                case Heal: return FromAscii(HealRows, null, false);
                case Orb: return OrbSprite(16);
                case Knife: return FromAscii(KnifeRows, null, false);
                case Whip: return WhipSprite(40, 14, false);
                case Whip + Frame2Suffix: return WhipSprite(40, 14, true);
                case Aura: return AuraSprite(48, 0);
                case Aura + Frame2Suffix: return AuraSprite(48, 1);
                case Book: return FromAscii(BookRows, null, false);
                case Bolt: return BoltSprite(12, 48, 11);
                case Bolt + Frame2Suffix: return BoltSprite(12, 48, 29);
                case Spark: return SparkSprite(11);
                case Glow: return GlowSprite(32);
                case Shadow: return ShadowSprite(16, 6);
                case Ring: return RingSprite(32);
                case Plus: return FromAscii(PlusRows, null, false);
                case Panel: return Frame(16, 16, new Color32(30, 26, 40, 255), new Color32(110, 100, 140, 255), 1);
                case Slot: return Frame(12, 12, new Color32(24, 22, 32, 255), new Color32(90, 84, 110, 255), 1);
                case ButtonFrame: return Frame(24, 12, new Color32(70, 110, 80, 255), new Color32(170, 220, 170, 255), 1);
                case Pixel: return Solid(4, 4, White);
                case Ground0: return GroundSprite(64, 0);
                case Ground1: return GroundSprite(64, 1);
                case Ground2: return GroundSprite(64, 2);
                case Canopy0: return CanopySprite(48, 0);
                case Canopy1: return CanopySprite(48, 1);
                default: return Frame(8, 8, new Color32(255, 0, 255, 255), new Color32(0, 0, 0, 255), 1); // 缺图占位
            }
        }

        /// <summary>角色字符画；带 _f2 后缀时返回压扁 1 像素的第二帧。不是角色返回 null。</summary>
        private static string[] CharacterAscii(string key)
        {
            if (key.EndsWith(Frame2Suffix))
            {
                string[] baseRows = CharacterRows(key.Substring(0, key.Length - Frame2Suffix.Length));
                return baseRows != null ? Squash(baseRows) : null;
            }
            return CharacterRows(key);
        }

        private static string[] CharacterRows(string key)
        {
            switch (key)
            {
                case Hunter: return HunterRows;
                case Bat: return BatRows;
                case Zombie: return ZombieRows;
                case Skeleton: return SkeletonRows;
                case Ghost: return GhostRows;
                case Golem: return GolemRows;
                case Reaper: return ReaperRows;
                default: return null;
            }
        }

        // ---------------- 字符画工具 ----------------

        private static Sprite FromAscii(string[] rows, Dictionary<char, Color32> overrides, bool silhouette)
        {
            int h = rows.Length;
            int w = 0;
            for (int i = 0; i < h; i++) w = Mathf.Max(w, rows[i].Length);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = rows[y];
                for (int x = 0; x < w; x++)
                {
                    Color32 color = Clear;
                    if (x < row.Length)
                    {
                        char c = row[x];
                        Color32 found;
                        if (overrides != null && overrides.TryGetValue(c, out found)) color = found;
                        else if (Palette.TryGetValue(c, out found)) color = found;
                    }
                    if (silhouette && color.a > 0) color = White;
                    pixels[(h - 1 - y) * w + x] = color; // 贴图坐标原点在左下
                }
            }
            return MakeSprite(pixels, w, h);
        }

        /// <summary>第二帧：去掉中间一行、顶部补一行空白 —— 身体下沉 1 像素，形成“呼吸”。</summary>
        private static string[] Squash(string[] rows)
        {
            int h = rows.Length;
            int removed = h / 2;
            var result = new string[h];
            result[0] = new string('.', rows[0].Length);
            int write = 1;
            for (int i = 0; i < h; i++)
            {
                if (i == removed) continue;
                result[write++] = rows[i];
            }
            return result;
        }

        // ---------------- 几何图形 ----------------

        private static Sprite Solid(int w, int h, Color32 color)
        {
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            return MakeSprite(pixels, w, h);
        }

        private static Sprite Frame(int w, int h, Color32 fill, Color32 border, int thickness)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                    bool edge = x < thickness || y < thickness || x >= w - thickness || y >= h - thickness;
                    pixels[y * w + x] = corner ? Clear : (edge ? border : fill);
                }
            }
            return MakeSprite(pixels, w, h);
        }

        // ---------------- 特效 ----------------

        /// <summary>魔弹：纯白芯 + 青色光晕（透明度分档抖动）。</summary>
        private static Sprite OrbSprite(int size)
        {
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size * 0.5f);
                    if (d < 0.28f)
                    {
                        pixels[y * size + x] = White;
                        continue;
                    }
                    float v = Quantize(Mathf.Clamp01(1f - d) * 1.4f, 4, x, y);
                    pixels[y * size + x] = new Color32(150, 225, 255, (byte)(v * 255f));
                }
            }
            return MakeSprite(pixels, size, size);
        }

        /// <summary>鞭痕：向上拱起的月牙，中间粗两头尖，纯白芯。thin = 第二帧（收尾变细）。</summary>
        private static Sprite WhipSprite(int w, int h, bool thin)
        {
            var pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                float arch = 1f - (2f * t - 1f) * (2f * t - 1f);
                float yc = 3f + (h - 7) * arch;
                float half = (thin ? 1.1f : 2.6f) * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.7f);
                for (int y = 0; y < h; y++)
                {
                    float d = Mathf.Abs(y - yc);
                    if (d > half) continue;
                    bool core = d <= half - 1f;
                    pixels[y * w + x] = core ? White : new Color32(255, 220, 150, thin ? (byte)160 : (byte)255);
                }
            }
            return MakeSprite(pixels, w, h);
        }

        /// <summary>大蒜光环：外圈实线 + 内部稀疏抖动点；phase 让两帧的抖动点错开。</summary>
        private static Sprite AuraSprite(int size, int phase)
        {
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color32 color = Clear;
                    if (d <= r && d > r - 1.6f)
                    {
                        color = new Color32(225, 240, 220, 190);
                    }
                    else if (d <= r - 1.6f)
                    {
                        float v = d / r; // 越靠外越密
                        int bx = (x + phase * 2) & 3;
                        int by = (y + phase) & 3;
                        float threshold = (Bayer[by * 4 + bx] + 0.5f) / 16f;
                        if (v * v * 0.55f > threshold) color = new Color32(210, 230, 200, 70);
                    }
                    pixels[y * size + x] = color;
                }
            }
            return MakeSprite(pixels, size, size);
        }

        /// <summary>闪电：从上往下的折线，纯白芯 + 青色边。seed 决定折线形状。</summary>
        private static Sprite BoltSprite(int w, int h, int seed)
        {
            var pixels = new Color32[w * h];
            int x = w / 2;
            for (int y = h - 1; y >= 0; y--)
            {
                float r = Hash(y, seed, 3);
                if (r < 0.33f) x--;
                else if (r > 0.66f) x++;
                x = Mathf.Clamp(x, 2, w - 3);

                pixels[y * w + x] = White;
                pixels[y * w + x - 1] = new Color32(150, 225, 255, 255);
                pixels[y * w + x + 1] = new Color32(150, 225, 255, 255);
                if (y < 4) // 落点更粗
                {
                    pixels[y * w + x - 2] = new Color32(150, 225, 255, 200);
                    pixels[y * w + x + 2] = new Color32(150, 225, 255, 200);
                }
            }
            return MakeSprite(pixels, w, h);
        }

        private static Sprite SparkSprite(int size)
        {
            var pixels = new Color32[size * size];
            int c = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Abs(x - c);
                    int dy = Mathf.Abs(y - c);
                    bool cross = (dx == 0 && dy <= c) || (dy == 0 && dx <= c);
                    bool diagonal = dx == dy && dx <= c / 2;
                    bool core = dx <= 1 && dy <= 1;
                    Color32 color = Clear;
                    if (core) color = White;
                    else if (cross || diagonal) color = new Color32(255, 240, 170, 255);
                    pixels[y * size + x] = color;
                }
            }
            return MakeSprite(pixels, size, size);
        }

        /// <summary>径向光晕：透明度分 5 档并做有序抖动。</summary>
        private static Sprite GlowSprite(int size)
        {
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float v = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    v = Quantize(v * v, 5, x, y);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(v * 255f));
                }
            }
            return MakeSprite(pixels, size, size);
        }

        private static Sprite ShadowSprite(int w, int h)
        {
            var pixels = new Color32[w * h];
            float cx = (w - 1) * 0.5f;
            float cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / (w * 0.5f);
                    float dy = (y - cy) / (h * 0.5f);
                    pixels[y * w + x] = dx * dx + dy * dy <= 1f ? new Color32(0, 0, 0, 110) : Clear;
                }
            }
            return MakeSprite(pixels, w, h);
        }

        /// <summary>升级时扩散的细圆环（纯白，会发光）。</summary>
        private static Sprite RingSprite(int size)
        {
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    pixels[y * size + x] = d <= r && d > r - 1.5f ? White : Clear;
                }
            }
            return MakeSprite(pixels, size, size);
        }

        // ---------------- 场景（灰度，运行时染色） ----------------

        /// <summary>
        /// 地面：可无缝平铺的值噪声（分 4 档抖动）。
        /// variant 0 = 零星碎石；1 = 草丛；2 = 石块与裂缝。
        /// </summary>
        private static Sprite GroundSprite(int size, int variant)
        {
            // 底层噪声所有花样共用同一个种子：任意两块地拼在一起边缘都能对上（只有点缀不同）。
            var values = new float[size * size];
            const int baseSeed = 17;
            int seed = baseSeed + variant * 31;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = 0.65f * ValueNoise(x, y, 16, size / 16, baseSeed) + 0.35f * ValueNoise(x, y, 8, size / 8, baseSeed + 7);
                    values[y * size + x] = 0.55f + 0.25f * Quantize(n, 4, x, y);
                }
            }

            if (variant == 0)
            {
                for (int k = 0; k < 14; k++)
                {
                    int px = (int)(Hash(k, 1, seed) * size);
                    int py = (int)(Hash(k, 2, seed) * size);
                    Plot(values, size, px, py, 0.38f);
                    Plot(values, size, px + 1, py + 1, 0.85f);
                }
            }
            else if (variant == 1)
            {
                for (int k = 0; k < 12; k++)
                {
                    int px = (int)(Hash(k, 3, seed) * size);
                    int py = (int)(Hash(k, 4, seed) * size);
                    // 一簇 “v” 形草叶
                    Plot(values, size, px, py, 0.95f);
                    Plot(values, size, px - 1, py + 1, 0.95f);
                    Plot(values, size, px + 1, py + 1, 0.95f);
                    Plot(values, size, px - 2, py + 2, 0.9f);
                    Plot(values, size, px + 2, py + 2, 0.9f);
                    Plot(values, size, px, py - 1, 0.42f);
                }
            }
            else
            {
                for (int k = 0; k < 4; k++)
                {
                    int cx = (int)(Hash(k, 5, seed) * size);
                    int cy = (int)(Hash(k, 6, seed) * size);
                    int r = 2 + (int)(Hash(k, 7, seed) * 2f);
                    for (int dy = -r; dy <= r; dy++)
                    {
                        for (int dx = -r - 1; dx <= r + 1; dx++)
                        {
                            float e = (dx * dx) / (float)((r + 1) * (r + 1)) + (dy * dy) / (float)(r * r);
                            if (e > 1f) continue;
                            float v = e > 0.6f ? 0.35f : (dx < 0 && dy > 0 ? 0.9f : 0.7f);
                            Plot(values, size, cx + dx, cy + dy, v);
                        }
                    }
                }
                for (int k = 0; k < 3; k++)
                {
                    int x = (int)(Hash(k, 8, seed) * size);
                    int y = (int)(Hash(k, 9, seed) * size);
                    for (int step = 0; step < 14; step++)
                    {
                        Plot(values, size, x, y, 0.4f);
                        float r = Hash(step, k, seed + 3);
                        x += r < 0.5f ? 1 : 0;
                        y += r > 0.25f ? 1 : -1;
                    }
                }
            }

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Gray(values[i]);
            return MakeSprite(pixels, size, size);
        }

        /// <summary>树冠剪影：几团圆形叶簇叠在一起，边缘做抖动。</summary>
        private static Sprite CanopySprite(int size, int variant)
        {
            int seed = 101 + variant * 17;
            const int blobs = 8;
            var bx = new float[blobs];
            var by = new float[blobs];
            var br = new float[blobs];
            float c = size * 0.5f;
            for (int i = 0; i < blobs; i++)
            {
                float a = Hash(i, 1, seed) * Mathf.PI * 2f;
                float d = Hash(i, 2, seed) * size * 0.26f;
                bx[i] = c + Mathf.Cos(a) * d;
                by[i] = c + Mathf.Sin(a) * d;
                br[i] = size * (0.12f + 0.1f * Hash(i, 3, seed));
            }

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float field = 0f;
                    for (int i = 0; i < blobs; i++)
                    {
                        float dx = x - bx[i];
                        float dy = y - by[i];
                        float f = 1f - Mathf.Sqrt(dx * dx + dy * dy) / br[i];
                        if (f > field) field = f;
                    }
                    if (field <= 0f) continue;
                    float threshold = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                    if (field < 0.2f && field / 0.2f < threshold) continue;
                    float v = 0.82f + 0.18f * Hash(x / 3, y / 3, seed);
                    Color32 g = Gray(v);
                    pixels[y * size + x] = g;
                }
            }
            return MakeSprite(pixels, size, size);
        }

        // ---------------- 基础 ----------------

        private static void Plot(float[] values, int size, int x, int y, float v)
        {
            x = ((x % size) + size) % size; // 环绕，保持可平铺
            y = ((y % size) + size) % size;
            values[y * size + x] = v;
        }

        /// <summary>可平铺的值噪声：cell 像素一个格点，period 个格点后重复。</summary>
        private static float ValueNoise(int x, int y, int cell, int period, int seed)
        {
            float gx = x / (float)cell;
            float gy = y / (float)cell;
            int x0 = Mathf.FloorToInt(gx);
            int y0 = Mathf.FloorToInt(gy);
            float fx = gx - x0;
            float fy = gy - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            float d = Hash(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Wrap(int v, int period)
        {
            return ((v % period) + period) % period;
        }

        /// <summary>确定性的整数哈希 → [0, 1)。同样的输入永远得到同样的图。</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Quantize(float value, int levels, int x, int y)
        {
            float threshold = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
            float scaled = Mathf.Clamp01(value) * levels;
            float floor = Mathf.Floor(scaled);
            if (scaled - floor > threshold) floor += 1f;
            return Mathf.Clamp01(floor / levels);
        }

        private static Color32 Gray(float v)
        {
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            return new Color32(b, b, b, 255);
        }

        private static Sprite MakeSprite(Color32[] pixels, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
    }
}
