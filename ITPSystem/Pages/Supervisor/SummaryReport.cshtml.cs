using ITPSystem.Data;
using ITPSystem.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ITPSystem.Pages.Supervisor
{
    public class SummaryReportModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SummaryReportModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int? cohortId { get; set; }

        [BindProperty(SupportsGet = true)]
        public byte? level { get; set; }

        public DateTime GeneratedAt { get; private set; }

        public List<SelectListItem> CohortOptions { get; private set; } = new();
        public List<ProgrammeSummaryItem> ProgrammeSummary { get; private set; } = new();
        public List<CompanySummaryItem> CompanySummary { get; private set; } = new();
        public TotalsItem Totals { get; private set; } = new();

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            GeneratedAt = DateTime.Now;
            LoadData();
            return Page();
        }

        public IActionResult OnGetExportCsv()
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadData();
            var sb = new StringBuilder();
            sb.AppendLine("Summary Report");
            sb.AppendLine($"CohortId,{(cohortId?.ToString() ?? "All")}");
            sb.AppendLine($"AcademicLevel,{GetLevelLabel(level)}");
            sb.AppendLine($"ReportGeneratedAt,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("Student Breakdown by Programme");
            sb.AppendLine("Programme,No of Student,Success,Failed,Pending,Rejected");
            foreach (var row in ProgrammeSummary)
            {
                sb.AppendLine($"{EscapeCsv(row.Programme)},{row.NoOfStudent},{row.Success},{row.Failed},{row.Pending},{row.Rejected}");
            }
            sb.AppendLine($"Total,{Totals.TotalStudents},{Totals.TotalSuccess},{Totals.TotalFailed},{Totals.TotalPending},{Totals.TotalRejected}");
            sb.AppendLine();

            sb.AppendLine("Student Breakdown by Company");
            sb.AppendLine("Company Name,Average Allowance,Applicant Count,Programme");
            foreach (var row in CompanySummary)
            {
                sb.AppendLine($"{EscapeCsv(row.CompanyName)},{row.AverageAllowance:0.00},{row.ApplicantCount},{EscapeCsv(row.Programmes)}");
            }
            sb.AppendLine($"Total, ,{Totals.TotalApplicantsByCompany}, ");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"summary_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        public IActionResult OnGetExportExcel()
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadData();
            GeneratedAt = DateTime.Now;

            using var wb = new XLWorkbook();
            var ws1 = wb.Worksheets.Add("By Programme");
            ws1.Cell("A1").Value = "Programme";
            ws1.Cell("B1").Value = "No of Student";
            ws1.Cell("C1").Value = "Success";
            ws1.Cell("D1").Value = "Failed";
            ws1.Cell("E1").Value = "Pending";
            ws1.Cell("F1").Value = "Rejected";

            var rowIdx = 2;
            foreach (var row in ProgrammeSummary)
            {
                ws1.Cell(rowIdx, 1).Value = row.Programme;
                ws1.Cell(rowIdx, 2).Value = row.NoOfStudent;
                ws1.Cell(rowIdx, 3).Value = row.Success;
                ws1.Cell(rowIdx, 4).Value = row.Failed;
                ws1.Cell(rowIdx, 5).Value = row.Pending;
                ws1.Cell(rowIdx, 6).Value = row.Rejected;
                rowIdx++;
            }
            ws1.Cell(rowIdx, 1).Value = "Total";
            ws1.Cell(rowIdx, 2).Value = Totals.TotalStudents;
            ws1.Cell(rowIdx, 3).Value = Totals.TotalSuccess;
            ws1.Cell(rowIdx, 4).Value = Totals.TotalFailed;
            ws1.Cell(rowIdx, 5).Value = Totals.TotalPending;
            ws1.Cell(rowIdx, 6).Value = Totals.TotalRejected;
            ws1.Range(1, 1, rowIdx, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws1.Range(1, 1, rowIdx, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws1.Columns().AdjustToContents();

            var ws2 = wb.Worksheets.Add("By Company");
            ws2.Cell("A1").Value = "Company Name";
            ws2.Cell("B1").Value = "Average Allowance";
            ws2.Cell("C1").Value = "Applicant Count";
            ws2.Cell("D1").Value = "Programme";
            rowIdx = 2;
            foreach (var row in CompanySummary)
            {
                ws2.Cell(rowIdx, 1).Value = row.CompanyName;
                ws2.Cell(rowIdx, 2).Value = row.AverageAllowance;
                ws2.Cell(rowIdx, 3).Value = row.ApplicantCount;
                ws2.Cell(rowIdx, 4).Value = row.Programmes;
                rowIdx++;
            }
            ws2.Cell(rowIdx, 1).Value = "Total";
            ws2.Cell(rowIdx, 3).Value = Totals.TotalApplicantsByCompany;
            ws2.Range(1, 1, rowIdx, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws2.Range(1, 1, rowIdx, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"summary_report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private void LoadData()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var userName = HttpContext.Session.GetString("UserName") ?? string.Empty;

            var cohortList = _db.Cohorts.AsNoTracking()
                .OrderByDescending(c => c.startDate)
                .ToList();

            CohortOptions = cohortList
                .Select(c => new SelectListItem($"{c.description} ({c.cohort_id})", c.cohort_id.ToString()))
                .ToList();

            var query = _db.StudentApplications.AsNoTracking()
                .Where(s =>
                    s.ucSupervisorEmail == userEmail ||
                    s.comSupervisorEmail == userEmail ||
                    s.ucSupervisor == userName ||
                    s.comSupervisor == userName);

            if (cohortId.HasValue)
            {
                query = query.Where(s => s.cohortId == cohortId.Value);
            }

            if (level.HasValue)
            {
                query = query.Where(s => s.level == level.Value);
            }

            var students = query.ToList();

            ProgrammeSummary = students
                .GroupBy(s => string.IsNullOrWhiteSpace(s.programme) ? "N/A" : s.programme!)
                .Select(g => new ProgrammeSummaryItem
                {
                    Programme = g.Key,
                    NoOfStudent = g.Count(),
                    Success = g.Count(x => string.Equals(x.applyStatus, "approved", StringComparison.OrdinalIgnoreCase)),
                    Pending = g.Count(x => string.Equals(x.applyStatus, "pending", StringComparison.OrdinalIgnoreCase)),
                    Rejected = g.Count(x => string.Equals(x.applyStatus, "rejected", StringComparison.OrdinalIgnoreCase)),
                    Failed = g.Count(x => !string.Equals(x.applyStatus, "approved", StringComparison.OrdinalIgnoreCase)
                                          && !string.Equals(x.applyStatus, "pending", StringComparison.OrdinalIgnoreCase)
                                          && !string.Equals(x.applyStatus, "rejected", StringComparison.OrdinalIgnoreCase))
                })
                .OrderBy(x => x.Programme)
                .ToList();

            CompanySummary = students
                .GroupBy(s => string.IsNullOrWhiteSpace(s.comName) ? "N/A" : s.comName!)
                .Select(g => new CompanySummaryItem
                {
                    CompanyName = g.Key,
                    AverageAllowance = g.Where(x => x.allowance.HasValue).Any()
                        ? g.Where(x => x.allowance.HasValue).Average(x => x.allowance) ?? 0m
                        : 0m,
                    ApplicantCount = g.Count(),
                    Programmes = string.Join(", ", g.Select(x => x.programme).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
                })
                .OrderBy(x => x.CompanyName)
                .ToList();

            Totals = new TotalsItem
            {
                TotalStudents = ProgrammeSummary.Sum(x => x.NoOfStudent),
                TotalSuccess = ProgrammeSummary.Sum(x => x.Success),
                TotalFailed = ProgrammeSummary.Sum(x => x.Failed),
                TotalPending = ProgrammeSummary.Sum(x => x.Pending),
                TotalRejected = ProgrammeSummary.Sum(x => x.Rejected),
                TotalApplicantsByCompany = CompanySummary.Sum(x => x.ApplicantCount)
            };
        }

        private bool IsSupervisor(out int userId)
        {
            userId = 0;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId);
        }

        private static string EscapeCsv(string? value)
        {
            var v = value ?? string.Empty;
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            {
                v = "\"" + v.Replace("\"", "\"\"") + "\"";
            }
            return v;
        }

        public static string GetLevelLabel(byte? value)
        {
            return value switch
            {
                0 => "0 - Unknown",
                1 => "1 - Diploma",
                2 => "2 - Degree",
                3 => "3 - Master",
                4 => "4 - PhD",
                _ => "All"
            };
        }

        public class ProgrammeSummaryItem
        {
            public string Programme { get; set; } = "N/A";
            public int NoOfStudent { get; set; }
            public int Success { get; set; }
            public int Failed { get; set; }
            public int Pending { get; set; }
            public int Rejected { get; set; }
        }

        public class CompanySummaryItem
        {
            public string CompanyName { get; set; } = "N/A";
            public decimal AverageAllowance { get; set; }
            public int ApplicantCount { get; set; }
            public string Programmes { get; set; } = "-";
        }

        public class TotalsItem
        {
            public int TotalStudents { get; set; }
            public int TotalSuccess { get; set; }
            public int TotalFailed { get; set; }
            public int TotalPending { get; set; }
            public int TotalRejected { get; set; }
            public int TotalApplicantsByCompany { get; set; }
        }
    }
}
