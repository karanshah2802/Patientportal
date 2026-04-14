using System.Text.Json.Serialization;

namespace Patientportal.Model
{
    public class ApiResponse
    {
        /// <summary>Patient portal v1 envelope: HTTP 200 may still indicate failure when false. Binds <c>succeeded</c> or <c>Succeeded</c> (case-insensitive JSON).</summary>
        public bool? Succeeded { get; set; }

        /// <summary>OTP / other APIs (e.g. <c>isSucceeded</c>).</summary>
        public bool IsSucceeded { get; set; }

        public bool IsSuccess { get; set; }

        public string? Message { get; set; }

        /// <summary>Binds <c>errorCode</c> or <c>ErrorCode</c> (case-insensitive JSON).</summary>
        public string? ErrorCode { get; set; }

        public string[]? Errors { get; set; }

        public int? MinimumLeadHours { get; set; }
    }
}
