using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Patientportal.AllApicall;
using Patientportal.Model;

namespace Patientportal.Pages.Calendar
{
    [IgnoreAntiforgeryToken(Order = 2000)]
    public class IndexModel : PageModel
    {
        [FromQuery(Name = "fromPatient")]
        public string? FromPatient { get; set; }

        public bool ReturnToPatientPortalBooking => string.Equals(FromPatient, "1", StringComparison.Ordinal);
        private readonly ILogger<IndexModel> _logger;
        //private readonly HttpClient _httpClient;
        private readonly HttpClient _httpClient;
        private readonly ApiService _apiService;
        private readonly IConfiguration _configuration;
        private readonly OpsTokenService _opsTokenService;
        public List<AppointmentListItem> Doctorblocktime { get; set; } = new List<AppointmentListItem>();
        public List<Holidays> Holidays { get; set; } = new List<Holidays>();
        public List<Leave> Leaves { get; set; } = new List<Leave>();

        public string? EjsDateTimePattern = "dd/MM/yyyy hh:mm:ss a";
        public IndexModel(ILogger<IndexModel> logger, HttpClient httpClientFactory, ApiService apiService, IConfiguration configuration, OpsTokenService opsTokenService)
        {
            _logger = logger;
            _httpClient = httpClientFactory;
            _apiService = apiService;
            _configuration = configuration;
            _opsTokenService = opsTokenService;
        }
        public async Task OnGet()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl = $"{baseUrl}/api/v1/Holiday/getHolidaysList";
            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/GetAppointmentsByDoctor";
            string apileavlist = $"{baseUrl}/api/v1/Holiday/getLeaveList";
            Doctorblocktime = (await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token) ?? new List<AppointmentListItem>())
                .Where(a => a.BlocksDoctorScheduleSlot())
                .ToList();
            Holidays = await _apiService.GetAsync<List<Holidays>>(apiUrl, token) ?? new List<Holidays>();
            Leaves = await _apiService.GetAsync<List<Leave>>(apileavlist, token) ?? new List<Leave>();
            //foreach (var appointment in Doctorblocktime)
            //{
            //    if (appointment.AppointmentStartTime != null)
            //    {
            //        appointment.AppointmentStartTime = appointment.AppointmentStartTime.Value.AddHours(-5).AddMinutes(-30);
            //    }

            //    if (appointment.AppointmentEndDateTime != null)
            //    {
            //        appointment.AppointmentEndDateTime = appointment.AppointmentEndDateTime.Value.AddHours(-5).AddMinutes(-30);
            //    }
            //}







        }
        public async Task OnGetCalendarAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string token = await _opsTokenService.GetTokenAsync();

            string apiUrl2 = $"{baseUrl}/api/v1/Appointment/GetAppointmentsByDoctor";
            
            Doctorblocktime = (await _apiService.GetAsync<List<AppointmentListItem>>(apiUrl2, token) ?? new List<AppointmentListItem>())
                .Where(a => a.BlocksDoctorScheduleSlot())
                .ToList();
            //if (Doctorblocktime != null && Doctorblocktime.Count > 0)
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
        }
    }
}
