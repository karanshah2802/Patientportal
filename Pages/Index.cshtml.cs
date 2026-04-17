using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Patientportal;
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
using System.Globalization;
using System.Security.Claims;

namespace Patientportal.Pages
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
        public AppointmentListItem AppoinmentData { get; set; }
        public string? EjsDateTimePattern = "dd/MM/yyyy hh:mm:ss a";
       
        public List<string> ChangeRequests { get; set; } = new List<string>();
        public List<AppointmentListItem> Doctorblocktime { get; set; } = new List<AppointmentListItem>();
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClientFactory, ApiService apiService, IConfiguration configuration, OpsTokenService opsTokenService)
        {
            _logger = logger;
            _httpClient = httpClientFactory;
            _apiService = apiService;
            _configuration = configuration;
            _opsTokenService = opsTokenService;
        }

        private void BindPatientIdFromQuery()
        {
            var raw = Request.Query["id"].FirstOrDefault() ?? Request.Query["Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(raw))
            {
                Id = null;
                return;
            }
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
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
                Id = null;
            }
        }

        public async Task<JsonResult> OnPostAppointmentView([FromBody] DataManagerRequest dm)
         {
            BindPatientIdFromQuery();
            if (dm == null)
            {
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/v1/Appointment/getPatientByAppointment?id={Id}";
            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/getPatientByAppointmentRequest?id={Id}";
            var appointments = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl, token);
            var appointmentsRequest = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token);
            if (appointments != null && appointments.Count > 0)
            {
                foreach (var appointment in appointments)
                {
                    if (appointment.AppointmentStartTime.HasValue)
                    {
                        appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                        appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
                    }
                    if (appointment.CreatedOn != null)
                    {
                        appointment.CreatedOn = appointment.CreatedOn.Value.AddHours(-5).AddMinutes(-30);
                    }
                    
                    if (appointment.StatusName == "Reschedule")
                    {
                        appointment.StatusName = "Booked";
                    }
                }
            }
            if (appointmentsRequest != null && appointmentsRequest.Count > 0)
            {
                foreach (var appointmentes in appointmentsRequest)
                {
                    if (appointmentes.AppointmentStartTime.HasValue)
                    {
                        appointmentes.AppointmentStartTime = appointmentes.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    }
                    if (appointmentes.StatusName == "Reschedule")
                    {
                        appointmentes.StatusName = "Booked";
                    }
                }
            }

            IEnumerable<object> data = appointmentsRequest.Concat(appointments).AsEnumerable().OrderByDescending(x => x.CreatedOn);
            int count = data.Count();

            var dataOperations = new DataOperations();

            // Filtering
            if (dm.Where != null && dm.Where.Count > 0)
            {
                data = dataOperations.PerformFiltering(data, dm.Where, "and");
                count = data.Count();
            }

            // Searching
            if (dm.Search != null && dm.Search.Count > 0)
            {
                data = dataOperations.PerformSearching(data, dm.Search);
                count = data.Count();
            }

            // Sorting
            if (dm.Sorted != null && dm.Sorted.Count > 0)
            {
                data = dataOperations.PerformSorting(data, dm.Sorted);
            }

            // Paging
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


        public async Task<JsonResult> OnPostAppointmentViewCard([FromBody] DataManagerRequest dm)
        {
            BindPatientIdFromQuery();
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();
            string apiUrl = $"{baseUrl}/api/v1/Appointment/getPatientByAppointment?id={Id}";
            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/getPatientByAppointmentRequest?id={Id}";
               var appointments =  await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl, token);
            var appointmentsRequest =  await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token);
            if (appointments != null && appointments.Count > 0)
            {
                foreach (var appointment in appointments)
                {
                    if (appointment.AppointmentStartTime.HasValue)
                    {
                        appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                        appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
                    }
                    if (appointment.StatusName == "Reschedule")
                    {
                        appointment.StatusName = "Booked";
                    }
                    if (appointment.AppoinmentType == "Consultation")
                    {
                        appointment.StatusName = "Dr. Sejal In-person Consultation";
                    }
                }
            }
            if (appointmentsRequest != null && appointmentsRequest.Count > 0)
            {
                foreach (var appointmentes in appointmentsRequest)
                {
                    if (appointmentes.AppointmentStartTime.HasValue)
                    {
                        appointmentes.AppointmentStartTime = appointmentes.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    }
                    if (appointmentes.StatusName == "Reschedule")
                    {
                        appointmentes.StatusName = "Booked";
                    }
                }
            }
            if (appointments == null || !appointments.Any())
            {
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }

            IEnumerable<object> data = appointmentsRequest.Concat(appointments).AsEnumerable().OrderByDescending(x => x.CreatedOn);
            int count = data.Count();


            return new JsonResult(new { result = data, count });
        }

        public async Task<IActionResult> OnGetAsync()
        {
            BindPatientIdFromQuery();
            if (!Id.HasValue)
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    var encryptedId = User.FindFirstValue(PatientPortalClaimTypes.EncryptedPatientId);
                    if (!string.IsNullOrEmpty(encryptedId))
                        return RedirectToPage("/Patient/Index", new { id = encryptedId });
                }

                return RedirectToPage("/Account/Index"); // Ya phir Redirect("/Login");
            }
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/Profile/getProfile?id={Id}";
            string apiUrl2 = $"{baseUrl}/api/Profile/getDetailsChangesbyId?id={Id}";
            string apiUrl4 = $"{baseUrl}/api/v1/Appointment/GetInvoiceAmount?id={Id}";

            // API Response Fetch karein
            var invoiceResponse = await _apiService.GetAsync<InvoiceResponse>(apiUrl4, token);
            await LoadIndexDoctorBlocksAsync(baseUrl, token);

            PatientData = await _apiService.GetAsync<ProfileListItem>(apiUrl, token) ?? new ProfileListItem();


            if (PatientData != null)
            {
                ViewData["PatientName"] = PatientData.Name;
            }
            if (PatientData != null)
            {
                ViewData["Invoice"] = PatientData?.UnPaidValue;
            }


            ChangeRequests = await _apiService.GetAsync<List<string>>(apiUrl2, token) ?? new List<string>();
            return Page();
        }

        private async Task LoadIndexDoctorBlocksAsync(string baseUrl, string token)
        {
            string apiUrl3 = $"{baseUrl}/api/v1/Appointment/GetAppointmentsByDoctor";
            Doctorblocktime = (await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl3, token) ?? new List<AppointmentListItem>())
                .Where(a => a.BlocksDoctorScheduleSlot())
                .ToList();
            if (Doctorblocktime != null && Doctorblocktime.Count > 0)
            {
                foreach (var appointment in Doctorblocktime)
                {
                    if (appointment.AppointmentStartTime != null)
                    {
                        appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    }

                    if (appointment.AppointmentEndDateTime != null)
                    {
                        appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
                    }
                }
            }
        }

        public async Task<IActionResult> OnGetSchedulerDataAsync()
        {
            BindPatientIdFromQuery();
            if (!Id.HasValue || Id.Value <= 0)
                return new JsonResult(new { error = "Invalid patient id." }) { StatusCode = 400 };

            string baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            string token = await _opsTokenService.GetTokenAsync();
            await LoadIndexDoctorBlocksAsync(baseUrl, token);
            return new JsonResult(new { doctorBlocks = Doctorblocktime });
        }

        public async Task<IActionResult> OnPostSavePatientAsync()
        {
            try
            {


                using var reader = new StreamReader(HttpContext.Request.Body);
                var json = await reader.ReadToEndAsync();
                ProfileListItem viewModel = JSON.Deserialize<ProfileListItem>(json);
                string baseUrl = _configuration["ApiSettings:BaseUrl"];
                string token = await _opsTokenService.GetTokenAsync();

                string apiUrl = $"{baseUrl}/api/Profile/Addpatientportalchanges";

                var response = await _apiService.PostAsync<ProfileListItem, ApiResponse>(apiUrl, viewModel, token);

                if (response != null && response.IsSuccess)
                {
                    return new JsonResult(new { message = "Your change request has been submitted." });

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

            string apiUrl = $"{baseUrl}/api/v1/Appointment/viewAppointmentButton";

            var (ok, response, status, raw) =
                await _apiService.PostAsyncWithStatus<AppointmentListItem, ApiResponse>(apiUrl, viewModel, token);

            if (ok && response != null && response.IsSuccess)
            {
                return new JsonResult(new { message = "Your change request has been submitted." });
            }

            var forwarded = ApiService.TryForwardBadRequestJson(status, raw);
            if (forwarded != null)
                return forwarded;

            return BadRequest("Failed to save patient details.");
        } 
        public async Task<IActionResult> OnPostAddaptallAsync()
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            AppointmentListItem viewModel = JSON.Deserialize<AppointmentListItem>(json);

            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/v1/Appointment/AddAppointmentbyportalAppointmentbyPatientId";

            var response = await _apiService.PostAsync<AppointmentListItem, ApiResponse>(apiUrl, viewModel, token);

            if (response != null && response.IsSuccess)
            {
                return new JsonResult(new {  message = "Your change request has been submitted." });

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

            var response = await _apiService.PostAsync<AppointmentListItem, ApiResponse>(apiUrl, viewModel, token);

            if (response != null && response.IsSuccess)
            {
                return new JsonResult(new {  message = "Your change request has been submitted." });

            }
            else
            {
                return BadRequest("Failed to save patient details.");
            }
        }

    }
}
