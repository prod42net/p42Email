using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using p42Email.Options;
using p42Email.Models;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using p42Email.Interfaces;

namespace p42Email.Services;

public sealed class MailKitEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(IOptions<EmailOptions> options, ILogger<MailKitEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    
    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var msg = new MimeMessage();
        var fromName = string.IsNullOrWhiteSpace(_options.Smtp.FromDisplayName) ? _options.Smtp.FromAddress : _options.Smtp.FromDisplayName;
        msg.From.Add(new MailboxAddress(fromName, _options.Smtp.FromAddress));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;

        var builder = new BodyBuilder();
        if (isHtml)
            builder.HtmlBody = body;
        else
            builder.TextBody = body;
        msg.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            var secure = _options.Smtp.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;
            await smtp.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, secure, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                await smtp.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password, cancellationToken);
            }

            await smtp.SendAsync(msg, cancellationToken);
        }
        finally
        {
            try { await smtp.DisconnectAsync(true, cancellationToken); } catch { /* ignore */ }
        }
    }

    public async Task SendEmailAsync(string fromEmail, string toEmail, string subject, string body, bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        var msg = new MimeMessage();
        
        msg.From.Add(new MailboxAddress(fromEmail, fromEmail));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;

        var builder = new BodyBuilder();
        if (isHtml)
            builder.HtmlBody = body;
        else
            builder.TextBody = body;
        msg.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            var secure = _options.Smtp.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;
            await smtp.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, secure, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                await smtp.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password, cancellationToken);
            }

            await smtp.SendAsync(msg, cancellationToken);
        }
        finally
        {
            try { await smtp.DisconnectAsync(true, cancellationToken); } catch { /* ignore */ }
        }
    }

    public async Task<int> CheckNewEmailsAsync(CancellationToken cancellationToken = default)
    {
        using var imap = new ImapClient();
        try
        {
            var secure = _options.Imap.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
            await imap.ConnectAsync(_options.Imap.Host, _options.Imap.Port, secure, cancellationToken);
            await imap.AuthenticateAsync(_options.Imap.Username, _options.Imap.Password, cancellationToken);

            var folder = await imap.GetFolderAsync(_options.Imap.Folder, cancellationToken);
            await folder.OpenAsync(MailKit.FolderAccess.ReadOnly, cancellationToken);

            var unseenUids = await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken);
            var count = unseenUids?.Count ?? 0;
            _logger.LogInformation("Email check completed: {Count} unseen messages in {Folder}", count, _options.Imap.Folder);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking new emails");
            throw;
        }
        finally
        {
            try { await imap.DisconnectAsync(true, cancellationToken); } catch { /* ignore */ }
        }
    }

    public async Task<IReadOnlyList<EmailMessageInfo>> GetRecentEmailsAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        using var imap = new ImapClient();
        try
        {
            var secure = _options.Imap.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
            await imap.ConnectAsync(_options.Imap.Host, _options.Imap.Port, secure, cancellationToken);
            await imap.AuthenticateAsync(_options.Imap.Username, _options.Imap.Password, cancellationToken);

            var folder = await imap.GetFolderAsync(_options.Imap.Folder, cancellationToken);
            await folder.OpenAsync(MailKit.FolderAccess.ReadOnly, cancellationToken);

            // Get UIDs of all messages, then take the most recent by UID (approx chronological on many servers)
            var allUids = await folder.SearchAsync(SearchQuery.All, cancellationToken);
            if (allUids?.Count is null or 0)
                return Array.Empty<EmailMessageInfo>();

            // Take last N (most recent)
            var selected = allUids.OrderBy(u => u.Id).TakeLast(Math.Max(1, take)).ToList();

            // Fetch summaries first to avoid downloading bodies where possible
            var items = MailKit.MessageSummaryItems.Envelope | MailKit.MessageSummaryItems.Flags | MailKit.MessageSummaryItems.InternalDate;
            var req = new MailKit.FetchRequest(items);
            var summaries = await folder.FetchAsync(selected, req, cancellationToken);

            var list = new List<EmailMessageInfo>(summaries.Count);
            foreach (var s in summaries.OrderByDescending(su => su.InternalDate))
            {
                var from = s.Envelope?.From?.Mailboxes?.FirstOrDefault();
                string fromText = from is null ? "" : (!string.IsNullOrWhiteSpace(from.Name) ? $"{from.Name} <{from.Address}>" : from.Address);
                string subject = s.Envelope?.Subject ?? string.Empty;
                var seen = s.Flags?.HasFlag(MailKit.MessageFlags.Seen) == true;

                // Try to get a short preview by downloading text body, swallow errors
                string preview = string.Empty;
                try
                {
                    var msg = await folder.GetMessageAsync(s.UniqueId, cancellationToken);
                    var textBody = msg.TextBody ?? (string.IsNullOrEmpty(msg.HtmlBody) ? null : StripHtml(msg.HtmlBody));
                    if (!string.IsNullOrEmpty(textBody))
                    {
                        var plain = textBody.Trim();
                        preview = plain.Length > 200 ? plain.Substring(0, 200) : plain;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get preview for message {Uid}", s.UniqueId.Id);
                }

                list.Add(new EmailMessageInfo
                {
                    Id = s.UniqueId.Id.ToString(),
                    From = fromText,
                    Subject = subject,
                    Date = s.InternalDate?.ToUniversalTime() ?? DateTimeOffset.MinValue,
                    IsSeen = seen,
                    Preview = preview
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching recent emails");
            throw;
        }
        finally
        {
            try { await imap.DisconnectAsync(true, cancellationToken); } catch { /* ignore */ }
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        try
        {
            var array = new char[html.Length];
            int arrayIndex = 0;
            bool inside = false;
            foreach (var @let in html)
            {
                if (@let == '<') { inside = true; continue; }
                if (@let == '>') { inside = false; continue; }
                if (!inside) array[arrayIndex++] = @let;
            }
            return new string(array, 0, arrayIndex);
        }
        catch
        {
            return html;
        }
    }
}
