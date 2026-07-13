using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Interfaces;
using TaskFlow.Repositories;
using TaskFlow.Services.Implementations;
using TaskFlow.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;

    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "TaskFlowAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.LoginPath = "/";
    options.AccessDeniedPath = "/Home/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<ITaskRepository, TaskRepository>();

builder.Services.AddScoped<ITaskService, TaskService>();

builder.Services.AddScoped<EmailService>();

builder.Services.AddScoped<GroqAiService>();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db =
        services.GetRequiredService<AppDbContext>();

    var userManager =
        services.GetRequiredService<
            UserManager<IdentityUser>>();

    var roleManager =
        services.GetRequiredService<
            RoleManager<IdentityRole>>();

    db.Database.Migrate();

    string[] roles =
    {
        "Admin",
        "User"
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(
                new IdentityRole(role));
        }
    }

    string adminUsername = "admin";

    string adminEmail =
        "admin@taskflow.com";

    string adminPassword =
        "Admin@123";

    var adminUser =
        await userManager.FindByNameAsync(
            adminUsername);

    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result =
            await userManager.CreateAsync(
                adminUser,
                adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(
                adminUser,
                "Admin");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine(
                    $"Admin creation error: {error.Description}");
            }
        }
    }
    else
    {
        var adminNeedsUpdate = false;

        if (string.IsNullOrWhiteSpace(adminUser.Email))
        {
            adminUser.Email = adminEmail;

            adminNeedsUpdate = true;
        }

        if (!adminUser.EmailConfirmed)
        {
            adminUser.EmailConfirmed = true;

            adminNeedsUpdate = true;
        }

        if (adminNeedsUpdate)
        {
            var updateResult =
                await userManager.UpdateAsync(adminUser);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    Console.WriteLine(
                        $"Admin update error: {error.Description}");
                }
            }
        }

        if (!await userManager.IsInRoleAsync(
                adminUser,
                "Admin"))
        {
            await userManager.AddToRoleAsync(
                adminUser,
                "Admin");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}");

app.Run();