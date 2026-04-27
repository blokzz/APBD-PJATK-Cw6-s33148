using WebApplication2.Dto;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Services;

namespace WebApplication2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _service;

    public AppointmentsController(AppointmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppointmentListDto>>> GetAll([FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        try
        {
            var data = await _service.GetAllAsync(status, patientLastName);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Błąd podczas pobierania listy wizyt",
                Details = ex.Message
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AppointmentDetailsDto>> GetById(int id)
    {
        try
        {
            var data = await _service.GetAppointmentById(id);
            if (data == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Message = "Nie znaleziono wizyty",
                    Details = $"Wizyta o ID {id} nie istnieje."
                });
            }

            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Błąd podczas pobierania szczegółów wizyty",
                Details = ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentRequestDto dto)
    {
        try
        {
            int result = await _service.CreateAppointmentAsync(dto);

            if (result == -1)
            {
                return Conflict(new ErrorResponseDto
                {
                    Message = "Konflikt terminu",
                    Details = "Lekarz ma już zaplanowaną wizytę w tym terminie."
                });
            }

            return Created($"/api/appointments/{result}", new { id = result, message = "Wizyta dodana." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponseDto { Message = "Błąd walidacji", Details = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(
                new ErrorResponseDto { Message = "Nieprawidłowe dane referencyjne", Details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Wystąpił nieoczekiwany błąd serwera",
                Details = ex.Message
            });
        }
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> Update(int idAppointment, [FromBody] UpdateAppointmentRequestDto dto)
    {
        try
        {
            int result = await _service.UpdateAppointmentAsync(idAppointment, dto);

            if (result == 404)
            {
                return NotFound(new ErrorResponseDto
                {
                    Message = "Nie znaleziono zasobu",
                    Details = $"Wizyta o ID {idAppointment} nie figuruje w bazie danych."
                });
            }

            if (result == 409)
            {
                return Conflict(new ErrorResponseDto
                {
                    Message = "Konflikt terminu",
                    Details = "Lekarz ma już zajęty ten termin inną wizytą."
                });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponseDto { Message = "Niedozwolona operacja", Details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Błąd serwera",
                Details = ex.Message
            });
        }
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> Delete(int idAppointment)
    {
        try
        {
            int result = await _service.DeleteAppointment(idAppointment);

            if (result == 404)
                return NotFound(new ErrorResponseDto { Message = "Błąd", Details = "Wizyta nie istnieje." });

            if (result == 409)
                return Conflict(new ErrorResponseDto
                    { Message = "Konflikt", Details = "Nie można usunąć zakończonej wizyty." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponseDto { Message = "Błąd", Details = ex.Message });
        }
    }
}