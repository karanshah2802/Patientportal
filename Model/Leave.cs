namespace Patientportal.Model
{
    public class Leave
    {

        public long? Id { get; set; }
        public int? DoctorId { get; set; }
        public DateTimeOffset? StartDateTime { get; set; }
        public DateTimeOffset? EndDateTime { get; set; }
        public string? Notes { get; set; }
    }
}
