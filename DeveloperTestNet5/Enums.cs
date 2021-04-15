using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DeveloperTestNet5
{
    public enum EncryptionOptions
    {
        Unencrypted,
        SSL,
        STARTTLS
    }

    public enum MailServerTypes
    {
        IMAP,
        POP3
    }
}
