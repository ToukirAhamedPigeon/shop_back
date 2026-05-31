// src/Shared/Application/Services/IEmailFetchService.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Services
{
    public interface IEmailFetchService
    {
        /// <summary>
        /// Starts the background email fetching service
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the background email fetching service
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually trigger email fetch
        /// </summary>
        Task FetchEmailsNowAsync();

        /// <summary>
        /// Get the current status of the service
        /// </summary>
        EmailFetchServiceStatus GetStatus();
    }

    public class EmailFetchServiceStatus
    {
        public bool IsRunning { get; set; }
        public DateTime? LastFetchTime { get; set; }
        public DateTime? NextFetchTime { get; set; }
        public int TotalEmailsFetched { get; set; }
        public string? LastError { get; set; }
        public TimeSpan FetchInterval { get; set; } = TimeSpan.FromMinutes(5);
    }
}