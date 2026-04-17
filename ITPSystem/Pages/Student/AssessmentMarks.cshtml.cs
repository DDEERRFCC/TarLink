using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Student
{
    public class AssessmentMarksModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public AssessmentMarksModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<MarkGroupItem> GroupedMarks { get; set; } = new();
        public StudentApplication? Student { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal TotalScore { get; set; }
        public decimal TotalMaxScore { get; set; }
        public string OverallRemarks { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool HasAssessments { get; set; }

        public IActionResult OnGet()
        {
            if (!TryGetStudentContext(out var userId, out var studentEmail))
            {
                return RedirectToPage("/Login/StudentLogin");
            }

            LoadAssessmentMarks(userId, studentEmail);
            return Page();
        }

        private void LoadAssessmentMarks(int userId, string studentEmail)
        {
            // Get student application
            Student = _db.StudentApplications.AsNoTracking()
                .FirstOrDefault(s => s.studentEmail == studentEmail);

            if (Student == null)
            {
                Message = "Student application not found.";
                HasAssessments = false;
                return;
            }

            StudentName = Student.studentName;
            StudentId = Student.studentID;
            CompanyName = Student.comName ?? "-";

            // Get assessment marks
            var marks = _db.AssessmentMarks.AsNoTracking()
                .Where(m => m.application_id == Student.application_id)
                .OrderBy(m => m.rubric_item)
                .ToList();

            if (marks.Count == 0)
            {
                Message = "No assessment marks have been submitted yet.";
                HasAssessments = false;
                return;
            }

            HasAssessments = true;

            // Group marks by rubric item
            GroupedMarks = marks
                .GroupBy(m => m.rubric_item)
                .Select(g => new MarkGroupItem
                {
                    RubricItem = g.Key,
                    Score = g.OrderByDescending(x => x.created_at).First().score,
                    MaxScore = g.OrderByDescending(x => x.created_at).First().max_score,
                    Remarks = g.OrderByDescending(x => x.created_at).First().remarks,
                    SubmittedAt = g.OrderByDescending(x => x.created_at).First().created_at
                })
                .ToList();

            TotalScore = GroupedMarks.Sum(m => m.Score);
            TotalMaxScore = GroupedMarks.Sum(m => m.MaxScore);
            OverallRemarks = GroupedMarks.FirstOrDefault()?.Remarks ?? "-";

            // Format display order
            var rubricOrder = new Dictionary<string, int>
            {
                { "Submission of progress reports (CLO6)", 1 },
                { "Content of progress reports (CLO1)", 2 },
                { "Submission of final report (CLO6)", 3 },
                { "Written presentation of final report (CLO4)", 4 },
                { "Awareness of business and entrepreneurial opportunities (CLO5)", 5 },
                { "Overall content of final report (CLO4)", 6 }
            };

            GroupedMarks = GroupedMarks
                .OrderBy(m => rubricOrder.ContainsKey(m.RubricItem) ? rubricOrder[m.RubricItem] : 99)
                .ToList();
        }

        private bool TryGetStudentContext(out int userId, out string studentEmail)
        {
            userId = 0;
            studentEmail = string.Empty;

            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            studentEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;

            return role == "student"
                && int.TryParse(rawUserId, out userId)
                && !string.IsNullOrWhiteSpace(studentEmail);
        }

        public class MarkGroupItem
        {
            public string RubricItem { get; set; } = string.Empty;
            public decimal Score { get; set; }
            public decimal MaxScore { get; set; }
            public string Remarks { get; set; } = string.Empty;
            public DateTime SubmittedAt { get; set; }
            public decimal Percentage => MaxScore > 0 ? Math.Round((Score / MaxScore) * 100, 2) : 0;
        }
    }
}
