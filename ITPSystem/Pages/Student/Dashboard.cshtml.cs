using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;
using ITPSystem.Data;

public class StudentDashboardModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;

    public StudentDashboardModel(IWebHostEnvironment env, ApplicationDbContext db)
    {
        _env = env;
        _db = db;
    }

    public List<DocumentItem> Documents { get; private set; } = new();
    public string ApplicationStatus { get; private set; } = "pending";
    public string? ApplicationRemark { get; private set; }

    public class DocumentItem
    {
        public string Title { get; set; } = string.Empty;
        public string WebPath { get; set; } = string.Empty;
        public bool Exists { get; set; }
    }

    public IActionResult OnGet()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/Login");
        }

        LoadDocuments();
        LoadApplicationStatus();

        return Page();
    }

    private void LoadDocuments()
    {
        var docsDir = Path.Combine(_env.WebRootPath, "documents", "templates");
        var requiredDocs = new (string Title, string FileName)[]
        {
            ("Company Acceptance Letter", "company-acceptance-letter.pdf"),
            ("Indemnity Letter", "indemnity-letter.pdf"),
            ("Parent Acknowledgement Form", "parent-acknowledgement-form.pdf"),
            ("Company Supervisor Evaluation Form", "company-supervisor-evaluation-form.pdf"),
            ("Progress report template", "progress-report-template.pdf"),
            ("Final report template", "final-report-template.pdf"),
            ("Student Support Letter", "student-support-letter.pdf")
        };

        Documents = requiredDocs
            .Select(d => new DocumentItem
            {
                Title = d.Title,
                WebPath = $"/documents/templates/{d.FileName}",
                Exists = System.IO.File.Exists(Path.Combine(docsDir, d.FileName))
            })
            .ToList();
    }

    private void LoadApplicationStatus()
    {
        var rawUserId = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(rawUserId, out var userId))
        {
            return;
        }

        var user = _db.SysUsers.FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return;
        }

        var application = _db.StudentApplications
            .FirstOrDefault(a => a.application_id == user.application_id.Value);

        if (application != null)
        {
            ApplicationStatus = application.applyStatus ?? "pending";
            ApplicationRemark = application.remark;
        }
    }
}
