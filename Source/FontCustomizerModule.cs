using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Cil;
using MonoMod.Utils;
using SharpFont.TrueType;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste.Mod.FontCustomizer
{
    public class FontCustomizerModule : EverestModule
    {
        GlyphAtlas glyphAtlas = new();
        public static readonly string basic_path = "Assets/FontFile";
        public static FontCustomizerModule Instance { get; private set; }

        public override Type SettingsType => typeof(FontCustomizerModuleSettings);
        public static FontCustomizerModuleSettings Settings => (FontCustomizerModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(FontCustomizerModuleSession);
        public static FontCustomizerModuleSession Session => (FontCustomizerModuleSession)Instance._Session;
        public static readonly HashSet<string> FreeTypeExtension = ["ttf", "otf", "pfb", "pfm", "cid", "cff", "fon", "fnt", "pcf"];
        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot)
        {
            optionMenu.Clear();
            base.CreateModMenuSection(menu, inGame, snapshot);
            menu.Add(end ??= new());
            if (!inGame)
            {
                ExtendMenu(menu);
            }
        }

        public void ExtendMenu(TextMenu menu)
        {
            while (Settings.FontList.Count > optionMenu.Count)
            {
                ExtendMenuOnce(menu);
            }
        }
        public void RebuildFontList(int index)
        {
            bool batch = true;
            var menu = end.Container;
            (menu.BatchMode, batch) = (batch, menu.BatchMode);
            if (index == optionMenu.Count - 1)
            {
                Settings.check = false;
                Settings.FontList = [.. Settings.FontList, new()];
                Settings.check = true;
                ExtendMenuOnce(end.Container);
            }
            else
            {
                if (Settings.FontList[index].FontName == null)
                {
                    Settings.FontList.RemoveRange(index + 1, Settings.FontList.Count - index - 1);
                }
                else
                {
                    try
                    {
                        var (cfg, i) =
                            Settings.FontList
                            .Select((x, i) => (cfg: x, i))
                            .Skip(index + 1)
                            .First(x => x.cfg.FontName == Settings.FontList[index].FontName);
                        //i += index + 1;
                        Settings.FontList.RemoveAt(i);
                    }
                    catch (InvalidOperationException) { }
                }
                var (opt, siz) = optionMenu[index + 1];
                var remove = menu.IndexOf(opt);
                while (menu.Items[remove] != end)
                {
                    menu.RemoveAt(remove);
                }

                optionMenu.RemoveRange(index + 1, optionMenu.Count - index - 1);
                ExtendMenu(menu);
            }
            (menu.BatchMode, batch) = (batch, menu.BatchMode);
        }
        List<(OptionConfirmed<string?> option, SliderConfirmed size)> optionMenu = [];
        PlaceHolder end;

        private void ExtendMenuOnce(TextMenu menu)
        {
            var index = optionMenu.Count;
            OptionConfirmed<string?> selectMenu = new(Dialog.Clean("USSRNAME_FontCustomizer_FontName"));
            SliderConfirmed slide = new(Dialog.Clean("USSRNAME_FontCustomizer_FontSize"), x => (x / 100f).ToString(), 1, 300, (int)Math.Round(Settings.FontList[index].FontSize * 100f));

            //OptionConfirmed<string?> skinSelectMenufb = new(Dialog.Clean("USSRNAME_FontCustomizer_FontName_Fallback"));
            menu.Insert(menu.IndexOf(end), selectMenu);
            menu.Insert(menu.IndexOf(end), slide);
            optionMenu.Add((selectMenu, slide));

            selectMenu.Add(Dialog.Clean("USSRNAME_FontCustomizer_VanillaFont"), null, true);

            foreach (var v in foundFonts
                            .Select(x => x.PathVirtual[(basic_path.Length + 1)..])
                            .Except(optionMenu.Select(x => x.option.currentValue)))
            {
                var name = v!;
                var nameless = name[..name.LastIndexOf('.')];
                if (!Dialog.Languages.Values.Select(x => x.FontFace).Contains(nameless))
                {
                    selectMenu.Add(nameless, name, name == Settings.FontList[index].FontName);
                }
            }

            var fb = selectMenu.AddDescriptionManually(menu, Dialog.Clean("USSRNAME_FontCustomizer_VanillaFont_FallbackRequired"));
            fb.TextColor = Color.Yellow;
            selectMenu
                .Change((string? s) =>
                {
                    var index = optionMenu.IndexOf((selectMenu, slide));
                    var t = Settings.FontList[index];
                    t.FontName = s;
                    Settings.FontList[index] = t;
                    RebuildFontList(index);
                    LoadFont();
                })
                .Preview(x => fb.FadeVisible = x is not null)
                .OnLeave += () =>
                {
                    fb.FadeVisible = false;
                };

            var fb2 = slide.AddDescriptionManually(menu, Dialog.Clean("USSRNAME_FontCustomizer_OutOfSafeRange"));
            var fb3 = slide.AddDescriptionManually(menu, Dialog.Clean("USSRNAME_FontCustomizer_VanillaFontSize"));
            fb2.TextColor = Color.Yellow;
            fb3.TextColor = Color.Yellow;
            slide
                .Change((int s) =>
                {
                    var t = Settings.FontList[index];
                    t.FontSize = s / 100f;
                    Settings.FontList[index] = t;
                    LoadFont();
                })
                .Preview(x => fb2.FadeVisible = x <= 10 || x > 110)
                .OnLeave += () =>
                {
                    fb3.FadeVisible = false;
                    fb2.FadeVisible = false;
                };
            slide.OnEnter += delegate
            {
                fb3.FadeVisible = selectMenu.currentValue == null;
            };
        }


        public FontCustomizerModule()
        {
            Instance = this;
#if DEBUG
            // debug builds use verbose logging
            Logger.SetLogLevel(nameof(FontCustomizerModule), LogLevel.Verbose);
#else
            // release builds use info logging to reduce spam in log files
            Logger.SetLogLevel(nameof(FontCustomizerModule), LogLevel.Info);
#endif
            //var t2d = new Texture2D(Engine.Graphics.GraphicsDevice, 64, 64, false, SurfaceFormat.Alpha8);
            ////t2d.SetData();
            //MemoryStream ms = new();
            //ms.ReadByte();
            //t2d.SaveAsPng(ms, t2d.Width, t2d.Height);

        }
        SharpFont.Library library = new();
        (SharpFont.Face face, double baseline, double scale, double targetSize, float exscale)[] currentList;

        public List<ModAsset> foundFonts = [];
        public override void LoadSettings()
        {
            base.LoadSettings();
        }
        public override void SaveSettings()
        {
            base.SaveSettings();
        }
        public override void LoadContent(bool firstLoad)
        {
            base.LoadContent(firstLoad);
            var dirs = Everest.Content.Get(basic_path) as ModAssetBranch;
            IEnumerable<ModAsset> Flatten(ModAsset assets)
            {
                yield return assets;
                foreach (var i in assets.Children.SelectMany(Flatten))
                {
                    yield return i;
                }
            }
            if (dirs is not null)
            {
                foreach (var v in Flatten(dirs))
                {
                    if (FreeTypeExtension.Contains(v.Format)/* && !vanillaFonts.ContainsValue(k[(basic_path.Length + 1)..])*/)
                    {
                        foundFonts.Add(v);
                    }
                }
            }
            //if (Settings.FontNameList is not null && !Everest.Content.TryGet($"{basic_path}/{Settings.FontNameList}", out _))

            Settings.check = false;
            if (Settings.FontList is null)
            {
                if (string.IsNullOrEmpty(Settings.old_ver_font_name))
                {
                    Settings.FontList = [new()];
                }
                else
                {
                    Settings.FontList = [new(Settings.old_ver_font_name), new()];
                }
                Settings.old_ver_font_name = null;
            }
            else
            {
                Settings.FontList = Settings.FontList.Where(xa =>
                {
                    var x = xa.FontName;
                    if (x is not null && Everest.Content.TryGet($"{basic_path}/{x}", out _))
                    {
                        return true;
                    }
                    return false;
                }).DistinctBy(x => x.FontName).Append(Settings.FontList.LastOrDefault(new FontConfig())).ToList();
            }
            Settings.check = true;
            //Settings.FontNameList = [null];
        }
        IEnumerable<char> generate_all()
        {

            static IEnumerable<char> _generate_all(SharpFont.Face current)
            {
                var p = current.GetFirstChar(out var i);
                while (i != 0)
                {
                    yield return (char)p;
                    p = current.GetNextChar(p, out i);
                }
            }
            return currentList.Select(x => x.face).SelectMany(_generate_all).ToHashSet();
        }



        public void LoadFont()
        => LoadFont(Dialog.Language, Settings.FontList);
        //called when Language changes.
        //or font changes.
        public void LoadFont(Language lang, List<FontConfig> _font_ext)
        {
            //var  = _font_ext[0];
            var font_ext_conv = _font_ext.Select(cfg =>
            {
                var x = cfg.FontName;
                if (string.IsNullOrEmpty(x))
                {
                    x = foundFonts
                          .Select(x => x.PathVirtual[(basic_path.Length + 1)..])
                          .FirstOrDefault(x => x[..x.LastIndexOf('.')] == lang.FontFace);
                }
                if (Everest.Content.TryGet($"{basic_path}/{x}", out var md) && FreeTypeExtension.Contains(md.Format))
                {
                    return (Asset: md, Size: cfg.FontSize);
                }
                return (null, cfg.FontSize);
            }).Where(x => x.Asset is not null);


            //if (!Everest.Content.TryGet($"{basic_path}/{font_ext}", out var md) || !FreeTypeExtension.Contains(md.Format))
            //{
            //    //TODO: install vanilla font here.
            //}
            //else
            if (font_ext_conv.Any())
            {
                ThreadCancel = true;
                RenderTask?.Wait();
                ThreadCancel = false;

                currentList = font_ext_conv.Select(_font_ext =>
                {
                    var (font_ext, exs) = _font_ext;
                    SharpFont.Face current;
                    if (cachedFonts.TryGetValue(font_ext!, out var rr))
                    {
                        current = rr;
                    }
                    else
                    {
                        current = new SharpFont.Face(library, font_ext!.Data, 0);
                        cachedFonts[font_ext] = current;
                    }
                    return (current, 0.0, 0.0, 0.0, exs);
                }).ToArray();
                for (int i = 0; i < currentList.Length; i++)
                {
                    ref var current = ref currentList[i].face;
                    ref var scale = ref currentList[i].scale;
                    ref var baseline = ref currentList[i].baseline;
                    ref var targetSize = ref currentList[i].targetSize;
                    ref var extrascale = ref currentList[i].exscale;
                    current.SetPixelSizes(0, 64);

                    scale = lang.FontFaceSize / current.Size.Metrics.Height.ToDouble();
                    if (lang.FontFace == "Renogare")
                    {
                        scale *= scale;//why vanilla font smaller than mine?
                    }
                    if (current.IsScalable)
                    {
                        scale *= extrascale;
                        SharpFont.FTMatrix mat = new()
                        {
                            XX = (SharpFont.Fixed16Dot16)scale,
                            YY = (SharpFont.Fixed16Dot16)scale,
                            XY = 0,
                            YX = 0,
                        };
                        current.SetTransform(mat);
                    }
                    else
                    {
                        //
                        scale = 1;
                        current.SetPixelSizes((uint)(extrascale * lang.FontFaceSize * (lang.FontFaceSize / current.Size.Metrics.Height.ToDouble())), 0);
                    }
                    baseline = -current.Size.Metrics.Descender.ToDouble() * scale;
                    targetSize = lang.FontFaceSize;


                    //switch (Settings.Strategy)
                    //{
                    //    case GenerationStrategy.Dialog:

                    //        foreach (var c in lang.Cleaned.Values.SelectMany(x => x).Concat(englishGenerated).Distinct())
                    //        {
                    //            LockedGenerateOrFallbackAndSave(c, lang.FontFace);
                    //        }

                    //        break;
                    //    case GenerationStrategy.Loaded:
                    //        foreach (var c in fallbacks[lang.FontFace].Keys)
                    //        {
                    //            LockedGenerateOrFallbackAndSave((char)c, lang.FontFace);
                    //        }
                    //        break;
                    //    case GenerationStrategy.LazyLoad:
                    //        break;
                    //    case GenerationStrategy.All:
                    //        var p = current.GetFirstChar(out var i);
                    //        while (i != 0)
                    //        {
                    //            LockedGenerateOrFallbackAndSave((char)p, lang.FontFace);
                    //            p = current.GetNextChar(p, out i);
                    //        }
                    //        break;
                    //};

                }
                lang.Font.Sizes[0].Characters.Clear();
                NullRenderTarget[lang.FontFace].Clear();

                var tar = Settings.Strategy switch
                {
                    GenerationStrategy.Dialog => lang.Cleaned.Values.SelectMany(x => x).Concat(englishGenerated).ToHashSet(),
                    GenerationStrategy.Loaded => fallbacks[lang.FontFace].Keys.Select(x => (char)x),
                    GenerationStrategy.All => generate_all(),
                    GenerationStrategy.LazyLoad => [],

                    _ => throw new NotImplementedException(),
                };
                RenderTask = Task.Run(() =>
                {
                    RenderThread(lang.FontFace, tar);
                });
            }
        }
        //public Dictionary<string, string> vanillaFonts = new()
        //{
        //    {"schinese", "Noto Sans CJK SC Medium.otf" },
        //    {"japanese", "Noto Sans CJK JP Medium.otf"},
        //    {"koreana", "Noto Sans CJK KR Medium.otf"},
        //    {"russian", "Noto Sans Med.ttf" },
        //    {"spanish", "Renogare.otf" },
        //    {"english", "Renogare.otf" },
        //    {"french", "Renogare.otf" },
        //    {"german", "Renogare.otf" },
        //    {"italian", "Renogare.otf" },
        //};

        static XmlElement fake_elem;

        static FontCustomizerModule()
        {
            XmlDocument doc = new();
            fake_elem = doc.CreateElement("m");
            fake_elem.SetAttribute("x", "0");
            fake_elem.SetAttribute("y", "0");
        }

        public void LockededMerge()
        {
            foreach (var (fnt, dir) in RenderTarget)
            {
                var mightunload = Fonts.Get(fnt);
                if (mightunload is not null)
                {
                    foreach (var (ch, tex) in dir)
                    {
                        mightunload.Sizes[0].Characters[ch] = tex();
                        //DynamicData.For(mightunload).Get<List<VirtualTexture>>("managedTextures").Add(tex.Texture.Texture);
                    }
                }
                dir.Clear();
            }
        }
        public void LockedUpload(int ch, string fontvanilla)
        {
            lock (this)
            {
                if (!RenderTarget.TryGetValue(fontvanilla, out var dir) || !dir.TryGetValue(ch, out _))
                {
                    LockedGenerateOrFallbackAndSave(ch, fontvanilla);
                }
                LockededMerge();
            }
        }
        //no need to concurrent.
        //just lock it manually.
        //there're only two threads, after all.
        Dictionary<string, Dictionary<int, Func<PixelFontCharacter>>> RenderTarget = [];
        Dictionary<string, HashSet<int>> NullRenderTarget = [];
        Dictionary<string, Queue<(ulong, IDisposable)>> Disposer = [];
        ////Don't support more than one thread.
        //string ThreadFont;
        //char? ThreadRequest;
        //PixelFontCharacter? ThreadResult;

        Task RenderTask;
        volatile bool ThreadCancel = false;
        public void RenderThread(string vanilla, /*HashSet<char>*/IEnumerable<char> gen)
        {
            var i = make_unique;
            Queue<(ulong, IDisposable)> rng;
            lock (this)
            {
                rng = Disposer[vanilla];
            }

            foreach (var c in gen)
            {
                bool lockTaken = false;
                try
                {
                    while (!(lockTaken = System.Threading.Monitor.TryEnter(this)))
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    LockedGenerateOrFallbackAndSave(c, vanilla);
                }
                finally
                {
                    if (lockTaken)
                    {
                        System.Threading.Monitor.Exit(this);
                    }
                }
                if (ThreadCancel)
                {
                    RenderTarget[vanilla].Clear();
                    return;
                }
            }
            while (rng.Count > 0)
            {
                if (ThreadCancel)
                {
                    return;
                }
                var (l, r) = rng.Peek();
                if (l >= i)
                {
                    break;
                }
                r.Dispose();
                rng.Dequeue();
            }
            Engine.Scene.OnEndOfFrame += () =>
            {
                if (Engine.Scene is Level)
                {
                    Engine.Scene.Add(new Guardian());
                }
                else
                {
                    lock (this)
                    {
                        LockededMerge();
                    }
                }
            };
            //Stopwatch sw = new();
            //char n;
            //ThreadFont = vanilla;
            //while (!ThreadCancel && gen.Count > 0)
            //{
            //    if (ThreadRequest is not null)
            //    {
            //        sw.Start();
            //        n = ThreadRequest.Value;
            //    }
            //    else if (sw.IsRunning && sw.ElapsedMilliseconds < 18)
            //    {
            //        //request char for multiple timesin 1 frame
            //        System.Threading.Thread.Sleep(0);
            //        continue;
            //    }
            //    else
            //    {
            //        if (sw.IsRunning)
            //        {
            //            sw.Stop();
            //        }
            //        n = gen.First();
            //    }
            //    var r = GenerateOrFallbackAndSave(n, vanilla);
            //    if (ThreadRequest is not null && n == ThreadRequest)
            //    {
            //        ThreadResult = r;
            //        ThreadRequest = null;
            //    }
            //    gen.Remove(n);
            //}
        }

        public Func<PixelFontCharacter>? LockedGenerateOrFallbackAndSave(int c, string fontvanilla)
        {
            lock (this)
            {
                Func<PixelFontCharacter>? Fallback()
                {
                    if (fallbacks.TryGetValue(fontvanilla, out var res))
                    {
                        if (res.TryGetValue(c, out var chars))
                        {
                            return RenderTarget[fontvanilla][c] = () => chars;
                            //return Fonts.Get(fontvanilla).Sizes[0].Characters[c] = chars;
                        }
                    }
                    return null;
                }
                if (currentList is null)
                {
                    return Fallback();
                }
                if (Emoji.Start <= c && c <= Emoji.End)
                {
                    return Fallback();
                }
                var (curtp, alloc) = currentList
                    .Select(cr => (cr, LockededGenerateChar(c, cr.face, _make_unique: fontvanilla)))
                    .FirstOrDefault(x => x.Item2 is not null);

                var cur = curtp.face;
                var targetSize = curtp.targetSize;
                var baseline = curtp.baseline;
                if (alloc is null)
                {
                    return Fallback();
                }
                var (_tex, dispose) = alloc!.Value;
                SharpFont.GlyphSlot glyph = cur.Glyph;
                SharpFont.BBox box = glyph.GetGlyph().GetCBox(SharpFont.GlyphBBoxMode.Pixels);
                var xadv = glyph.Advance.X.ToInt32();

                var charx = () =>
                {
                    var tex = _tex();

                    fake_elem.SetAttribute("width", tex.Width.ToString());
                    fake_elem.SetAttribute("height", tex.Height.ToString());
                    fake_elem.SetAttribute("xoffset", box.Left.ToString());
                    fake_elem.SetAttribute("yoffset", ((int)(targetSize - box.Top - baseline)).ToString());
                    fake_elem.SetAttribute("xadvance", xadv.ToString());

                    var charx = new PixelFontCharacter(c, tex, fake_elem);
                    //return Fonts.Get(fontvanilla).Sizes[0].Characters[c] = charx;
                    return charx;
                };
                Disposer[fontvanilla].Enqueue((make_unique++, dispose));
                return RenderTarget[fontvanilla][c] = charx;
            }
        }
        public Dictionary<string, Dictionary<int, PixelFontCharacter>> fallbacks = [];
        static ulong make_unique = 0;
        public AllocatedMTexture? LockededGenerateChar(int c, SharpFont.Face lang, string _make_unique)
        {
            var wh = lang.GetCharIndex((uint)c);
            if (wh == 0)
            {
                return null;
            }
            lang.LoadGlyph(wh, SharpFont.LoadFlags.Render, SharpFont.LoadTarget.Normal);

            var bmp = lang.Glyph.Bitmap;
            if (bmp.Width == 0 || bmp.Rows == 0)
            {
                return null;
            }
            Color[] data = new Color[bmp.Width * bmp.Rows];
            for (int i = 0; i < bmp.Rows; i++)
            {
                for (int j = 0; j < bmp.Width; j++)
                {
                    var vv = bmp.BufferData[i * bmp.Pitch + j];
                    data[i * bmp.Width + j] = new(vv, vv, vv, vv);
                }
            }
            var alloc = glyphAtlas.Allocate(data, bmp.Width, bmp.Rows);
            bmp.Dispose();
            return alloc;
        }

        Dictionary<ModAsset, SharpFont.Face> cachedFonts = [];
        Dictionary<SharpFont.Face, Dictionary<int, MTexture>> cachedChars = [];
        public readonly IEnumerable<char> englishGenerated = //from Celeste\Content\Dialog\Fonts\configs\renogare.bmfc
            new (int f, int t)[] { (32, 126), (160, 163), (165, 180), (182, 263), (268, 275), (278, 283), (286, 287), (290, 291), (298, 299), (302, 305), (310, 311), (313, 318), (321, 321), (322, 328), (332, 333), (336, 347), (350, 357), (362, 363), (366, 371), (376, 382), (536, 539), (710, 711), (728, 733), (1460, 1460), (8211, 8212), (8216, 8218), (8220, 8222), (8224, 8226), (8230, 8230), (8240, 8240), (8249, 8250), (8260, 8260), (8364, 8364), (8482, 8482), (8800, 8800), (8804, 8805) }
            .SelectMany(x => Enumerable.Range(x.f, x.t - x.f + 1))
            .Select(x => (char)x);
        //public static Dictionary<string, string> _GetLanguage = new()
        //{
        //    { "Noto Sans CJK SC Medium" , "schinese" },
        //    {"Renogare" , "english" },//The Renogare Language is better. English/Brazilian Portuguese/French/German/Italian/Spanish all use the same fonts.
        //    {"Noto Sans CJK JP Medium" , "japanese" },
        //    {"Noto Sans CJK KR Medium" , "koreana" },
        //    {"Noto Sans Med" , "russian" },
        //};
        //public static string GetLanguage(string face)
        //{
        //    if(_GetLanguage.TryGetValue(face, out var rface))
        //    {
        //        return rface;
        //    }
        //    return face;
        //}
        static MethodInfo[] orig_draw =
            [
                .. typeof(PixelFontSize)
                .GetMethods()
                .Where(x => x.Name == "orig_Draw")
                .OrderBy(x => x.GetParameters().Length),
            ];
        static MethodInfo orig_measure = typeof(PixelFontSize).GetMethod("orig_Measure")!;
        static MonoMod.RuntimeDetour.ILHook orig_draw_s;
        static MonoMod.RuntimeDetour.ILHook orig_draw_l;
        static MonoMod.RuntimeDetour.ILHook orig_measure_h;
        public override void Unload()
        {
            ThreadCancel = true;
            RenderTask?.Wait();
            // TODO: unapply any hooks applied in Load()
            On.Celeste.Fonts.Load -= Fonts_Load;
            On.Celeste.Settings.ApplyLanguage -= Settings_ApplyLanguage;
            IL.Celeste.GameLoader.LoadThread -= GameLoader_LoadThread;
            On.Monocle.PixelFont.Get -= PixelFont_Get;
            IL.Monocle.PixelFontSize.Draw_string_Vector2_Vector2_Vector2_Color_float_Color_float_Color -= GenericForeachPatcher;
            IL.Monocle.PixelFontSize.Get -= GenericForeachPatcher;
            IL.Monocle.PixelFontSize.Measure_char -= GenericForeachPatcher;
            IL.Monocle.PixelFontSize.WidthToNextLine -= GenericForeachPatcher;
            orig_draw_s?.Dispose();
            orig_draw_l?.Dispose();
            orig_measure_h?.Dispose();

            On.Monocle.Engine.UnloadContent -= Engine_UnloadContent;
        }
        public override void Load()
        {
            // TODO: apply any hooks that should always be active
            On.Celeste.Fonts.Load += Fonts_Load;
            On.Celeste.Settings.ApplyLanguage += Settings_ApplyLanguage;
            IL.Celeste.GameLoader.LoadThread += GameLoader_LoadThread;
            On.Monocle.PixelFont.Get += PixelFont_Get;
            IL.Monocle.PixelFontSize.Draw_string_Vector2_Vector2_Vector2_Color_float_Color_float_Color += GenericForeachPatcher;
            IL.Monocle.PixelFontSize.Get += GenericForeachPatcher;
            IL.Monocle.PixelFontSize.Measure_char += GenericForeachPatcher;
            IL.Monocle.PixelFontSize.WidthToNextLine += GenericForeachPatcher;
            orig_draw_s = new(orig_draw[0], GenericForeachPatcher);
            //orig_draw_l = new(orig_draw[1], GenericForeachPatcher);
            orig_measure_h = new(orig_measure, GenericForeachPatcher);

            On.Monocle.Engine.UnloadContent += Engine_UnloadContent;
        }

        private void Engine_UnloadContent(On.Monocle.Engine.orig_UnloadContent orig, Engine self)
        {
            ThreadCancel = true;
            RenderTask?.Wait();
            orig(self);
        }

        private PixelFontSize PixelFont_Get(On.Monocle.PixelFont.orig_Get orig, PixelFont self, float size)
        {
            if (self?.Sizes?.Count == 0)
            {
                return null!;
            }
            return self!.Sizes[0];
        }


        private void GenericForeachPatcher(ILContext il)
        {
            var ic = new ILCursor(il);
            while (ic.TryGotoNext(MoveType.Before,
                i => i.MatchCallOrCallvirt(typeof(Dictionary<int, PixelFontCharacter>), "TryGetValue")))
            {
                ic.Remove();
                ic.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                ic.EmitDelegate(PatchWorker);
            }
        }
        static bool PatchWorker(Dictionary<int, PixelFontCharacter> dir, int o, out PixelFontCharacter? px, PixelFontSize self)
        {
            bool b = dir.TryGetValue(o, out px);
            if (b == false)
            {
                var lang = Dialog.Languages.Values.FirstOrDefault(x => x.Font is not null && x.FontSize == self);
                if (lang == null)
                {
                    return false;
                }
                if (Instance.NullRenderTarget[lang.FontFace].Contains(o))
                {
                    return false;
                }
                Instance.LockedUpload(o, lang.FontFace);
                if (!dir.TryGetValue(o, out px))
                {
                    //if (!fallbacks.TryGetValue(lang?.FontFace ?? "", out var dirfb) || !dirfb.TryGetValue(o, out px))
                    {
                        Instance.NullRenderTarget[lang.FontFace].Add(o);
                        return false;
                    }
                }
            }
            return true;
        }
        private void GameLoader_LoadThread(ILContext il)
        {
            var ic = new ILCursor(il);
            ic.Index = -1;
            if (ic.TryGotoPrev(MoveType.After, i => i.MatchCall(typeof(Fonts), "Load")))
            {
                static void cb()
                {
                    Instance.LoadFont();
                }
                ic.EmitDelegate(cb);
            }
        }

        private void Settings_ApplyLanguage(On.Celeste.Settings.orig_ApplyLanguage orig, Settings self)
        {
            orig(self);
            LoadFont();
        }

        private PixelFont Fonts_Load(On.Celeste.Fonts.orig_Load orig, string face)
        {
            var ret = orig(face);

            lock (this)
            {
                fallbacks[face] = new(ret.Sizes[0].Characters);
            }

            if (!RenderTarget.ContainsKey(face))
            {
                lock (this)
                {
                    RenderTarget.TryAdd(face, []);
                    NullRenderTarget.TryAdd(face, []);
                    Disposer.TryAdd(face, new());
                }
            }
            return ret;
        }
    }
}