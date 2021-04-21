using DeveloperTestNet5.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperTestNet5.Services
{
    public record ConnectionParams(string server, int port, string username, SecureString password, EncryptionOptions encryption);

    public class EmailContextError
    {
        public string Error { get; init; }
        public Exception Exception { get; init; }
    }

    public abstract class EmailContext: INotifyPropertyChanged, IDisposable
    {
        private bool disposedValue;

        public const int MAX_CONNECTIONS = 5;
        public const int BATCH_SIZE = 100;

        public EmailContext(ConnectionParams connectionParams) { ConnectionParameters = connectionParams; }
        public EmailList Emails { get; } = new();

        public ConnectionParams ConnectionParameters { get; }

        protected CancellationTokenSource CancelToken = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public abstract void StartDownloadingInbox();
        public abstract void LoadEmailBody(Email email);
        public abstract Task<(string filename, Stream stream)> DownloadAttachment(Email email, string attachmentName);

        public bool IsLoading { get; protected set; }
        public int NumEmails { get; protected set; } = 0;
        public int NumEmailBodiesLoaded { get; protected set; } = 0;
        public int NumWorkerThreads { get; protected set; } = 0;

        private object lockObject = new();

        public event EventHandler<EmailContextError> OnError;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CancelToken.Cancel();
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected static string SecureStringToString(SecureString value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        protected void ReportError(string error, Exception ex)
        {
            OnError?.Invoke(this, new EmailContextError { Error = error, Exception = ex });
        }

        protected void IncrementWorkerCount()
        {
            lock (lockObject)
            {
                IsLoading = (++NumWorkerThreads) > 0;
            }
        }

        protected void DecrementWorkerCount()
        {
            lock (lockObject)
            {
                IsLoading = (--NumWorkerThreads) > 0;
            }
        }
    }

}
