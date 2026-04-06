using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class CommitteeProgressEvaluationModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeProgressEvaluationModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentProgressRow> Items { get; private set; } = new();
    public int TotalReports { get; private set; }
    public int PendingReports { get; private set; }
    public int SubmittedReports { get; private set; }
    public int LateReports { get; private set; }
    public int ApprovedReports { get; private set; }
    public int RejectedReports { get; private set; }
    public int TotalStudents { get; private set; }
    public List<SelectListItem> CohortOptions { get; private set; } = new();
    public List<SelectListItem> ProgrammeOptions { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string StatusFilter { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int? CohortFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ProgrammeFilter { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadFilterOptions();

        var studentQuery = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .AsQueryable();

        TotalStudents = studentQuery.Count();

        var keyword = (Search ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            studentQuery = studentQuery.Where(s =>
                (s.studentID ?? string.Empty).Contains(keyword) ||
                (s.studentName ?? string.Empty).Contains(keyword) ||
                (s.studentEmail ?? string.Empty).Contains(keyword) ||
                (s.programme ?? string.Empty).Contains(keyword) ||
                (s.comName ?? string.Empty).Contains(keyword));
        }

        if (CohortFilter.HasValue)
        {
            studentQuery = studentQuery.Where(s => s.cohortId == CohortFilter.Value);
        }

        var normalizedProgramme = (ProgrammeFilter ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedProgramme))
        {
            studentQuery = studentQuery.Where(s => (s.programme ?? string.Empty) == normalizedProgramme);
        }

        var students = studentQuery
            .OrderBy(s => s.studentName)
            .ToList();

        var studentIds = students.Select(s => s.application_id).ToList();

        var markAgg = _db.AssessmentMarks.AsNoTracking()
            .Where(m => studentIds.Contains(m.application_id))
            .GroupBy(m => m.application_id)
            .Select(g => new
            {
                ApplicationId = g.Key,
                TotalScore = g.Sum(x => x.score),
                TotalMax = g.Sum(x => x.max_score)
            })
            .ToDictionary(x => x.ApplicationId);

        var reports = _db.ProgressReports.AsNoTracking()
            .Where(r => studentIds.Contains(r.applicantId))
            .OrderByDescending(r => r.updated_at)
            .ToList();

        var normalizedStatus = (StatusFilter ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            var filteredStudentIds = GetApplicantIdsByStatus(reports, normalizedStatus);
            students = students.Where(s => filteredStudentIds.Contains(s.application_id)).ToList();
            studentIds = students.Select(s => s.application_id).ToList();
            reports = reports.Where(r => studentIds.Contains(r.applicantId)).ToList();
            markAgg = markAgg
                .Where(x => studentIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        TotalReports = reports.Count;
        PendingReports = reports.Count(r => r.status == 0);
        SubmittedReports = reports.Count(r => r.status == 1);
        LateReports = reports.Count(r => r.status == 4);
        ApprovedReports = reports.Count(r => r.status == 2);
        RejectedReports = reports.Count(r => r.status == 3);

        var reportLookup = reports
            .GroupBy(r => r.applicantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Items = students
            .Select(st =>
            {
                reportLookup.TryGetValue(st.application_id, out var studentReports);
                studentReports ??= new List<ProgressReport>();
                markAgg.TryGetValue(st.application_id, out var mk);

                return new StudentProgressRow
                {
                    StudentId = st.studentID ?? "-",
                    StudentName = st.studentName ?? "Unknown",
                    Report1 = BuildReportCell(studentReports, false, 1),
                    Report2 = BuildReportCell(studentReports, false, 2),
                    Report3 = BuildReportCell(studentReports, false, 3),
                    Report4 = BuildReportCell(studentReports, false, 4),
                    Report5 = BuildReportCell(studentReports, false, 5),
                    Report6 = BuildReportCell(studentReports, false, 6),
                    FinalReport = BuildReportCell(studentReports, true, null),
                    EvaluationSummary = mk == null ? "-" : $"{mk.TotalScore:0.##}/{mk.TotalMax:0.##}"
                };
            })
            .ToList();

        return Page();
    }

    private void LoadFilterOptions()
    {
        CohortOptions = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.isActive)
            .ThenBy(c => c.description)
            .Select(c => new SelectListItem
            {
                Value = c.cohort_id.ToString(),
                Text = string.IsNullOrWhiteSpace(c.description) ? $"Cohort {c.cohort_id}" : c.description
            })
            .ToList();

        ProgrammeOptions = _db.StudentApplications.AsNoTracking()
            .Select(s => s.programme)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .OrderBy(p => p)
            .Select(p => new SelectListItem
            {
                Value = p,
                Text = p
            })
            .ToList();
    }

    private static HashSet<int> GetApplicantIdsByStatus(List<ProgressReport> reports, string normalizedStatus)
    {
        return normalizedStatus switch
        {
            "pending" => reports.Where(r => r.status == 0).Select(r => r.applicantId).ToHashSet(),
            "submitted" => reports.Where(r => r.status == 1).Select(r => r.applicantId).ToHashSet(),
            "submittedlate" => reports.Where(r => r.status == 4).Select(r => r.applicantId).ToHashSet(),
            "approved" => reports.Where(r => r.status == 2).Select(r => r.applicantId).ToHashSet(),
            "rejected" => reports.Where(r => r.status == 3).Select(r => r.applicantId).ToHashSet(),
            _ => reports.Select(r => r.applicantId).ToHashSet()
        };
    }

    public string GetStatusText(byte status)
    {
        return status switch
        {
            1 => "Submitted",
            4 => "Submitted Late",
            2 => "Approved",
            3 => "Rejected",
            _ => "Pending"
        };
    }

    private ReportCell BuildReportCell(List<ProgressReport> reports, bool isFinal, byte? reportNo)
    {
        var report = reports
            .Where(r =>
                string.Equals(r.reportType, isFinal ? "final" : "progress", StringComparison.OrdinalIgnoreCase) &&
                (isFinal || r.reportNo == reportNo))
            .OrderByDescending(r => r.updated_at)
            .FirstOrDefault();

        if (report == null)
        {
            return new ReportCell
            {
                Exists = false,
                Status = 0,
                StatusText = "Pending",
                DueDateText = "-",
                Remark = null
            };
        }

        return new ReportCell
        {
            Exists = true,
            Status = report.status,
            StatusText = GetStatusText(report.status),
            DueDateText = report.dueDate.ToString("yyyy-MM-dd"),
            Remark = report.remark
        };
    }

    public class StudentProgressRow
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public ReportCell Report1 { get; set; } = new();
        public ReportCell Report2 { get; set; } = new();
        public ReportCell Report3 { get; set; } = new();
        public ReportCell Report4 { get; set; } = new();
        public ReportCell Report5 { get; set; } = new();
        public ReportCell Report6 { get; set; } = new();
        public ReportCell FinalReport { get; set; } = new();
        public string EvaluationSummary { get; set; } = "-";
    }

    public class ReportCell
    {
        public bool Exists { get; set; }
        public byte Status { get; set; }
        public string StatusText { get; set; } = "Pending";
        public string DueDateText { get; set; } = "-";
        public string? Remark { get; set; }
    }
}
