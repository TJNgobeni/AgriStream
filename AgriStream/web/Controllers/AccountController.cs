using System.Security.Claims;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AgriStream.Web.Controllers;

public class AccountController : Controller
{
    private static readonly string[] Roles = { "Farmer", "Agronomist", "Admin" };
    private readonly AppDbContext _db;
    private readonly PasswordHasher<AppUser> _hasher = new();
    public AccountController(AppDbContext db) => _db = db;

    [HttpGet] public IActionResult Login() => View();
    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(string username, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || !Roles.Contains(role))
        { ViewBag.Error = "Enter a username, a password and a role."; return View(); }
        if (await _db.Users.AnyAsync(u => u.Username == username))
        { ViewBag.Error = "That username is taken."; return View(); }
        var u = new AppUser { Username = username.Trim(), Role = role };
        u.PasswordHash = _hasher.HashPassword(u, password);
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Username == username);
        if (u == null || _hasher.VerifyHashedPassword(u, u.PasswordHash, password) == PasswordVerificationResult.Failed)
        { ViewBag.Error = "Wrong username or password."; return View(); }
        var claims = new List<Claim> { new(ClaimTypes.Name, u.Username), new(ClaimTypes.Role, u.Role) };
        await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")));
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index", "Home");
    }
}
