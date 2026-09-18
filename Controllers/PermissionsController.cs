using System.Linq.Expressions;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using BizfreeApp.Models.DTOs.Permissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Policy = "SuperAdmin")] 
    public class PermissionsController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;

        public PermissionsController(Data.ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /api/permissions
        [HttpGet]
        public async Task<ActionResult<PagedResult<PermissionDto>>> GetPermissions(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string? searchKeyword = null,
            [FromQuery] int? moduleId = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc")
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 50;

            var q = _context.Permissions
                .AsNoTracking()
                .Include(p => p.Module)
                .AsQueryable();

            if (moduleId.HasValue && moduleId.Value > 0)
            {
                q = q.Where(p => p.ModuleId == moduleId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                q = q.Where(p =>
                    p.PermissionName.ToLower().Contains(term) ||
                    (p.Module.ModuleName != null && p.Module.ModuleName.ToLower().Contains(term)));
            }

            var desc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
            Expression<Func<Permission, object>> sortExpr = (sortBy ?? "name").ToLower() switch
            {
                "module" => p => p.Module.ModuleName!,
                "name" => _ => EF.Property<string>(_, nameof(Permission.PermissionName)),
                _ => _ => EF.Property<string>(_, nameof(Permission.PermissionName))
            };
            q = desc ? q.OrderByDescending(sortExpr) : q.OrderBy(sortExpr);

            var totalCount = await q.CountAsync();

            var items = await q
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    ModuleId = p.ModuleId,
                    ModuleName = p.Module.ModuleName
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var result = new PagedResult<PermissionDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalTasks = totalCount,
                SortBy = sortBy,
                SortOrder = sortOrder,
                SearchKeyword = searchKeyword
            };

            return Ok(result);
        }

        // GET: /api/permissions/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PermissionDto>> GetPermissionById(int id)
        {
            var dto = await _context.Permissions
                .AsNoTracking()
                .Include(p => p.Module)
                .Where(p => p.PermissionId == id)
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    ModuleId = p.ModuleId,
                    ModuleName = p.Module.ModuleName
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();
            return Ok(dto);
        }

        // GET: /api/permissions/by-module/{moduleId}
        [HttpGet("by-module/{moduleId:int}")]
        public async Task<ActionResult<IEnumerable<PermissionDto>>> GetByModule(int moduleId)
        {
            var exists = await _context.Modules.AnyAsync(m => m.ModuleId == moduleId);
            if (!exists) return NotFound("Module not found.");

            var list = await _context.Permissions
                .AsNoTracking()
                .Where(p => p.ModuleId == moduleId)
                .OrderBy(p => p.PermissionName)
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    ModuleId = p.ModuleId
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: /api/permissions
        [HttpPost]
        public async Task<ActionResult<PermissionDto>> CreatePermission([FromBody] CreatePermissionDto dto)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(dto.PermissionName))
                return BadRequest("PermissionName is required.");

            if (dto.PermissionName.Length > 100)
                return BadRequest("PermissionName must be <= 100 characters.");

            var moduleExists = await _context.Modules.AnyAsync(m => m.ModuleId == dto.ModuleId);
            if (!moduleExists) return BadRequest("Invalid ModuleId.");

            // Global uniqueness for permission_name is recommended
            var exists = await _context.Permissions
                .AnyAsync(p => p.PermissionName.ToLower() == dto.PermissionName.Trim().ToLower());
            if (exists) return Conflict("A permission with the same name already exists.");

            var entity = new Permission
            {
                PermissionName = dto.PermissionName.Trim(),
                ModuleId = dto.ModuleId
            };

            _context.Permissions.Add(entity);
            await _context.SaveChangesAsync();

            var result = new PermissionDto
            {
                PermissionId = entity.PermissionId,
                PermissionName = entity.PermissionName,
                ModuleId = entity.ModuleId
            };

            return CreatedAtAction(nameof(GetPermissionById), new { id = entity.PermissionId }, result);
        }

        // PUT: /api/permissions/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePermission(int id, [FromBody] UpdatePermissionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PermissionName))
                return BadRequest("PermissionName is required.");

            if (dto.PermissionName.Length > 100)
                return BadRequest("PermissionName must be <= 100 characters.");

            var entity = await _context.Permissions.FindAsync(id);
            if (entity == null) return NotFound();

            var moduleExists = await _context.Modules.AnyAsync(m => m.ModuleId == dto.ModuleId);
            if (!moduleExists) return BadRequest("Invalid ModuleId.");

            if (!string.Equals(entity.PermissionName, dto.PermissionName, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _context.Permissions
                    .AnyAsync(p => p.PermissionId != id && p.PermissionName.ToLower() == dto.PermissionName.Trim().ToLower());
                if (exists) return Conflict("A permission with the same name already exists.");
            }

            entity.PermissionName = dto.PermissionName.Trim();
            entity.ModuleId = dto.ModuleId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: /api/permissions/{id}?force=false
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePermission(int id, [FromQuery] bool force = false)
        {
            var entity = await _context.Permissions
                .Include(p => p.Rolespermissions)
                .FirstOrDefaultAsync(p => p.PermissionId == id);

            if (entity == null) return NotFound();

            var inUse = entity.Rolespermissions?.Any() == true;
            if (inUse && !force)
            {
                return Conflict(new
                {
                    message = "Permission is assigned in roles. Pass force=true to remove dependent links and delete."
                });
            }

            if (inUse && force)
            {
                _context.RemoveRange(entity.Rolespermissions);
            }

            _context.Permissions.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /api/permissions/by-modules
        public class PermissionsByModulesRequest
        {
            public List<int> ModuleIds { get; set; } = new();
        }

        [HttpPost("by-modules")]
        public async Task<ActionResult<IEnumerable<PermissionDto>>> GetByModules([FromBody] PermissionsByModulesRequest req)
        {
            if (req.ModuleIds == null || req.ModuleIds.Count == 0)
                return Ok(Array.Empty<PermissionDto>());

            var set = new HashSet<int>(req.ModuleIds);
            var list = await _context.Permissions
                .AsNoTracking()
                .Where(p => set.Contains(p.ModuleId))
                .OrderBy(p => p.ModuleId).ThenBy(p => p.PermissionName)
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    ModuleId = p.ModuleId
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}
