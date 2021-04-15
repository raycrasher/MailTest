using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeveloperTestNet5.Models
{
    public class Attachment
    {
        public string Name { get; set; }
        public string ContentType { get; set; }
        public bool IsDownloading { get; set; }
        public BodyLoadStatus Status { get; set; } = BodyLoadStatus.NotLoaded;
    }

    public enum BodyLoadStatus
    {
        NotLoaded, Loading, Available, Error
    }

    public record EmailAddress(string Address, string Name);

    public class Email: INotifyPropertyChanged
    {
        const int MAX_SUMMARY_LEN = 100;

        public EmailAddress[] From { get; set; }
        public EmailAddress[] To { get; set; }
        public EmailAddress[] Cc { get; set; }
        public EmailAddress[] Bcc { get; set; }

        public string FromString => string.Join("; ", From?.Select(s => s.Address));
        [DependsOn(nameof(BodyText))]
        public string BodyTextSummary => GetSummary(BodyText);

        public string Subject { get; set; }
        public DateTime? DateTime { get; set; }
        public string BodyHtml { get; set; }
        public string BodyText { get; set; }
        public BodyLoadStatus BodyStatus { get; set; } = BodyLoadStatus.NotLoaded;
        public List<Attachment> AttachmentsAsync { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        static Regex spaceRegex = new Regex("[ \r\n]{2,}", RegexOptions.None);

        private string GetSummary(string bodyText)
        {
            if (bodyText == null) return "";
            var repl = spaceRegex.Replace(bodyText, " ");
            if (repl.Length > MAX_SUMMARY_LEN)
                return repl.Substring(0, MAX_SUMMARY_LEN) + " [...]";
            else return repl;
        }
    }
}
