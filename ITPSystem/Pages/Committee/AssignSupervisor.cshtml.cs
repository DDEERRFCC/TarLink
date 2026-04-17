using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

public class CommitteeAssignSupervisorModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeAssignSupervisorModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<SupervisorOptionItem> SupervisorOptions { get; private set; } = new();
    public List<StudentAssignmentItem> Students { get; private set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Please select at least one student.")]
        public List<int> ApplicationIds { get; set; } = new();

        [Required(ErrorMessage = "Please select a supervisor.")]
        public string UcSupervisor { get; set; } = string.Empty;

        public string? UcSupervisorEmail { get; set; }
        public string? UcSupervisorContact { get; set; }
    }

    public class SupervisorOptionItem
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Contact { get; set; }
    }

    public class StudentAssignmentItem
    {
        public int ApplicationId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Programme { get; set; } = string.Empty;
        public string? SupervisorName { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadSupervisorOptions();
        LoadStudents();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadSupervisorOptions();
        LoadStudents();

        if (!ModelState.IsValid)
        {
            SyncSelectedSupervisorFields();
            return Page();
        }

        var applicationIds = Input.ApplicationIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (!applicationIds.Any())
        {
            ModelState.AddModelError(nameof(Input.ApplicationIds), "Please select at least one student.");
            SyncSelectedSupervisorFields();
            return Page();
        }

        var selectedSupervisor = SupervisorOptions.FirstOrDefault(s =>
            string.Equals(s.Name, Input.UcSupervisor?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (selectedSupervisor == null)
        {
            ModelState.AddModelError(nameof(Input.UcSupervisor), "Please select a valid supervisor.");
            SyncSelectedSupervisorFields();
            return Page();
        }

        var students = _db.StudentApplications
            .Where(s => applicationIds.Contains(s.application_id))
            .ToList();

        if (!students.Any())
        {
            TempData["StatusMessage"] = "Student record was not found.";
            return RedirectToPage("/Committee/Students");
        }

        var timestamp = DateTime.Now;
        foreach (var student in students)
        {
            student.ucSupervisor = selectedSupervisor.Name;
            student.ucSupervisorEmail = string.IsNullOrWhiteSpace(selectedSupervisor.Email) ? null : selectedSupervisor.Email.Trim();
            student.ucSupervisorContact = string.IsNullOrWhiteSpace(selectedSupervisor.Contact) ? null : selectedSupervisor.Contact.Trim();
            student.updated_at = timestamp;
        }

        _db.SaveChanges();
        TempData["StatusMessage"] = students.Count == 1
            ? "Supervisor assigned successfully."
            : $"Supervisor assigned successfully to {students.Count} students.";
        return RedirectToPage("/Committee/Students");
    }

    private void LoadSupervisorOptions()
    {
        SupervisorOptions = _db.UcSupervisors.AsNoTracking()
            .Where(s => s.isActive && !string.IsNullOrWhiteSpace(s.name))
            .OrderBy(s => s.name)
            .Select(s => new SupervisorOptionItem
            {
                Name = s.name.Trim(),
                Email = string.IsNullOrWhiteSpace(s.email) ? null : s.email.Trim(),
                Contact = string.IsNullOrWhiteSpace(s.contact) ? null : s.contact.Trim()
            })
            .ToList();
    }

    private void LoadSelectedStudents(IEnumerable<int> applicationIds)
    {
        var idList = applicationIds.Distinct().ToList();
        Students = _db.StudentApplications.AsNoTracking()
            .Where(s => idList.Contains(s.application_id))
            .OrderBy(s => s.studentName)
            .Select(s => new StudentAssignmentItem
            {
                ApplicationId = s.application_id,
                StudentName = s.studentName ?? string.Empty,
                StudentId = s.studentID ?? string.Empty,
                Programme = s.programme ?? string.Empty,
                SupervisorName = s.ucSupervisor
            })
            .ToList();
    }

    private void LoadStudents()
    {
        LoadSelectedStudents(_db.StudentApplications.AsNoTracking()
            .Where(s => string.IsNullOrWhiteSpace(s.ucSupervisor))
            .OrderBy(s => s.studentName)
            .Select(s => s.application_id)
            .ToList());
    }

    private void SyncSelectedSupervisorFields()
    {
        var selectedSupervisor = SupervisorOptions.FirstOrDefault(s =>
            string.Equals(s.Name, Input.UcSupervisor?.Trim(), StringComparison.OrdinalIgnoreCase));

        Input.UcSupervisorEmail = selectedSupervisor?.Email;
        Input.UcSupervisorContact = selectedSupervisor?.Contact;
    }
}
