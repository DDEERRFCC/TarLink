using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class CommitteeAddCompanyModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeAddCompanyModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
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

        [Required]
        [StringLength(15)]
        public string regNo { get; set; } = string.Empty;

        [StringLength(15)]
        public string? vacancyLevel { get; set; }

        [StringLength(100)]
        public string? website { get; set; }

        [StringLength(500)]
        public string? remark { get; set; }

        public byte? status { get; set; } = 0;
        public byte? visibility { get; set; } = 1;
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var name = Input.name.Trim();
        var address1 = Input.address1.Trim();
        var regNo = Input.regNo.Trim();
        var exists = _db.Companies.Any(c =>
            c.regNo.ToLower() == regNo.ToLower());
        if (exists)
        {
            ModelState.AddModelError("", "Company with same registration number already exists.");
            return Page();
        }

        var company = new Company
        {
            created_at = DateTime.Now,
            name = name,
            regNo = regNo,
            website = TrimOrNull(Input.website),
            remark = TrimOrNull(Input.remark),
            updated_at = DateTime.Now
        };

        AddBranches(company, Input.status ?? 0, Input.visibility ?? 1, TrimOrNull(Input.vacancyLevel), Input.address1, Input.address2, Input.address3);
        _db.Companies.Add(company);

        _db.SaveChanges();
        TempData["StatusMessage"] = "Company added successfully.";
        return RedirectToPage("/Committee/Companies");
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddBranches(Company company, byte status, byte visibility, string? vacancyLevel, params string?[] addresses)
    {
        var branchIndex = 0;
        foreach (var address in addresses.Select(TrimOrNull).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            company.Branches.Add(new CompanyBranch
            {
                address_line = address,
                vacancyLevel = vacancyLevel,
                status = status,
                visibility = visibility,
                is_hq = branchIndex == 0
            });
            branchIndex++;
        }
    }
}
