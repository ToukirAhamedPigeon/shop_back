using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using shop_back.src.Shared.Application.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class EmailFetchBackgroundService : BackgroundService, IEmailFetchService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailFetchBackgroundService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        
        private EmailFetchServiceStatus _status;
        private readonly SemaphoreSlim _fetchLock = new SemaphoreSlim(1, 1);
        private DateTime? _lastFetchTime;
        private int _totalEmailsFetched;
        private string? _lastError;
        private bool _isRunning;

        public EmailFetchBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<EmailFetchBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _status = new EmailFetchServiceStatus
            {
                IsRunning = false,
                FetchInterval = _interval
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _isRunning = true;
            _status.IsRunning = true;
            _logger.LogInformation("Email Fetch Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _status.NextFetchTime = DateTime.UtcNow.Add(_interval);
                    await Task.Delay(_interval, stoppingToken);
                    
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await FetchEmailsInternalAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in email fetch background service loop");
                    _lastError = ex.Message;
                    _status.LastError = _lastError;
                }
            }

            _isRunning = false;
            _status.IsRunning = false;
            _logger.LogInformation("Email Fetch Background Service is stopping.");
        }

        private async Task FetchEmailsInternalAsync(CancellationToken cancellationToken)
        {
            if (!await _fetchLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogWarning("Email fetch already in progress, skipping...");
                return;
            }

            try
            {
                _logger.LogInformation("Fetching emails from IMAP server...");
                
                using var scope = _serviceProvider.CreateScope();
                var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();
                
                await mailService.FetchAndStoreEmailsAsync();
                
                _lastFetchTime = DateTime.UtcNow;
                _totalEmailsFetched++;
                _lastError = null;
                
                _status.LastFetchTime = _lastFetchTime;
                _status.TotalEmailsFetched = _totalEmailsFetched;
                _status.LastError = null;
                
                _logger.LogInformation("Email fetch completed successfully at {FetchTime}", _lastFetchTime);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _status.LastError = _lastError;
                _logger.LogError(ex, "Error fetching emails");
                throw;
            }
            finally
            {
                _fetchLock.Release();
            }
        }

        public async Task FetchEmailsNowAsync()
        {
            _logger.LogInformation("Manual email fetch triggered");
            await FetchEmailsInternalAsync(CancellationToken.None);
        }

        public EmailFetchServiceStatus GetStatus()
        {
            _status.IsRunning = _isRunning;
            _status.LastFetchTime = _lastFetchTime;
            _status.TotalEmailsFetched = _totalEmailsFetched;
            _status.LastError = _lastError;
            _status.NextFetchTime = DateTime.UtcNow.Add(_interval);
            _status.FetchInterval = _interval;
            
            return _status;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Email Fetch Background Service is stopping...");
            _isRunning = false;
            _status.IsRunning = false;
            await base.StopAsync(cancellationToken);
        }
    }
}