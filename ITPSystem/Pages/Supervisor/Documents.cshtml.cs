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

        public IActionResult OnPostApprove(int applicationId, string documentType, string? remarks)
        {
            return SaveReview(applicationId, documentType, "approved", remarks);
        }

        public IActionResult OnPostReject(int applicationId, string documentType, string? remarks)
        {
            return SaveReview(applicationId, documentType, "rejected", remarks);
        }

        public IActionResult OnPostDownload(int applicationId, string documentType)
        {
            return DownloadDocument(applicationId, documentType, asAttachment: true);
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
                return RedirectToPage();
            }

            var fileName = GetDocumentFileName(student, documentType);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Message = "No file found for selected document.";
                return RedirectToPage();
            }

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
            var safeFileName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(uploadsPath, safeFileName);

            if (!System.IO.File.Exists(fullPath))
            {
                Message = $"File not found on server: {fileName}";
                return RedirectToPage();
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

        private IActionResult SaveReview(int applicationId, string documentType, string status, string? remarks)
        {
            if (!IsSupervisor(out var supervisorId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var student = _db.StudentApplications.AsNoTracking().FirstOrDefault(s => s.application_id == applicationId);
            if (student == null)
            {
                Message = "Student application not found.";
                return RedirectToPage();
            }

            _db.DocumentReviews.Add(new DocumentReview
            {
                application_id = applicationId,
                reviewed_by = supervisorId,
                document_type = documentType,
                status = status,
                remarks = remarks,
                reviewed_at = DateTime.UtcNow
            });
            _db.SaveChanges();

            var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == applicationId);
            if (studentUser != null)
            {
                _db.Notifications.Add(new Notification
                {
                    from_user_id = supervisorId,
                    to_user_id = studentUser.user_id,
                    type = "document_review",
                    title = $"Your {documentType} was {status}",
                    message = string.IsNullOrWhiteSpace(remarks) ? "Please check your document status." : remarks
                });
                _db.SaveChanges();
            }

            Message = $"Document {status} successfully.";
            return RedirectToPage();
        }

        private void LoadStudents()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            Students = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();
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


