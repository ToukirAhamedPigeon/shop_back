using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using shop_back.App.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace shop_back.App.Authorization
{

    public class PermissionFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _db;
        private readonly List<string> _permissions;
        private readonly PermissionRelation _relation;

        public PermissionFilter(AppDbContext db, List<string> permissions, PermissionRelation relation)
        {
            _db = db;
            _permissions = permissions ?? new List<string>();
            _relation = relation;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1️⃣ Get user ID from JWT claims
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) 
                         ?? context.HttpContext.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // 2️⃣ Fetch direct user permissions (null-safe)
            var userPermissions = await _db.ModelPermissions
                .Include(mp => mp.Permission)
                .Where(mp => mp.ModelId.ToString() == userId && mp.Permission != null)
                .Select(mp => $"{mp.Permission!.GuardName}:{mp.Permission!.Name}")
                .ToListAsync();

            // 3️⃣ Fetch role-based permissions (null-safe)
            // Fetch role-based permissions safely
            var rolePermissions = await _db.ModelRoles
            .Include(mr => mr.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(mr => mr.ModelId.ToString() == userId && mr.Role != null)
            .SelectMany(mr => mr.Role != null
                ? mr.Role.RolePermissions
                    .Where(rp => rp.Permission != null)
                    .Select(rp => new 
                    { 
                        GuardName = rp.Permission!.GuardName,
                        Name = rp.Permission!.Name
                    })
                : Enumerable.Empty<dynamic>() // return empty if Role is null
            )
            .ToListAsync();

    // Convert to string format
    // Convert rolePermissions to string format first
    var rolePermissionStrings = rolePermissions
        .Select(rp => $"{rp.GuardName}:{rp.Name}")
        .ToList();

    // Merge all permissions safely
    var allPermissions = userPermissions
        .Concat(rolePermissionStrings) // now both are List<string>
        .Distinct()
        .ToList();

            // 5️⃣ Evaluate based on PermissionRelation
            bool authorized = _relation switch
            {
                PermissionRelation.Or => _permissions.Any(p => allPermissions.Contains(p)),
                PermissionRelation.And => _permissions.All(p => allPermissions.Contains(p)),
                _ => false
            };

            if (!authorized)
            {
                context.Result = new ForbidResult();
                return;
            }

            // 6️⃣ Continue execution if authorized
            await next();
        }
    }
}
