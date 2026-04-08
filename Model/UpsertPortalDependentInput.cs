namespace Patientportal.Model
{
    /// <summary>JSON from the Add Dependent modal (parent id is applied server-side).</summary>
    public class UpsertPortalDependentInput
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public int PatientRelationShipId { get; set; }
        public string? Gender { get; set; }

        /// <summary>Dependent profile id when updating an existing dependent (e.g. after grid selection).</summary>
        public long? DependentProfileId { get; set; }
    }
}
