using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Patientportal.Model
{
    public class AppointmentListItem
    {
        //public string? Location { get; set; }
        public int? DoctoreId { get; set; }
        public int? SourceId { get; set; }
        public DateTime? AppointmentStartTime { get; set; }
        public DateTime? ProcedureScheduleDateTime { get; set; }
        public DateTime? ProcedureScheduleEndDateTime { get; set; }
        public string? FormofAppointment { get; set; }
        public string? Name { get; set; }
        public DateTime? AppointmentEndDateTime { get; set; }

        public int? StatusId { get; set; }
        public string? AppoinmentType { get; set; }
        //[NotMapped]
        public long? LeadId { get; set; }
        //[NotMapped]
        //public string? Name { get; set; }
        //[NotMapped]
        public string? Mobile { get; set; }
        public int? Age { get; set; }
        public string? Location { get; set; }
        //[NotMapped]
        public string? Gender { get; set; }

        //[NotMapped]
        public string? Email { get; set; }
        //[NotMapped]
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }

        public string? ProfileImage { get; set; }

        //[NotMapped]
        public string? AppointmentNo { get; set; }

        public short? Year { get; set; }
        public string? StatusName { get; set; }

        public string? AppointmentForm { get; set; }
        public string? Comment { get; set; }
        /// <summary>Optional UI field; merged into Comment for API if the backend has no separate property.</summary>
        [NotMapped]
        public string? Relation { get; set; }
        public string? SourceName { get; set; }
        public string? DoctorName { get; set; }
        public bool? IsPatientAppointment { get; set; }

        /// <summary>Who the slot is for (self or dependent); set when binding the portal grid.</summary>
        [NotMapped]
        public string? BookedForPatientName { get; set; }

        //[NotMapped]
        public string? ConcernGroups { get; set; }
        [NotMapped]
        public List<string>? NotAllowedAction { get; set; }
        public long Id { get; set; }
        public long? CreatedBy { get; set; }
        //public string? CreatedByUserName { get; set; }
        public long? ModifiedBy { get; set; }
        //public string? ModifiedByUserName { get; set; }
        public DateTimeOffset? CreatedOn { get; set; }
        public DateTimeOffset? ModifiedOn { get; set; }

        /// <summary>True when this row is a pending appointment request (not a booked appointment).</summary>
        [NotMapped]
        [JsonProperty("isAppointmentRequest")]
        public bool IsAppointmentRequest { get; set; }

        /// <summary>
        /// When false, the procedure slot must not appear as busy on the booking scheduler (e.g. cancelled).
        /// Prefer excluding these rows in the API; the portal filters here so all consumers stay consistent.
        /// </summary>
        public bool BlocksDoctorScheduleSlot()
        {
            var name = StatusName?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                if (name.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Canceled", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Portal cancel flow uses status id 79; keep if API sends id without a normalized name.
            if (StatusId == 79)
                return false;

            return true;
        }
    }
}
