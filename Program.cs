using CarCareTracker.External.Implementations;
using CarCareTracker.External.Interfaces;
using CarCareTracker.Helper;
using CarCareTracker.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IVehicleDataAccess, VehicleDataAccess>();
builder.Services.AddSingleton<INoteDataAccess, NoteDataAccess>();
builder.Services.AddSingleton<IServiceRecordDataAccess, ServiceRecordDataAccess>();
builder.Services.AddSingleton<IGasRecordDataAccess, GasRecordDataAccess>();
builder.Services.AddSingleton<ICollisionRecordDataAccess, CollisionRecordDataAccess>();
builder.Services.AddSingleton<ITaxRecordDataAccess, TaxRecordDataAccess>();
builder.Services.AddSingleton<IReminderRecordDataAccess, ReminderRecordDataAccess>();
builder.Services.AddSingleton<IUpgradeRecordDataAccess, UpgradeRecordDataAccess>();
builder.Services.AddSingleton<IUserRecordDataAccess, UserRecordDataAccess>();

//configure helpers
builder.Services.AddSingleton<IFileHelper, FileHelper>();
builder.Services.AddSingleton<IGasHelper, GasHelper>();
builder.Services.AddSingleton<IReminderHelper, ReminderHelper>();
builder.Services.AddSingleton<ILoginHelper, LoginHelper>();
builder.Services.AddSingleton<IReportHelper, ReportHelper>();

if (!Directory.Exists("data"))
{
    Directory.CreateDirectory("data");
}

//Additional JsonFile
builder.Configuration.AddJsonFile(StaticHelper.UserConfigPath, optional: true, reloadOnChange: true);

//Configure Auth
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication("AuthN").AddScheme<AuthenticationSchemeOptions, Authen>("AuthN", opts => { });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder().AddAuthenticationSchemes("AuthN").RequireAuthenticatedUser().Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
