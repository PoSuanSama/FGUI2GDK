namespace ET
{
    public sealed class PeriodicLogTestService : BackgroundService
    {
        private readonly LogService _logService;
        private readonly ConfigService _configService;
        private readonly ILogger<PeriodicLogTestService> _logger;
        private const int ConfigPollingIntervalMilliseconds = 1000;

        public PeriodicLogTestService(
            LogService logService,
            ConfigService configService,
            ILogger<PeriodicLogTestService> logger)
        {
            _logService = logService;
            _configService = configService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            long sequence = 0;
            int previousIntervalSeconds = -1;
            DateTime nextLogAtUtc = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                int intervalSeconds = _configService.LogTestIntervalSeconds;
                DateTime nowUtc = DateTime.UtcNow;

                if (intervalSeconds != previousIntervalSeconds)
                {
                    previousIntervalSeconds = intervalSeconds;
                    nextLogAtUtc = nowUtc;
                    if (intervalSeconds == 0)
                    {
                        _logger.LogInformation("Periodic log test disabled");
                    }
                    else
                    {
                        _logger.LogInformation("Periodic log test interval changed to {Interval}s", intervalSeconds);
                    }
                }

                if (intervalSeconds > 0 && nowUtc >= nextLogAtUtc)
                {
                    string message = $"Periodic log test #{++sequence}";
                    _logService.AddLog("INFO", "LogTest", message);
                    _logger.LogInformation("{Message}", message);
                    nextLogAtUtc = nowUtc.AddSeconds(intervalSeconds);
                }

                await Task.Delay(ConfigPollingIntervalMilliseconds, stoppingToken);
            }
        }
    }
}
