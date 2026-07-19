namespace CureFlow.Application.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";
    public string LabReportsPath { get; set; } = "lab-reports";
    public string PatientDocumentsPath { get; set; } = "patient-documents";
}
