using Innovura.CSharp.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Patientportal.AllApicall;
using Patientportal.Model;
using Patientportal.Pages.Account;
using Syncfusion.EJ2.Base;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Patientportal.Pages.Appointment
{
    [IgnoreAntiforgeryToken(Order = 2000)]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        //private readonly HttpClient _httpClient;
        private readonly HttpClient _httpClient;
        private readonly ApiService _apiService;
        private readonly IConfiguration _configuration;
        private readonly OTPService _otpService;
        private readonly OpsTokenService _opsTokenService;
        public AppointmentListItem AppoinmentData { get; set; }
        public List<Holidays> Holidays { get; set; } = new List<Holidays>();
        [FromQuery(Name = "selectedDateTime")]
        public DateTimeOffset SelectedDateTime { get; set; }
        public string? EjsDateTimePattern = "dd/MM/yyyy hh:mm:ss a";
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClientFactory, ApiService apiService, IConfiguration configuration, OTPService otpService, OpsTokenService opsTokenService)
        {
            _logger = logger;
            _httpClient = httpClientFactory;
            _apiService = apiService;
            _configuration = configuration;
            _otpService = otpService;
            _opsTokenService = opsTokenService;
        }
        public void OnGet()
        {
        }
        public async Task<JsonResult> OnPostSendOTPAsync([FromBody] InputModel request)
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            //string apiUrl2 = $"{baseUrl}/api/Profile/GetpatientByMobilenumber?Mobilenumber={request.Mobile}";
            //var PatientDetails = await _apiService.GetAsync<ProfileListItem>(apiUrl2, token);
            //if (string.IsNullOrEmpty(request.Mobile) || request.Mobile.Length < 10)
            //{
            //    return new JsonResult(new { success = false, message = "Invalid phone number" });
            //}
            //if (PatientDetails == null)
            //{
            //    return new JsonResult(new { success = false, message = "Mobile number not registered." });
            //}
            //if (!_otpService.CanSendOTP(request.Mobile))
            //{
            //    return new JsonResult(new { success = false, message = "Maximum OTP attempts reached. Try again after 24 hours." });
            //}

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

            string apiUrl = $"{baseUrl}/api/v1/Account/Patientportalverify-otp";
            var payload = new { mobile = request.Mobile, otp = request.OTP };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _apiService.PostAsync<InputModel, ApiResponse>(apiUrl, request, token);
            var Id = 1; 
            if (response.IsSucceeded)
            {
                _otpService.ClearOTPAttempts(request.Mobile); 

                var claims = new List<Claim>
      {
          new Claim(ClaimTypes.Name, request.Mobile),
          new Claim("OTPVerified", "true")
      };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                //string encryptedId = EncryptionHelper.EncryptId(Id ?? 0);

                return new JsonResult(new
                {
                    success = true,
                    message = "OTP Verified Successfully",
                });
            }
            else
            {
                return new JsonResult(new { success = false, message = response.Errors });
            }
        }
        public async Task<IActionResult> OnPostPirescheduleAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            AppointmentListItem viewModel = JSON.Deserialize<AppointmentListItem>(json);


            string apiUrl = $"{baseUrl}/api/v1/Appointment/AddAppointmentbyPatientPortal";
             string apiUrl5 = $"{baseUrl}/api/v1/Holiday/getHolidaysList";

            Holidays = await _apiService.GetAsync<List<Holidays>>(apiUrl5, token) ?? new List<Holidays>();
            var appointmentDate = viewModel.AppointmentStartTime.Value.ToString("dd/MM/yyyy");
            var isHoliday = Holidays.Any(h => h.StartDate.HasValue &&
                                              h.StartDate.Value.ToString("dd/MM/yyyy") == appointmentDate);

            if (isHoliday)
            {
                return new JsonResult(new { isSuccess = false, errorMessage = "Appointments cannot be scheduled on holidays." });
            }
            var apiHelper = new ApiService(_httpClient);
            var response = await _apiService.PostAsync<AppointmentListItem, ApiResponse>(apiUrl, viewModel, token);

            if (response != null && response.IsSuccess)
            {
                return new JsonResult(new { isSuccess = true, message = "Your change request has been submitted." });

            }
            else
            {
                return BadRequest("Failed to save patient details.");
            }
        }
    }
}
