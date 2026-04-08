using Innovura.CSharp.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Patientportal;
using Patientportal.AllApicall;
using Patientportal.Model;
using Patientportal.Pages.Account;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Syncfusion.EJ2.Base;
using System.Linq;

namespace Patientportal.Pages.Appointment
{
    [AllowAnonymous]
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
        public List<AppointmentListItem> Doctorblocktime { get; set; } = new List<AppointmentListItem>();
        public List<Leave> Leaves { get; set; } = new List<Leave>();
        [FromQuery(Name = "selectedDateTime")]
        public DateTimeOffset? SelectedDateTime { get; set; }

        [FromQuery(Name = "fromPatient")]
        public string? FromPatient { get; set; }

        /// <summary>Logged-in patient booking from the Patient Portal (full form). Set on each GET from query or cookie.</summary>
        public bool IsPatientPortalBooking { get; set; }

        /// <summary>Public / non–patient-portal booking (simplified three-field flow).</summary>
        public bool IsAnonymousBooking => !IsPatientPortalBooking;

        public ProfileListItem? PortalPatientProfile { get; set; }
        public string? LoggedInEncryptedPatientId { get; set; }

        /// <summary>Numeric profile id from <see cref="PatientPortalClaimTypes.EncryptedPatientId"/>; used when API profile <c>Id</c> is missing or zero.</summary>
        public long LoggedInProfileId { get; set; }

        public List<PatientRelationshipPortalItem> PatientRelationshipOptions { get; set; } = new();

        public IReadOnlyList<string> DependentGenderOptions { get; } = new[] { "Male", "Female", "Other" };

        /// <summary>Self + dependents for "Appointment for" dropdown (portal booking).</summary>
        public List<PortalBookingSubjectOption> PortalBookingSubjects { get; set; } = new();

        /// <summary>CamelCase JSON for <c>window.__portalBookingSubjects</c>.</summary>
        public string PortalBookingSubjectsJson { get; set; } = "[]";

        /// <summary>Add Dependent + dependents grid: only for signed-in patient-portal booking (not anonymous).</summary>
        public bool ShowPortalDependentSection =>
            User.Identity?.IsAuthenticated == true
            && IsPatientPortalBooking
            && !string.IsNullOrEmpty(LoggedInEncryptedPatientId);

        /// <summary>True when the logged-in portal profile has no name; booking form must collect display name for self.</summary>
        public bool RequirePortalDisplayName =>
            IsPatientPortalBooking
            && string.IsNullOrWhiteSpace(PortalPatientProfile?.Name);

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
        public async Task<IActionResult> OnGetAsync()
        {
            var hasSlotQuery = Request.Query.ContainsKey("selectedDateTime") || Request.Query.ContainsKey("SelectedDateTime");
            var encryptedId = User.FindFirstValue(PatientPortalClaimTypes.EncryptedPatientId);

            if (string.Equals(FromPatient, "1", StringComparison.Ordinal))
            {
                if (User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(encryptedId))
                    return RedirectToPage("/Account/Index");

                Response.Cookies.Append(PatientPortalCookies.AppointmentPortalBooking, "1", new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Secure = Request.IsHttps
                });

                if (SelectedDateTime.HasValue)
                    return RedirectToPage("/Appointment/Index", new { selectedDateTime = SelectedDateTime.Value.ToString("o", CultureInfo.InvariantCulture) });
                return RedirectToPage("/Appointment/Index");
            }

            IsPatientPortalBooking = string.Equals(
                Request.Cookies[PatientPortalCookies.AppointmentPortalBooking],
                "1",
                StringComparison.Ordinal);

            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(encryptedId))
            {
                LoggedInEncryptedPatientId = encryptedId;
                try
                {
                    LoggedInProfileId = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedId));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not decrypt EncryptedPatientId for LoggedInProfileId.");
                    LoggedInProfileId = 0;
                }
                if (!IsPatientPortalBooking && !hasSlotQuery)
                    return RedirectToPage("/Patient/Index", new { id = encryptedId });
            }
            else if (IsPatientPortalBooking)
                return RedirectToPage("/Account/Index");

            if (IsPatientPortalBooking && !string.IsNullOrEmpty(encryptedId))
            {
                string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
                string token = await _opsTokenService.GetTokenAsync();
                try
                {
                    var id = LoggedInProfileId > 0
                        ? LoggedInProfileId
                        : Convert.ToInt64(EncryptionHelper.DecryptId(encryptedId));
                    string apiUrl = $"{baseUrl}/api/Profile/getProfileforpatientportal?id={id}";
                    PortalPatientProfile = await _apiService.GetProfileForPatientPortalAsync(apiUrl, token, id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load profile for portal appointment booking.");
                }

                try
                {
                    string relUrl = $"{baseUrl.TrimEnd('/')}/api/v1/PatientRelationship/GetPatientRelationshipPatientportal";
                    var relList = await _apiService.GetAsync<List<PatientRelationshipPortalItem>>(relUrl, token);
                    if (relList != null && relList.Count > 0)
                    {
                        PatientRelationshipOptions = NormalizeRelationshipOptions(relList);
                    }
                    else if (relList != null)
                    {
                        PatientRelationshipOptions = new List<PatientRelationshipPortalItem>();
                    }
                    else
                    {
                        var asStrings = await _apiService.GetAsync<List<string>>(relUrl, token);
                        PatientRelationshipOptions = (asStrings ?? new List<string>())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => new PatientRelationshipPortalItem { Name = s.Trim() })
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load patient relationships for dependent modal.");
                    PatientRelationshipOptions = new List<PatientRelationshipPortalItem>();
                }

                await PopulatePortalBookingSubjectsAsync(baseUrl, token);
            }

            await LoadSchedulerDataAsync();

            // Match Patient portal: show user menu + Logout whenever signed in (Patient page leaves ViewData unset so header is always full).
            var showHeaderPatient = User.Identity?.IsAuthenticated == true;
            ViewData["ShowHeaderPatientDetails"] = showHeaderPatient;
            if (showHeaderPatient)
            {
                ViewData["PatientName"] = !string.IsNullOrWhiteSpace(PortalPatientProfile?.Name)
                    ? PortalPatientProfile!.Name
                    : User.Identity?.Name ?? string.Empty;
                var profileIdDisplay = PortalPatientProfile?.ProfileId;
                if (string.IsNullOrWhiteSpace(profileIdDisplay) && LoggedInProfileId > 0)
                    profileIdDisplay = LoggedInProfileId.ToString(CultureInfo.InvariantCulture);
                ViewData["ProfileId"] = profileIdDisplay;
            }

            return Page();
        }

        private async Task LoadSchedulerDataAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/v1/Holiday/getHolidaysList";
            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/GetAppointmentsByDoctor";
            string apiLeave = $"{baseUrl}/api/v1/Holiday/getLeaveList";
            Holidays = await _apiService.GetAsync<List<Holidays>>(apiUrl, token) ?? new List<Holidays>();
            Doctorblocktime = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token) ?? new List<AppointmentListItem>();
            Leaves = await _apiService.GetAsync<List<Leave>>(apiLeave, token) ?? new List<Leave>();
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
            if (response.IsSucceeded)
            {
                _otpService.ClearOTPAttempts(request.Mobile);

                string apiUrl2 = $"{baseUrl}/api/Profile/GetpatientByMobilenumber?Mobilenumber={request.Mobile}";
                var patient = await _apiService.GetPatientByMobilePreferIndependentAsync(apiUrl2, token);
                var hasExistingProfile = patient != null && patient.Id > 0;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, request.Mobile),
                    new Claim("OTPVerified", "true")
                };

                if (hasExistingProfile)
                {
                    var encryptedId = EncryptionHelper.EncryptId(patient!.Id);
                    claims.Add(new Claim(PatientPortalClaimTypes.EncryptedPatientId, encryptedId));
                    Response.Cookies.Append(PatientPortalCookies.AppointmentPortalBooking, "1", new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        Secure = Request.IsHttps
                    });
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                object? profileDto = null;
                if (hasExistingProfile)
                {
                    profileDto = new
                    {
                        id = patient!.Id,
                        name = patient.Name ?? string.Empty,
                        email = patient.Email ?? string.Empty,
                        gender = patient.Gender ?? string.Empty,
                        dob = patient.Dob,
                        age = patient.Age,
                        mobile = !string.IsNullOrWhiteSpace(patient.Mobile) ? patient.Mobile : request.Mobile
                    };
                }

                return new JsonResult(new
                {
                    success = true,
                    message = "OTP Verified Successfully",
                    hasExistingProfile,
                    profile = profileDto,
                    reloadForPortalBooking = hasExistingProfile
                });
            }
            else
            {
                return new JsonResult(new { success = false, message = response.Errors });
            }
        }
        public async Task<IActionResult> OnPostPirescheduleAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            string token = await _opsTokenService.GetTokenAsync();
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            AppointmentListItem viewModel = JSON.Deserialize<AppointmentListItem>(json);

            var jsonPatientIdToken = "absent";
            try
            {
                using var logDoc = JsonDocument.Parse(json);
                var root = logDoc.RootElement;
                if (root.TryGetProperty("PatientId", out var pEl))
                    jsonPatientIdToken = pEl.GetRawText();
                else if (root.TryGetProperty("patientId", out var p2))
                    jsonPatientIdToken = p2.GetRawText();
            }
            catch (JsonException)
            {
                jsonPatientIdToken = "(parse-error)";
            }

            long? parentNumericFromClaim = null;
            var encryptedBooking = User.FindFirstValue(PatientPortalClaimTypes.EncryptedPatientId);
            if (!string.IsNullOrEmpty(encryptedBooking))
            {
                try
                {
                    parentNumericFromClaim = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedBooking));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pireschedule: could not decrypt EncryptedPatientId for logging.");
                }
            }

            _logger.LogInformation(
                "Pireschedule booking: JSON PatientId token={JsonPatientId}, deserialized PatientId={PatientId}, PatientName={PatientName}, parentProfileIdFromClaim={ParentId}",
                jsonPatientIdToken,
                viewModel.PatientId,
                viewModel.PatientName ?? viewModel.Name,
                parentNumericFromClaim);

            TryPatchPatientIdFromJsonIfMissing(json, viewModel);
            if (viewModel.PatientId.HasValue && viewModel.PatientId.Value > 0)
            {
                _logger.LogInformation(
                    "Pireschedule booking: PatientId after JSON patch={PatientId}",
                    viewModel.PatientId);
            }

            if (User.Identity?.IsAuthenticated == true
                && !string.IsNullOrEmpty(encryptedBooking)
                && viewModel.PatientId.HasValue
                && viewModel.PatientId.Value > 0)
            {
                long parentProfileId;
                try
                {
                    parentProfileId = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedBooking));
                }
                catch
                {
                    return new JsonResult(new { isSuccess = false, errorMessage = "Invalid session." }) { StatusCode = 400 };
                }

                var pid = viewModel.PatientId.Value;
                if (pid != parentProfileId)
                {
                    string depUrl = $"{baseUrl.TrimEnd('/')}/api/DependentProfile/dependentList?dependentId={parentProfileId}";
                    _logger.LogInformation("dependentList (reschedule validation): dependentId={DependentId}, url={Url}", parentProfileId, depUrl);
                    var allowed = await _apiService.GetAsync<List<ProfileListItem>>(depUrl, token) ?? new List<ProfileListItem>();
                    if (!allowed.Any(d => d.Id == pid))
                    {
                        return new JsonResult(new { isSuccess = false, errorMessage = "You cannot book for this profile." }) { StatusCode = 403 };
                    }
                }
                else if (string.IsNullOrWhiteSpace(viewModel.Name))
                {
                    string? profileNameOnFile = null;
                    try
                    {
                        string profileUrl = $"{baseUrl.TrimEnd('/')}/api/Profile/getProfileforpatientportal?id={pid}";
                        var prof = await _apiService.GetProfileForPatientPortalAsync(profileUrl, token, pid);
                        profileNameOnFile = prof?.Name;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Pireschedule: could not load profile to validate display name for self-booking.");
                    }

                    if (string.IsNullOrWhiteSpace(profileNameOnFile))
                    {
                        return new JsonResult(new { isSuccess = false, errorMessage = "Patient name is required." })
                        {
                            StatusCode = 400
                        };
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(viewModel.Relation))
            {
                var rel = viewModel.Relation.Trim();
                viewModel.Comment = string.IsNullOrWhiteSpace(viewModel.Comment)
                    ? "Relation: " + rel
                    : "Relation: " + rel + "\n" + viewModel.Comment;
            }

            string? dobRaw = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Dob", out var dobEl))
                    dobRaw = dobEl.GetString();
                else if (root.TryGetProperty("dob", out var dobEl2))
                    dobRaw = dobEl2.GetString();
            }
            catch (JsonException)
            {
                // ignore; appointment flow continues without DOB patch
            }

            if (viewModel.PatientId.HasValue && viewModel.PatientId.Value > 0)
            {
                var profileUpdate = new ProfileListItem
                {
                    Id = viewModel.PatientId.Value,
                    Name = viewModel.Name,
                    Email = viewModel.Email,
                    Mobile = viewModel.Mobile,
                    Gender = viewModel.Gender,
                    Age = viewModel.Age
                };
                var parsedDob = ParsePortalDob(dobRaw);
                if (parsedDob.HasValue)
                    profileUpdate.Dob = parsedDob;

                var directPath = (_configuration["ApiSettings:PatientPortalDirectProfileUpdatePath"] ?? "").Trim();
                var useDirectSelfUpsert = ShouldUseDirectPatientPortalSelfUpsert(viewModel, encryptedBooking, directPath, User);

                ApiResponse? profileResponse = null;
                if (useDirectSelfUpsert)
                {
                    var nameTrimmed = viewModel.Name?.Trim();
                    if (!string.IsNullOrWhiteSpace(nameTrimmed))
                    {
                        string directUrl = $"{baseUrl.TrimEnd('/')}/{directPath.TrimStart('/')}";
                        var (directOk, directErr) = await TryUpdatePatientPortalNameAsync(
                            directUrl,
                            viewModel.PatientId.Value,
                            nameTrimmed,
                            token);
                        if (!directOk)
                        {
                            _logger.LogWarning(
                                "UpdatePatientPortalName failed ({Error}); not falling back to change-request queue for name.",
                                directErr ?? "(unknown)");
                            return new JsonResult(new
                            {
                                isSuccess = false,
                                errorMessage = directErr ?? "Could not save your profile name. Try again or contact the clinic."
                            });
                        }

                        profileResponse = new ApiResponse { IsSuccess = true };
                    }
                    else
                    {
                        string profileApiUrl = $"{baseUrl}/api/Profile/Addpatientportalchanges";
                        profileResponse = await _apiService.PostAsync<ProfileListItem, ApiResponse>(profileApiUrl, profileUpdate, token);
                    }
                }
                else
                {
                    string profileApiUrl = $"{baseUrl}/api/Profile/Addpatientportalchanges";
                    profileResponse = await _apiService.PostAsync<ProfileListItem, ApiResponse>(profileApiUrl, profileUpdate, token);
                }

                if (profileResponse == null || !profileResponse.IsSuccess)
                {
                    var err = profileResponse?.Message
                        ?? (profileResponse?.Errors != null && profileResponse.Errors.Length > 0
                            ? string.Join(" ", profileResponse.Errors)
                            : "Failed to update profile.");
                    return new JsonResult(new { isSuccess = false, errorMessage = err });
                }
            }

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

        private async Task PopulatePortalBookingSubjectsAsync(string baseUrl, string token)
        {
            PortalBookingSubjects = new List<PortalBookingSubjectOption>();
            var parentProfileId = LoggedInProfileId > 0
                ? LoggedInProfileId
                : (PortalPatientProfile?.Id ?? 0);
            if (parentProfileId <= 0)
            {
                RefreshPortalBookingSubjectsJson();
                return;
            }

            if (PortalPatientProfile?.Id > 0 && LoggedInProfileId > 0 && PortalPatientProfile.Id != LoggedInProfileId)
                _logger.LogWarning(
                    "Portal profile Id {ProfileApiId} differs from claim LoggedInProfileId {ClaimProfileId}; using claim for dependents.",
                    PortalPatientProfile.Id, LoggedInProfileId);

            var self = PortalPatientProfile;
            var selfAge = self?.Age ?? ComputeAgeFromDob(self?.Dob);
            var selfName = string.IsNullOrWhiteSpace(self?.Name) ? "Me" : self!.Name.Trim();
            PortalBookingSubjects.Add(new PortalBookingSubjectOption
            {
                ProfileId = parentProfileId,
                DisplayLabel = $"Self ({selfName})",
                Name = self?.Name,
                Age = selfAge,
                Gender = self?.Gender,
                RelationName = null,
                IsSelf = true
            });

            try
            {
                string depUrl = $"{baseUrl.TrimEnd('/')}/api/DependentProfile/dependentList?dependentId={parentProfileId}";
                _logger.LogInformation("dependentList (portal booking subjects): dependentId={DependentId}, url={Url}", parentProfileId, depUrl);
                var deps = await _apiService.GetAsync<List<ProfileListItem>>(depUrl, token) ?? new List<ProfileListItem>();
                foreach (var d in deps.Where(x => x.Id > 0).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var nm = string.IsNullOrWhiteSpace(d.Name) ? $"Dependent #{d.Id}" : d.Name.Trim();
                    var age = d.Age ?? ComputeAgeFromDob(d.Dob);
                    PortalBookingSubjects.Add(new PortalBookingSubjectOption
                    {
                        ProfileId = d.Id,
                        DisplayLabel = nm,
                        Name = d.Name,
                        Age = age,
                        Gender = d.Gender,
                        RelationName = d.PatientRelationshipName,
                        IsSelf = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load dependents for appointment-for dropdown.");
            }

            RefreshPortalBookingSubjectsJson();
        }

        private void RefreshPortalBookingSubjectsJson()
        {
            PortalBookingSubjectsJson = JsonSerializer.Serialize(PortalBookingSubjects, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private static int? ComputeAgeFromDob(DateTimeOffset? dob)
        {
            if (!dob.HasValue) return null;
            var today = DateTime.Today;
            var birth = dob.Value.Date;
            var age = today.Year - birth.Year;
            if (birth > today.AddYears(-age)) age--;
            if (age < 0 || age > 150) return null;
            return age;
        }

        private static List<PatientRelationshipPortalItem> NormalizeRelationshipOptions(List<PatientRelationshipPortalItem>? raw)
        {
            if (raw == null || raw.Count == 0)
                return new List<PatientRelationshipPortalItem>();
            return raw
                .Select(r => new PatientRelationshipPortalItem
                {
                    Id = r.Id,
                    Name = r.Label
                })
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .GroupBy(r => r.Name!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Newtonsoft may not bind <see cref="AppointmentListItem.PatientId"/> from the client payload; read raw JSON so the selected "Appointment for" id is not lost.
        /// </summary>
        private static void TryPatchPatientIdFromJsonIfMissing(string jsonBody, AppointmentListItem? vm)
        {
            if (vm == null || string.IsNullOrWhiteSpace(jsonBody)) return;
            if (vm.PatientId.HasValue && vm.PatientId.Value > 0) return;
            try
            {
                using var doc = JsonDocument.Parse(jsonBody);
                var root = doc.RootElement;
                if (!root.TryGetProperty("PatientId", out var el) && !root.TryGetProperty("patientId", out el))
                    return;
                if (el.ValueKind == JsonValueKind.Number)
                {
                    if (el.TryGetInt64(out var lid) && lid > 0)
                        vm.PatientId = lid;
                }
                else if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                        vm.PatientId = parsed;
                }
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        private static DateTimeOffset? ParsePortalDob(string? dobRaw)
        {
            if (string.IsNullOrWhiteSpace(dobRaw))
                return null;
            var trimmed = dobRaw.Trim();
            var formats = new[] { "M/d/yyyy", "MM/dd/yyyy", "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return new DateTimeOffset(dt);
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return new DateTimeOffset(dt);
            return null;
        }

        /// <summary>
        /// When the patient books for <b>self</b> (not a dependent), persist display name via
        /// <c>POST api/v1/Profile/UpdatePatientPortalName</c> instead of <c>Addpatientportalchanges</c> when a name is supplied.
        /// </summary>
        private static bool ShouldUseDirectPatientPortalSelfUpsert(
            AppointmentListItem viewModel,
            string? encryptedBooking,
            string directPath,
            ClaimsPrincipal? user)
        {
            if (string.IsNullOrWhiteSpace(directPath))
                return false;
            if (user?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(encryptedBooking))
                return false;
            if (!viewModel.PatientId.HasValue || viewModel.PatientId.Value <= 0)
                return false;
            long parentProfileId;
            try
            {
                parentProfileId = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedBooking));
            }
            catch
            {
                return false;
            }

            return viewModel.PatientId.Value == parentProfileId;
        }

        private async Task<(bool Success, string? ErrorMessage)> TryUpdatePatientPortalNameAsync(
            string absoluteUrl,
            long profileId,
            string nameTrimmed,
            string token)
        {
            var body = new UpdatePatientPortalNameRequest
            {
                ProfileId = profileId,
                Name = nameTrimmed
            };
            var json = JsonSerializer.Serialize(body);

            using var req = new HttpRequestMessage(HttpMethod.Post, absoluteUrl);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage resp;
            try
            {
                resp = await _httpClient.SendAsync(req);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdatePatientPortalName: HTTP call failed.");
                return (false, ex.Message);
            }

            var respText = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                var shortBody = respText.Length > 500 ? respText[..500] + "…" : respText;
                return (false, $"HTTP {(int)resp.StatusCode}: {shortBody}");
            }

            if (JsonResponseIndicatesExplicitFailure(respText, out var apiMsg))
                return (false, apiMsg);

            return (true, null);
        }

        private static bool JsonResponseIndicatesExplicitFailure(string? json, out string message)
        {
            message = "";
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                foreach (var prop in new[] { "isSuccess", "IsSuccess", "success", "Success" })
                {
                    if (!r.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.False)
                        continue;
                    message = ExtractJsonErrorMessage(r);
                    return true;
                }
            }
            catch (JsonException)
            {
                // treat as success
            }

            return false;
        }

        private static string ExtractJsonErrorMessage(JsonElement root)
        {
            foreach (var prop in new[] { "errorMessage", "ErrorMessage", "message", "Message" })
            {
                if (root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s!;
                }
            }

            if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
            {
                var parts = errs.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (parts.Count > 0)
                    return string.Join(" ", parts!);
            }

            return "Update was rejected.";
        }

        /// <summary>
        /// UrlAdaptor may send a query where <c>&amp;</c> was not decoded, producing key <c>amp;profileId</c> instead of <c>profileId</c>.
        /// Model binding then leaves <paramref name="modelBoundProfileId"/> at 0 while the raw string still contains <c>profileId=…</c>.
        /// </summary>
        private static long ResolvePortalDependentsProfileIdFromRequest(HttpRequest request, long modelBoundProfileId)
        {
            if (modelBoundProfileId > 0)
                return modelBoundProfileId;
            if (request.Query.TryGetValue("profileId", out var qv) && long.TryParse(qv.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) && q > 0)
                return q;
            if (request.Query.TryGetValue("amp;profileId", out var av) && long.TryParse(av.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) && a > 0)
                return a;
            var qs = request.QueryString.Value;
            if (string.IsNullOrEmpty(qs))
                return 0;
            var m = Regex.Match(qs, @"profileId=(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) && r > 0)
                return r;
            return 0;
        }

        private static JsonResult PortalDependentsEmptyJson() =>
            new JsonResult(new { result = new List<object>(), count = 0 });

        /// <summary>Shared: authorize portal parent and GET dependentList (same rows as <see cref="OnPostPortalDependentsAsync"/>).</summary>
        private async Task<(List<ProfileListItem> List, JsonResult? Deny)> TryLoadPortalDependentsFromApiAsync(long modelBoundProfileId)
        {
            var effectiveProfileId = ResolvePortalDependentsProfileIdFromRequest(Request, modelBoundProfileId);

            if (User.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("GridPortalDependents: user not authenticated; returning no rows.");
                return (new List<ProfileListItem>(), PortalDependentsEmptyJson());
            }
            var encryptedId = User.FindFirstValue(PatientPortalClaimTypes.EncryptedPatientId);
            if (string.IsNullOrEmpty(encryptedId))
            {
                _logger.LogWarning("GridPortalDependents: no EncryptedPatientId claim; returning no rows.");
                return (new List<ProfileListItem>(), PortalDependentsEmptyJson());
            }
            long expectedProfileId;
            try
            {
                expectedProfileId = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GridPortalDependents: could not decrypt EncryptedPatientId; returning no rows.");
                return (new List<ProfileListItem>(), PortalDependentsEmptyJson());
            }
            if (effectiveProfileId <= 0 || effectiveProfileId != expectedProfileId)
            {
                _logger.LogWarning(
                    "GridPortalDependents: not calling backend API — resolved profileId={ResolvedProfileId} (modelBound={ModelBound}) must match session profile id={ExpectedProfileId}",
                    effectiveProfileId, modelBoundProfileId, expectedProfileId);
                return (new List<ProfileListItem>(), PortalDependentsEmptyJson());
            }

            string baseUrl = (_configuration["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/DependentProfile/dependentList?dependentId={effectiveProfileId}";
            _logger.LogInformation(
                "GridPortalDependents: loading rows from backend API — GET {DependentListUrl}",
                apiUrl);
            var (list, rawBody, httpStatus) = await _apiService.GetAsyncWithRawBody<List<ProfileListItem>>(apiUrl, token);
            list ??= new List<ProfileListItem>();
            const int maxLogChars = 12000;
            var bodyForLog = string.IsNullOrEmpty(rawBody)
                ? "(empty body)"
                : rawBody.Length <= maxLogChars
                    ? rawBody
                    : rawBody.Substring(0, maxLogChars) + $"… [truncated, totalLength={rawBody.Length}]";
            if (httpStatus == HttpStatusCode.OK)
            {
                _logger.LogInformation(
                    "GridPortalDependents: dependentList response HTTP {Status} — raw body: {RawBody}",
                    (int)httpStatus, bodyForLog);
                _logger.LogInformation(
                    "GridPortalDependents: dependentList deserialized rowCount={Count} (url={Url}, profileId={ProfileId})",
                    list.Count, apiUrl, effectiveProfileId);
            }
            else
            {
                _logger.LogWarning(
                    "GridPortalDependents: dependentList response HTTP {Status} — raw body: {RawBody}",
                    (int)httpStatus, bodyForLog);
            }

            return (list, null);
        }

        /// <summary>Full dependent list for mobile card UI (no paging / DataManager ops).</summary>
        public async Task<JsonResult> OnPostPortalDependentsCardAsync([FromQuery] long profileId)
        {
            var effectiveProfileId = ResolvePortalDependentsProfileIdFromRequest(Request, profileId);
            _logger.LogInformation(
                "PortalDependentsCard: mobile list POST {Path}{Query} (modelBound profileId={ModelBound}, resolved profileId={Resolved})",
                Request.Path, Request.QueryString, profileId, effectiveProfileId);

            var (list, deny) = await TryLoadPortalDependentsFromApiAsync(profileId);
            if (deny != null)
                return deny;

            var sorted = list
                .OrderByDescending(x => x.CreatedOn ?? DateTimeOffset.MinValue)
                .ToList();
            return new JsonResult(new { result = sorted, count = sorted.Count });
        }

        /// <summary>Syncfusion UrlAdaptor: dependents linked to the logged-in patient profile (same source as panel I-icon grid).</summary>
        public async Task<JsonResult> OnPostPortalDependentsAsync([FromBody] DataManagerRequest dm, [FromQuery] long profileId)
        {
            var effectiveProfileId = ResolvePortalDependentsProfileIdFromRequest(Request, profileId);
            var pageEndpoint = $"{Request.Path}{Request.QueryString}";
            _logger.LogInformation(
                "GridPortalDependents (Name/Age/Gender/Relation): Syncfusion posts to this app first — POST {PageEndpoint} (modelBound profileId={ModelBoundProfileId}, resolved profileId={ResolvedProfileId})",
                pageEndpoint, profileId, effectiveProfileId);

            if (dm == null)
            {
                _logger.LogWarning("GridPortalDependents: empty DataManagerRequest; returning no rows.");
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }

            var (list, deny) = await TryLoadPortalDependentsFromApiAsync(profileId);
            if (deny != null)
                return deny;

            IEnumerable<object> data = list
                .OrderByDescending(x => x.CreatedOn ?? DateTimeOffset.MinValue)
                .Cast<object>();
            int count = list.Count;
            var dataOperations = new DataOperations();

            if (dm.Where != null && dm.Where.Count > 0)
            {
                data = dataOperations.PerformFiltering(data, dm.Where, "and");
                count = data.Count();
            }

            if (dm.Search != null && dm.Search.Count > 0)
            {
                data = dataOperations.PerformSearching(data, dm.Search);
                count = data.Count();
            }

            if (dm.Sorted != null && dm.Sorted.Count > 0)
                data = dataOperations.PerformSorting(data, dm.Sorted);

            if (dm.Skip != 0)
                data = dataOperations.PerformSkip(data, dm.Skip);
            if (dm.Take != 0)
                data = dataOperations.PerformTake(data, dm.Take);

            return new JsonResult(new { result = data, count });
        }

        public async Task<IActionResult> OnPostUpsertPortalDependentAsync([FromBody] UpsertPortalDependentInput? input)
        {
            if (input == null)
                return new JsonResult(new { success = false, message = "Invalid request." }) { StatusCode = 400 };
            if (User.Identity?.IsAuthenticated != true)
                return new JsonResult(new { success = false, message = "Unauthorized." }) { StatusCode = 401 };

            var encryptedId = User.FindFirstValue(PatientPortalClaimTypes.EncryptedPatientId);
            if (string.IsNullOrEmpty(encryptedId))
                return new JsonResult(new { success = false, message = "Unauthorized." }) { StatusCode = 401 };

            long parentProfileId;
            try
            {
                parentProfileId = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedId));
            }
            catch
            {
                return new JsonResult(new { success = false, message = "Invalid session." }) { StatusCode = 400 };
            }

            if (parentProfileId <= 0)
                return new JsonResult(new { success = false, message = "Parent profile not found." }) { StatusCode = 400 };

            var name = input.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                return new JsonResult(new { success = false, message = "Name is required." }) { StatusCode = 400 };
            if (input.Age < 1 || input.Age > 99)
                return new JsonResult(new { success = false, message = "Enter a valid age (1–99)." }) { StatusCode = 400 };
            if (input.PatientRelationShipId <= 0)
                return new JsonResult(new { success = false, message = "Relation is required." }) { StatusCode = 400 };
            var gender = input.Gender?.Trim();
            if (string.IsNullOrEmpty(gender))
                return new JsonResult(new { success = false, message = "Gender is required." }) { StatusCode = 400 };

            string baseUrl = (_configuration["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/v1/Profile/UpsertPatientPortalDependent";

            var body = new UpsertPatientPortalDependentApiBody
            {
                ParentProfileId = parentProfileId,
                Name = name,
                Age = input.Age,
                PatientRelationShipId = input.PatientRelationShipId,
                Gender = gender,
                Id = input.DependentProfileId.HasValue && input.DependentProfileId.Value > 0
                    ? input.DependentProfileId.Value
                    : null
            };

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage resp;
            try
            {
                resp = await _httpClient.SendAsync(req);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpsertPatientPortalDependent HTTP failed.");
                return new JsonResult(new { success = false, message = "Could not reach server. Try again." }) { StatusCode = 502 };
            }

            var respText = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                long? dependentId = null;
                try
                {
                    using var doc = JsonDocument.Parse(respText);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var idEl))
                    {
                        if (idEl.ValueKind == JsonValueKind.Number)
                            dependentId = idEl.GetInt64();
                    }
                }
                catch (JsonException)
                {
                    // ignore parse errors; still success
                }

                long? returnedId = dependentId;
                if ((!returnedId.HasValue || returnedId.Value <= 0)
                    && input.DependentProfileId.HasValue && input.DependentProfileId.Value > 0)
                    returnedId = input.DependentProfileId.Value;

                return new JsonResult(new { success = true, id = returnedId, message = "Dependent saved." });
            }

            string errMsg = "Could not save dependent.";
            try
            {
                using var doc = JsonDocument.Parse(respText);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    errMsg = m.GetString() ?? errMsg;
            }
            catch (JsonException)
            {
                if (!string.IsNullOrWhiteSpace(respText) && respText.Length < 400)
                    errMsg = respText;
            }

            return new JsonResult(new { success = false, message = errMsg }) { StatusCode = (int)resp.StatusCode };
        }
    }
}
