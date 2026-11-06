using Azure.Storage.Blobs;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])) builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("AgriDb"),
        s => s.EnableRetryOnFailure()));
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
});
builder.Services.AddHttpClient("func", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["FunctionApp:BaseUrl"]!);
    var key = builder.Configuration["FunctionApp:Key"];
    if (!string.IsNullOrEmpty(key)) c.DefaultRequestHeaders.Add("x-functions-key", key);
});
builder.Services.AddSingleton(new BlobServiceClient(builder.Configuration["Storage:Conn"]));

var app = builder.Build();

// Seed a default admin so you can log in the first time
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.Users.AnyAsync())
    {
        var admin = new AppUser { Username = "admin", Role = "Admin" };
        admin.PasswordHash = new PasswordHasher<AppUser>().HashPassword(admin, "Admin123!");
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
