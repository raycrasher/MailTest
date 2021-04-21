using DeveloperTestNet5.Models;
using Limilabs.Client;
using Limilabs.Client.IMAP;
using Limilabs.Mail;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DeveloperTestNet5.Services
{
    public class ImapEmailContext : EmailContext
    {
        class ImapEmail : Email
        {
            public ImapEmailContext Context;
            public MessageInfo Info;
            public object BodyLoadLock = new();
        }
        

        ConcurrentQueue<List<long>> inboxLoadQueue = new();
        ConcurrentQueue<ImapEmail> inboxContentLoadQueue = new();
        bool inboxStillLoading = false;

        public ImapEmailContext(ConnectionParams connectionParams) : base(connectionParams)
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

                    var email = (ImapEmail)emailBase;
                    if (email.Context != this)
                        throw new InvalidOperationException("Email does not belong to this context");
                    lock (email.BodyLoadLock)
                    {
                        if (email.BodyStatus == BodyLoadStatus.Available)
                            return;
                        var conn = new Imap();
                        try
                        {
                            Connect(conn);
                            conn.SelectInbox();
                            LoadEmailBody(conn, email);
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

        private void LoadEmailBody(Imap connection, ImapEmail email)
        {
            try
            {
                email.BodyStatus = BodyLoadStatus.Loading;
                if (email.Info.BodyStructure.Text != null)
                {
                    email.BodyText = connection.GetTextByUID(email.Info.BodyStructure.Text);
                }
                if (email.Info.BodyStructure.Html != null)
                {
                    email.BodyHtml = connection.GetTextByUID(email.Info.BodyStructure.Html);
                }
                email.BodyStatus = BodyLoadStatus.Available;
                NumEmailBodiesLoaded++;
            }
            catch(Exception ex) // not sure what the correct exception to catch is
            {
                email.BodyStatus = BodyLoadStatus.Error;
            }
        }

        public override void StartDownloadingInbox()
        {
            Task.Run(() =>
            {
                IncrementWorkerCount();
                inboxStillLoading = true;
                var connection = new Imap();
                try
                {
                    var cancelToken = CancelToken.Token;

                    Connect(connection);
                    connection.SelectInbox();

                    var inboxUIDs = connection.Search(Flag.All);
                    inboxUIDs.Reverse(); // newest to oldest

                    if (inboxUIDs.Count <= 0)
                    {
                        connection.Close(false);
                        return;
                    }

                    foreach (var uidBatch in inboxUIDs.Batch(BATCH_SIZE))
                    {
                        inboxLoadQueue.Enqueue(uidBatch.ToList());
                    }

                    if (cancelToken.IsCancellationRequested)
                        return;

                    // load initial emails
                    inboxLoadQueue.TryDequeue(out var initialEmails);
                    LoadInboxData(initialEmails, connection, cancelToken);

                    // spawn MAX_CONNECTIONS - 1 tasks for loading email bodies
                    for (int i = 0; i < MAX_CONNECTIONS - 1; i++)
                    {
                        Task.Run(() =>
                        {
                            IncrementWorkerCount();
                            var conn = new Imap();
                            try
                            {
                                Connect(conn);
                                conn.SelectInbox();
                                PreloadEmailBodies(conn, cancelToken);
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
                    LoadBatchedInboxData(connection, cancelToken);
                    inboxStillLoading = false;
                    PreloadEmailBodies(connection, cancelToken);

                }
                catch(Exception ex)
                {
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

        private void PreloadEmailBodies(Imap connection, CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested )
            {
                if(inboxContentLoadQueue.TryDequeue(out var imapEmail))
                {
                    lock (imapEmail.BodyLoadLock)
                    {
                        if (imapEmail.BodyStatus != BodyLoadStatus.Available)
                        {
                            LoadEmailBody(connection, imapEmail);
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

        private void LoadBatchedInboxData(Imap connection, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && inboxLoadQueue.TryDequeue(out var uids))
            {
                LoadInboxData(uids, connection, cancellationToken);
            }            
        }

        private void LoadInboxData(List<long> uids, Imap connection, CancellationToken cancellationToken)
        {
            List<MessageInfo> messageInfos = connection.GetMessageInfoByUID(uids);
            messageInfos = messageInfos.OrderByDescending(m => m.Envelope.Date ?? DateTime.MinValue).ToList();

            foreach (var info in messageInfos)
            {
                if (cancellationToken.IsCancellationRequested) return;
                var email = new ImapEmail
                {
                    Context = this,
                    Info = info,
                    DateTime = info.Envelope.Date,
                    Subject = info.Envelope.Subject,
                    From = info.Envelope.From.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                    Cc = info.Envelope.Cc.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                    Bcc = info.Envelope.Bcc.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                    To = info.Envelope.To.Select(f => new EmailAddress(f.Render(), f.Name)).ToArray(),
                };
                NumEmails++;
                Emails.Add(email);
                inboxContentLoadQueue.Enqueue(email);
            }
        }

        private void Connect(Imap imap)
        {
            switch (ConnectionParameters.encryption)
            {
                case EncryptionOptions.Unencrypted:
                    imap.Connect(ConnectionParameters.server, ConnectionParameters.port, false);
                    break;
                case EncryptionOptions.SSL:
                    imap.ConnectSSL(ConnectionParameters.server, ConnectionParameters.port);
                    break;
                case EncryptionOptions.STARTTLS:
                    imap.Connect(ConnectionParameters.server, ConnectionParameters.port);
                    imap.StartTLS();
                    break;
            }

            imap.UseBestLogin(ConnectionParameters.username, SecureStringToString(ConnectionParameters.password));
        }
    }

}
