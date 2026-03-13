using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace ITPSystem.Pages.Supervisor
{
    public class MarksModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public MarksModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<StudentApplication> Students { get; set; } = new();
        public List<MarkViewItem> MarkItems { get; set; } = new();
        public List<MarkViewItem> DetailItems { get; set; } = new();
        public List<StudentSummary> Summaries { get; set; } = new();
        public List<RubricDefinition> RubricDefinitions { get; } = BuildRubricDefinitions();

        [BindProperty]
        public MarksInput Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? applicationId { get; set; }

        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadData(userEmail);
            InitializeRubrics();
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!IsSupervisor(out var supervisorId, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var appId = Input.ApplicationId;
            if (appId <= 0)
            {
                ModelState.AddModelError(nameof(Input.ApplicationId), "Please select a student.");
            }
            else
            {
                var assignedStudent = _db.StudentApplications.AsNoTracking()
                    .FirstOrDefault(s => s.application_id == appId
                        && (s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail));
                if (assignedStudent == null)
                {
                    ModelState.AddModelError(nameof(Input.ApplicationId), "Selected student is not assigned to you.");
                }
            }

            if (string.IsNullOrWhiteSpace(Input.OverallRemarks))
            {
                ModelState.AddModelError(nameof(Input.OverallRemarks), "Please provide overall comments.");
            }

            if (Input.RubricScores == null || Input.RubricScores.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Rubric scores are required.");
            }

            var rubricMap = RubricDefinitions.ToDictionary(r => r.Item);
            if (Input.RubricScores != null)
            {
                foreach (var item in Input.RubricScores)
                {
                    if (!rubricMap.TryGetValue(item.RubricItem, out var def))
                    {
                        ModelState.AddModelError(string.Empty, $"Invalid rubric item: {item.RubricItem}");
                        continue;
                    }

                    if (item.Score == null)
                    {
                        ModelState.AddModelError(string.Empty, $"Score is required for {def.Item}.");
                        continue;
                    }

                    if (item.Score < 0 || item.Score > def.MaxScore)
                    {
                        ModelState.AddModelError(string.Empty, $"Score for {def.Item} must be between 0 and {def.MaxScore}.");
                        continue;
                    }

                    if (item.MaxScore != def.MaxScore)
                    {
                        ModelState.AddModelError(string.Empty, $"Max score mismatch for {def.Item}.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                LoadData(userEmail);
                InitializeRubrics();
                return Page();
            }

            var rubricItems = rubricMap.Keys.ToList();
            var existing = _db.AssessmentMarks
                .Where(m => m.application_id == appId && rubricItems.Contains(m.rubric_item))
                .ToList();
            if (existing.Count > 0)
            {
                _db.AssessmentMarks.RemoveRange(existing);
            }

            var rubricScores = Input.RubricScores ?? new List<RubricScoreInput>();
            foreach (var item in rubricScores)
            {
                var def = rubricMap[item.RubricItem];
                _db.AssessmentMarks.Add(new AssessmentMark
                {
                    application_id = appId,
                    supervisor_user_id = supervisorId,
                    rubric_item = def.Item,
                    score = item.Score!.Value,
                    max_score = def.MaxScore,
                    remarks = Input.OverallRemarks.Trim(),
                    created_at = DateTime.UtcNow
                });
            }
            _db.SaveChanges();

            var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == appId);
            if (studentUser != null)
            {
                _db.Notifications.Add(new Notification
                {
                    from_user_id = supervisorId,
                    to_user_id = studentUser.user_id,
                    type = "assessment",
                    title = "New assessment mark published",
                    message = "Your supervisor has submitted the assessment marks."
                });
                _db.SaveChanges();
            }

            Message = "Mark saved.";
            LoadData(userEmail);
            InitializeRubrics();
            return Page();
        }

        public IActionResult OnGetExportExcel()
        {
            if (!IsSupervisor(out _, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadData(userEmail);
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Assessment Marks");
            ws.Cell("A1").Value = "Student Name";
            ws.Cell("B1").Value = "Student ID";
            ws.Cell("C1").Value = "Date";
            ws.Cell("D1").Value = "Rubric Item";
            ws.Cell("E1").Value = "Score";
            ws.Cell("F1").Value = "Max Score";
            ws.Cell("G1").Value = "Remarks";

            var latestMarks = MarkItems
                .GroupBy(m => new { m.Mark.application_id, m.Mark.rubric_item })
                .Select(g => g.OrderByDescending(x => x.Mark.created_at).First())
                .ToList();

            var row = 2;
            foreach (var student in Students)
            {
                var marks = latestMarks
                    .Where(m => m.Mark.application_id == student.application_id)
                    .OrderBy(m => m.Mark.rubric_item)
                    .ToList();

                if (marks.Count == 0)
                {
                    continue;
                }

                var startRow = row;
                var latestDate = marks.Max(m => m.Mark.created_at);

                foreach (var item in marks)
                {
                    ws.Cell(row, 1).Value = student.studentName;
                    ws.Cell(row, 2).Value = student.studentID;
                    ws.Cell(row, 3).Value = latestDate.ToString("yyyy-MM-dd");
                    ws.Cell(row, 4).Value = item.Mark.rubric_item;
                    ws.Cell(row, 5).Value = item.Mark.score;
                    ws.Cell(row, 6).Value = item.Mark.max_score;
                    ws.Cell(row, 7).Value = item.Mark.remarks;
                    row++;
                }

                if (row - 1 > startRow)
                {
                    ws.Range(startRow, 1, row - 1, 1).Merge();
                    ws.Range(startRow, 2, row - 1, 2).Merge();
                    ws.Range(startRow, 3, row - 1, 3).Merge();
                    ws.Range(startRow, 7, row - 1, 7).Merge();
                }
            }

            if (row > 2)
            {
                ws.Range(1, 1, row - 1, 7).Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                ws.Range(1, 1, row - 1, 7).Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"assessment_marks_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private void LoadData(string userEmail)
        {
            Students = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();

            var appMap = Students.ToDictionary(s => s.application_id, s => s.studentName);
            var idMap = Students.ToDictionary(s => s.application_id, s => s.studentID);
            var appIds = appMap.Keys.ToList();

            MarkItems = _db.AssessmentMarks.AsNoTracking()
                .Where(m => appIds.Contains(m.application_id))
                .OrderByDescending(m => m.created_at)
                .ToList()
                .Select(m => new MarkViewItem
                {
                    Mark = m,
                    StudentName = appMap.TryGetValue(m.application_id, out var name) ? name : "Unknown",
                    StudentId = idMap.TryGetValue(m.application_id, out var id) ? id : "-"
                })
                .ToList();

            if (applicationId.HasValue)
            {
                DetailItems = MarkItems
                    .Where(m => m.Mark.application_id == applicationId.Value)
                    .OrderBy(m => m.Mark.rubric_item)
                    .ToList();
            }

            var latestMarks = MarkItems
                .GroupBy(m => new { m.Mark.application_id, m.Mark.rubric_item })
                .Select(g => g.OrderByDescending(x => x.Mark.created_at).First())
                .ToList();

            var finalReports = _db.ProgressReports.AsNoTracking()
                .Where(r => appIds.Contains(r.applicantId)
                            && r.reportType == "final")
                .OrderByDescending(r => r.updated_at)
                .ToList()
                .GroupBy(r => r.applicantId)
                .ToDictionary(g => g.Key, g => g.First());

            Summaries = Students
                .Select(s =>
                {
                    var marks = latestMarks.Where(m => m.Mark.application_id == s.application_id).ToList();
                    var totalScore = marks.Sum(m => m.Mark.score);
                    var totalMax = marks.Sum(m => m.Mark.max_score);
                    finalReports.TryGetValue(s.application_id, out var report);
                    return new StudentSummary
                    {
                        ApplicationId = s.application_id,
                        StudentName = s.studentName,
                        StudentId = s.studentID,
                        TotalScore = totalScore,
                        TotalMax = totalMax,
                        FinalReportId = report?.report_id
                    };
                })
                .OrderBy(s => s.StudentName)
                .ToList();
        }

        private bool IsSupervisor(out int userId, out string userEmail)
        {
            userId = 0;
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId) && !string.IsNullOrWhiteSpace(userEmail);
        }

        public class MarkViewItem
        {
            public AssessmentMark Mark { get; set; } = new();
            public string StudentName { get; set; } = string.Empty;
            public string StudentId { get; set; } = string.Empty;
        }

        public class StudentSummary
        {
            public int ApplicationId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string StudentId { get; set; } = string.Empty;
            public decimal TotalScore { get; set; }
            public decimal TotalMax { get; set; }
            public long? FinalReportId { get; set; }
        }

        public class MarksInput
        {
            [Required]
            public int ApplicationId { get; set; }

            [Required]
            [StringLength(2000)]
            public string OverallRemarks { get; set; } = string.Empty;

            public List<RubricScoreInput> RubricScores { get; set; } = new();
        }

        public class RubricScoreInput
        {
            public string RubricItem { get; set; } = string.Empty;
            public decimal MaxScore { get; set; }
            public decimal? Score { get; set; }
        }

        public class RubricDefinition
        {
            public string Section { get; set; } = string.Empty;
            public string Item { get; set; } = string.Empty;
            public decimal MaxScore { get; set; }
            public string RatingScale { get; set; } = string.Empty;
        }

        private void InitializeRubrics()
        {
            if (Input.RubricScores.Count == RubricDefinitions.Count)
            {
                return;
            }

            Input.RubricScores = RubricDefinitions
                .Select(r => new RubricScoreInput
                {
                    RubricItem = r.Item,
                    MaxScore = r.MaxScore
                })
                .ToList();
        }

        private static List<RubricDefinition> BuildRubricDefinitions()
        {
            return new List<RubricDefinition>
            {
                new()
                {
                    Section = "Section A. Progress Reports",
                    Item = "Submission of progress reports (CLO6)",
                    MaxScore = 4,
                    RatingScale = "Very Poor (0), Poor (1), Average (2), Good (3), Excellent (4)"
                },
                new()
                {
                    Section = "Section A. Progress Reports",
                    Item = "Content of progress reports (CLO1)",
                    MaxScore = 6,
                    RatingScale = "Very Poor (0), Poor (1-2), Average (3), Good (4-5), Excellent (6)"
                },
                new()
                {
                    Section = "Section B. Final Report",
                    Item = "Submission of final report (CLO6)",
                    MaxScore = 4,
                    RatingScale = "Very Poor (0), Poor (1), Average (2), Good (3), Excellent (4)"
                },
                new()
                {
                    Section = "Section B. Final Report",
                    Item = "Written presentation of final report (CLO4)",
                    MaxScore = 6,
                    RatingScale = "Very Poor (0), Poor (1-2), Average (3), Good (4-5), Excellent (6)"
                },
                new()
                {
                    Section = "Section B. Final Report",
                    Item = "Awareness of business and entrepreneurial opportunities (CLO5)",
                    MaxScore = 10,
                    RatingScale = "Very Poor (0), Poor (1-4), Average (5), Good (6-7), Excellent (8-10)"
                },
                new()
                {
                    Section = "Section B. Final Report",
                    Item = "Overall content of final report (CLO4)",
                    MaxScore = 10,
                    RatingScale = "Very Poor (0), Poor (1-4), Average (5), Good (6-7), Excellent (8-10)"
                }
            };
        }
    }
}


