using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeProgressEvaluationModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeProgressEvaluationModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<ProgressViewItem> Items { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var students = _db.StudentApplications.AsNoTracking()
            .ToDictionary(s => s.application_id);

        var markAgg = _db.AssessmentMarks.AsNoTracking()
            .GroupBy(m => m.application_id)
            .Select(g => new
            {
                ApplicationId = g.Key,
                TotalScore = g.Sum(x => x.score),
                TotalMax = g.Sum(x => x.max_score)
            })
            .ToDictionary(x => x.ApplicationId);

        Items = _db.ProgressReports.AsNoTracking()
            .OrderByDescending(r => r.updated_at)
            .ToList()
            .Select(r =>
            {
                students.TryGetValue(r.applicantId, out var st);
                markAgg.TryGetValue(r.applicantId, out var mk);
                return new ProgressViewItem
                {
                    StudentId = st?.studentID ?? "-",
                    StudentName = st?.studentName ?? "Unknown",
                    ReportTitle = string.Equals(r.reportType, "final", StringComparison.OrdinalIgnoreCase)
                        ? "Final Report"
                        : $"Progress Report {r.reportNo}",
                    DueDate = r.dueDate,
                    Status = r.status,
                    Remark = r.remark,
                    EvaluationSummary = mk == null ? "-" : $"{mk.TotalScore:0.##}/{mk.TotalMax:0.##}"
                };
            })
            .ToList();

        return Page();
    }

    public string GetStatusText(byte status)
    {
        return status switch
        {
            1 => "Submitted",
            2 => "Approved",
            3 => "Rejected",
            _ => "Pending"
        };
    }

    public class ProgressViewItem
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string ReportTitle { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public byte Status { get; set; }
        public string? Remark { get; set; }
        public string EvaluationSummary { get; set; } = "-";
    }
}
