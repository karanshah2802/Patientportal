namespace Patientportal.Model
{
    /// <summary>Self or dependent row for the portal "Appointment for" dropdown.</summary>
    public class PortalBookingSubjectOption
    {
        public long ProfileId { get; set; }
        public string DisplayLabel { get; set; } = "";
        public string? Name { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? RelationName { get; set; }
        public bool IsSelf { get; set; }
    }
}
