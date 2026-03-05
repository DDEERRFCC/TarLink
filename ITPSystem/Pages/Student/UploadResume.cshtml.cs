using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class StudentUploadResumeModel : PageModel
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public StudentUploadResumeModel(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool HasResume { get; private set; }
    public string CurrentResumeFileName { get; private set; } = "-";

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Please choose a file to upload.")]
        public IFormFile? ResumeFile { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!TryEnsureStudentRole())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: true);
        if (student == null)
        {
            return RedirectToPage("/Student/Dashboard");
        }

        SetCurrentResume(GetLatestStudentCv(student.application_id));
        return Page();
    }

    public IActionResult OnPostUpload()
    {
        if (!TryEnsureStudentRole())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: false);
        if (student == null)
        {
            ModelState.AddModelError("", "No linked student application found.");
            return Page();
        }

        ValidateResumeFile(Input.ResumeFile);
        if (!ModelState.IsValid)
        {
            SetCurrentResume(GetLatestStudentCv(student.application_id));
            return Page();
        }

        var latestCv = GetLatestStudentCv(student.application_id);
        var savedName = SaveUploadedFile(Input.ResumeFile!, "resume", latestCv?.file_path);
        if (string.IsNullOrWhiteSpace(savedName))
        {
            ModelState.AddModelError("", "Failed to save uploaded file.");
            SetCurrentResume(GetLatestStudentCv(student.application_id));
            return Page();
        }

        if (latestCv == null)
        {
            _db.StudentCvs.Add(new StudentCv
            {
                application_id = student.application_id,
                file_path = savedName,
                file_name = Path.GetFileName(Input.ResumeFile!.FileName),
                uploaded_at = DateTime.UtcNow
            });
        }
        else
        {
            latestCv.file_path = savedName;
            latestCv.file_name = Path.GetFileName(Input.ResumeFile!.FileName);
            latestCv.uploaded_at = DateTime.UtcNow;
        }

        student.updated_at = DateTime.Now;
        _db.SaveChanges();

        StatusMessage = "Resume uploaded successfully.";
        return RedirectToPage();
    }

    public IActionResult OnGetResumeFile(bool download = false)
    {
        if (!TryEnsureStudentRole())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: true);
        var latestCv = student == null ? null : GetLatestStudentCv(student.application_id);
        var fileName = Path.GetFileName(latestCv?.file_path ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        var fullPath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return download
            ? PhysicalFile(fullPath, contentType, fileName)
            : PhysicalFile(fullPath, contentType);
    }

    private void SetCurrentResume(StudentCv? cv)
    {
        var displayName = !string.IsNullOrWhiteSpace(cv?.file_name)
            ? cv!.file_name!
            : Path.GetFileName(cv?.file_path ?? string.Empty);
        HasResume = !string.IsNullOrWhiteSpace(displayName);
        CurrentResumeFileName = HasResume ? displayName : "-";
    }

    private void ValidateResumeFile(IFormFile? file)
    {
        if (file == null)
        {
            return;
        }

        if (file.Length <= 0)
        {
            ModelState.AddModelError(nameof(Input.ResumeFile), "Uploaded file is empty.");
            return;
        }

        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(nameof(Input.ResumeFile), "File must not exceed 10MB.");
            return;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx" };
        if (!allowed.Contains(ext))
        {
            ModelState.AddModelError(nameof(Input.ResumeFile), "Only .pdf, .doc, .docx files are allowed.");
        }
    }

    private string? SaveUploadedFile(IFormFile file, string prefix, string? existingFileName)
    {
        if (file.Length <= 0)
        {
            return existingFileName;
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var ext = Path.GetExtension(file.FileName);
        var savedName = $"{prefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsPath, savedName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        var oldSafeName = Path.GetFileName(existingFileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(oldSafeName))
        {
            var oldPath = Path.Combine(uploadsPath, oldSafeName);
            if (System.IO.File.Exists(oldPath))
            {
                try
                {
                    System.IO.File.Delete(oldPath);
                }
                catch
                {
                    // keep upload success even if old cleanup fails
                }
            }
        }

        return savedName;
    }

    private StudentApplication? GetCurrentStudentApplication(bool asNoTracking)
    {
        var userIdText = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdText, out var userId))
        {
            return null;
        }

        var userQuery = asNoTracking ? _db.SysUsers.AsNoTracking() : _db.SysUsers;
        var user = userQuery.FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return null;
        }

        var studentQuery = asNoTracking ? _db.StudentApplications.AsNoTracking() : _db.StudentApplications;
        return studentQuery.FirstOrDefault(s => s.application_id == user.application_id.Value);
    }

    private StudentCv? GetLatestStudentCv(int applicationId)
    {
        return _db.StudentCvs
            .OrderByDescending(c => c.uploaded_at)
            .ThenByDescending(c => c.cv_id)
            .FirstOrDefault(c => c.application_id == applicationId);
    }

    private bool TryEnsureStudentRole()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return string.Equals(role, "student", StringComparison.OrdinalIgnoreCase);
    }
}
