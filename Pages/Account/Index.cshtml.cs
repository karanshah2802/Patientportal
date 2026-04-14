using Patientportal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Patientportal.AllApicall;
using Patientportal.Model;
using Innovura.CSharp.Core;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Patientportal.Pages.Account
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken(Order = 2000)]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;
        //private readonly HttpClient _httpClient;
        private readonly HttpClient _httpClient;
        private readonly ApiService _apiService;
        private readonly OTPService _otpService;
        private readonly OpsTokenService _opsTokenService;
        [BindProperty]
        public InputModel Input { get; set; }
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClientFactory, IConfiguration configuration, ApiService apiService, OTPService oTPService, OpsTokenService opsTokenService)
        {
            _logger = logger;
            _httpClient = httpClientFactory;
            _apiService = apiService;
            _otpService = oTPService;
            _configuration = configuration;
            _opsTokenService = opsTokenService;
        }
        public async Task<IActionResult> OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var encryptedId = User.FindFirstValue(PatientPortalClaimTypes.EncryptedPatientId);
                if (!string.IsNullOrEmpty(encryptedId))
                    return RedirectToPage("/Patient/Index", new { id = encryptedId });

                Response.Cookies.Delete(PatientPortalCookies.AppointmentPortalBooking, new CookieOptions { Path = "/" });
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return Page();
        }

        /// <summary>
        /// New numbers: POST the same verify-otp style login API as <see cref="OpsTokenService"/> (ApiSettings:LoginPath).
        /// Backend creates the user and returns an access token; use that token for profile + SendAuthToken if present.
        /// </summary>
        private async Task<string?> TryRegisterViaLoginApiAsync(string mobile)
        {
            var loginPath = _configuration["ApiSettings:LoginPath"] ?? "/api/v1/Account/portalverify-otp";
            if (loginPath.IndexOf("verify-otp", StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            var baseUrl = (_configuration["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
            var path = loginPath.StartsWith("/") ? loginPath : "/" + loginPath;
            var url = $"{baseUrl}{path}";

            var otp = _configuration["ApiSettings:RegisterLoginOtp"] ?? "";
            var jsonBody = JsonSerializer.Serialize(new { mobile = mobile.Trim(), otp = otp });
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Register via login API request failed for {Url}", url);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Register via login API returned {Status} for mobile ending …{Tail}. Body: {Body}",
                    response.StatusCode,
                    mobile.Length >= 4 ? mobile[^4..] : mobile,
                    body.Length > 500 ? body[..500] + "…" : body);
                return null;
            }

            return OpsTokenService.ExtractAccessTokenFromJson(body);
        }

        public async Task<JsonResult> OnPostSendOTPAsync([FromBody] InputModel request)
        {
            if (string.IsNullOrEmpty(request.Mobile) || request.Mobile.Length < 10)
            {
                return new JsonResult(new { success = false, message = "Invalid phone number" });
            }

            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            var bearerToken = await _opsTokenService.GetTokenAsync();
            string apiUrl2 = $"{baseUrl}/api/Profile/GetpatientByMobilenumber?Mobilenumber={request.Mobile}";
            var PatientDetails = await _apiService.GetPatientByMobilePreferIndependentAsync(apiUrl2, bearerToken);
            if (PatientDetails == null)
            {
                var userToken = await TryRegisterViaLoginApiAsync(request.Mobile);
                if (!string.IsNullOrEmpty(userToken))
                    bearerToken = userToken;

                PatientDetails = await _apiService.GetPatientByMobilePreferIndependentAsync(apiUrl2, bearerToken);
            }

            if (!_otpService.CanSendOTP(request.Mobile))
            {
                return new JsonResult(new { success = false, message = "Maximum OTP attempts reached. Try again after 24 hours." });
            }

            string apiUrl = $"{baseUrl}/api/v1/Account/PatientportalSendAuthToken/{request.Mobile}.json";

            try
            {
                var response = await _apiService.GetAsync<List<InputModel>>(apiUrl, bearerToken);
                _otpService.RecordOTPAttempt(request.Mobile);
                return new JsonResult(new { success = true, message = "OTP Sent Successfully" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = "Server error: " + ex.Message });
            }
        }

        public async Task<JsonResult> OnPostVerifyotpAsync([FromBody] InputModel request)
        {

            if (string.IsNullOrEmpty(request.OTP) || request.OTP.Length < 4)
            {
                return new JsonResult(new { success = false, message = "Invalid OTP number" });
            }
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/v1/Account/Patientportalverify-otp";

            var response = await _apiService.PostAsync<InputModel, ApiResponse>(apiUrl, request, token);

            if (response != null && response.IsSucceeded)
            {
                _otpService.ClearOTPAttempts(request.Mobile); // OTP attempts reset

                string apiUrlProfile = $"{baseUrl}/api/Profile/GetpatientByMobilenumber?Mobilenumber={request.Mobile}";
                var patient = await _apiService.GetPatientByMobilePreferIndependentAsync(apiUrlProfile, token);
                if (patient == null || patient.Id == 0)
                {
                    return new JsonResult(new { success = false, message = "Profile could not be loaded after verification. Please try again." });
                }

                var encryptedId = EncryptionHelper.EncryptId(patient.Id);

                var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, request.Mobile),
                new Claim("OTPVerified", "true"),
                new Claim(PatientPortalClaimTypes.EncryptedPatientId, encryptedId)
            };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return new JsonResult(new
                {
                    success = true,
                    message = "OTP Verified Successfully",
                    patientId = encryptedId,
                    redirectUrl = "/Patient?id=" + encryptedId
                });
            }
            else
            {
                return new JsonResult(new { success = false, message = response?.Message ?? "Verification failed." });
            }
        }

        public async Task<IActionResult> OnGetLogoutAsync()
        {
            Response.Cookies.Delete(PatientPortalCookies.AppointmentPortalBooking, new CookieOptions { Path = "/" });
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Index");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Response.Cookies.Delete(PatientPortalCookies.AppointmentPortalBooking, new CookieOptions { Path = "/" });
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); // Logout user
            return new JsonResult(new { success = true }); // Success response
        }

    }
    public class InputModel
    {
        public string? Email { get; set; }

        public string? Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
        [Required]
        [RegularExpression(@"^(\+?1?\d{10}|\d{10}|\d{12}|\d{13})$", ErrorMessage = "Invalid mobile number")]
        [MaxLength(13)]
        [MinLength(10)]
        public string Mobile { get; set; }
        public string OTP { get; set; }
        public string OTP1 { get; set; }
        public string OTP2 { get; set; }
        public string OTP3 { get; set; }
    }
}
