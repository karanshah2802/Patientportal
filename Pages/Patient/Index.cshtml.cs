using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Patientportal.AllApicall;
using Syncfusion.EJ2.Base;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.Json;
using Innovura.CSharp.Core;
using System.ComponentModel.DataAnnotations;
using Patientportal.Model;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics.Metrics;
using System.Linq;
using Patientportal;

namespace Patientportal.Pages.Patient
{
    [Authorize]
    [IgnoreAntiforgeryToken(Order = 2000)]  
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        //private readonly HttpClient _httpClient;
        private readonly HttpClient _httpClient;
        private readonly ApiService _apiService;
        private readonly IConfiguration _configuration;
        private readonly OpsTokenService _opsTokenService;
        [FromQuery(Name = "id")]
        public long? Id { get; set; }
        public ProfileListItem PatientData { get; set; }
        public List<Country> CountryLists { get; set; }
        public AppointmentListItem AppoinmentData { get; set; }
        public string? EjsDateTimePattern = "dd/MM/yyyy hh:mm:ss a";
        public IEnumerable<State> StateLists { get; set; }
        public IEnumerable<City> CityLists { get; set; }
        public IEnumerable<Pincode> Pincodes { get; set; }
        public List<string> ChangeRequests { get; set; } = new List<string>();
        public List<PatientRelationshipPortalItem> PatientRelationshipOptions { get; set; } = new();
        public IReadOnlyList<string> DependentGenderOptions { get; } = new[] { "Male", "Female", "Other" };
        /// <summary>Numeric profile id from the signed-in patient claim.</summary>
        public long LoggedInProfileId { get; set; }
        /// <summary>Dependents section: only when the page profile matches the signed-in portal user.</summary>
        public bool ShowPortalDependentSection =>
            Id.HasValue && Id.Value > 0 && LoggedInProfileId > 0 && Id.Value == LoggedInProfileId;
        public List<AppointmentListItem> Doctorblocktime { get; set; } = new List<AppointmentListItem>();
        public List<Holidays> Holidays { get; set; } = new List<Holidays>();
        public List<Leave> Leaves { get; set; } = new List<Leave>();
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClientFactory, ApiService apiService, IConfiguration configuration, OpsTokenService opsTokenService)
        {
            _logger = logger;
            _httpClient = httpClientFactory;
            _apiService = apiService;
            _configuration = configuration;
            _opsTokenService = opsTokenService;
        }
        public async Task<JsonResult> OnPostAppointmentView([FromBody] DataManagerRequest dm)
        {
            BindPatientIdFromRequest();
            if (dm == null)
            {
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }
            if (!Id.HasValue || Id.Value <= 0)
            {
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }

            var sorted = await LoadMergedSortedPortalAppointmentsAsync(Id.Value);
            return ToDataManagerJsonResult(sorted, dm);
        }


        public async Task<JsonResult> OnPostAppointmentViewCard([FromBody] DataManagerRequest dm)
        {
            BindPatientIdFromRequest();
            if (!Id.HasValue || Id.Value <= 0)
            {
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }

            if (dm == null)
            {
                dm = new DataManagerRequest { Skip = 0, Take = 15 };
            }

            var sorted = await LoadMergedSortedPortalAppointmentsAsync(Id.Value);
            return ToDataManagerJsonResult(sorted, dm);
        }

        private static JsonResult ToDataManagerJsonResult(List<AppointmentListItem> sortedList, DataManagerRequest dm)
        {
            IEnumerable<object> data = sortedList.Cast<object>();
            int count = sortedList.Count;

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
            {
                data = dataOperations.PerformSorting(data, dm.Sorted);
            }

            if (dm.Skip != 0)
            {
                data = dataOperations.PerformSkip(data, dm.Skip);
            }

            if (dm.Take != 0)
            {
                data = dataOperations.PerformTake(data, dm.Take);
            }

            return new JsonResult(new { result = data, count });
        }

        private async Task<List<AppointmentListItem>> LoadMergedSortedPortalAppointmentsAsync(long portalProfileId)
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            string token = await _opsTokenService.GetTokenAsync();
            var subjectIds = await GetParentAndDependentProfileIdsAsync(portalProfileId, baseUrl, token);

            var loadTasks = subjectIds.Select(async sid =>
            {
                string apiUrl = $"{baseUrl}/api/v1/Appointment/getPatientByAppointment?id={sid}";
                string apiUrl2 = $"{baseUrl}/api/v1/Appointment/getPatientByAppointmentRequest?id={sid}";
                var appointments = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl, token) ?? new List<AppointmentListItem>();
                var appointmentsRequest = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token) ?? new List<AppointmentListItem>();
                EnsurePatientIdForSubject(appointments, sid);
                EnsurePatientIdForSubject(appointmentsRequest, sid);
                return (appointments, appointmentsRequest);
            });

            var pairs = await Task.WhenAll(loadTasks);

            var allBooked = new List<AppointmentListItem>();
            var allReq = new List<AppointmentListItem>();
            foreach (var (a, r) in pairs)
            {
                allBooked.AddRange(a);
                allReq.AddRange(r);
            }

            var dedupBooked = DedupeAppointmentsById(allBooked);
            var dedupReq = DedupeAppointmentsById(allReq);

            NormalizePortalBookedAppointmentsForGrid(dedupBooked);
            NormalizePortalAppointmentRequestsForGrid(dedupReq);

            var sorted = dedupReq.Concat(dedupBooked).OrderByDescending(x => x.CreatedOn).ToList();
            await EnrichBookedForPatientNamesAsync(portalProfileId, sorted);
            return sorted;
        }

        private async Task<List<long>> GetParentAndDependentProfileIdsAsync(long parentProfileId, string baseUrl, string token)
        {
            var ids = new List<long> { parentProfileId };
            try
            {
                var depUrl = $"{baseUrl.TrimEnd('/')}/api/DependentProfile/dependentList?dependentId={parentProfileId}";
                var deps = await _apiService.GetAsync<List<ProfileListItem>>(depUrl, token) ?? new List<ProfileListItem>();
                foreach (var d in deps.Where(x => x.Id > 0))
                {
                    if (!ids.Contains(d.Id))
                        ids.Add(d.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load dependents for combined portal appointment list.");
            }

            return ids;
        }

        private static List<AppointmentListItem> DedupeAppointmentsById(IEnumerable<AppointmentListItem> items)
        {
            var list = items.ToList();
            var withId = list
                .Where(x => x.Id > 0)
                .GroupBy(x => x.Id)
                .Select(g => g
                    .OrderByDescending(x => x.ModifiedOn)
                    .ThenByDescending(x => x.CreatedOn)
                    .First());
            var withoutId = list.Where(x => x.Id <= 0);
            return withId.Concat(withoutId).ToList();
        }

        private static void EnsurePatientIdForSubject(IEnumerable<AppointmentListItem> rows, long subjectProfileId)
        {
            foreach (var row in rows)
            {
                if (!row.PatientId.HasValue || row.PatientId.Value <= 0)
                    row.PatientId = subjectProfileId;
            }
        }

        private static void NormalizePortalBookedAppointmentsForGrid(IEnumerable<AppointmentListItem> appointments)
        {
            foreach (var appointment in appointments)
            {
                if (appointment.StatusName == "Rescheduled" || appointment.StatusName == "Booked")
                    appointment.StatusName = "Booked";
                if (appointment.StatusName == "Released" || appointment.StatusName == "Completed" ||
                    appointment.StatusName == "Converted To Appointment")
                    appointment.StatusName = "Completed";
                if (appointment.AppoinmentType == "Consultation")
                    appointment.AppoinmentType = "Dr. Sejal In-person Consultation";
                if (appointment.StatusName == "Confirmed" || appointment.StatusName == "ReverseCheckin")
                    appointment.StatusName = "Confirmed";
                if (appointment.StatusName == "Checked-In" || appointment.StatusName == "ReverseCheckout")
                    appointment.StatusName = "Checked-In";
                if (appointment.StatusName == "Walked-Out")
                    appointment.StatusName = "Walked-Out";
            }
        }

        private static void NormalizePortalAppointmentRequestsForGrid(IEnumerable<AppointmentListItem> appointmentsRequest)
        {
            foreach (var appointmentes in appointmentsRequest)
            {
                if (appointmentes.StatusName == "Reschedule")
                    appointmentes.StatusName = "Booked";
                if (appointmentes.StatusName == "Converted To Appointment")
                    appointmentes.StatusName = "Completed";
                if (appointmentes.AppoinmentType == "Consultation")
                    appointmentes.AppoinmentType = "Dr. Sejal In-person Consultation";
                if (appointmentes.AppoinmentType == "Appointment Request for Online Consultation")
                    appointmentes.AppoinmentType = "Appointment Request for Online Consultation";
            }
        }

        private async Task EnrichBookedForPatientNamesAsync(long portalProfileId, List<AppointmentListItem> items)
        {
            if (items.Count == 0 || portalProfileId <= 0)
                return;

            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            string token = await _opsTokenService.GetTokenAsync();

            var nameByProfileId = new Dictionary<long, string>();

            try
            {
                var profileUrl = $"{baseUrl.TrimEnd('/')}/api/Profile/getProfileforpatientportal?id={portalProfileId}";
                _logger.LogInformation(
                    "Patient portal profile (enrich appointment names): portalProfileId={PortalProfileId}, url={Url}",
                    portalProfileId, profileUrl);
                var patientProfile = await _apiService.GetProfileForPatientPortalAsync(profileUrl, token, portalProfileId);
                var selfName = patientProfile?.Name?.Trim();
                _logger.LogInformation(
                    "Patient portal profile response: portalProfileId={PortalProfileId}, profileNull={IsNull}, id={Id}, hasName={HasName}",
                    portalProfileId, patientProfile == null, patientProfile?.Id ?? 0, !string.IsNullOrEmpty(selfName));
                if (!string.IsNullOrEmpty(selfName))
                    nameByProfileId[portalProfileId] = selfName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load profile for appointment grid patient names.");
            }

            try
            {
                var depUrl = $"{baseUrl.TrimEnd('/')}/api/DependentProfile/dependentList?dependentId={portalProfileId}";
                var deps = await _apiService.GetAsync<List<ProfileListItem>>(depUrl, token) ?? new List<ProfileListItem>();
                foreach (var d in deps.Where(x => x.Id > 0))
                {
                    var nm = string.IsNullOrWhiteSpace(d.Name) ? $"Dependent #{d.Id}" : d.Name.Trim();
                    nameByProfileId[d.Id] = nm;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load dependents for appointment grid patient names.");
            }

            foreach (var a in items)
            {
                var subjectId = a.PatientId ?? portalProfileId;
                if (nameByProfileId.TryGetValue(subjectId, out var resolved))
                    a.BookedForPatientName = resolved;
                else if (!string.IsNullOrWhiteSpace(a.PatientName))
                    a.BookedForPatientName = a.PatientName.Trim();
                else if (!string.IsNullOrWhiteSpace(a.Name))
                    a.BookedForPatientName = a.Name.Trim();
            }
        }

        private void BindPatientIdFromRequest()
        {
            var raw = Request.Query["id"].FirstOrDefault() ?? Request.Query["Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(raw))
                return;
            if (long.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var numericId))
            {
                Id = numericId;
                return;
            }
            try
            {
                Id = Convert.ToInt64(EncryptionHelper.DecryptId(raw));
            }
            catch
            {
                // Keep Id from model binding if decryption fails.
            }
        }

        public async Task<IActionResult> OnGetStatesAsync(int countryId)
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl4 = $"{baseUrl}/api/v1/CountryStateCity/GetStatesByCountry?countryId={countryId}";
           
            var states = await _apiService.GetAsync<List<State>>(apiUrl4, token) ?? new List<State>();
            return new JsonResult(states) { StatusCode = 200 };
        }
        public async Task<IActionResult> OnGetCitiesAsync(int stateId)
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/v1/CountryStateCity/GetCitiesByState?stateId={stateId}";
            

            var citiesdp = await _apiService.GetAsync<List<City>>(apiUrl, token) ?? new List<City>();
            return new JsonResult(citiesdp) { StatusCode = 200 };
        }
        public async Task<IActionResult> OnGetPincodeforleadDataSourceAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/v1/Pincode/getpincode";

            var pincodes = await _apiService.GetAsync<List<Pincode>>(apiUrl, token) ?? new List<Pincode>();
            return new JsonResult(pincodes) { StatusCode = 200 };
        }
        public async Task<IActionResult> OnGetAsync()
        {

            var queryId = Request.Query["id"];
            if (queryId.Any())
            {
                Id = Convert.ToInt64(EncryptionHelper.DecryptId(queryId));
            }
            else
            {
                return RedirectToPage("/Account/Index"); // Ya phir Redirect("/Login");
            }
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/Profile/getProfileforpatientportal?id={Id}";
            string apiUrl2 = $"{baseUrl}/api/Profile/getDetailsChangesbyId?id={Id}";

            string apiUrl3 = $"{baseUrl}/api/v1/Appointment/GetAppointmentsPortalByDoctor?id={Id}";
            string apiUrl4 = $"{baseUrl}/api/v1/Appointment/GetInvoiceAmount?id={Id}";
            string apiUrl5 = $"{baseUrl}/api/v1/Holiday/getHolidaysList";
            string apileavlist = $"{baseUrl}/api/v1/Holiday/getLeaveList";
            string apiUrl6 = $"{baseUrl}/api/v1/CountryStateCity/GetCountry";
            string token1 = "yJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiIyIiwic3ViIjoiMiIsInVuaXF1ZV9uYW1lIjoiSmFscGEiLCJlbWFpbCI6ImphbHBhQGludXJza24uaW4iLCJyb2xlIjoiRnJvbnREZXNrUHJlU2FsZXMiLCJuYmYiOjE3NTA0MDE5ODUsImV4cCI6MTc1MTAwNjc4NSwiaWF0IjoxNzUwNDAxOTg1LCJpc3MiOiJDb25uZXR3ZWxsQ0lTIiwiYXVkIjoiQ29ubmV0d2VsbENJUyJ9.xS0iiGb41T-V0kx0OXK2oOMAO5B_a-thQFx-bfHruOMp8QQUuEGqHPY04ZzjVBSJceZQq5qmokgFNbkotSSrOw";
            string apiUrl7 = $"{baseUrl}/api/v1/Pincode/getallpincode";


            // API Response Fetch karein
            var invoiceResponse = await _apiService.GetAsync<InvoiceResponse>(apiUrl4, token);
            Doctorblocktime = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl3, token) ?? new List<AppointmentListItem>();
            Holidays = await _apiService.GetAsync<List<Holidays>>(apiUrl5, token) ?? new List<Holidays>();
            Leaves = await _apiService.GetAsync<List<Leave>>(apileavlist, token) ?? new List<Leave>();
            CountryLists = await _apiService.GetAsync<List<Country>>(apiUrl6, token) ?? new List<Country>();
            Pincodes = await _apiService.GetAsync<List<Pincode>>(apiUrl7, token1) ?? new List<Pincode>();
            //if (Doctorblocktime != null && Doctorblocktime.Count > 0 )
            //{
            //    foreach (var appointment in Doctorblocktime)
            //    {
            //        if (appointment.AppointmentStartTime != null)
            //        {
            //            appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
            //        }

            //        if (appointment.AppointmentEndDateTime != null)
            //        {
            //            appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
            //        }
            //    }
            //}

            PatientData = await _apiService.GetProfileForPatientPortalAsync(apiUrl, token, Id ?? 0) ?? new ProfileListItem();


            if (PatientData != null)
            {
                ViewData["PatientName"] = PatientData.Name;
            }
            if (PatientData != null)
            {
                ViewData["Invoice"] = PatientData?.UnPaidValue;
            }
             if (PatientData != null)
            {
                ViewData["ProfileId"] = PatientData?.ProfileId;
            }

            ChangeRequests = await _apiService.GetAsync<List<string>>(apiUrl2, token) ?? new List<string>();

            var encryptedClaim = User?.Claims.FirstOrDefault(c => c.Type == PatientPortalClaimTypes.EncryptedPatientId)?.Value;
            try
            {
                if (!string.IsNullOrEmpty(encryptedClaim))
                    LoggedInProfileId = Convert.ToInt64(EncryptionHelper.DecryptId(encryptedClaim));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not decrypt EncryptedPatientId for LoggedInProfileId on Patient page.");
                LoggedInProfileId = 0;
            }

            if (ShowPortalDependentSection)
            {
                try
                {
                    string relUrl = $"{baseUrl.TrimEnd('/')}/api/v1/PatientRelationship/GetPatientRelationshipPatientportal";
                    var relList = await _apiService.GetAsync<List<PatientRelationshipPortalItem>>(relUrl, token);
                    if (relList != null && relList.Count > 0)
                        PatientRelationshipOptions = NormalizeRelationshipOptions(relList);
                    else if (relList != null)
                        PatientRelationshipOptions = new List<PatientRelationshipPortalItem>();
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
                    _logger.LogWarning(ex, "Could not load patient relationships for dependents modal on Patient page.");
                    PatientRelationshipOptions = new List<PatientRelationshipPortalItem>();
                }
            }

            return Page();
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

        public async Task<IActionResult> OnPostSavePatientAsync()
        {
            try
            {

                string baseUrl = _configuration["ApiSettings:BaseUrl"];
                string token = await _opsTokenService.GetTokenAsync();
                using var reader = new StreamReader(HttpContext.Request.Body);
                var json = await reader.ReadToEndAsync();
                ProfileListItem viewModel = JSON.Deserialize<ProfileListItem>(json);


                string apiUrl = $"{baseUrl}/api/Profile/Addpatientportalchanges";
             
                var apiHelper = new ApiService(_httpClient);
                var response = await _apiService.PostAsync<ProfileListItem, ApiResponse>(apiUrl, viewModel, token);

                if (response != null && response.IsSuccess)
                {
                    return new JsonResult(new { isSuccess = true,  message = "Your change request has been submitted." });

                }
                else
                {
                    return BadRequest("Failed to save patient details.");
                }
            }
            catch (Exception ex)
            {
                throw new NotImplementedException();   
            }
        } 
        public async Task<IActionResult> OnPostPirescheduleAsync()
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            AppointmentListItem viewModel = JSON.Deserialize<AppointmentListItem>(json);

            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/v1/Appointment/viewAppointmentButtonPatientPortal";
          
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
        public async Task<IActionResult> OnPostAddaptallAsync()
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            AppointmentListItem viewModel = JSON.Deserialize<AppointmentListItem>(json);

            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/v1/Appointment/AddAppointmentbyportalAppointmentbyPatientId";
            
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
        public async Task<IActionResult> OnPostAddAppointmentRequestAsync()
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            AppointmentListItem viewModel = JSON.Deserialize<AppointmentListItem>(json);
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/v1/Appointment/UpsertAppointmentRequest";
            string apiUrl5 = $"{baseUrl}/api/v1/Holiday/getHolidaysList";
            string apileavlist = $"{baseUrl}/api/v1/Holiday/getLeaveList";
            Holidays = await _apiService.GetAsync<List<Holidays>>(apiUrl5, token) ?? new List<Holidays>();

            Leaves = await _apiService.GetAsync<List<Leave>>(apileavlist, token) ?? new List<Leave>();
            var appointmentDate = viewModel.AppointmentStartTime.Value.ToString("dd/MM/yyyy");
            var isHoliday = Holidays.Any(h => h.StartDate.HasValue &&
                                              h.StartDate.Value.ToString("dd/MM/yyyy") == appointmentDate);
            int doctorId = 1; // <-- Default doctor

            var appointmentStart = viewModel.AppointmentStartTime.Value;
            var isleave = Leaves.Any(h =>
                h.DoctorId == doctorId &&
                h.StartDateTime.HasValue &&
                h.EndDateTime.HasValue &&
                appointmentStart >= h.StartDateTime.Value.LocalDateTime &&
                appointmentStart < h.EndDateTime.Value.LocalDateTime
            );

            if (isHoliday)
            {
                return new JsonResult(new { isSuccess = false, errorMessage = "Appointments cannot be scheduled on holidays." });
            }
            if (isleave)
            {
                return new JsonResult(new { isSuccess = false, errorMessage = "Appointments cannot be scheduled on leave." });
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
public class Pincode
{
    public int Id { get; set; }
    public string LocalityName { get; set; }
    public string Pincodes { get; set; }  // match this with what API returns
}

