using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using System.ComponentModel.DataAnnotations;

public class CommitteeStudentDetailsModel : CommitteePageModelBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CommitteeStudentDetailsModel(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public List<Cohort> Cohorts { get; private set; } = new();
    public List<CompanyOptionItem> CompanyOptions { get; private set; } = new();
    public List<SupervisorOptionItem> SupervisorOptions { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsCreateMode { get; private set; }

    [BindProperty]
    public StudentDetailInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? FormAcceptanceFile { get; set; }

    [BindProperty]
    public IFormFile? FormAcknowledgementFile { get; set; }

    [BindProperty]
    public IFormFile? LetterIdentityFile { get; set; }

    [BindProperty]
    public IFormFile? OtherEvidenceFile { get; set; }

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
        public string applyStatus { get; set; } = "none";

        public string? remark { get; set; }
        public string? formAcceptance { get; set; }
        public string? formAcknowledgement { get; set; }
        public string? letterIdentity { get; set; }
        public string? otherEvidence { get; set; }
        public string? doVerifier { get; set; }
        public string? doVerifierEmail { get; set; }
        public bool isAgreed { get; set; }
    }

    public class CompanyOptionItem
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Addresses { get; set; } = new();
    }

    public class SupervisorOptionItem
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Contact { get; set; }
    }

    public IActionResult OnGet(int? applicationId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCohorts();
        LoadCompanyOptions();
        LoadSupervisorOptions();

        if (!applicationId.HasValue || applicationId.Value <= 0)
        {
            IsCreateMode = true;
            InitializeNewStudentInput();
            return Page();
        }

        var student = _db.StudentApplications.AsNoTracking()
            .FirstOrDefault(s => s.application_id == applicationId.Value);
        if (student == null)
        {
            TempData["StatusMessage"] = "Student record was not found.";
            return RedirectToPage("/Committee/Students");
        }

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
        LoadCompanyOptions();
        LoadSupervisorOptions();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ValidateUpload(FormAcceptanceFile, "Form Acceptance");
        ValidateUpload(FormAcknowledgementFile, "Form Acknowledgement");
        ValidateUpload(LetterIdentityFile, "Letter Identity");
        ValidateUpload(OtherEvidenceFile, "Other Evidence");

        if (!ModelState.IsValid)
        {
            IsCreateMode = Input.application_id <= 0;
            return Page();
        }

        var isCreateMode = Input.application_id <= 0;

        if (_db.StudentApplications.Any(s => (isCreateMode || s.application_id != Input.application_id) && s.number_ic == Input.number_ic.Trim()))
        {
            ModelState.AddModelError(nameof(Input.number_ic), "IC number already exists.");
        }
        if (_db.StudentApplications.Any(s => (isCreateMode || s.application_id != Input.application_id) && s.studentID == Input.studentID.Trim()))
        {
            ModelState.AddModelError(nameof(Input.studentID), "Student ID already exists.");
        }
        if (_db.StudentApplications.Any(s => (isCreateMode || s.application_id != Input.application_id) && s.studentEmail == Input.studentEmail.Trim()))
        {
            ModelState.AddModelError(nameof(Input.studentEmail), "Student email already exists.");
        }

        if (!ModelState.IsValid)
        {
            IsCreateMode = isCreateMode;
            return Page();
        }

        StudentApplication student;
        if (isCreateMode)
        {
            student = new StudentApplication
            {
                created_at = DateTime.Now
            };
            _db.StudentApplications.Add(student);
        }
        else
        {
            student = _db.StudentApplications.FirstOrDefault(s => s.application_id == Input.application_id)!;
            if (student == null)
            {
                TempData["StatusMessage"] = "Student record was not found.";
                return RedirectToPage("/Committee/Students");
            }
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
        student.formAcceptance = SaveUploadedFile(FormAcceptanceFile, "formAcceptance", student.formAcceptance, student);
        student.formAcknowledgement = SaveUploadedFile(FormAcknowledgementFile, "formAcknowledgement", student.formAcknowledgement, student);
        student.letterIdentity = SaveUploadedFile(LetterIdentityFile, "letterIdentity", student.letterIdentity, student);
        student.otherEvidence = SaveUploadedFile(OtherEvidenceFile, "otherEvidence", student.otherEvidence, student);
        student.doVerifier = TrimOrNull(Input.doVerifier);
        student.doVerifierEmail = TrimOrNull(Input.doVerifierEmail);
        student.isAgreed = Input.isAgreed;
        student.updated_at = DateTime.Now;

        _db.SaveChanges();
        StatusMessage = isCreateMode
            ? "Student created successfully."
            : "Student details updated successfully.";
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

        var storedPath = GetDocumentFileName(student, field);
        var fileName = Path.GetFileName(storedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var fullPath = ResolveUploadFullPath(storedPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
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

    private string? ResolveUploadFullPath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        var normalizedPath = storedPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var combinedPath = Path.GetFullPath(Path.Combine(uploadsRoot, normalizedPath));
        var uploadsRootFullPath = Path.GetFullPath(uploadsRoot);

        if (!combinedPath.StartsWith(uploadsRootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return combinedPath;
    }

    private void LoadCohorts()
    {
        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.isActive)
            .ThenBy(c => c.description)
            .ToList();
    }

    private void LoadCompanyOptions()
    {
        var companies = _db.Companies.AsNoTracking()
            .Include(c => c.Branches)
            .OrderBy(c => c.name)
            .ToList();

        CompanyOptions = companies
            .Select(c => new CompanyOptionItem
            {
                Name = c.name,
                Addresses = c.Branches
                    .OrderByDescending(b => b.is_hq)
                    .ThenBy(b => b.branch_id)
                    .Select(FormatBranchAddress)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct()
                    .ToList()
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && c.Addresses.Count > 0)
            .ToList();
    }

    private void LoadSupervisorOptions()
    {
        SupervisorOptions = _db.UcSupervisors.AsNoTracking()
            .Where(s => s.isActive && !string.IsNullOrWhiteSpace(s.name))
            .Select(s => new SupervisorOptionItem
            {
                Name = s.name.Trim(),
                Email = string.IsNullOrWhiteSpace(s.email) ? null : s.email.Trim(),
                Contact = string.IsNullOrWhiteSpace(s.contact) ? null : s.contact.Trim()
            })
            .ToList()
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Email))
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Contact))
                .First())
            .OrderBy(s => s.Name)
            .ToList();
    }

    private void MapStudentToInput(StudentApplication student)
    {
        IsCreateMode = false;
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

    private void InitializeNewStudentInput()
    {
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
        Input = new StudentDetailInput
        {
            application_id = 0,
            gender = "O",
            applyStatus = "none",
            ownTransport = false,
            isAgreed = false,
            level = 1,
            cohortId = Cohorts.FirstOrDefault()?.cohort_id ?? 0,
            programme = string.Empty
        };
    }

    private static string NormalizeStatus(string? rawStatus)
    {
        var value = (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "none" => "none",
            "approved" => "approved",
            "rejected" => "rejected",
            "withdrawn" => "withdrawn",
            "pending" => "pending",
            _ => "none"
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

    private static string FormatBranchAddress(CompanyBranch branch)
    {
        var parts = new[]
        {
            branch.address_line,
            branch.city,
            branch.state,
            branch.postcode,
            branch.country
        };

        return string.Join(", ", parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));
    }

    private void ValidateUpload(IFormFile? file, string label)
    {
        if (file == null)
        {
            return;
        }

        if (file.Length <= 0)
        {
            ModelState.AddModelError(string.Empty, $"{label}: uploaded file is empty.");
            return;
        }

        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(string.Empty, $"{label}: file must not exceed 10MB.");
        }
    }

    private string? SaveUploadedFile(IFormFile? file, string prefix, string? existingFileName, StudentApplication student)
    {
        if (file == null || file.Length <= 0)
        {
            return existingFileName;
        }

        var studentFolderPath = EnsureStudentDocumentFolder(student);
        var savedName = BuildSafeUploadFileName(file.FileName, prefix);
        var fullPath = Path.Combine(studentFolderPath, savedName);

        var existingFullPath = ResolveUploadFullPath(existingFileName);
        if (!string.IsNullOrWhiteSpace(existingFullPath) && System.IO.File.Exists(existingFullPath))
        {
            try
            {
                System.IO.File.Delete(existingFullPath);
            }
            catch
            {
                // keep upload success even if cleanup fails
            }
        }

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        var relativeFolder = Path.GetRelativePath(Path.Combine(_env.WebRootPath, "uploads"), studentFolderPath)
            .Replace('\\', '/');
        return $"{relativeFolder}/{savedName}";
    }

    private string EnsureStudentDocumentFolder(StudentApplication student)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);

        var cohortFolderName = BuildSafeFolderName(Cohorts.FirstOrDefault(c => c.cohort_id == student.cohortId)?.description, $"Cohort_{student.cohortId}");
        var cohortFolderPath = Path.Combine(uploadsRoot, "Cohorts", cohortFolderName);
        Directory.CreateDirectory(cohortFolderPath);

        var studentFolderName = BuildSafeFolderName(student.studentName, $"Student_{student.application_id}");
        var studentFolderPath = Path.Combine(cohortFolderPath, studentFolderName);
        Directory.CreateDirectory(studentFolderPath);

        return studentFolderPath;
    }

    private static string BuildSafeFolderName(string? rawValue, string fallback)
    {
        var baseValue = string.IsNullOrWhiteSpace(rawValue) ? fallback : rawValue.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseValue
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        sanitized = string.Join("_", sanitized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string BuildSafeUploadFileName(string? originalFileName, string fallbackPrefix)
    {
        var rawFileName = Path.GetFileName(originalFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return fallbackPrefix;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawFileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? fallbackPrefix : sanitized;
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
