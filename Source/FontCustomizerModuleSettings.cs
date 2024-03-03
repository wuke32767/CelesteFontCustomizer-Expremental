using System;
using System.Xml.Serialization;

namespace Celeste.Mod.FontCustomizer
{
    public enum GenerationStrategy
    {
        Dialog, LazyLoad, Loaded, All,
    }

    public class FontCustomizerModuleSettings : EverestModuleSettings
    {
        GenerationStrategy _strategy;
        bool _backgroundLoad;
        string?[] _FontName = [null];
        float[] _FontSize = [0];
        private string? _FontNameFallBack;

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
        [XmlIgnore]
        public bool BackgroundLoad
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
        public string?[] FontNameList
        {
            get
            {
                return _FontName;
            }
            set
            {
                _FontName = value;
            }
        }
        [SettingIgnore]
        public float[] FontSizeList
        {
            get
            {
                return _FontSize;
            }
            set
            {
                _FontSize = value;
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
