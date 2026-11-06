using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
namespace AgriStream.Functions;

// FUNCTION 4 - QUEUE TRIGGER: process command and update the Devices registry in Azure SQL.
// Failures retry up to 5 times, then the message moves to 'irrigation-commands-poison' (no data loss).
public class ProcessIrrigationCommand(ILogger<ProcessIrrigationCommand> log)
{
    [Function("ProcessIrrigationCommand")]
    public async Task Run([QueueTrigger("irrigation-commands", Connection = "StorageConn")] string message)
    {
        var cmd = JsonSerializer.Deserialize<IrrigationCommand>(message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        await using var conn = new SqlConnection(Environment.GetEnvironmentVariable("SqlConn"));
        await conn.OpenAsync();
        const string sql = @"UPDATE Devices SET Status='Irrigating', LastCommand=@c, LastUpdatedUtc=SYSUTCDATETIME()
                             WHERE DeviceId=@id";
        await using var q = new SqlCommand(sql, conn);
        q.Parameters.AddWithValue("@c", $"Irrigate {cmd.Minutes} min by {cmd.RequestedBy}");
        q.Parameters.AddWithValue("@id", cmd.DeviceId);
        var rows = await q.ExecuteNonQueryAsync();
        log.LogInformation("Device {Id} updated, rows affected: {Rows}", cmd.DeviceId, rows);
    }
}
