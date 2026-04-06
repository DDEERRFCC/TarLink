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
    public string SelectedAddressLabel { get; private set; } = "Address 1";

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        public int company_id { get; set; }

        public int? branch_id { get; set; }

        [Required]
        [StringLength(250)]
        public string name { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string regNo { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string address1 { get; set; } = string.Empty;

        [StringLength(255)]
        public string? address2 { get; set; }

        [StringLength(255)]
        public string? address3 { get; set; }

        [StringLength(100)]
        public string? address1City { get; set; }

        [StringLength(100)]
        public string? address1State { get; set; }

        [StringLength(20)]
        public string? address1Postcode { get; set; }

        [StringLength(100)]
        public string? address1Country { get; set; }

        [StringLength(100)]
        public string? address2City { get; set; }

        [StringLength(100)]
        public string? address2State { get; set; }

        [StringLength(20)]
        public string? address2Postcode { get; set; }

        [StringLength(100)]
        public string? address2Country { get; set; }

        [StringLength(100)]
        public string? address3City { get; set; }

        [StringLength(100)]
        public string? address3State { get; set; }

        [StringLength(20)]
        public string? address3Postcode { get; set; }

        [StringLength(100)]
        public string? address3Country { get; set; }

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

        public byte? status { get; set; } = 0;
        public byte? visibility { get; set; } = 1;

        public IFormFile? logoFile { get; set; }
        public IFormFile? ssmCertFile { get; set; }
    }

    public IActionResult OnGet(int companyId, int? branchId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var company = _db.Companies
            .AsNoTracking()
            .Include(c => c.Branches)
            .FirstOrDefault(c => c.company_id == companyId);
        if (company == null)
        {
            TempData["StatusMessage"] = "Company record not found.";
            return RedirectToPage("/Committee/Companies");
        }

        var branches = GetOrderedBranches(company);
        var selectedBranch = GetSelectedBranch(branches, branchId);

        Input = new InputModel
        {
            company_id = company.company_id,
            branch_id = selectedBranch?.branch_id,
            name = company.name,
            regNo = company.regNo,
            address1 = selectedBranch?.address_line ?? string.Empty,
            address1City = selectedBranch?.city,
            address1State = selectedBranch?.state,
            address1Postcode = selectedBranch?.postcode,
            address1Country = selectedBranch?.country,
            vacancyLevel = selectedBranch?.vacancyLevel,
            lastVisit = selectedBranch?.lastVisit,
            lastContact = selectedBranch?.lastContact,
            totalNoOfStaff = selectedBranch?.totalNoOfStaff,
            industryInvolved = company.industryInvolved,
            productsAndServices = company.productsAndServices,
            companyBackground = company.companyBackground,
            website = company.website,
            remark = company.remark,
            status = selectedBranch?.status ?? 0,
            visibility = selectedBranch?.visibility ?? 1
        };
        SelectedAddressLabel = GetBranchLabel(branches, selectedBranch);
        CreatedAt = company.created_at;
        LastUpdatedAt = company.updated_at;
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

        var company = _db.Companies
            .Include(c => c.Branches)
            .FirstOrDefault(c => c.company_id == Input.company_id);
        if (company == null)
        {
            TempData["StatusMessage"] = "Company record not found.";
            return RedirectToPage("/Committee/Companies");
        }

        var name = Input.name.Trim();
        var regNo = Input.regNo.Trim();
        var duplicate = _db.Companies.Any(c =>
            c.company_id != Input.company_id &&
            c.regNo.ToLower() == regNo.ToLower());
        if (duplicate)
        {
            ModelState.AddModelError("", "Another company with same registration number already exists.");
            ReloadDisplayState(company);
            return Page();
        }

        company.name = name;
        company.regNo = regNo;
        company.industryInvolved = TrimOrNull(Input.industryInvolved);
        company.productsAndServices = TrimOrNull(Input.productsAndServices);
        company.companyBackground = TrimOrNull(Input.companyBackground);
        company.website = TrimOrNull(Input.website);
        company.remark = TrimOrNull(Input.remark);
        company.updated_at = DateTime.Now;
        var branches = GetOrderedBranches(company);
        var selectedBranch = GetSelectedBranch(branches, Input.branch_id);
        if (selectedBranch == null)
        {
            ModelState.AddModelError("", "Selected address branch was not found.");
            ReloadDisplayState(company);
            return Page();
        }

        var addressLine = Input.address1.Trim();
        selectedBranch.address_line = addressLine;
        selectedBranch.city = TrimOrNull(Input.address1City);
        selectedBranch.state = TrimOrNull(Input.address1State);
        selectedBranch.postcode = TrimOrNull(Input.address1Postcode);
        selectedBranch.country = TrimOrNull(Input.address1Country);
        selectedBranch.vacancyLevel = TrimOrNull(Input.vacancyLevel);
        selectedBranch.totalNoOfStaff = Input.totalNoOfStaff;
        selectedBranch.lastVisit = Input.lastVisit;
        selectedBranch.lastContact = Input.lastContact;
        selectedBranch.status = Input.status ?? 0;
        selectedBranch.visibility = Input.visibility ?? 1;
        selectedBranch.updated_at = DateTime.Now;
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
        return RedirectToPage(new { companyId = company.company_id, branchId = selectedBranch.branch_id });
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
        LastUpdatedAt = company.updated_at;
        HasLogo = company.logo != null && company.logo.Length > 0;
        HasSsmCert = company.ssmCert != null && company.ssmCert.Length > 0;
    }

    private static List<CompanyBranch> GetOrderedBranches(Company company)
    {
        return company.Branches
            .OrderByDescending(a => a.is_hq)
            .ThenBy(a => a.branch_id)
            .Where(a => !string.IsNullOrWhiteSpace(a.address_line))
            .ToList();
    }

    private static CompanyBranch? GetSelectedBranch(List<CompanyBranch> branches, int? branchId)
    {
        if (branchId.HasValue)
        {
            return branches.FirstOrDefault(b => b.branch_id == branchId.Value);
        }

        return branches.FirstOrDefault();
    }

    private static string GetBranchLabel(List<CompanyBranch> branches, CompanyBranch? selectedBranch)
    {
        if (selectedBranch == null)
        {
            return "Address";
        }

        var index = branches.FindIndex(b => b.branch_id == selectedBranch.branch_id);
        return index >= 0 ? $"Address {index + 1}" : "Address";
    }
}
