using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
namespace AgriStream.Functions;

// FUNCTION 2 - HTTP GET: latest 20 telemetry rows for a field (newest first)
public class GetLatestTelemetry
{
    [Function("GetLatestTelemetry")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/{fieldId}")] HttpRequestData req,
        string fieldId)
    {
        var table = new TableClient(Environment.GetEnvironmentVariable("StorageConn"), "Telemetry");
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {fieldId}");
        var rows = new List<object>();
        // RowKey is inverted ticks, so the first rows returned are the newest.
        await foreach (var e in table.QueryAsync<TableEntity>(filter, maxPerPage: 20))
        {
            rows.Add(new
            {
                DeviceId = e.GetString("DeviceId"),
                SoilMoisture = e.GetDouble("SoilMoisture"),
                Temperature = e.GetDouble("Temperature"),
                Timestamp = e.Timestamp
            });
            if (rows.Count == 20) break;
        }
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(rows);
        return res;
    }
}
