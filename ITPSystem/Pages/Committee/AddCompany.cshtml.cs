using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

public class CommitteeAddCompanyModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeAddCompanyModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public CompanyInputModel CompanyInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class CompanyInputModel
    {
        [Required]
        [StringLength(250)]
        public string name { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string regNo { get; set; } = string.Empty;

        [StringLength(150)]
        public string? industryInvolved { get; set; }

        [StringLength(150)]
        public string? productsAndServices { get; set; }

        public string? companyBackground { get; set; }

        [StringLength(255)]
        public string? website { get; set; }

        [StringLength(500)]
        public string? remark { get; set; }

        public IFormFile? logoFile { get; set; }
        public IFormFile? ssmCertFile { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }

    public IActionResult OnPostCreateCompany()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var regNo = CompanyInput.regNo.Trim();
        var exists = _db.Companies.Any(c => c.regNo.ToLower() == regNo.ToLower());
        if (exists)
        {
            ModelState.AddModelError("CompanyInput.regNo", "Company with same registration number already exists.");
            return Page();
        }

        var company = new Company
        {
            created_at = DateTime.Now,
            updated_at = DateTime.Now,
            name = CompanyInput.name.Trim(),
            regNo = regNo,
            industryInvolved = TrimOrNull(CompanyInput.industryInvolved),
            productsAndServices = TrimOrNull(CompanyInput.productsAndServices),
            companyBackground = TrimOrNull(CompanyInput.companyBackground),
            website = TrimOrNull(CompanyInput.website),
            remark = TrimOrNull(CompanyInput.remark),
            logo = ReadBytes(CompanyInput.logoFile),
            ssmCert = ReadBytes(CompanyInput.ssmCertFile)
        };

        _db.Companies.Add(company);
        _db.SaveChanges();

        StatusMessage = $"Company {company.name} created successfully. You can add its branch from the Add Address page.";
        return RedirectToPage("/Committee/AddCompany");
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static byte[]? ReadBytes(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return null;
        }

        using var ms = new MemoryStream();
        file.CopyTo(ms);
        return ms.ToArray();
    }
}
