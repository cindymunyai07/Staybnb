namespace Staybnb.Data;

public static class RoleSeeder
{
    public const string SeedPassword = "Staybnb@123";
    public const string SuperAdminEmail = "superadmin@staybnb.com";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await EnsureUserAsync(userManager, SuperAdminEmail, "Super", "Admin", AppRoles.SuperAdmin);
    }

    public static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager,
        string email, string firstName, string lastName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                CreatedAt = DateTime.Now
            };
            var result = await userManager.CreateAsync(user, SeedPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not seed user " + email + ": " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);

        return user;
    }
}
