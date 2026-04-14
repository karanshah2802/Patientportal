using System.Text.Json.Serialization;

namespace Patientportal.Model
{
    /// <summary>POST api/v1/Profile/UpsertPatientPortalDependent</summary>
    public class UpsertPatientPortalDependentApiBody
    {
        [JsonPropertyName("parentProfileId")]
        public long ParentProfileId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("patientRelationShipId")]
        public int PatientRelationShipId { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }
    }
}
