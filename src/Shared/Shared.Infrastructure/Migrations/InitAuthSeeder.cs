using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using shop_back.src.Shared.Infrastructure.Data; 
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.ECommerce.Domain.Entities;
using BCrypt.Net;
using shop_back.src.Shared.Infrastructure.Migrations;

namespace shop_back.src.Shared.Infrastructure.Seeds
{
    public static class InitAuthSeeder
    {
        public static async Task Seed(IServiceProvider serviceProvider)
        {
            using var context = new AppDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>());

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Role
                var devRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "developer");
                if (devRole == null)
                {
                    devRole = new Role { Name = "developer", GuardName = "admin" };
                    context.Roles.Add(devRole);
                    await context.SaveChangesAsync();
                }

                // 2. Permission
                var readDashboard = await context.Permissions.FirstOrDefaultAsync(p => p.Name == "read-admin-dashboard");
                if (readDashboard == null)
                {
                    readDashboard = new Permission { Name = "read-admin-dashboard", GuardName = "admin" };
                    context.Permissions.Add(readDashboard);
                    await context.SaveChangesAsync();
                }

                // 3. Role-Permission
                if (!context.RolePermissions.Any(rp => rp.RoleId == devRole.Id && rp.PermissionId == readDashboard.Id))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = devRole.Id,
                        PermissionId = readDashboard.Id
                    });
                    await context.SaveChangesAsync();
                }

                // 3b. Documentation permissions (ships with the Documentation feature)
                var readDocDeveloper = await context.Permissions.FirstOrDefaultAsync(p => p.Name == "read-admin-doc-developer");
                if (readDocDeveloper == null)
                {
                    readDocDeveloper = new Permission { Name = "read-admin-doc-developer", GuardName = "admin" };
                    context.Permissions.Add(readDocDeveloper);
                    await context.SaveChangesAsync();
                }

                var readDocUserGuide = await context.Permissions.FirstOrDefaultAsync(p => p.Name == "read-admin-doc-user-guide");
                if (readDocUserGuide == null)
                {
                    readDocUserGuide = new Permission { Name = "read-admin-doc-user-guide", GuardName = "admin" };
                    context.Permissions.Add(readDocUserGuide);
                    await context.SaveChangesAsync();
                }

                // read-admin-doc-developer -> developer role only
                if (!context.RolePermissions.Any(rp => rp.RoleId == devRole.Id && rp.PermissionId == readDocDeveloper.Id))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = devRole.Id,
                        PermissionId = readDocDeveloper.Id
                    });
                    await context.SaveChangesAsync();
                }

                // read-admin-doc-user-guide -> every existing role (developer, admin, super-admin, ...).
                // Iterating all roles rather than hardcoding names keeps this correct regardless of
                // exactly which roles exist in a given environment's database.
                var allRoles = await context.Roles.ToListAsync();
                foreach (var role in allRoles)
                {
                    if (!context.RolePermissions.Any(rp => rp.RoleId == role.Id && rp.PermissionId == readDocUserGuide.Id))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = readDocUserGuide.Id
                        });
                        await context.SaveChangesAsync();
                    }
                }

                // 4. User
                var userEmail = "toukir.ahamed.pigeon@gmail.com";
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                {
                    user = new User
                    {
                        Name = "Toukir Ahamed Pigeon",
                        Username = "pigeon",
                        Email = userEmail,
                        MobileNo = "+8801754479709",
                        Password = BCrypt.Net.BCrypt.HashPassword("developer")
                    };
                    context.Users.Add(user);
                    await context.SaveChangesAsync();
                }

                // 5. Assign Role to User
                if (!context.ModelRoles.Any(mr => mr.ModelId == user.Id && mr.RoleId == devRole.Id && mr.ModelName == "User"))
                {
                    context.ModelRoles.Add(new ModelRole
                    {
                        ModelId = user.Id,
                        RoleId = devRole.Id,
                        ModelName = "User"
                    });
                    await context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}