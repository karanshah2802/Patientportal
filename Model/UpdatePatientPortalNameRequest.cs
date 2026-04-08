using System.Text.Json.Serialization;

namespace Patientportal.Model
{
    /// <summary>POST <c>api/v1/Profile/UpdatePatientPortalName</c></summary>
    public sealed class UpdatePatientPortalNameRequest
    {
        [JsonPropertyName("profileId")]
        public long ProfileId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
