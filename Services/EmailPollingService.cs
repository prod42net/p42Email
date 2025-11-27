using System;
using System.Threading;
using System.Threading.Tasks;
using p42Email.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using p42Email.Interfaces;

namespace p42Email.Services;

public sealed class EmailPollingService : BackgroundService
{
    private readonly IEmailService _emailService;
    private readonly IOptionsMonitor<EmailOptions> _optionsMonitor;
    private readonly ILogger<EmailPollingService> _logger;
    private readonly IEmailEvents _events;
    private int _lastUnseen = -1;

    public EmailPollingService(
        IEmailService emailService,
        IOptionsMonitor<EmailOptions> optionsMonitor,
        ILogger<EmailPollingService> logger,
        IEmailEvents events)
    {
        _emailService = emailService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _events = events;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailPollingService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = await _emailService.CheckNewEmailsAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation("{Count} new emails detected.", count);
                }
                if (count != _lastUnseen)
                {
                    _lastUnseen = count;
                    _events.RaiseNewMailDetected(count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email polling cycle");
            }

            var seconds = Math.Max(5, _optionsMonitor.CurrentValue.PollingIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
            }
        }
        _logger.LogInformation("EmailPollingService stopped");
    }
}
