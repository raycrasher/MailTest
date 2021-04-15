
namespace DeveloperTestNet5.i18n
{
    using System.ComponentModel;
    using System.Globalization;
    using System.Resources;

    public class TranslationSource
            : INotifyPropertyChanged
    {
        private static TranslationSource instance = new TranslationSource(); // this is for the editor only, the actual instance used in the running app is in the ServiceProvider.

        public TranslationSource()
        {
            instance = this;
        }

        public static TranslationSource Instance
        {
            get { return instance; }
        }

        private readonly ResourceManager resManager = Properties.Resources.ResourceManager;
        private CultureInfo currentCulture = CultureInfo.CurrentCulture;

        public string this[string key]
        {
            get {
                var str = this.resManager.GetString(key, this.currentCulture);
                return str;
            }
        }

        public CultureInfo CurrentCulture
        {
            get { return this.currentCulture; }
            set
            {
                if (this.currentCulture != value)
                {
                    this.currentCulture = value;
                    var @event = this.PropertyChanged;
                    if (@event != null)
                    {
                        @event.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
