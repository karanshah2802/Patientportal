using System.Text.Json.Serialization;

namespace Patientportal.Model
{
    /// <summary>Item from GET api/v1/PatientRelationship/GetPatientRelationshipPatientportal</summary>
    public class PatientRelationshipPortalItem
    {
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("relationshipName")]
        public string? RelationshipName { get; set; }

        [JsonPropertyName("patientRelationshipName")]
        public string? PatientRelationshipName { get; set; }

        [JsonIgnore]
        public string Label => !string.IsNullOrWhiteSpace(Name)
            ? Name!.Trim()
            : !string.IsNullOrWhiteSpace(RelationshipName)
                ? RelationshipName!.Trim()
                : (PatientRelationshipName?.Trim() ?? string.Empty);
    }
}
