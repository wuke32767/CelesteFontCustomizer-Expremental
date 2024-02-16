using System.Xml.Serialization;

namespace Celeste.Mod.FontCustomizer
{
    public enum GenerationStrategy
    {
        Loaded, Dialog, All, LazyLoad,
    }

    public class FontCustomizerModuleSettings : EverestModuleSettings
    {
        GenerationStrategy _strategy;
        bool _backgroundLoad;
        string? _FontName;
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
        public string? FontName
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
    }
}
