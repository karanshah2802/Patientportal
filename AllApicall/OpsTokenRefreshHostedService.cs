namespace Patientportal.AllApicall
{
    /// <summary>
    /// Background service that refreshes OPS token periodically (every 6 hours or before expiry).
    /// </summary>
    public class OpsTokenRefreshHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OpsTokenRefreshHostedService> _logger;

        public OpsTokenRefreshHostedService(IServiceProvider services, ILogger<OpsTokenRefreshHostedService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Initial delay

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var tokenService = scope.ServiceProvider.GetRequiredService<OpsTokenService>();
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                    var username = config["ApiSettings:Username"];
                    var password = config["ApiSettings:Password"];
                    var mobile = config["ApiSettings:Mobile"];
                    var otp = config["ApiSettings:Otp"];
                    var serviceEmail = config["ApiSettings:ServiceAccountEmail"];
                    var servicePassword = config["ApiSettings:ServiceAccountPassword"];
                    var loginPath = config["ApiSettings:LoginPath"] ?? "";
                    var usePortalLogin = loginPath.IndexOf("portal-login", StringComparison.OrdinalIgnoreCase) >= 0;
                    var useVerifyOtp = loginPath.IndexOf("verify-otp", StringComparison.OrdinalIgnoreCase) >= 0;
                    var hasPortalLogin = usePortalLogin && !string.IsNullOrEmpty(serviceEmail) && !string.IsNullOrEmpty(servicePassword);
                    var hasLogin = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
                    var hasVerifyOtp = useVerifyOtp && !string.IsNullOrEmpty(mobile) && !string.IsNullOrEmpty(otp);
                    var canRefresh = hasPortalLogin
                        || hasVerifyOtp
                        || (hasLogin && !usePortalLogin && !useVerifyOtp);
                    if (canRefresh)
                    {
                        _ = await tokenService.GetTokenAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OPS token refresh failed in background");
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
    }
}
