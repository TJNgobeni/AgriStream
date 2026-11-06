using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AgriStream.Web.Models;

[Table("Users")]
public class AppUser
{
    [Key] public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Farmer";
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }
    public DbSet<AppUser> Users => Set<AppUser>();
}

public class TelemetryDto
{
    public string? DeviceId { get; set; }
    public double? SoilMoisture { get; set; }
    public double? Temperature { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}
