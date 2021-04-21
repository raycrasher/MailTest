using DeveloperTestNet5.Models;
using Limilabs.Client.POP3;
using Limilabs.Mail;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DeveloperTestNet5.Services
{
    class Pop3EmailContext: EmailContext
    {
        const int MAX_POP3_CONNECTIONS = 2;
        const int POP3_BATCH_SIZE = 5;

        class Pop3Email : Email
        {
            public object BodyLoadLock = new();
            internal Pop3EmailContext Context;

            public IMail Info { get; internal set; }
            public string Uid { get; internal set; }
        }
        ConcurrentQueue<List<string>> inboxLoadQueue = new();
        ConcurrentQueue<Pop3Email> inboxContentLoadQueue = new();
        bool inboxStillLoading = false;

        public Pop3EmailContext(ConnectionParams connectionParams) : base(connectionParams)
        {
        }

        public override Task<(string filename, Stream stream)> DownloadAttachment(Email email, string attachmentName)
        {
            throw new NotImplementedException();
        }

        public override void LoadEmailBody(Email emailBase)
        {
            Task.Run(() => {
                try
                {
                    IncrementWorkerCount();
                    var email = (Pop3Email)emailBase;
                    if (email.Context != this)
                        throw new InvalidOperationException("Email does not belong to this context");
                    lock (email.BodyLoadLock)
                    {
                        if (email.BodyStatus == BodyLoadStatus.Available)
                            return;
                        var conn = new Pop3();
                        try
                        {
                            var builder = new MailBuilder();
                            Connect(conn);
                            LoadEmailBody(conn, email, builder);
                        }
                        finally
                        {
                            if (conn != null)
                                conn.Close(false);
                        }
                    }
                }
                finally
                {
                    DecrementWorkerCount();
                }
            });
        }

        public override void StartDownloadingInbox()
        {
            Task.Run(() =>
            {
                IncrementWorkerCount();
                inboxStillLoading = true;
                var connection = new Pop3();
                try
                {
                    var cancelToken = CancelToken.Token;

                    Connect(connection);

                    List<string> inboxUIDs = connection.GetAll();
                    inboxUIDs.Reverse(); // newest to oldest

                    if (inboxUIDs.Count <= 0)
                    {
                        connection.Close(false);
                        return;
                    }

                    foreach (var uidBatch in inboxUIDs.Batch(POP3_BATCH_SIZE))
                    {
                        inboxLoadQueue.Enqueue(uidBatch.ToList());
                    }

                    if (cancelToken.IsCancellationRequested)
                        return;

                    inboxLoadQueue.TryDequeue(out var firstBatch);
                    MailBuilder builder = new MailBuilder();
                    LoadInboxData(firstBatch, connection, builder, cancelToken);

                    // spawn MAX_CONNECTIONS - 1 tasks
                    for (int i = 0; i < MAX_POP3_CONNECTIONS - 1; i++)
                    {
                        Task.Run(() =>
                        {
                            IncrementWorkerCount();
                            var conn = new Pop3();
                            try
                            {
                                var builderPerThread = new MailBuilder();
                                Connect(conn);
                                
                                //LoadInboxData(conn, cancelToken);
                                PreloadEmailBodies(conn, builderPerThread, cancelToken);
                            }
                            finally
                            {
                                if (conn != null)
                                    conn.Close(false);
                                DecrementWorkerCount();
                            }
                        });
                    }

                    // re-use first connection so we get emails much faster
                    LoadInboxData(connection, builder, cancelToken);
                    inboxStillLoading = false;
                    PreloadEmailBodies(connection, builder, cancelToken);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(Properties.Resources.ERR_CantLoadInbox + "\n" + ex.Message);
                    ReportError(Properties.Resources.ERR_CantLoadInbox, ex);
                }
                finally
                {
                    if (connection != null)
                        connection.Close(false);
                    DecrementWorkerCount();
                    inboxStillLoading = false;
                }
            });
        }

        private void PreloadEmailBodies(Pop3 connection, MailBuilder builder, System.Threading.CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (inboxContentLoadQueue.TryDequeue(out var pop3Email))
                {
                    lock (pop3Email.BodyLoadLock)
                    {
                        if (pop3Email.BodyStatus != BodyLoadStatus.Available)
                        {
                            LoadEmailBody(connection, pop3Email, builder);
                        }
                    }
                }
                else
                {
                    if (inboxStillLoading)
                        Task.Delay(5000).Wait();
                    else
                        break;
                }
            }
        }

        private void LoadEmailBody(Pop3 connection, Pop3Email email, MailBuilder builder)
        {
            try
            {
                email.BodyStatus = BodyLoadStatus.Loading;

                var eml = connection.GetMessageByUID(email.Uid);
                IMail emailRaw = builder.CreateFromEml(eml);
                email.BodyText = emailRaw.GetBodyAsText();
                email.BodyHtml = emailRaw.GetBodyAsHtml();
                email.BodyStatus = BodyLoadStatus.Available;
                NumEmailBodiesLoaded++;
            }
            catch (Exception ex) // not sure what the correct exception to catch is
            {
                email.BodyStatus = BodyLoadStatus.Error;
            }
        }

        private void LoadInboxData(Pop3 connection, MailBuilder builder, System.Threading.CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && inboxLoadQueue.TryDequeue(out var uids))
            {
                LoadInboxData(uids, connection, builder, cancellationToken);
            }
        }

        private void LoadInboxData(List<string> uids, Pop3 connection, MailBuilder builder, System.Threading.CancellationToken cancellationToken)
        {
            foreach (var uid in uids)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var headerBytes = connection.GetHeadersByUID(uid);
                IMail emailData = builder.CreateFromEml(headerBytes);
                var email = new Pop3Email
                {
                    Uid = uid,
                    Context = this,
                    Info = emailData,
                    DateTime = emailData.Date,
                    Subject = emailData.Subject,
                    From = emailData.From.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                    Cc = emailData.Cc.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                    Bcc = emailData.Bcc.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                    To = emailData.To.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                };
                NumEmails++;
                Emails.Add(email);
                inboxContentLoadQueue.Enqueue(email);
            }
        }

        private void Connect(Pop3 pop3)
        {
            switch (ConnectionParameters.encryption)
            {
                case EncryptionOptions.Unencrypted:
                    pop3.Connect(ConnectionParameters.server, ConnectionParameters.port, false);
                    break;
                case EncryptionOptions.SSL:
                    pop3.ConnectSSL(ConnectionParameters.server, ConnectionParameters.port);
                    break;
                case EncryptionOptions.STARTTLS:
                    pop3.Connect(ConnectionParameters.server, ConnectionParameters.port);
                    pop3.StartTLS();
                    break;
            }

            pop3.UseBestLogin(ConnectionParameters.username, SecureStringToString(ConnectionParameters.password));
        }
    }
}
