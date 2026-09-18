using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models;
using BizfreeApp.DTOs;
using BizfreeApp.Models.DTOs;

namespace BizfreeApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PackageController : ControllerBase
{
    private readonly Data.ApplicationDbContext _context;

    public PackageController(Data.ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Package
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PackageDto>>> GetPackages()
    {
        var packages = await _context.Packages
            .AsNoTracking()
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageName = p.PackageName,
                PriceMonthly = p.PriceMonthly,
                PriceYearly = p.PriceYearly,
                IsActive = p.IsActive,
                Description = p.Description,
                TrialDays = p.TrialDays
            })
            .ToListAsync();

        return Ok(packages);
    }

    // GET: api/Package/5
    [HttpGet("{id}")]
    public async Task<ActionResult<PackageDto>> GetPackageById(int id)
    {
        var package = await _context.Packages
            .AsNoTracking()
            .Where(p => p.PackageId == id)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageName = p.PackageName,
                PriceMonthly = p.PriceMonthly,
                PriceYearly = p.PriceYearly,
                IsActive = p.IsActive,
                Description = p.Description,
                TrialDays = p.TrialDays
            })
            .FirstOrDefaultAsync();

        if (package == null)
        {
            return NotFound();
        }

        return Ok(package);
    }

    // POST: api/Package
    [HttpPost]
    public async Task<ActionResult<Package>> CreatePackage(CreatePackageDto dto)
    {
        var package = new Package
        {
            PackageName = dto.PackageName,
            PriceMonthly = dto.PriceMonthly,
            PriceYearly = dto.PriceYearly,
            IsActive = dto.IsActive,
            Description = dto.Description,
            TrialDays = dto.TrialDays,
            CreatedAt = DateTime.UtcNow
        };

        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPackageById), new { id = package.PackageId }, package);
    }

    // PUT: api/Package/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePackage(int id, UpdatePackageDto dto)
    {
        var package = await _context.Packages.FindAsync(id);
        if (package == null)
        {
            return NotFound();
        }

        // Update fields
        package.PackageName = dto.PackageName;
        package.PriceMonthly = dto.PriceMonthly;
        package.PriceYearly = dto.PriceYearly;
        package.IsActive = dto.IsActive;
        package.Description = dto.Description;
        package.TrialDays = dto.TrialDays;
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/Package/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePackage(int id)
    {
        var package = await _context.Packages.FindAsync(id);
        if (package == null)
            return NotFound();

        _context.Packages.Remove(package);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
