using System.Linq.Expressions;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Policy = "SuperAdmin")]
    public class ModulesController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;

        public ModulesController(Data.ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /api/modules
        [HttpGet]
        public async Task<ActionResult<PagedResult<Module>>> GetModules(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string? searchKeyword = null,
            [FromQuery] bool? activeOnly = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc")
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 50;

            var q = _context.Modules.AsNoTracking().AsQueryable();

            // Filter: activeOnly
            if (activeOnly == true)
            {
                q = q.Where(m => m.IsActive == true);
            }

            // Search: name/description
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                q = q.Where(m =>
                    (m.ModuleName != null && m.ModuleName.ToLower().Contains(term)) ||
                    (m.Description != null && m.Description.ToLower().Contains(term)));
            }

            // Sorting
            var desc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
            Expression<Func<Module, object>> sortExpr = (sortBy ?? "name").ToLower() switch
            {
                "name" => m => m.ModuleName!,
                _ => m => m.ModuleName!
            };
            q = desc ? q.OrderByDescending(sortExpr) : q.OrderBy(sortExpr);

            // Count + page
            var totalCount = await q.CountAsync();
            var items = await q
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Build your PagedResult
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var result = new PagedResult<Module>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                // Map optional fields from your DTO
                TotalTasks = totalCount,          
                SortBy = sortBy,
                SortOrder = sortOrder,
                SearchKeyword = searchKeyword
            };

            return Ok(result);
        }

        // GET: /api/modules/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Module>> GetModuleById(int id)
        {
            var module = await _context.Modules.AsNoTracking().FirstOrDefaultAsync(m => m.ModuleId == id);
            if (module == null) return NotFound();
            return Ok(module);
        }
        public class CreateModuleDto
        {
            public string ModuleName { get; set; } = null!;
            public string? Description { get; set; }
            public bool? IsActive { get; set; } = true;
        }

        [HttpPost]
        public async Task<ActionResult<Module>> CreateModule([FromBody] CreateModuleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ModuleName))
                return BadRequest("ModuleName is required.");

            if (dto.ModuleName.Length > 255)
                return BadRequest("ModuleName must be <= 255 characters.");

            var exists = await _context.Modules
                .AnyAsync(m => m.ModuleName!.ToLower() == dto.ModuleName.Trim().ToLower());
            if (exists) return Conflict("A module with the same name already exists.");

            var entity = new Module
            {
                ModuleName = dto.ModuleName.Trim(),
                Description = dto.Description,
                IsActive = dto.IsActive ?? true
            };

            _context.Modules.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetModuleById), new { id = entity.ModuleId }, entity);
        }

        // PUT: /api/modules/{id}
        public class UpdateModuleDto
        {
            public string ModuleName { get; set; } = null!;
            public string? Description { get; set; }
            public bool? IsActive { get; set; }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateModule(int id, [FromBody] UpdateModuleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ModuleName))
                return BadRequest("ModuleName is required.");

            if (dto.ModuleName.Length > 255)
                return BadRequest("ModuleName must be <= 255 characters.");

            var entity = await _context.Modules.FindAsync(id);
            if (entity == null) return NotFound();

            if (!string.Equals(entity.ModuleName, dto.ModuleName, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _context.Modules
                    .AnyAsync(m => m.ModuleId != id && m.ModuleName!.ToLower() == dto.ModuleName.Trim().ToLower());
                if (exists) return Conflict("A module with the same name already exists.");
            }

            entity.ModuleName = dto.ModuleName.Trim();
            entity.Description = dto.Description;
            if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PATCH: /api/modules/{id}/activate
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            var entity = await _context.Modules.FindAsync(id);
            if (entity == null) return NotFound();

            entity.IsActive = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PATCH: /api/modules/{id}/deactivate
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var entity = await _context.Modules.FindAsync(id);
            if (entity == null) return NotFound();

            entity.IsActive = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: /api/modules/{id}?force=false
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteModule(int id, [FromQuery] bool force = false)
        {
            var entity = await _context.Modules
                .Include(m => m.Permissions)
                .Include(m => m.Packagemodules)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (entity == null) return NotFound();

            var hasRefs = (entity.Permissions?.Any() == true) || (entity.Packagemodules?.Any() == true);
            if (hasRefs && !force)
            {
                return Conflict(new
                {
                    message = "Module is used by permissions or packages. Pass force=true to remove dependents and delete."
                });
            }

            if (hasRefs && force)
            {
                if (entity.Permissions.Any())
                    _context.Permissions.RemoveRange(entity.Permissions);

                if (entity.Packagemodules.Any())
                    _context.Packagemodules.RemoveRange(entity.Packagemodules);
            }

            _context.Modules.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
