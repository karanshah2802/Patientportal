using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Patientportal.Model
{
    public class ProfileListItem
    {
        public string? Name { get; set; }
        public string? Gender { get; set; }
        public string? Type { get; set; }
        public DateTimeOffset? Dob { get; set; }
        public int? Age { get; set; }
        public string? Mobile { get; set; }

       
        public int? UnPaidValue { get; set; }

        [RegularExpression(@"^[\w\.-]+@[\w\.-]+\.\w+$", ErrorMessage = "Invalid email address")]
        public string? Email { get; set; }
        public string? Address { get; set; }
        public long? State { get; set; }
        public long? PincodeId { get; set; }
        public long? City { get; set; }
        public long? Country { get; set; }
        public string? MaritalStatus { get; set; }


        public string? CountryName { get; set; }
        public string? Category { get; set; }
        public string? InterestIndicator { get; set; }
        public string? ActivityLevel { get; set; }

        public string? CityName { get; set; }

        public string? StateName { get; set; }
      

        public string? Pincode { get; set; }
        public string? Locality { get; set; }
        public string? ProfileId { get; set; }
        public bool IsMergeProfileto { get; set; }

        public int? Leadcount { get; set; }
        public int? LqlCount { get; set; }

        //public long? ProfileNumber { get; set; }

        public long? LeadId { get; set; }
        public string? Leadname { get; set; }
        public string? CreatedByUserName { get; set; }
        public string? ModifiedByUserName { get; set; }

        public long? MergeId { get; set; }
        [NotMapped]
        public long? DependentProfileId { get; set; }


        public int? NewEnquiryCount { get; set; }
        public int? RecMedicaalCount { get; set; }
        public int? RecProcedureCount { get; set; }
        public int? SingleProceCount { get; set; }
        public int? Lastsessionfollowup { get; set; }
        public int? InactiveFollowup { get; set; }
        public int? CancelationFollowup { get; set; }
        public int? PlannedPatientFup { get; set; }
        public int? RecProduct { get; set; }


        public short? Year { get; set; }
        public short? YearNo { get; set; }
        public short? Month { get; set; }
        public int? SourceId { get; set; }
        public string? SourceName { get; set; }
        public int? ChannelId { get; set; }
        public string? Channelname { get; set; }
        public long Id { get; set; }

        //public virtual string? PincodeName { get; set; }

        //public virtual string? LocalityName { get; set; }

        public string? ConcernGroups { get; set; }

        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedOn { get; set; }

        public long? CreatedBy { get; set; }
        //public string? CreatedByUserName { get; set; }
        public long? ModifiedBy { get; set; }
        //public string? ModifiedByUserName { get; set; }
        public DateTimeOffset? CreatedOn { get; set; }
        public DateTimeOffset? ModifiedOn { get; set; }

        public string? Mobiles { get; set; }
        public long? MergeFrom { get; set; }
        public bool Isdependent { get; set; }
        public string? IndependentOrDependentLabel { get; set; }
        public string? PatientRelationshipName { get; set; }
        public bool IsMainProfile { get; set; }

        public bool IsMergeProfile { get; set; }
        public string? OldProfileId { get; set; }
        public string? Emails { get; set; }
        public int? numberOfOpenAppointment { get; set; }
        public int? numberOfcancelAppointment { get; set; }
        public int? numberOfCloseAppointment { get; set; }
        //public int? numberOfCancelProfile {  get; set; }

        public int? numberOfOpenProfile { get; set; }
        public int? numberOfCompletedProfile { get; set; }

        public string? ProfileImage { get; set; }
    }
}
