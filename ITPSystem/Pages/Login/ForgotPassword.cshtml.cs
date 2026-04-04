using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

public class ForgotPasswordModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public ForgotPasswordModel(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [BindProperty]
    [Required]
    [EmailAddress]
    [Display(Name = "Account Email")]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? StatusMessage { get; set; }

    public void OnGet(string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email;
        }
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Email.Trim();
        var user = _db.SysUsers.FirstOrDefault(u => u.email == email);

        // Keep response generic so account existence is not exposed.
        StatusMessage = "If the account exists, a password reset link has been prepared.";

        if (user == null || string.IsNullOrWhiteSpace(user.email))
        {
            return Page();
        }

        var token = GenerateToken();
        user.password_reset_token = token;
        user.password_reset_expires = DateTime.UtcNow.AddHours(1);
        user.updated_at = DateTime.Now;
        _db.SaveChanges();

        var resetUrl = Url.Page(
            "/Login/ResetPassword",
            null,
            new { token },
            Request.Scheme);

        if (!TrySendResetEmail(user.email, resetUrl!))
        {
            StatusMessage = $"Email is not configured right now. Use this reset link manually: {resetUrl}";
        }

        return Page();
    }

    private bool TrySendResetEmail(string recipient, string resetUrl)
    {
        var host = _config["Email:SmtpHost"];
        var portText = _config["Email:SmtpPort"];
        var user = _config["Email:SmtpUser"];
        var pass = _config["Email:SmtpPass"];
        var from = _config["Email:From"];
        var useSslText = _config["Email:UseSsl"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || !int.TryParse(portText, out var port))
        {
            return false;
        }

        var useSsl = true;
        if (!string.IsNullOrWhiteSpace(useSslText) && bool.TryParse(useSslText, out var parsedSsl))
        {
            useSsl = parsedSsl;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = "ITPSystem Password Reset",
                Body = "A password reset was requested for your ITPSystem account.\n\n" +
                       $"Open this link to reset your password:\n{resetUrl}\n\n" +
                       "This link will expire in 1 hour."
            };

            message.To.Add(recipient);

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = useSsl
            };

            if (!string.IsNullOrWhiteSpace(user))
            {
                smtp.Credentials = new NetworkCredential(user, pass ?? string.Empty);
            }

            smtp.Send(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
