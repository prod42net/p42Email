using System;

namespace p42Email.Models;

public sealed class EmailMessageInfo
{
    public string Id { get; set; } = string.Empty; // IMAP UniqueId string
    public string From { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
   
    public DateTimeOffset Date { get; set; }
    public bool IsSeen { get; set; }
    public string Preview { get; set; } = string.Empty;
}
