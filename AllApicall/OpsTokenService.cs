using System.Text;
using System.Text.Json;

namespace Patientportal.AllApicall
{
    /// <summary>
    /// Service for OPS API token management with auto-refresh support.
    /// If Username/Password provided, calls login API to refresh token before expiry.
    /// Otherwise uses static AuthToken from config (manual update still required).
    /// </summary>
    public class OpsTokenService : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpsTokenService> _logger;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private string? _cachedToken;
        private DateTime _tokenExpiryUtc = DateTime.MinValue;
        private bool _disposed;

        public OpsTokenService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<OpsTokenService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("OpsInsecure");
            _logger = logger;
        }

        /// <summary>
        /// Returns a valid OPS API token. Refreshes automatically via Login or verify-otp API when token is expiring.
        /// </summary>
        public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            var username = _configuration["ApiSettings:Username"];
            var password = _configuration["ApiSettings:Password"];
            var mobile = _configuration["ApiSettings:Mobile"];
            var otp = _configuration["ApiSettings:Otp"];
            var staticToken = _configuration["ApiSettings:AuthToken"];
            var loginPath = _configuration["ApiSettings:LoginPath"] ?? "/api/v1/Account/verify-otp";

            var useVerifyOtp = loginPath.IndexOf("verify-otp", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasVerifyOtpCreds = useVerifyOtp && !string.IsNullOrEmpty(mobile) && !string.IsNullOrEmpty(otp);
            var hasLoginCreds = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);

            // No credentials for token refresh - use static token from config
            if (!hasLoginCreds && !hasVerifyOtpCreds)
            {
                if (!string.IsNullOrEmpty(staticToken))
                    return staticToken;
                throw new InvalidOperationException("Configure ApiSettings:Username/Password, or ApiSettings:Mobile/Otp with LoginPath verify-otp, or ApiSettings:AuthToken.");
            }

            // Check if cached token is still valid (refresh 5 minutes before expiry)
            var buffer = TimeSpan.FromMinutes(5);
            if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiryUtc > DateTime.UtcNow.Add(buffer))
            {
                return _cachedToken;
            }

            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiryUtc > DateTime.UtcNow.Add(buffer))
                    return _cachedToken;

                var baseUrl = _configuration["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? "";
                var loginUrl = $"{baseUrl}{loginPath}";

                string jsonBody;
                if (useVerifyOtp && hasVerifyOtpCreds)
                {
                    jsonBody = JsonSerializer.Serialize(new { mobile = mobile!.Trim(), otp = otp!.Trim() });
                    _logger.LogInformation("Refreshing OPS API token via verify-otp");
                }
                else
                {
                    jsonBody = JsonSerializer.Serialize(new { userName = username, password = password });
                    _logger.LogInformation("Refreshing OPS API token via login");
                }

                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(loginUrl, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "OPS token refresh failed ({StatusCode}). Url={Url}. Body={Body}. Response={Response}",
                        response.StatusCode,
                        loginUrl,
                        jsonBody,
                        errorBody
                    );
                    if (!string.IsNullOrEmpty(staticToken))
                        return staticToken;
                    throw new InvalidOperationException($"OPS token refresh failed: {(int)response.StatusCode} {response.StatusCode}. Url={loginUrl}. Response={errorBody}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var token = ExtractAccessTokenFromJson(json);

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Could not extract token from login response, using static token");
                    return !string.IsNullOrEmpty(staticToken) ? staticToken : throw new InvalidOperationException("Login response had no token");
                }

                _cachedToken = token;
                _tokenExpiryUtc = GetTokenExpiryUtc(token) ?? DateTime.UtcNow.AddHours(1);
                _logger.LogInformation("OPS token refreshed, expires at {Expiry}", _tokenExpiryUtc);
                return _cachedToken;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Synchronous overload for callers that cannot use async.
        /// </summary>
        public string GetToken()
        {
            return GetTokenAsync().GetAwaiter().GetResult();
        }

        /// <summary>Parse AccessToken / JWT from OPS login or verify-otp JSON response.</summary>
        public static string? ExtractAccessTokenFromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                foreach (var key in new[] { "AccessToken", "accessToken", "access_token", "token", "Token", "jwt" })
                {
                    if (root.TryGetProperty(key, out var prop))
                        return prop.GetString();
                }

                if (root.TryGetProperty("data", out var data))
                {
                    foreach (var key in new[] { "token", "accessToken", "access_token" })
                    {
                        if (data.TryGetProperty(key, out var prop))
                            return prop.GetString();
                    }
                }
            }
            catch { /* ignore parse errors */ }
            return null;
        }

        private static DateTime? GetTokenExpiryUtc(string jwt)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length != 3) return null;

                var payload = parts[1];
                var padding = 4 - payload.Length % 4;
                if (padding != 4) payload += new string('=', padding);
                var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
                var json = Encoding.UTF8.GetString(bytes);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var exp))
                    return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }
            catch { /* ignore */ }
            return null;
        }

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing) _refreshLock.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
