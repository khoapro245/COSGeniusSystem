namespace COSGeniusSystem.Commons;

public static class SystemMessages
{
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
