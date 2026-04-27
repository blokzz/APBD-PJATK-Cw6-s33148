namespace WebApplication2.Services;
using WebApplication2.Dto;
using Microsoft.Data.SqlClient;
using System.Data;

public class AppointmentService
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<IEnumerable<AppointmentListDto>> GetAllAsync(string? status, string? patientLastName)
    {
        var appointments = new List<AppointmentListDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        string query = @"
            SELECT 
                a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, 
                p.FirstName, p.LastName, p.Email 
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
              AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
            ORDER BY a.AppointmentDate;";

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = (object?)status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80).Value = (object?)patientLastName ?? DBNull.Value;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4) + " " + reader.GetString(5),
                PatientEmail = reader.GetString(6)
            });
        }
        return appointments;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentById(int id)
    {
        AppointmentDetailsDto appointment = null;
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        string query = @"
            SELECT p.FirstName, p.LastName, a.InternalNotes, a.CreatedAt, p.Email, p.PhoneNumber, d.LicenseNumber 
            FROM Appointments a
            JOIN Patients p ON p.IdPatient = a.IdPatient 
            JOIN Doctors d ON d.IdDoctor = a.IdDoctor 
            WHERE a.IdAppointment = @idAppointment";

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@idAppointment", SqlDbType.Int).Value = id;
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            appointment = new AppointmentDetailsDto
            {
                Patient = reader.GetString(0) + " " + reader.GetString(1),
                InternalNotes = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                CreatedAt = reader.GetDateTime(3),
                PatientEmail = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                PhoneNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                LicenseNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            };
        }
        return appointment;
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        if (dto.AppointmentDate <= DateTime.Now)
            throw new ArgumentException("Termin wizyty nie może być w przeszłości.");
        
        var patientCmd = new SqlCommand("SELECT IsActive FROM Patients WHERE IdPatient = @id", connection);
        patientCmd.Parameters.Add("@id", SqlDbType.Int).Value = dto.IdPatient;
        var patientStatus = await patientCmd.ExecuteScalarAsync();
        if (patientStatus == null || !(bool)patientStatus)
            throw new KeyNotFoundException("Pacjent nie istnieje lub jest nieaktywny.");
        
        var doctorCmd = new SqlCommand("SELECT IsActive FROM Doctors WHERE IdDoctor = @id", connection);
        doctorCmd.Parameters.Add("@id", SqlDbType.Int).Value = dto.IdDoctor;
        var doctorStatus = await doctorCmd.ExecuteScalarAsync();
        if (doctorStatus == null || !(bool)doctorStatus)
            throw new KeyNotFoundException("Lekarz nie istnieje lub jest nieaktywny.");
        
        var conflictCmd = new SqlCommand(
            "SELECT COUNT(*) FROM Appointments WHERE IdDoctor = @id AND AppointmentDate = @date AND Status = 'Scheduled'", connection);
        conflictCmd.Parameters.Add("@id", SqlDbType.Int).Value = dto.IdDoctor;
        conflictCmd.Parameters.Add("@date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        
        if ((int)await conflictCmd.ExecuteScalarAsync() > 0) return -1;
        
        var insertCmd = new SqlCommand(
            @"INSERT INTO Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason) 
              VALUES (@pId, @dId, @date, 'Scheduled', @reason);
              SELECT SCOPE_IDENTITY();", connection);
        
        insertCmd.Parameters.Add("@pId", SqlDbType.Int).Value = dto.IdPatient;
        insertCmd.Parameters.Add("@dId", SqlDbType.Int).Value = dto.IdDoctor;
        insertCmd.Parameters.Add("@date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        insertCmd.Parameters.Add("@reason", SqlDbType.NVarChar, 250).Value = dto.Reason;

        return Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
    }

    public async Task<int> UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto dto)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var existingCmd = new SqlCommand("SELECT Status, AppointmentDate FROM Appointments WHERE IdAppointment = @id", connection);
        existingCmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
        
        using var reader = await existingCmd.ExecuteReaderAsync();
        if (!reader.Read()) return 404;

        string currentStatus = reader.GetString(0);
        DateTime currentDate = reader.GetDateTime(1);
        reader.Close();

        if (currentStatus == "Completed" && dto.AppointmentDate != currentDate)
            throw new InvalidOperationException("Nie można zmienić terminu zakończonej wizyty.");

        if (dto.AppointmentDate != currentDate)
        {
            var conflictCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Appointments WHERE IdDoctor = @dId AND AppointmentDate = @date AND IdAppointment <> @id AND Status = 'Scheduled'", connection);
            conflictCmd.Parameters.Add("@dId", SqlDbType.Int).Value = dto.IdDoctor;
            conflictCmd.Parameters.Add("@date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
            conflictCmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
            if ((int)await conflictCmd.ExecuteScalarAsync() > 0) return 409;
        }

        var updateCmd = new SqlCommand(@"
            UPDATE Appointments SET 
            IdPatient = @pId, IdDoctor = @dId, AppointmentDate = @date, 
            Status = @status, Reason = @reason, InternalNotes = @notes
            WHERE IdAppointment = @id", connection);
        
        updateCmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
        updateCmd.Parameters.Add("@pId", SqlDbType.Int).Value = dto.IdPatient;
        updateCmd.Parameters.Add("@dId", SqlDbType.Int).Value = dto.IdDoctor;
        updateCmd.Parameters.Add("@date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        updateCmd.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = dto.Status;
        updateCmd.Parameters.Add("@reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
        updateCmd.Parameters.Add("@notes", SqlDbType.NVarChar, 500).Value = (object?)dto.InternalNotes ?? DBNull.Value;

        await updateCmd.ExecuteNonQueryAsync();
        return 204;
    }

    public async Task<int> DeleteAppointment(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var checkCmd = new SqlCommand("SELECT Status FROM Appointments WHERE IdAppointment = @Id", connection);
        checkCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        
        var currentStatus = await checkCmd.ExecuteScalarAsync() as string;
        if (currentStatus == null) return 404;
        if (currentStatus == "Completed") return 409;

        var deleteCmd = new SqlCommand("DELETE FROM Appointments WHERE IdAppointment = @Id", connection);
        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        
        await deleteCmd.ExecuteNonQueryAsync();
        return 204;
    }
}