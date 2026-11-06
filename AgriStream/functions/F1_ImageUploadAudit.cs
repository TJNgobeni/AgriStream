using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
namespace AgriStream.Functions;

// FUNCTION 1 - BLOB TRIGGER: drone image uploaded -> log + audit row in Table Storage
public class ImageUploadAudit(ILogger<ImageUploadAudit> log)
{
    [Function("ImageUploadAudit")]
    public async Task Run(
        [BlobTrigger("drone-images/{name}", Connection = "StorageConn")] BlobClient blob, string name)
    {
        var props = (await blob.GetPropertiesAsync()).Value;
        log.LogInformation("Drone image uploaded: {Name} ({Size} bytes, {Type})", name, props.ContentLength, props.ContentType);

        var table = new TableClient(Environment.GetEnvironmentVariable("StorageConn"), "ImageAudit");
        await table.CreateIfNotExistsAsync();
        var fieldId = name.Contains('/') ? name.Split('/')[0] : "unknown";
        await table.UpsertEntityAsync(new TableEntity(fieldId, Uri.EscapeDataString(name).Replace("%", "_"))
        {
            ["BlobName"] = name,
            ["SizeBytes"] = props.ContentLength,
            ["ContentType"] = props.ContentType,
            ["Status"] = "Received",
            ["ProcessedUtc"] = DateTime.UtcNow
        });
    }
}
