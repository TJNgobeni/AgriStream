using Microsoft.AspNetCore.Mvc;
namespace AgriStream.Web.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _http;
    public HomeController(IHttpClientFactory http) => _http = http;

    public IActionResult Index() => View();

    // Public CPU-bound endpoint: used by the load test to push CPU up so autoscale triggers.
    public IActionResult Ping(int ms = 150)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double x = 0;
        while (sw.ElapsedMilliseconds < ms) x += Math.Sqrt(Random.Shared.NextDouble());
        return Content("ok " + x.ToString("F0"));
    }

    // Public pass-through to the HTTP GET function, for load testing without logging in.
    public async Task<IActionResult> Latest(string fieldId = "field-A")
    {
        var json = await _http.CreateClient("func").GetStringAsync($"api/telemetry/{fieldId}");
        return Content(json, "application/json");
    }

    public IActionResult Error() => Content("Something went wrong.");
}
