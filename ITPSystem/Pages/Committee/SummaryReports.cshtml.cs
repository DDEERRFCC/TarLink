using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeSummaryReportsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeSummaryReportsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public List<string> SelectedReportKeys { get; set; } = new();

    [BindProperty]
    public int? SelectedCohortId { get; set; }

    [BindProperty]
    public byte? SelectedLevel { get; set; }

    public List<ReportOption> AvailableReports { get; } = new()
    {
        new() { Key = "students", Title = "Programme Student Summary", Description = "Programme, no. of student, success, rejected, submitted, pending." },
        new() { Key = "progress", Title = "Progress Report Summary", Description = "Submission status totals and type breakdown." },
        new() { Key = "companies", Title = "Company Summary", Description = "Registered company status and visibility overview." },
        new() { Key = "assessment", Title = "Assessment Marks Summary", Description = "Overall marks volume and scoring averages." }
    };

    public List<CohortOption> CohortOptions { get; private set; } = new();
    public List<GeneratedReportView> GeneratedReports { get; private set; } = new();
    public string? Message { get; private set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCohortOptions();
        return Page();
    }

    public IActionResult OnPostGenerate()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCohortOptions();
        var allowedKeys = AvailableReports.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = SelectedReportKeys
            .Where(x => !string.IsNullOrWhiteSpace(x) && allowedKeys.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            Message = "Select at least one report type to generate.";
            return Page();
        }

        var generatedAt = DateTime.Now;
        GeneratedReports = keys
            .Select(k => BuildReport(k, generatedAt))
            .Where(r => r != null)
            .Cast<GeneratedReportView>()
            .ToList();

        Message = $"{GeneratedReports.Count} summary report(s) generated.";
        return Page();
    }

    private GeneratedReportView? BuildReport(string key, DateTime generatedAt)
    {
        return key.ToLowerInvariant() switch
        {
            "students" => BuildStudentSummary(generatedAt),
            "progress" => BuildProgressSummary(generatedAt),
            "companies" => BuildCompanySummary(generatedAt),
            "assessment" => BuildAssessmentSummary(generatedAt),
            _ => null
        };
    }

    private GeneratedReportView BuildStudentSummary(DateTime generatedAt)
    {
        var studentsQuery = _db.StudentApplications.AsNoTracking().AsQueryable();
        if (SelectedCohortId.HasValue)
        {
            studentsQuery = studentsQuery.Where(x => x.cohortId == SelectedCohortId.Value);
        }

        if (SelectedLevel.HasValue)
        {
            studentsQuery = studentsQuery.Where(x => x.level == SelectedLevel.Value);
        }

        var students = studentsQuery
            .Select(x => new
            {
                x.application_id,
                Programme = x.programme ?? "N/A",
                Status = (x.applyStatus ?? string.Empty).ToLower()
            })
            .ToList();

        var applicantIds = students.Select(x => x.application_id).ToList();
        var reportStatusByApplicant = _db.ProgressReports.AsNoTracking()
            .Where(x => applicantIds.Contains(x.applicantId))
            .GroupBy(x => x.applicantId)
            .Select(g => new
            {
                ApplicantId = g.Key,
                HasSubmitted = g.Any(x => x.status == 1),
                HasPending = g.Any(x => x.status == 0)
            })
            .ToDictionary(x => x.ApplicantId);

        var programmeRows = students
            .GroupBy(x => x.Programme)
            .Select(g =>
            {
                var appIds = g.Select(x => x.application_id).ToList();
                return new ProgrammeSummaryRow
                {
                    Programme = g.Key,
                    NoOfStudent = g.Count(),
                    Success = g.Count(x => x.Status == "approved"),
                    Rejected = g.Count(x => x.Status == "rejected"),
                    Submitted = appIds.Count(id => reportStatusByApplicant.TryGetValue(id, out var r) && r.HasSubmitted),
                    Pending = appIds.Count(id => reportStatusByApplicant.TryGetValue(id, out var r) && r.HasPending)
                };
            })
            .OrderByDescending(x => x.NoOfStudent)
            .ThenBy(x => x.Programme)
            .ToList();

        var cohortLabel = SelectedCohortId.HasValue
            ? CohortOptions.FirstOrDefault(x => x.CohortId == SelectedCohortId.Value)?.Name ?? SelectedCohortId.Value.ToString()
            : "All";
        var levelLabel = SelectedLevel.HasValue ? GetLevelLabel(SelectedLevel.Value) : "All";

        return new GeneratedReportView
        {
            Key = "students",
            Title = $"Programme Student Summary (Cohort: {cohortLabel}, Level: {levelLabel})",
            GeneratedAt = generatedAt,
            ProgrammeRows = programmeRows
        };
    }

    private GeneratedReportView BuildProgressSummary(DateTime generatedAt)
    {
        var total = _db.ProgressReports.Count();
        var pending = _db.ProgressReports.Count(x => x.status == 0);
        var submitted = _db.ProgressReports.Count(x => x.status == 1);
        var approved = _db.ProgressReports.Count(x => x.status == 2);
        var rejected = _db.ProgressReports.Count(x => x.status == 3);
        var progressType = _db.ProgressReports.Count(x => x.reportType == "progress");
        var finalType = _db.ProgressReports.Count(x => x.reportType == "final");

        return new GeneratedReportView
        {
            Key = "progress",
            Title = "Progress Report Summary",
            GeneratedAt = generatedAt,
            Rows = new List<ReportRow>
            {
                new() { Label = "Total Reports", Value = total.ToString() },
                new() { Label = "Pending", Value = pending.ToString() },
                new() { Label = "Submitted", Value = submitted.ToString() },
                new() { Label = "Approved", Value = approved.ToString() },
                new() { Label = "Rejected", Value = rejected.ToString() },
                new() { Label = "Progress Type", Value = progressType.ToString() },
                new() { Label = "Final Type", Value = finalType.ToString() }
            }
        };
    }

    private GeneratedReportView BuildCompanySummary(DateTime generatedAt)
    {
        var programmes = _db.StudentApplications.AsNoTracking()
            .Select(x => x.programme)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var companies = _db.Companies.AsNoTracking()
            .Select(x => x.name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        var applications = _db.StudentApplications.AsNoTracking()
            .Select(x => new CompanyApplicationItem
            {
                Company = x.comName ?? string.Empty,
                Programme = x.programme ?? "N/A",
                Allowance = x.allowance
            })
            .ToList();

        var applicationGroups = applications
            .GroupBy(x => (x.Company ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var allCompanyNames = companies
            .Concat(applicationGroups.Keys)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var rows = new List<CompanySummaryRow>();
        var no = 1;
        foreach (var companyName in allCompanyNames)
        {
            applicationGroups.TryGetValue(companyName, out var companyApps);
            companyApps ??= new List<CompanyApplicationItem>();

            var allowanceValues = companyApps
                .Where(x => x.Allowance.HasValue)
                .Select(x => x.Allowance!.Value)
                .ToList();

            var programmeCounts = programmes
                .Select(p => new CompanyProgrammeCount
                {
                    Programme = p,
                    Count = companyApps.Count(x => string.Equals(x.Programme, p, StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

            rows.Add(new CompanySummaryRow
            {
                No = no++,
                Company = companyName,
                Allowance = allowanceValues.Count == 0 ? null : Math.Round(allowanceValues.Average(), 2),
                ApplicationCount = companyApps.Count,
                ProgrammeCounts = programmeCounts
            });
        }

        return new GeneratedReportView
        {
            Key = "companies",
            Title = "Company Summary",
            GeneratedAt = generatedAt,
            CompanyProgrammes = programmes,
            CompanyRows = rows
        };
    }

    private GeneratedReportView BuildAssessmentSummary(DateTime generatedAt)
    {
        var totalMarks = _db.AssessmentMarks.Count();
        var totalScore = _db.AssessmentMarks.Sum(x => (decimal?)x.score) ?? 0m;
        var totalMax = _db.AssessmentMarks.Sum(x => (decimal?)x.max_score) ?? 0m;
        var distinctApplications = _db.AssessmentMarks
            .Select(x => x.application_id)
            .Distinct()
            .Count();
        var average = totalMarks == 0 ? 0m : Math.Round(totalScore / totalMarks, 2);
        var attainment = totalMax == 0m ? 0m : Math.Round((totalScore / totalMax) * 100m, 2);

        return new GeneratedReportView
        {
            Key = "assessment",
            Title = "Assessment Marks Summary",
            GeneratedAt = generatedAt,
            Rows = new List<ReportRow>
            {
                new() { Label = "Total Mark Records", Value = totalMarks.ToString() },
                new() { Label = "Applications Evaluated", Value = distinctApplications.ToString() },
                new() { Label = "Total Score", Value = totalScore.ToString("0.##") },
                new() { Label = "Total Max Score", Value = totalMax.ToString("0.##") },
                new() { Label = "Average Score per Record", Value = average.ToString("0.##") },
                new() { Label = "Overall Attainment", Value = $"{attainment:0.##}%" }
            }
        };
    }

    public class ReportOption
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class GeneratedReportView
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public List<ReportRow> Rows { get; set; } = new();
        public List<ProgrammeSummaryRow> ProgrammeRows { get; set; } = new();
        public List<string> CompanyProgrammes { get; set; } = new();
        public List<CompanySummaryRow> CompanyRows { get; set; } = new();
    }

    public class ReportRow
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ProgrammeSummaryRow
    {
        public string Programme { get; set; } = string.Empty;
        public int NoOfStudent { get; set; }
        public int Success { get; set; }
        public int Rejected { get; set; }
        public int Submitted { get; set; }
        public int Pending { get; set; }
    }

    public class CohortOption
    {
        public int CohortId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CompanySummaryRow
    {
        public int No { get; set; }
        public string Company { get; set; } = string.Empty;
        public decimal? Allowance { get; set; }
        public int ApplicationCount { get; set; }
        public List<CompanyProgrammeCount> ProgrammeCounts { get; set; } = new();
    }

    public class CompanyProgrammeCount
    {
        public string Programme { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class CompanyApplicationItem
    {
        public string Company { get; set; } = string.Empty;
        public string Programme { get; set; } = string.Empty;
        public decimal? Allowance { get; set; }
    }

    private void LoadCohortOptions()
    {
        CohortOptions = _db.Cohorts.AsNoTracking()
            .OrderByDescending(x => x.cohort_id)
            .Select(x => new CohortOption
            {
                CohortId = x.cohort_id,
                Name = string.IsNullOrWhiteSpace(x.description) ? $"Cohort {x.cohort_id}" : x.description
            })
            .ToList();
    }

    private string GetLevelLabel(byte level)
    {
        return level switch
        {
            1 => "Diploma",
            2 => "Degree",
            _ => $"Level {level}"
        };
    }
}
