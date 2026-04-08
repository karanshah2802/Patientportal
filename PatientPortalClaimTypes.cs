namespace Patientportal
{
    public static class PatientPortalClaimTypes
    {
        public const string EncryptedPatientId = "urn:patientportal:encrypted_patient_id";
    }

    /// <summary>Cookie names for patient-portal UI (no patient id in the URL).</summary>
    public static class PatientPortalCookies
    {
        /// <summary>User is in the logged-in patient appointment flow; allows clean <c>/Appointment</c> URL.</summary>
        public const string AppointmentPortalBooking = "pp_appt_portal";
    }
}
