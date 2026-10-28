namespace COSGeniusSystem.Commons;

public static class SystemMessages
{
    public static readonly SystemMessage UserRegistered = new(200, "User registered successfully.", true);
    public static readonly SystemMessage LoginSuccessful = new(200, "Login successful.", true);
    public static readonly SystemMessage LogoutSuccessful = new(200, "Logged out successfully.", true);
    public static readonly SystemMessage UserProfileRetrieved = new(200, "User profile retrieved successfully.", true);
    public static readonly SystemMessage DoctorsRetrieved = new(200, "Doctors retrieved successfully.", true);
    public static readonly SystemMessage DoctorRetrieved = new(200, "Doctor retrieved successfully.", true);
    public static readonly SystemMessage ServicesRetrieved = new(200, "Treatment services retrieved successfully.", true);
    public static readonly SystemMessage ServiceCreated = new(201, "Treatment service created successfully.", true);
    public static readonly SystemMessage ServiceUpdated = new(200, "Treatment service updated successfully.", true);
    public static readonly SystemMessage PatientsRetrieved = new(200, "Patients retrieved successfully.", true);
    public static readonly SystemMessage PatientRetrieved = new(200, "Patient retrieved successfully.", true);
    public static readonly SystemMessage PatientCreated = new(201, "Patient created successfully.", true);
    public static readonly SystemMessage PatientUpdated = new(200, "Patient updated successfully.", true);
    public static readonly SystemMessage PatientDeleted = new(200, "Patient deleted successfully.", true);
    public static readonly SystemMessage ContactsRetrieved = new(200, "Patient contacts retrieved successfully.", true);
    public static readonly SystemMessage ContactRetrieved = new(200, "Patient contact retrieved successfully.", true);
    public static readonly SystemMessage ContactCreated = new(201, "Patient contact created successfully.", true);
    public static readonly SystemMessage ContactUpdated = new(200, "Patient contact updated successfully.", true);
    public static readonly SystemMessage ContactDeleted = new(200, "Patient contact deleted successfully.", true);
    public static readonly SystemMessage ValidationFailed = new(400, "One or more fields are invalid.", false);
    public static readonly SystemMessage InternalError = new(500, "An unexpected error occurred.", false);
}
