using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using System.ComponentModel.DataAnnotations;

public class CommitteeStudentDetailsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeStudentDetailsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Cohort> Cohorts { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BindProperty]
    public StudentDetailInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class StudentDetailInput
    {
        [Required]
        public int application_id { get; set; }

        [Required]
        [StringLength(20)]
        public string number_ic { get; set; } = string.Empty;

        [Required]
        [StringLength(45)]
        public string studentID { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string studentName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string studentEmail { get; set; } = string.Empty;

        public DateTime? contactDate { get; set; }

        [Required]
        [Range(1, 2)]
        public byte? level { get; set; }

        [Required]
        [StringLength(1)]
        public string gender { get; set; } = "O";

        public decimal? CGPA { get; set; }

        [Required]
        public int cohortId { get; set; }

        [Required]
        [StringLength(4)]
        public string programme { get; set; } = string.Empty;

        [Required]
        public int? groupNo { get; set; }

        public string? comName { get; set; }
        public string? comAddress { get; set; }
        public string? comSupervisor { get; set; }
        public string? comSupervisorEmail { get; set; }
        public string? comSupervisorContact { get; set; }
        public decimal? allowance { get; set; }
        public string? ucSupervisor { get; set; }
        public string? ucSupervisorEmail { get; set; }
        public string? ucSupervisorContact { get; set; }
        public string? personalEmail { get; set; }
        public string? tempAddress { get; set; }
        public string? permanentAddress { get; set; }
        public string? permanentContact { get; set; }
        public bool ownTransport { get; set; }
        public string? healthRemark { get; set; }
        public string? programmingKnowledge { get; set; }
        public string? databaseKnowledge { get; set; }
        public string? networkingKnowledge { get; set; }
        public byte? templateVersion { get; set; }

        [Required]
        public string applyStatus { get; set; } = "pending";

        public string? remark { get; set; }
        public string? formAcceptance { get; set; }
        public string? formAcknowledgement { get; set; }
        public string? letterIdentity { get; set; }
        public string? otherEvidence { get; set; }
        public string? doVerifier { get; set; }
        public string? doVerifierEmail { get; set; }
        public bool isAgreed { get; set; }
    }

    public IActionResult OnGet(int applicationId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var student = _db.StudentApplications.AsNoTracking()
            .FirstOrDefault(s => s.application_id == applicationId);
        if (student == null)
        {
            TempData["StatusMessage"] = "Student record was not found.";
            return RedirectToPage("/Committee/Students");
        }

        LoadCohorts();
        MapStudentToInput(student);
        return Page();
    }

    public IActionResult OnPostSave()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCohorts();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var student = _db.StudentApplications.FirstOrDefault(s => s.application_id == Input.application_id);
        if (student == null)
        {
            TempData["StatusMessage"] = "Student record was not found.";
            return RedirectToPage("/Committee/Students");
        }

        if (_db.StudentApplications.Any(s => s.application_id != Input.application_id && s.number_ic == Input.number_ic.Trim()))
        {
            ModelState.AddModelError(nameof(Input.number_ic), "IC number already exists.");
        }
        if (_db.StudentApplications.Any(s => s.application_id != Input.application_id && s.studentID == Input.studentID.Trim()))
        {
            ModelState.AddModelError(nameof(Input.studentID), "Student ID already exists.");
        }
        if (_db.StudentApplications.Any(s => s.application_id != Input.application_id && s.studentEmail == Input.studentEmail.Trim()))
        {
            ModelState.AddModelError(nameof(Input.studentEmail), "Student email already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        student.number_ic = Input.number_ic.Trim();
        student.studentID = Input.studentID.Trim();
        student.studentName = Input.studentName.Trim();
        student.studentEmail = Input.studentEmail.Trim();
        student.contactDate = Input.contactDate;
        student.level = Input.level;
        student.gender = NormalizeGender(Input.gender);
        student.CGPA = Input.CGPA;
        student.cohortId = Input.cohortId;
        student.programme = Input.programme.Trim();
        student.groupNo = Input.groupNo;
        student.comName = TrimOrNull(Input.comName);
        student.comAddress = TrimOrNull(Input.comAddress);
        student.comSupervisor = TrimOrNull(Input.comSupervisor);
        student.comSupervisorEmail = TrimOrNull(Input.comSupervisorEmail);
        student.comSupervisorContact = TrimOrNull(Input.comSupervisorContact);
        student.allowance = Input.allowance;
        student.ucSupervisor = TrimOrNull(Input.ucSupervisor);
        student.ucSupervisorEmail = TrimOrNull(Input.ucSupervisorEmail);
        student.ucSupervisorContact = TrimOrNull(Input.ucSupervisorContact);
        student.personalEmail = TrimOrNull(Input.personalEmail);
        student.tempAddress = TrimOrNull(Input.tempAddress);
        student.permanentAddress = TrimOrNull(Input.permanentAddress);
        student.permanentContact = TrimOrNull(Input.permanentContact);
        student.ownTransport = Input.ownTransport;
        student.healthRemark = TrimOrNull(Input.healthRemark);
        student.programmingKnowledge = TrimOrNull(Input.programmingKnowledge);
        student.databaseKnowledge = TrimOrNull(Input.databaseKnowledge);
        student.networkingKnowledge = TrimOrNull(Input.networkingKnowledge);
        student.templateVersion = Input.templateVersion;
        student.applyStatus = NormalizeStatus(Input.applyStatus);
        student.remark = TrimOrNull(Input.remark);
        student.formAcceptance = TrimOrNull(Input.formAcceptance);
        student.formAcknowledgement = TrimOrNull(Input.formAcknowledgement);
        student.letterIdentity = TrimOrNull(Input.letterIdentity);
        student.otherEvidence = TrimOrNull(Input.otherEvidence);
        student.doVerifier = TrimOrNull(Input.doVerifier);
        student.doVerifierEmail = TrimOrNull(Input.doVerifierEmail);
        student.isAgreed = Input.isAgreed;
        student.updated_at = DateTime.Now;

        _db.SaveChanges();
        StatusMessage = "Student details updated successfully.";
        return RedirectToPage(new { applicationId = student.application_id });
    }

    public IActionResult OnPostDelete(int applicationId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var student = _db.StudentApplications.FirstOrDefault(s => s.application_id == applicationId);
        if (student == null)
        {
            TempData["StatusMessage"] = "Student record was not found.";
            return RedirectToPage("/Committee/Students");
        }

        _db.StudentApplications.Remove(student);
        _db.SaveChanges();

        TempData["StatusMessage"] = "Student deleted successfully.";
        return RedirectToPage("/Committee/Students");
    }

    public IActionResult OnGetDocument(int applicationId, string field, bool download = false)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var student = _db.StudentApplications.AsNoTracking()
            .FirstOrDefault(s => s.application_id == applicationId);
        if (student == null)
        {
            return NotFound();
        }

        var fileName = Path.GetFileName(GetDocumentFileName(student, field));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return download
            ? PhysicalFile(fullPath, contentType, fileName)
            : PhysicalFile(fullPath, contentType);
    }

    private void LoadCohorts()
    {
        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.isActive)
            .ThenBy(c => c.description)
            .ToList();
    }

    private void MapStudentToInput(StudentApplication student)
    {
        CreatedAt = student.created_at;
        UpdatedAt = student.updated_at;

        Input = new StudentDetailInput
        {
            application_id = student.application_id,
            number_ic = student.number_ic,
            studentID = student.studentID,
            studentName = student.studentName,
            studentEmail = student.studentEmail,
            contactDate = student.contactDate,
            level = student.level,
            gender = NormalizeGender(student.gender),
            CGPA = student.CGPA,
            cohortId = student.cohortId,
            programme = student.programme ?? string.Empty,
            groupNo = student.groupNo,
            comName = student.comName,
            comAddress = student.comAddress,
            comSupervisor = student.comSupervisor,
            comSupervisorEmail = student.comSupervisorEmail,
            comSupervisorContact = student.comSupervisorContact,
            allowance = student.allowance,
            ucSupervisor = student.ucSupervisor,
            ucSupervisorEmail = student.ucSupervisorEmail,
            ucSupervisorContact = student.ucSupervisorContact,
            personalEmail = student.personalEmail,
            tempAddress = student.tempAddress,
            permanentAddress = student.permanentAddress,
            permanentContact = student.permanentContact,
            ownTransport = student.ownTransport,
            healthRemark = student.healthRemark,
            programmingKnowledge = student.programmingKnowledge,
            databaseKnowledge = student.databaseKnowledge,
            networkingKnowledge = student.networkingKnowledge,
            templateVersion = student.templateVersion,
            applyStatus = NormalizeStatus(student.applyStatus),
            remark = student.remark,
            formAcceptance = student.formAcceptance,
            formAcknowledgement = student.formAcknowledgement,
            letterIdentity = student.letterIdentity,
            otherEvidence = student.otherEvidence,
            doVerifier = student.doVerifier,
            doVerifierEmail = student.doVerifierEmail,
            isAgreed = student.isAgreed
        };
    }

    private static string NormalizeStatus(string? rawStatus)
    {
        var value = (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            "withdrawn" => "withdrawn",
            _ => "pending"
        };
    }

    private static string NormalizeGender(string? rawGender)
    {
        var value = (rawGender ?? "O").Trim().ToUpperInvariant();
        return value switch
        {
            "M" => "M",
            "F" => "F",
            _ => "O"
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? GetDocumentFileName(StudentApplication student, string field)
    {
        return (field ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "formacceptance" => student.formAcceptance,
            "formacknowledgement" => student.formAcknowledgement,
            "letteridentity" => student.letterIdentity,
            "otherevidence" => student.otherEvidence,
            _ => null
        };
    }
}
