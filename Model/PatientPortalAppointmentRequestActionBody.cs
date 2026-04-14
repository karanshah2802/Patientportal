using System.Text.Json.Serialization;

namespace Patientportal.Model
{
    /// <summary>POST body for <c>/api/v1/Appointment/patientPortal/appointmentRequest/action</c>.</summary>
    public class PatientPortalAppointmentRequestActionBody
    {
        [JsonPropertyName("patientId")]
        public long PatientId { get; set; }

        [JsonPropertyName("appointmentRequestId")]
        public long AppointmentRequestId { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = "";

        [JsonPropertyName("appointmentStartTime")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AppointmentStartTime { get; set; }
    }
}
