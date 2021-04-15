using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Security;
using System.Text;
using System.Linq;
using DeveloperTestNet5.i18n;
using System.Collections.Concurrent;
using DeveloperTestNet5.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DeveloperTestNet5.Services;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System;

namespace DeveloperTestNet5.ViewModels
{

    public class MainWindowViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public EncryptionOptions Encryption { get; set; }
        public MailServerTypes MailServerType { get; set; }
        public string Server { get; set; } = "imap.gmail.com";
        public int Port { get; set; } = 993;
        public string Username { get; set; }
        public SecureString Password { private get; set; }

        public EmailContext Context { get; private set; }

        public HashSet<CultureInfo> AvailableCultures { get; init; }

        public TranslationSource Translation { get; init; }

        public DelegateCommand StartCommand { get; }
        public bool CurrentlyLoadingEmails { get; private set; }
        public Email SelectedEmail { get; set; }

        public ICollectionView EmailView { get; set; }

        public MainWindowViewModel(TranslationSource translationSource)
        {
            Translation = translationSource;
            AvailableCultures = new HashSet<CultureInfo>();
            AvailableCultures.Add(CultureInfo.CurrentCulture);
            AvailableCultures.Add(CultureInfo.GetCultureInfo("ja-JP"));

            StartCommand = new DelegateCommand(StartLoadingEmails, () => !Context?.IsLoading ?? true);

            // DEBUG
            //Username = "cinquedia@gmail.com";
            Port = 993;
            Encryption = EncryptionOptions.SSL;

            PropertyChanged += LoadEmailBodyOnSelectedEmailChanged;
        }

        private void LoadEmailBodyOnSelectedEmailChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedEmail) && Context != null && SelectedEmail != null)
            {
                Context.LoadEmailBody(SelectedEmail);
            }
            else if(e.PropertyName == nameof(MailServerType))
            {
                if(MailServerType == MailServerTypes.IMAP && Server.EndsWith(".gmail.com"))
                {
                    Port = 993;
                    Server = "imap.gmail.com";
                }
                else if(MailServerType == MailServerTypes.POP3 && Server.EndsWith(".gmail.com"))
                {
                    Port = 995;
                    Server = "pop.gmail.com";
                }
            }
        }

        private void StartLoadingEmails()
        {
            CurrentlyLoadingEmails = true;

            if (string.IsNullOrEmpty(Server))
            {
                MessageBox.Show("Please enter a server.");
                return;
            }
            if (Port == 0)
            {
                MessageBox.Show("Please enter a valid port.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Please enter a valid username.");
                return;
            }
            if (Password == null || Password.Length == 0)
            {
                MessageBox.Show("Please enter a password.");
                return;
            }

            if (MailServerType == MailServerTypes.IMAP)
            {
                Context = new ImapEmailContext(new ConnectionParams(Server, Port, Username, Password, Encryption));
            }
            else
            {
                Context = new Pop3EmailContext(new ConnectionParams(Server, Port, Username, Password, Encryption));
            }
            Context.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == "IsLoading")
                    StartCommand.FireExecuteChanged();
            };
            EmailView = CollectionViewSource.GetDefaultView(Context.Emails);
            EmailView.SortDescriptions.Add(new SortDescription("DateTime", ListSortDirection.Descending));
            Context.StartDownloadingInbox();
            StartCommand.FireExecuteChanged();
        }
    }
}
