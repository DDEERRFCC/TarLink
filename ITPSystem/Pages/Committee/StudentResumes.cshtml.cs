using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

public class CommitteeStudentResumesModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeStudentResumesModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentResumeItem> Students { get; private set; } = new();

    public class StudentResumeItem
    {
        public StudentApplication Student { get; set; } = null!;
        public StudentCv? Cv { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var studentList = _db.StudentApplications.AsNoTracking()
            .OrderBy(s => s.studentName)
            .ToList();

        var appIds = studentList.Select(s => s.application_id).ToList();
        var latestCvMap = _db.StudentCvs.AsNoTracking()
            .Where(c => appIds.Contains(c.application_id))
            .GroupBy(c => c.application_id)
            .Select(g => g.OrderByDescending(x => x.uploaded_at).ThenByDescending(x => x.cv_id).First())
            .ToDictionary(c => c.application_id);

        Students = studentList
            .Select(s => new StudentResumeItem
            {
                Student = s,
                Cv = latestCvMap.TryGetValue(s.application_id, out var cv) ? cv : null
            })
            .ToList();

        return Page();
    }

    public IActionResult OnGetResumeFile(int applicationId, bool download = false)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var cv = _db.StudentCvs.AsNoTracking()
            .Where(c => c.application_id == applicationId)
            .OrderByDescending(c => c.uploaded_at)
            .ThenByDescending(c => c.cv_id)
            .FirstOrDefault();
        if (cv == null || string.IsNullOrWhiteSpace(cv.file_path))
        {
            return NotFound();
        }

        var safeFile = Path.GetFileName(cv.file_path);
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", safeFile);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(safeFile, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var downloadName = string.IsNullOrWhiteSpace(cv.file_name) ? safeFile : cv.file_name!;
        return download
            ? PhysicalFile(fullPath, contentType, downloadName)
            : PhysicalFile(fullPath, contentType);
    }
}
