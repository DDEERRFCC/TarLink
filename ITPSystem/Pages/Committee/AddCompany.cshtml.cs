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

        [StringLength(15)]
        public string? regNo { get; set; }

        [StringLength(15)]
        public string? vacancyLevel { get; set; }

        [StringLength(100)]
        public string? website { get; set; }

        [StringLength(500)]
        public string? remark { get; set; }

        public byte? status { get; set; } = 1;
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
        var exists = _db.Companies.Any(c =>
            c.name.ToLower() == name.ToLower() &&
            (c.address1 ?? string.Empty).ToLower() == address1.ToLower());
        if (exists)
        {
            ModelState.AddModelError("", "Company with same name and address already exists.");
            return Page();
        }

        _db.Companies.Add(new Company
        {
            created_at = DateTime.Now,
            lastUpdate = DateTime.Now,
            name = name,
            address1 = address1,
            address2 = TrimOrNull(Input.address2),
            address3 = TrimOrNull(Input.address3),
            regNo = TrimOrNull(Input.regNo),
            vacancyLevel = TrimOrNull(Input.vacancyLevel),
            website = TrimOrNull(Input.website),
            remark = TrimOrNull(Input.remark),
            status = Input.status ?? 1,
            visibility = Input.visibility ?? 1
        });

        _db.SaveChanges();
        TempData["StatusMessage"] = "Company added successfully.";
        return RedirectToPage("/Committee/Companies");
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
