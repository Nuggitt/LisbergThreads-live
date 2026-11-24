using Microsoft.AspNetCore.Identity;

namespace LisbergThreads.Api.Data
{
    public static class SeedData
    {
        public static async Task SeedRolesAsync(IServiceProvider services)
        {
            var roleManager =
                services.GetRequiredService<RoleManager<ApplicationRole>>();

            string[] roles = { "Admin", "Customer" };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = roleName
                    });
                }
            }
        }

        public static async Task SeedAdminAsync(
    IServiceProvider services,
    string email)
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                return;
            }

            if (!await userManager.IsInRoleAsync(user, "Admin"))
            {
                await userManager.AddToRoleAsync(user, "Admin");
            }

            if (await userManager.IsInRoleAsync(user, "Customer"))
            {
                await userManager.RemoveFromRoleAsync(user, "Customer");
            }
        }
    }
}