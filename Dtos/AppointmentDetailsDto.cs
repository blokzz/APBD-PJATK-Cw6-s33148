namespace WebApplication2.Dto;

public class AppointmentDetailsDto
{
    public string Patient { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public string InternalNotes { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    
    public string PhoneNumber { get; set; } = string.Empty;
    
    public string LicenseNumber { get; set; } = string.Empty;
}