using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

public class CommitteeBlacklistCompaniesModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CommitteeBlacklistCompaniesModel(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public List<BlacklistCompany> BlacklistCompanies { get; private set; } = new();
    public List<CompanyAddressItem> CompanyAddressItems { get; private set; } = new();
    public List<SelectListItem> AddressOptions { get; private set; } = new();

    [BindProperty]
    public BlacklistInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? AttachmentFile { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadData();

        return Page();
    }

    public IActionResult OnPostAdd()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadData();

        if (Input.CompanyId <= 0)
        {
            StatusMessage = "Please select a company.";
            return Page();
        }

        var company = _db.Companies
            .AsNoTracking()
            .Include(c => c.Branches)
            .FirstOrDefault(c => c.company_id == Input.CompanyId);
        if (company == null)
        {
            StatusMessage = "Selected company not found.";
            return Page();
        }

        var address = ResolveAddress(company, Input.AddressChoice, Input.CustomAddress);
        if (string.IsNullOrWhiteSpace(address))
        {
            StatusMessage = "Please provide a company address.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.Reason))
        {
            StatusMessage = "Please provide a reason.";
            return Page();
        }

        var normalizedName = company.name.Trim().ToLowerInvariant();
        var normalizedAddress = address.Trim().ToLowerInvariant();
        if (_db.BlacklistCompanies.Any(x =>
            !x.is_removed &&
            (x.comName ?? string.Empty).Trim().ToLower() == normalizedName &&
            (x.address ?? string.Empty).Trim().ToLower() == normalizedAddress))
        {
            StatusMessage = "This company with the same address is already blacklisted.";
            return Page();
        }

        var fileName = SaveAttachment(AttachmentFile);
        var committeeName = HttpContext.Session.GetString("UserName") ?? "committee";

        var entry = new BlacklistCompany
        {
            created_at = DateTime.Now,
            comName = company.name,
            address = address,
            reason = Input.Reason.Trim(),
            byCommittee = committeeName,
            attachment = fileName,
            is_removed = false,
            removed_at = null
        };

        _db.BlacklistCompanies.Add(entry);
        _db.SaveChanges();

        StatusMessage = "Company added to blacklist.";
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var existing = _db.BlacklistCompanies.FirstOrDefault(x => x.id == id);
        if (existing == null)
        {
            StatusMessage = "Blacklist company not found.";
            return RedirectToPage();
        }

        if (existing.is_removed)
        {
            StatusMessage = "Blacklist company already removed.";
            return RedirectToPage();
        }

        existing.is_removed = true;
        existing.removed_at = DateTime.Now;
        _db.SaveChanges();

        StatusMessage = "Blacklist company marked as removed.";
        return RedirectToPage();
    }

    private void LoadData()
    {
        BlacklistCompanies = _db.BlacklistCompanies.AsNoTracking()
            .OrderByDescending(x => x.created_at)
            .ThenBy(x => x.comName)
            .ToList();

        var companies = _db.Companies.AsNoTracking()
            .Include(c => c.Branches)
            .OrderBy(c => c.name)
            .ToList();

        CompanyAddressItems = companies
            .Select(c => new CompanyAddressItem
            {
                CompanyId = c.company_id,
                Name = c.name,
                Addresses = c.Branches
                    .OrderByDescending(b => b.is_hq)
                    .ThenBy(b => b.branch_id)
                    .Select(b => b.address_line)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

        var selectedCompany = companies.FirstOrDefault(c => c.company_id == Input.CompanyId)
            ?? companies.FirstOrDefault();
        if (selectedCompany != null)
        {
            Input.CompanyId = selectedCompany.company_id;
            AddressOptions = BuildAddressOptions(selectedCompany);
        }
    }

    private static List<SelectListItem> BuildAddressOptions(Company company)
    {
        var options = new List<SelectListItem>();
        var addresses = company.Branches
            .OrderByDescending(b => b.is_hq)
            .ThenBy(b => b.branch_id)
            .Select(b => b.address_line)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < addresses.Count; i++)
        {
            AddAddressOption(options, $"address{i + 1}", addresses[i]);
        }

        options.Add(new SelectListItem("Custom address", "custom"));
        return options;
    }

    private static void AddAddressOption(List<SelectListItem> options, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            options.Add(new SelectListItem(value, key));
        }
    }

    private static string? ResolveAddress(Company company, string? choice, string? custom)
    {
        var selected = (choice ?? string.Empty).Trim().ToLowerInvariant();
        if (selected == "custom")
        {
            return string.IsNullOrWhiteSpace(custom) ? null : custom.Trim();
        }

        if (!selected.StartsWith("address", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!int.TryParse(selected["address".Length..], out var index) || index <= 0)
        {
            return null;
        }

        return company.Branches
            .OrderByDescending(b => b.is_hq)
            .ThenBy(b => b.branch_id)
            .Select(b => b.address_line)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ElementAtOrDefault(index - 1);
    }

    private string? SaveAttachment(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return null;
        }

        var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "Blacklist");
        Directory.CreateDirectory(uploadsRoot);

        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploadsRoot, safeName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        return safeName;
    }

    public class BlacklistInput
    {
        public int CompanyId { get; set; }
        public string? AddressChoice { get; set; }
        public string? CustomAddress { get; set; }
        public string? Reason { get; set; }
    }

    public class CompanyAddressItem
    {
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Addresses { get; set; } = new();
    }
}
