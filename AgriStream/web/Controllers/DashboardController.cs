using System.Text.Json;
using AgriStream.Web.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AgriStream.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly BlobServiceClient _blob;
    public DashboardController(IHttpClientFactory h, BlobServiceClient b) { _http = h; _blob = b; }

    public async Task<IActionResult> Index(string fieldId = "field-A")
    {
        ViewBag.FieldId = fieldId;
        try
        {
            var json = await _http.CreateClient("func").GetStringAsync($"api/telemetry/{fieldId}");   // Function 2 (GET)
            var rows = JsonSerializer.Deserialize<List<TelemetryDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return View(rows);
        }
        catch (Exception ex)
        {
            ViewBag.Error = "Could not load telemetry: " + ex.Message;
            return View(new List<TelemetryDto>());
        }
    }

    [HttpPost]
    public async Task<IActionResult> Irrigate(string deviceId, int minutes, string fieldId)
    {
        var res = await _http.CreateClient("func").PostAsJsonAsync("api/irrigation",                  // Function 3 (POST)
            new { deviceId, minutes, requestedBy = User.Identity!.Name });
        TempData["msg"] = res.IsSuccessStatusCode
            ? $"Irrigation command for {deviceId} was queued."
            : "The command could not be queued.";
        return RedirectToAction(nameof(Index), new { fieldId });
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, string fieldId)
    {
        if (file == null || file.Length == 0) { TempData["msg"] = "Choose an image first."; return RedirectToAction(nameof(Index), new { fieldId }); }
        var container = _blob.GetBlobContainerClient("drone-images");
        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient($"{fieldId}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}");
        await blob.UploadAsync(file.OpenReadStream(), new BlobHttpHeaders { ContentType = file.ContentType });   // triggers Function 1
        TempData["msg"] = "Image uploaded. The audit entry will appear shortly.";
        return RedirectToAction(nameof(Index), new { fieldId });
    }
}
