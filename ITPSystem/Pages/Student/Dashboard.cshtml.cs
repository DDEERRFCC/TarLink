using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;

public class StudentDashboardModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public StudentDashboardModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public List<DocumentItem> Documents { get; private set; } = new();

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
}
