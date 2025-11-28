# p42Email

Email utilities for .NET apps built on MailKit: send messages via SMTP, check IMAP for new mail, list recent emails, and optionally run a background polling service that raises events when new messages arrive.

- PackageId: `p42Email`
- Target framework: .NET 10.0
- Language: C# 14.0
- License: MIT
- Repository: https://github.com/prod42net/p42Email

## Overview

`p42Email` provides a small, focused abstraction around common email workflows using MailKit:

- Send emails via SMTP
- Check an IMAP mailbox for unseen messages
- Retrieve a list of recent messages
- Optionally run a hosted background service that periodically polls for email and raises a `NewMailDetected` event

All configuration is provided through a single `Email` options section (`Smtp`, `Imap`, and `PollingIntervalSeconds`).

## Features

- SMTP send: `IEmailService.SendEmailAsync(...)`
- IMAP check for unseen: `IEmailService.CheckNewEmailsAsync(...)`
- List recent: `IEmailService.GetRecentEmailsAsync(...)`
- Background polling (hosted service): `EmailPollingService`
- New mail eventing: `IEmailEvents.NewMailDetected`

## Requirements

- .NET SDK 10.0+
- C# 14.0
- NuGet dependency: `MailKit`

## Installation

Install from NuGet:

```
dotnet add package p42Email --version 1.0.0
```

Or reference the project directly if you are working from source.

## Configuration

Add an `Email` section to your `appsettings.json` (or other configuration source). The structure maps to `EmailOptions`.

```
{
  "Email": {
    "PollingIntervalSeconds": 60,
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "UseSsl": true,
      "Username": "smtp-user",
      "Password": "smtp-password",
      "FromAddress": "no-reply@example.com",
      "FromDisplayName": "Example App"
      
    },
    "Imap": {
      "Host": "imap.example.com",
      "Port": 993,
      "UseSsl": true,
      "Username": "imap-user",
      "Password": "imap-password",
      "Folder": "INBOX"
    }
  }
}
```
here are optional settings for Azure AD authentication:
it goes to the SMTP section:
```
{
  "Email": {
    "PollingIntervalSeconds": 60,
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "UseSsl": true,
      "Username": "smtp-user",
      "Password": "smtp-password",
      "FromAddress": "no-reply@example.com",
      "FromDisplayName": "Example App",
   
      "ClientSecret": "your-secret"
      "ClientId": "client-id",
      "TenantId": "tentant-id"
      
    },
    "Imap": {
      "Host": "imap.example.com",
      "Port": 993,
      "UseSsl": true,
      "Username": "imap-user",
      "Password": "imap-password",
      "Folder": "INBOX"
    }
  }
}
```


Tip: Store secrets (usernames/passwords) in user secrets or environment variables in production.

## Dependency Injection Setup

Register the services in your `Program.cs` or `Startup` using the Microsoft.Extensions.* stack:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using p42Email.Interfaces;
using p42Email.Options;
using p42Email.Services;

var builder = Host.CreateApplicationBuilder(args);

// Bind Email options from configuration
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

// Core email services
builder.Services.AddSingleton<IEmailEvents, EmailEvents>();
builder.Services.AddSingleton<IEmailService, MailKitEmailService>();

// Optional: enable background polling for new messages
builder.Services.AddHostedService<EmailPollingService>();

var app = builder.Build();
await app.RunAsync();
```

If you don't need background polling/events, omit the `AddHostedService<EmailPollingService>()` line.

## Usage

### Send an email

```csharp
using p42Email.Interfaces;

public class MyController
{
    private readonly IEmailService _email;
    public MyController(IEmailService email) => _email = email;

    public async Task SendWelcomeAsync(string userEmail)
    {
        await _email.SendEmailAsync(
            toEmail: userEmail,
            subject: "Welcome!",
            body: "<p>Thanks for joining.</p>",
            isHtml: true
        );
    }
}
```

### Check for new (unseen) messages

```csharp
int unseen = await _email.CheckNewEmailsAsync();
Console.WriteLine($"Unseen messages: {unseen}");
```

### Get recent messages

```csharp
using p42Email.Models;

IReadOnlyList<EmailMessageInfo> recent = await _email.GetRecentEmailsAsync(take: 20);
foreach (var m in recent)
{
    Console.WriteLine($"[{m.Date}] {m.From} -> {m.Subject}");
}
```

### React to new mail via event

When using the hosted polling service, subscribe to `IEmailEvents.NewMailDetected` to react to new unseen messages detected during polling.

```csharp
using p42Email.Interfaces;

public sealed class NewMailHandler
{
    public NewMailHandler(IEmailEvents events)
    {
        events.NewMailDetected += OnNewMailDetected;
    }

    private void OnNewMailDetected(int unseenCount)
    {
        Console.WriteLine($"New unseen messages detected: {unseenCount}");
        // Add your handling logic here (e.g., trigger processing workflow)
    }
}
```

## Public API Surface (at a glance)

Interfaces and models you will commonly use:

- `p42Email.Interfaces.IEmailService`
  - `Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken ct = default)`
  - `Task<int> CheckNewEmailsAsync(CancellationToken ct = default)`
  - `Task<IReadOnlyList<EmailMessageInfo>> GetRecentEmailsAsync(int take = 50, CancellationToken ct = default)`

- `p42Email.Interfaces.IEmailEvents`
  - `event Action<int>? NewMailDetected`

- `p42Email.Options.EmailOptions`
  - `SmtpOptions Smtp`, `ImapOptions Imap`, `int PollingIntervalSeconds = 60`

## Notes

- SMTP defaults typically: 587 with STARTTLS or SSL depending on provider; IMAP defaults typically: 993 SSL. Adjust to your provider's requirements.
- Ensure less-secure app access or app passwords are configured as required by your email provider.
- For production, do not commit secrets. Prefer environment variables or a secret manager.

## License

MIT © pro42net

---

The NuGet package includes a small icon (`prod42net.jpg`) and this README for package metadata.
