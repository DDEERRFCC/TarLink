using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

public class CommitteeAddAddressModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeAddAddressModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public AddressInputModel AddressInput { get; set; } = new();

    public IReadOnlyList<SelectListItem> CompanyOptions { get; private set; } = Array.Empty<SelectListItem>();

    [TempData]
    public string? StatusMessage { get; set; }

    public class AddressInputModel
    {
        [Required]
        public int? companyId { get; set; }

        [Required]
        [StringLength(255)]
        public string addressLine { get; set; } = string.Empty;

        [StringLength(100)]
        public string? city { get; set; }

        [StringLength(100)]
        public string? state { get; set; }

        [StringLength(20)]
        public string? postcode { get; set; }

        [StringLength(100)]
        public string? country { get; set; }

        [StringLength(15)]
        public string? vacancyLevel { get; set; }

        public int? totalNoOfStaff { get; set; }
        public DateTime? lastVisit { get; set; }
        public DateTime? lastContact { get; set; }
        public byte? status { get; set; } = 0;
        public byte? visibility { get; set; } = 1;
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCompanyOptions();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        ValidateAddressInput();

        if (!ModelState.IsValid)
        {
            LoadCompanyOptions();
            return Page();
        }

        var company = _db.Companies
            .Where(c => c.company_id == AddressInput.companyId!.Value)
            .Select(c => new
            {
                Company = c,
                HasBranches = c.Branches.Any()
            })
            .FirstOrDefault();

        if (company == null)
        {
            ModelState.AddModelError("AddressInput.companyId", "Selected company was not found.");
            LoadCompanyOptions();
            return Page();
        }

        var branch = new CompanyBranch
        {
            company_id = company.Company.company_id,
            address_line = AddressInput.addressLine.Trim(),
            city = TrimOrNull(AddressInput.city),
            state = TrimOrNull(AddressInput.state),
            postcode = TrimOrNull(AddressInput.postcode),
            country = TrimOrNull(AddressInput.country),
            totalNoOfStaff = AddressInput.totalNoOfStaff,
            vacancyLevel = TrimOrNull(AddressInput.vacancyLevel),
            status = AddressInput.status ?? 0,
            visibility = AddressInput.visibility ?? 1,
            lastVisit = AddressInput.lastVisit,
            lastContact = AddressInput.lastContact,
            is_hq = !company.HasBranches
        };

        _db.CompanyBranches.Add(branch);
        _db.SaveChanges();

        StatusMessage = branch.is_hq
            ? $"HQ address added to {company.Company.name}."
            : $"New branch address added to {company.Company.name}.";

        return RedirectToPage("/Committee/AddAddress");
    }

    private void LoadCompanyOptions()
    {
        CompanyOptions = _db.Companies
            .OrderBy(c => c.name)
            .ThenBy(c => c.regNo)
            .Select(c => new SelectListItem
            {
                Value = c.company_id.ToString(),
                Text = $"{c.name} ({c.regNo})"
            })
            .ToList();
    }

    private void ValidateAddressInput()
    {
        if (AddressInput.status.HasValue && AddressInput.status.Value > 3)
        {
            ModelState.AddModelError("AddressInput.status", "Status must be 0, 1, 2, or 3.");
        }

        if (AddressInput.visibility.HasValue && AddressInput.visibility.Value > 1)
        {
            ModelState.AddModelError("AddressInput.visibility", "Visibility must be 0 or 1.");
        }
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
