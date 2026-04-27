namespace WebApplication2.Dto;
using System.ComponentModel.DataAnnotations;
public class CreateAppointmentRequestDto
{ 
    [Required]
    public int IdPatient { get; set; }
    [Required]
    public int IdDoctor { get; set; }
    
    [Required]
    public DateTime AppointmentDate { get; set; }

    [Required]
    [MaxLength(250, ErrorMessage = "Opis może mieć maksymalnie 250 znaków.")]
    public string Reason { get; set; } = string.Empty;

}