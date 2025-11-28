using System.Threading;
using System.Threading.Tasks;
using p42Email.Models;

namespace p42Email.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends an email to a single recipient.
    /// </summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
    Task<bool> SendEmailAsync(string fromEmail, string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the configured mailbox for new (unseen) messages.
    /// Returns the count of unseen messages in the configured folder.
    /// </summary>
    Task<int> CheckNewEmailsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a list of recent messages from the configured IMAP folder.
    /// </summary>
    Task<IReadOnlyList<EmailMessageInfo>> GetRecentEmailsAsync(int take = 50, CancellationToken cancellationToken = default);
}
