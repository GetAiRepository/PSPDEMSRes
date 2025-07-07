using EMS.WebApp.Authorization;
using EMS.WebApp.Data;
using EMS.WebApp.Filters;
using EMS.WebApp.Middleware;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IMedDiseaseRepository, MedDiseaseRepository>();
builder.Services.AddScoped<IMedRefHospitalRepository, MedRefHospitalRepository>(); 
builder.Services.AddScoped<IMedExamCategoryRepository, MedExamCategoryRepository>();
builder.Services.AddScoped<IMedCategoryRepository, MedCategoryRepository>(); 
builder.Services.AddScoped<IMedBaseRepository, MedBaseRepository>(); 
builder.Services.AddScoped<IMedMasterRepository, MedMasterRepository>();
builder.Services.AddScoped<IHrEmployeeRepository, HrEmployeeRepository>();
builder.Services.AddScoped<IHrEmployeeDependentRepository, HrEmployeeDependentRepository>();
builder.Services.AddScoped<IMedDiagnosisRepository, MedDiagnosisRepository>();
builder.Services.AddScoped<ISysUserRepository, SysUserRepository>();
builder.Services.AddScoped<ISysRoleRepository, SysRoleRepository>();
builder.Services.AddScoped<ISysAttachScreenRoleRepository, SysAttachScreenRoleRepository>();
builder.Services.AddScoped<IPlantMasterRepository, PlantMasterRepository>();
builder.Services.AddScoped<IDepartmentMasterRepository, DepartmentMasterRepository>();
builder.Services.AddScoped<IMedAmbulanceMasterRepository, MedAmbulanceMasterRepository>();
builder.Services.AddScoped<ISystemScreenMasterRepository, SystemScreenMasterRepository>();
builder.Services.AddScoped<IAccountLoginRepository, AccountLoginRepository>();
// Add services for menu and authorization
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IScreenAccessRepository, ScreenAccessRepository>();
builder.Services.AddScoped<IAuthorizationHandler, ScreenAccessHandler>();
builder.Services.AddHttpContextAccessor();
//Transition
builder.Services.AddScoped<IHealthProfileRepository, HealthProfileRepository>();
// Store Indent
builder.Services.AddScoped<IStoreIndentRepository, StoreIndentRepository>();
// Register the CompounderIndent repository
builder.Services.AddScoped<ICompounderIndentRepository, CompounderIndentRepository>();
// Register the DoctorDiagnosis repository
builder.Services.AddScoped<IDoctorDiagnosisRepository, DoctorDiagnosisRepository>();
// Register the Others Diagnosis repository
builder.Services.AddScoped<IOthersDiagnosisRepository, OthersDiagnosisRepository>();
// Register the Expired Medicine repository
builder.Services.AddScoped<IExpiredMedicineRepository, ExpiredMedicineRepository>();
builder.Services.AddHostedService<ExpiredMedicineBackgroundService>();
// Add Encryption Service
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// Add Medical Data Masking Service
builder.Services.AddScoped<IMedicalDataMaskingService, MedicalDataMaskingService>();
//this is for rate limiter cache
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
})
.AddNegotiate();
builder.Services.AddAuthorization();

// Register audit services
builder.Services.AddScoped<IAuditService, AuditService>();

// Register the action filter
builder.Services.AddScoped<AuditActionFilter>();

// Configure MVC with the audit filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AuditActionFilter>();
});

// Make sure session is enabled for session tracking
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});



var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var authorizationOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    var screens = context.sys_screen_names.Select(s => s.screen_name).ToList();

    foreach (var screen in screens)
    {
        var policyName = $"Access{screen.Replace(" ", "")}"; // sanitize screen name
        authorizationOptions.AddPolicy(policyName, policy =>
            policy.Requirements.Add(new ScreenAccessRequirement(screen)));
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<SingleSessionMiddleware>();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.UseAuthorization();
app.UseSession();
app.MapControllerRoute(
    name: "compounder-indent-report",
    pattern: "compounder-indent/report",
    defaults: new { controller = "CompounderIndent", action = "CompounderIndentReportView" });

app.MapControllerRoute(
    name: "compounder-inventory-report",
    pattern: "compounder-inventory/report",
    defaults: new { controller = "CompounderIndent", action = "CompounderInventoryReportView" });
app.MapControllerRoute(
    name: "store-indent-report",
    pattern: "store-indent/report",
    defaults: new { controller = "StoreIndent", action = "StoreIndentReportView" });

app.MapControllerRoute(
    name: "store-inventory-report",
    pattern: "store-inventory/report",
    defaults: new { controller = "StoreIndent", action = "StoreInventoryReportView" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
