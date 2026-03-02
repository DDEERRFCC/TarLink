using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class StudentsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public StudentsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<StudentApplication> Students { get; set; } = new();
        private Dictionary<int, Cohort> CohortMap { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (role != "supervisor" || string.IsNullOrWhiteSpace(userEmail))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            CohortMap = _db.Cohorts.AsNoTracking().ToDictionary(c => c.cohort_id);
            Students = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();
            return Page();
        }

        public string GetCohortRange(int cohortId)
        {
            if (!CohortMap.TryGetValue(cohortId, out var cohort))
            {
                return "-";
            }

            var start = cohort.startDate?.ToString("yyyy-MM-dd") ?? "-";
            var end = cohort.endDate?.ToString("yyyy-MM-dd") ?? "-";
            return $"{start} to {end}";
        }
    }
}

