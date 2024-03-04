using System;
using System.Xml.Serialization;

namespace Celeste.Mod.FontCustomizer
{
    public enum GenerationStrategy
    {
        Dialog, LazyLoad, Loaded, All,
    }
    public struct FontConfig()
    {
        public FontConfig(string? fontName = null, float fontSize = 1) : this()
        {
            FontName = fontName;
            FontSize = fontSize;
        }

        public string? FontName { get; set; } = null;
        public float FontSize { get; set; } = 1;
    }
    public class FontCustomizerModuleSettings : EverestModuleSettings
    {
        GenerationStrategy _strategy;
        bool _backgroundLoad;
        internal bool check = false;
        internal System.Collections.Generic.List<FontConfig> _FontList;

        [SettingName("USSRNAME_FontCustomizer_FontGenerateStrategy")]
        [SettingSubText("USSRNAME_FontCustomizer_FontGenerateStrategy_Description")]
        public GenerationStrategy Strategy
        {
            get
            {
                return _strategy;
            }

            set
            {
                _strategy = value;
            }
        }
        [SettingIgnore]//No reason to disable it.
        internal bool BackgroundLoad
        {
            get
            {
                return true;
            }
            set
            {
            }
        }
        [SettingIgnore]
        public System.Collections.Generic.List<FontConfig> FontList
        {
            get
            {
                if (check && _FontList[^1].FontName is not null)
                {
                    throw new Exception($"{nameof(FontCustomizer)} Settings has been corrupted.");
                }
                return _FontList;
            }
            set
            {
                if (check && value[^1].FontName is not null)
                {
                    throw new Exception($"{nameof(FontCustomizer)} Settings has been corrupted.");
                }
                _FontList = value;
            }
        }

        internal string? old_ver_font_name;

        [SettingIgnore]
        [Obsolete]
        public string? FontName
        {
            get
            {
                return null;
            }
            set
            {
                old_ver_font_name = value;
            }
        }

    }
}
