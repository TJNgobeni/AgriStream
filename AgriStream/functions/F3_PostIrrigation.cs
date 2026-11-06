using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
namespace AgriStream.Functions;

public record IrrigationCommand(string DeviceId, int Minutes, string RequestedBy, DateTime? IssuedUtc = null);

public class IrrigationOutput
{
    [QueueOutput("irrigation-commands", Connection = "StorageConn")]
    public string? QueueMessage { get; set; }
    [HttpResult]
    public HttpResponseData HttpResponse { get; set; } = default!;
}

// FUNCTION 3 - HTTP POST: validate the command and place it on the Storage Queue
public class PostIrrigationCommand
{
    [Function("PostIrrigationCommand")]
    public async Task<IrrigationOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation")] HttpRequestData req)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        IrrigationCommand? cmd = null;
        try { cmd = JsonSerializer.Deserialize<IrrigationCommand>(await req.ReadAsStringAsync() ?? "", opts); }
        catch (JsonException) { }

        if (cmd is null || string.IsNullOrWhiteSpace(cmd.DeviceId) || cmd.Minutes <= 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("deviceId and minutes (>0) are required.");
            return new IrrigationOutput { HttpResponse = bad };
        }
        var res = req.CreateResponse(HttpStatusCode.Accepted);
        await res.WriteStringAsync("Queued");
        return new IrrigationOutput
        {
            QueueMessage = JsonSerializer.Serialize(cmd with { IssuedUtc = DateTime.UtcNow }),
            HttpResponse = res
        };
    }
}
