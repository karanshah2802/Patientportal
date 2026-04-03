using Innovura.CSharp.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Patientportal.AllApicall;
using Patientportal.Model;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Patientportal.Pages.Account
{
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
        public IActionResult OnGet()
        {
            if (User.Identity.IsAuthenticated)
            {
                OnPostAsync(); // Already logged in user ko home page pe bheje
            }
            return Page();
        }

        public async Task<JsonResult> OnPostSendOTPAsync([FromBody] InputModel request)
        {
            if (string.IsNullOrEmpty(request.Mobile) || request.Mobile.Length < 10)
            {
                return new JsonResult(new { success = false, message = "Invalid phone number" });
            }

            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl2 = $"{baseUrl}/api/Profile/GetpatientByMobilenumber?Mobilenumber={request.Mobile}";
            var PatientDetails = await _apiService.GetAsync<ProfileListItem>(apiUrl2, token);
            if (PatientDetails == null)
            {
                return new JsonResult(new { success = false, message = "PatientDetails API failed or mobile number not registered." });
            }
            if (!_otpService.CanSendOTP(request.Mobile))
            {
                return new JsonResult(new { success = false, message = "Maximum OTP attempts reached. Try again after 24 hours." });
            }

            string apiUrl = $"{baseUrl}/api/v1/Account/PatientportalSendAuthToken/{request.Mobile}.json";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new { phone = request.Mobile };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {

                var response = await _apiService.GetAsync<List<InputModel>>(apiUrl, token);
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
            string apiUrl2 = $"{baseUrl}/api/Profile/GetpatientByMobilenumber?Mobilenumber={request.Mobile}";
            var PatientDetails = await _apiService.GetAsync<ProfileListItem>(apiUrl2, token);
            var patient = PatientDetails; 
           
            string apiUrl = $"{baseUrl}/api/v1/Account/Patientportalverify-otp";
            var payload = new { mobile = request.Mobile, otp = request.OTP };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _apiService.PostAsync<InputModel, ApiResponse>(apiUrl, request, token);

            if (response.IsSucceeded)
            {
                _otpService.ClearOTPAttempts(request.Mobile); // OTP attempts reset

                var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, request.Mobile),
                new Claim("OTPVerified", "true")
            };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                string encryptedId = EncryptionHelper.EncryptId(patient?.Id ?? 0);

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
                return new JsonResult(new { success = false, message = response.Message });
            }
        }
        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync(); // Logout user
            return new JsonResult(new { success = true }); // Success response
        }

    }
    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

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
