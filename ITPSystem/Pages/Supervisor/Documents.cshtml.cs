using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

namespace ITPSystem.Pages.Supervisor
{
    public class DocumentsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public DocumentsModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<StudentApplication> Students { get; set; } = new();
        public string? SelectedStudent { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? applicationId { get; set; }

        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadStudents();
            return Page();
        }

        public IActionResult OnPostApprove(int applicationId, string? remarks)
        {
            return SaveReview(applicationId, "approved", remarks);
        }

        public IActionResult OnPostReject(int applicationId, string? remarks)
        {
            return SaveReview(applicationId, "rejected", remarks);
        }

        public IActionResult OnGetView(int applicationId, string documentType)
        {
            return DownloadDocument(applicationId, documentType, asAttachment: false);
        }

        public IActionResult OnGetDownload(int applicationId, string documentType)
        {
            return DownloadDocument(applicationId, documentType, asAttachment: true);
        }

        public string? GetDocumentFileName(StudentApplication student, string documentType)
        {
            return documentType switch
            {
                "formAcceptance" => student.formAcceptance,
                "formAcknowledgement" => student.formAcknowledgement,
                "letterIdentity" => student.letterIdentity,
                "otherEvidence" => student.otherEvidence,
                _ => null
            };
        }

        private IActionResult DownloadDocument(int applicationId, string documentType, bool asAttachment)
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var student = _db.StudentApplications.AsNoTracking().FirstOrDefault(s => s.application_id == applicationId);
            if (student == null)
            {
                Message = "Student application not found.";
                return RedirectToPage(new { applicationId });
            }

            var fileName = GetDocumentFileName(student, documentType);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Message = "No file found for selected document.";
                return RedirectToPage(new { applicationId });
            }

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
            var safeFileName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(uploadsPath, safeFileName);

            if (!System.IO.File.Exists(fullPath))
            {
                Message = $"File not found on server: {fileName}";
                return RedirectToPage(new { applicationId });
            }

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(safeFileName, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            if (asAttachment)
            {
                return PhysicalFile(fullPath, contentType, safeFileName);
            }

            return PhysicalFile(fullPath, contentType);
        }

        private IActionResult SaveReview(int applicationId, string status, string? remarks)
        {
            if (!IsSupervisor(out var supervisorId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var student = _db.StudentApplications.FirstOrDefault(s => s.application_id == applicationId);
            if (student == null)
            {
                Message = "Student application not found.";
                return RedirectToPage(new { applicationId });
            }

            _db.DocumentReviews.Add(new DocumentReview
            {
                application_id = applicationId,
                reviewed_by = supervisorId,
                document_type = "application",
                status = status,
                remarks = remarks,
                reviewed_at = DateTime.UtcNow
            });

            student.applyStatus = status;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                student.remark = remarks.Trim();
            }
            student.updated_at = DateTime.UtcNow;
            _db.SaveChanges();

            var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == applicationId);
            if (studentUser != null)
            {
                _db.Notifications.Add(new Notification
                {
                    from_user_id = supervisorId,
                    to_user_id = studentUser.user_id,
                    type = "document_review",
                    title = $"Your document application was {status}",
                    message = string.IsNullOrWhiteSpace(remarks) ? "Please check your document application status." : remarks
                });
                _db.SaveChanges();
            }

            Message = $"Application {status} successfully.";
            return RedirectToPage(new { applicationId });
        }

        private void LoadStudents()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var userName = HttpContext.Session.GetString("UserName") ?? string.Empty;

            var query = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName);

            if (applicationId.HasValue)
            {
                query = query.Where(s => s.application_id == applicationId.Value).OrderBy(s => s.studentName);
            }

            Students = query.ToList();
            if (Students.Count == 1)
            {
                SelectedStudent = $"{Students[0].studentName} ({Students[0].studentID})";
            }
        }

        private bool IsSupervisor(out int userId)
        {
            userId = 0;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId);
        }
    }
}


