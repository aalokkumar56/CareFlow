namespace CureFlow.Application.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";
    public string? PrivateRoot { get; set; }
    public string LabReportsPath { get; set; } = "lab-reports";
    public string PatientDocumentsPath { get; set; } = "patient-documents";
}
