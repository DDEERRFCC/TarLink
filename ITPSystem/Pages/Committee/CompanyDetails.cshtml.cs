using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class CommitteeCompanyDetailsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeCompanyDetailsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public DateTime CreatedAt { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public bool HasLogo { get; private set; }
    public bool HasSsmCert { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        public int company_id { get; set; }

        [Required]
        [StringLength(250)]
        public string name { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string address1 { get; set; } = string.Empty;

        [StringLength(255)]
        public string? address2 { get; set; }

        [StringLength(255)]
        public string? address3 { get; set; }

        [StringLength(15)]
        public string? regNo { get; set; }

        [StringLength(15)]
        public string? vacancyLevel { get; set; }

        public DateTime? lastVisit { get; set; }
        public DateTime? lastContact { get; set; }
        public int? totalNoOfStaff { get; set; }

        [StringLength(150)]
        public string? industryInvolved { get; set; }

        [StringLength(150)]
        public string? productsAndServices { get; set; }

        [StringLength(255)]
        public string? companyBackground { get; set; }

        [StringLength(100)]
        public string? website { get; set; }

        [StringLength(500)]
        public string? remark { get; set; }

        public byte? status { get; set; } = 1;
        public byte? visibility { get; set; } = 1;

        public IFormFile? logoFile { get; set; }
        public IFormFile? ssmCertFile { get; set; }
    }

    public IActionResult OnGet(int companyId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var company = _db.Companies.AsNoTracking().FirstOrDefault(c => c.company_id == companyId);
        if (company == null)
        {
            TempData["StatusMessage"] = "Company record not found.";
            return RedirectToPage("/Committee/Companies");
        }

        Input = new InputModel
        {
            company_id = company.company_id,
            name = company.name,
            address1 = company.address1 ?? string.Empty,
            address2 = company.address2,
            address3 = company.address3,
            regNo = company.regNo,
            vacancyLevel = company.vacancyLevel,
            lastVisit = company.lastVisit,
            lastContact = company.lastContact,
            totalNoOfStaff = company.totalNoOfStaff,
            industryInvolved = company.industryInvolved,
            productsAndServices = company.productsAndServices,
            companyBackground = company.companyBackground,
            website = company.website,
            remark = company.remark,
            status = company.status ?? 1,
            visibility = company.visibility ?? 1
        };
        CreatedAt = company.created_at;
        LastUpdatedAt = company.lastUpdate;
        HasLogo = company.logo != null && company.logo.Length > 0;
        HasSsmCert = company.ssmCert != null && company.ssmCert.Length > 0;

        return Page();
    }

    public IActionResult OnPostSave()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var company = _db.Companies.FirstOrDefault(c => c.company_id == Input.company_id);
        if (company == null)
        {
            TempData["StatusMessage"] = "Company record not found.";
            return RedirectToPage("/Committee/Companies");
        }

        var name = Input.name.Trim();
        var address1 = Input.address1.Trim();
        var duplicate = _db.Companies.Any(c =>
            c.company_id != Input.company_id &&
            c.name.ToLower() == name.ToLower() &&
            (c.address1 ?? string.Empty).ToLower() == address1.ToLower());
        if (duplicate)
        {
            ModelState.AddModelError("", "Another company with same name and address already exists.");
            ReloadDisplayState(company);
            return Page();
        }

        company.name = name;
        company.address1 = address1;
        company.address2 = TrimOrNull(Input.address2);
        company.address3 = TrimOrNull(Input.address3);
        company.regNo = TrimOrNull(Input.regNo);
        company.vacancyLevel = TrimOrNull(Input.vacancyLevel);
        company.lastVisit = Input.lastVisit;
        company.lastContact = Input.lastContact;
        company.totalNoOfStaff = Input.totalNoOfStaff;
        company.industryInvolved = TrimOrNull(Input.industryInvolved);
        company.productsAndServices = TrimOrNull(Input.productsAndServices);
        company.companyBackground = TrimOrNull(Input.companyBackground);
        company.website = TrimOrNull(Input.website);
        company.remark = TrimOrNull(Input.remark);
        company.status = Input.status ?? 1;
        company.visibility = Input.visibility ?? 1;
        if (Input.logoFile != null && Input.logoFile.Length > 0)
        {
            company.logo = ReadBytes(Input.logoFile);
        }
        if (Input.ssmCertFile != null && Input.ssmCertFile.Length > 0)
        {
            company.ssmCert = ReadBytes(Input.ssmCertFile);
        }

        _db.SaveChanges();
        StatusMessage = "Company updated successfully.";
        return RedirectToPage(new { companyId = company.company_id });
    }

    public IActionResult OnPostDelete(int companyId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var company = _db.Companies.FirstOrDefault(c => c.company_id == companyId);
        if (company == null)
        {
            TempData["StatusMessage"] = "Company record not found.";
            return RedirectToPage("/Committee/Companies");
        }

        _db.Companies.Remove(company);
        _db.SaveChanges();
        TempData["StatusMessage"] = "Company deleted successfully.";
        return RedirectToPage("/Committee/Companies");
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static byte[] ReadBytes(IFormFile file)
    {
        using var ms = new MemoryStream();
        file.CopyTo(ms);
        return ms.ToArray();
    }

    private void ReloadDisplayState(Company company)
    {
        CreatedAt = company.created_at;
        LastUpdatedAt = company.lastUpdate;
        HasLogo = company.logo != null && company.logo.Length > 0;
        HasSsmCert = company.ssmCert != null && company.ssmCert.Length > 0;
    }
}
