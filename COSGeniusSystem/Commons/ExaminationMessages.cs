namespace COSGeniusSystem.Commons;

public static class ExaminationMessages
{
    public static readonly SystemMessage Retrieved = new(200, "Examination data retrieved.", true);
    public static readonly SystemMessage Created = new(201, "Examination registration created.", true);
    public static readonly SystemMessage Saved = new(200, "Examination data saved.", true);
    public static readonly SystemMessage Started = new(200, "Medical record ready.", true);
    public static readonly SystemMessage DraftReady = new(200, "Order ready.", true);
    public static readonly SystemMessage Sent = new(200, "Prescription sent to Herbalist.", true);
    public static readonly SystemMessage Confirmed = new(200, "Treatment order confirmed.", true);
    public static readonly SystemMessage Completed = new(200, "Examination completed.", true);
    public static readonly SystemMessage Deleted = new(200, "Draft deleted.", true);
}
