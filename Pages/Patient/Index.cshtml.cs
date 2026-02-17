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
        public List<AppointmentListItem> Doctorblocktime { get; set; } = new List<AppointmentListItem>();
        public List<Holidays> Holidays { get; set; } = new List<Holidays>();
        public List<Leave> Leaves { get; set; } = new List<Leave>();
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClientFactory, ApiService apiService, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClientFactory;
            _apiService = apiService;
            _configuration = configuration;
        }
        public async Task<JsonResult> OnPostAppointmentView([FromBody] DataManagerRequest dm)
        {
            var queryId = Request.Query["id"];
            if (queryId.Any())
            {
                Id = Convert.ToInt64(queryId);
            }
            if (dm == null)
            {
                return new JsonResult(new { result = new List<object>(), count = 0 });
            }
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = _configuration["ApiSettings:AuthToken"];
            string apiUrl = $"{baseUrl}/api/v1/Appointment/getPatientByAppointment?id={Id}";
            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/getPatientByAppointmentRequest?id={Id}";
              var appointments = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl, token);
            var appointmentsRequest = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token);
            if (appointments != null && appointments.Count > 0)
            {
                foreach (var appointment in appointments)
                {
                    //if (appointment.AppointmentStartTime.HasValue)
                    //{
                    //    appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    //    appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
                    //}
                    //if (appointment.CreatedOn != null)
                    //{
                    //    appointment.CreatedOn = appointment.CreatedOn.Value.AddHours(-5).AddMinutes(-30);
                    //}

                    if (appointment.StatusName == "Rescheduled" || appointment.StatusName == "Booked")
                    {
                        appointment.StatusName = "Booked";
                    }
                    if (appointment.StatusName == "Released" || appointment.StatusName == "Completed")
                    {
                        appointment.StatusName = "Completed";
                    }
                    if (appointment.AppoinmentType == "Consultation")
                    {
                        appointment.AppoinmentType = "Dr. Sejal In-person Consultation";
                    }
                    if (appointment.StatusName == "Confirmed" ||
                      appointment.StatusName == "ReverseCheckin")
                    {
                        appointment.StatusName = "Confirmed";
                    }
                    if (appointment.StatusName == "Checked-In" || appointment.StatusName == "ReverseCheckout")
                    {
                        appointment.StatusName = "Checked-In";
                    }
                    if (appointment.StatusName == "Walked-Out")
                    {
                        appointment.StatusName = "Walked-Out";
                    }
                }
            }
            if (appointmentsRequest != null && appointmentsRequest.Count > 0)
            {
                foreach (var appointmentes in appointmentsRequest)
                {
                    //if (appointmentes.AppointmentStartTime.HasValue)
                    //{
                    //    appointmentes.AppointmentStartTime = appointmentes.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    //}
                    if (appointmentes.StatusName == "Reschedule")
                    {
                        appointmentes.StatusName = "Booked";
                    }
                    if (appointmentes.AppoinmentType == "Consultation")
                    {
                        appointmentes.AppoinmentType = "Dr. Sejal In-person Consultation";
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
            var queryId = Request.Query["id"];
            if (queryId.Any())
            {
                Id = Convert.ToInt64(queryId);
            }
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = _configuration["ApiSettings:AuthToken"];

            string apiUrl = $"{baseUrl}/api/v1/Appointment/getPatientByAppointment?id={Id}";
            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/getPatientByAppointmentRequest?id={Id}";
            var appointments = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl, token);
            var appointmentsRequest = await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token);
            if (appointments != null && appointments.Count > 0)
            {
                foreach (var appointment in appointments)
                {
                    //if (appointment.AppointmentStartTime.HasValue)
                    //{
                    //    appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    //    appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
                    //}
                    //if (appointment.CreatedOn != null)
                    //{
                    //    appointment.CreatedOn = appointment.CreatedOn.Value.AddHours(-5).AddMinutes(-30);
                    //}

                    if (appointment.StatusName == "Rescheduled" || appointment.StatusName == "Booked")
                    {
                        appointment.StatusName = "Booked";
                    }
                    if (appointment.StatusName == "Released" || appointment.StatusName == "Completed" || appointment.StatusName == "Converted To Appointment")
                    {
                        appointment.StatusName = "Completed";
                    }
                    if (appointment.AppoinmentType == "Consultation")
                    {
                        appointment.AppoinmentType = "Dr. Sejal In-person Consultation";
                    }
					if (appointment.StatusName == "Confirmed" ||
					  appointment.StatusName == "ReverseCheckin")
					{
						appointment.StatusName = "Confirmed";
					}
                    if (appointment.StatusName == "Checked-In" || appointment.StatusName == "ReverseCheckout")
					{
						appointment.StatusName = "Checked-In";
					}
                    if (appointment.StatusName == "Walked-Out")
					{
						appointment.StatusName = "Walked-Out";
					}
				}
            }
            if (appointmentsRequest != null && appointmentsRequest.Count > 0)
            {
                foreach (var appointmentes in appointmentsRequest)
                {
                    //if (appointmentes.AppointmentStartTime.HasValue)
                    //{
                    //    appointmentes.AppointmentStartTime = appointmentes.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
                    //}
                    if (appointmentes.StatusName == "Converted To Appointment")
                    {
                        appointmentes.StatusName = "Completed";
                    }

                    if (appointmentes.AppoinmentType == "Consultation")
                    {
                        appointmentes.AppoinmentType = "Dr. Sejal In-person Consultation";
                    } 
                    if (appointmentes.AppoinmentType == "Appointment Request for Online Consultation")
                    {
                        appointmentes.AppoinmentType = "Appointment Request for Online Consultation";
                    }
                }
            }

            IEnumerable<object> data = appointmentsRequest.Concat(appointments).AsEnumerable().OrderByDescending(x => x.CreatedOn);
            int count = data.Count();


            return new JsonResult(new { result = data, count });
        }
        public async Task<IActionResult> OnGetStatesAsync(int countryId)
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = _configuration["ApiSettings:AuthToken"];
            string apiUrl4 = $"{baseUrl}/api/v1/CountryStateCity/GetStatesByCountry?countryId={countryId}";
           
            var states = await _apiService.GetAsync<List<State>>(apiUrl4, token) ?? new List<State>();
            return new JsonResult(states) { StatusCode = 200 };
        }
        public async Task<IActionResult> OnGetCitiesAsync(int stateId)
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = _configuration["ApiSettings:AuthToken"];
            string apiUrl = $"{baseUrl}/api/v1/CountryStateCity/GetCitiesByState?stateId={stateId}";
            

            var citiesdp = await _apiService.GetAsync<List<City>>(apiUrl, token) ?? new List<City>();
            return new JsonResult(citiesdp) { StatusCode = 200 };
        }
        public async Task<IActionResult> OnGetPincodeforleadDataSourceAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = _configuration["ApiSettings:AuthToken"];
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
            string token = _configuration["ApiSettings:AuthToken"];

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

            PatientData = await _apiService.GetAsync<ProfileListItem>(apiUrl, token) ?? new ProfileListItem();


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
            return Page();
        }

        public async Task<IActionResult> OnPostSavePatientAsync()
        {
            try
            {

                string baseUrl = _configuration["ApiSettings:BaseUrl"];
                string token = _configuration["ApiSettings:AuthToken"];
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
            string token = _configuration["ApiSettings:AuthToken"];
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
            string token = _configuration["ApiSettings:AuthToken"];

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
            string token = _configuration["ApiSettings:AuthToken"];

            string apiUrl = $"{baseUrl}/api/v1/Appointment/UpsertAppointmentRequest";
            string apiUrl5 = $"{baseUrl}/api/v1/Holiday/getHolidaysList";
            string apileavlist = $"{baseUrl}/api/v1/Holiday/getLeaveList";
            Holidays = await _apiService.GetAsync<List<Holidays>>(apiUrl5, token) ?? new List<Holidays>();

            Leaves = await _apiService.GetAsync<List<Leave>>(apileavlist, token) ?? new List<Leave>();
            var appointmentDate = viewModel.AppointmentStartTime.Value.ToString("dd/MM/yyyy");
            var isHoliday = Holidays.Any(h => h.StartDate.HasValue &&
                                              h.StartDate.Value.ToString("dd/MM/yyyy") == appointmentDate);
            int doctorId = 1; // <-- Default doctor

            var isleave = Leaves.Any(h =>
                h.StartDateTime.HasValue &&
                h.DoctorId == doctorId &&     // 🔥 Only check for doctor ID = 1
                h.StartDateTime.Value.ToString("dd/MM/yyyy") == appointmentDate
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

