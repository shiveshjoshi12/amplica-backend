using BizfreeApp.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims; // Required for ClaimTypes and custom claims
using BizfreeApp.Services; // Add this for IEmailSender, SmtpSettings, and IUploadHandler
using BizfreeApp.Models; // Add this for your 'User' model, needed by IPasswordHasher
using Microsoft.AspNetCore.Identity; // Add this for IPasswordHasher
using Microsoft.Extensions.FileProviders; // Required for PhysicalFileProvider
using System.IO; // Required for Path.Combine
using Microsoft.Extensions.Options; // Add this for IOptions<SmtpSettings>
using Microsoft.Extensions.Logging; // Add this for ILogger (if you plan to inject it)
using BizfreeApp.Constants; // IMPORTANT: Add this line to import your Permissions static class
using BizfreeApp.Infrastructure.Security;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BizfreeApp.Data.ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
           .EnableSensitiveDataLogging());

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

builder.Services.AddSingleton(resolver =>
    resolver.GetRequiredService<IOptions<SmtpSettings>>().Value);

FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile("bizfreetest-firebase-adminsdk-fbsvc-7171d79a79.json"),
    ProjectId = builder.Configuration["Firebase:bizfreetest"]
});

// Register notification services
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<ITaskNotificationService, TaskNotificationService>();

// Register background service for automated notifications
builder.Services.AddHostedService<TaskNotificationBackgroundService>();

builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddTransient<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IProjectEmailService, ProjectEmailService>();
builder.Services.AddScoped<ITaskEmailService, TaskEmailService>();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddLogging();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, FineGrainedAuthorizationHandler>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin() // In production, replace with specific origins: .WithOrigins("http://localhost:4200", "https://yourfrontend.com")
                         .AllowAnyMethod()
                         .AllowAnyHeader());
});

// Add controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.MaxDepth = 256; // You might want to increase this as well if cycles are deep
});
builder.Services.AddScoped<IUploadHandler, UploadHandler>();
builder.Services.AddHostedService<TaskStatusBackgroundService>();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
// Ensure the Key exists and is long enough for HS256 (minimum 32 bytes for SHA256)
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not found in configuration."));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],

        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // optional: no extra tolerance time
    };
});

// Configure Authorization services and policies
builder.Services.AddAuthorization(options =>
{
    // Policy for general authenticated users
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());

    // --- TASK PERMISSIONS ---
    options.AddPolicy("CanTaskRead", policy => policy.RequireClaim("permission", Permissions.TaskRead));
    options.AddPolicy("CanTaskReadAll", policy => policy.RequireClaim("permission", Permissions.TaskReadAll));
    options.AddPolicy("CanTaskCreate", policy => policy.RequireClaim("permission", Permissions.TaskCreate));
    options.AddPolicy("CanTaskUpdate", policy => policy.RequireClaim("permission", Permissions.TaskUpdate));
    options.AddPolicy("CanTaskDelete", policy => policy.RequireClaim("permission", Permissions.TaskDelete));
    options.AddPolicy("CanTaskReadAssigned", policy => policy.RequireClaim("permission", Permissions.TaskReadAssigned));
    options.AddPolicy("CanTaskReadOrAssigned", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("permission", Permissions.TaskReadAll) ||
            context.User.HasClaim("permission", Permissions.TaskRead) ||
            context.User.HasClaim("permission", Permissions.TaskReadAssigned)));

    // Task List Permissions
    options.AddPolicy("CanTaskListRead", policy => policy.RequireClaim("permission", Permissions.TaskListRead));
    options.AddPolicy("CanTaskListCreate", policy => policy.RequireClaim("permission", Permissions.TaskListCreate));
    options.AddPolicy("CanTaskListUpdate", policy => policy.RequireClaim("permission", Permissions.TaskListUpdate));
    options.AddPolicy("CanTaskListDelete", policy => policy.RequireClaim("permission", Permissions.TaskListDelete));

    // Task Document Permissions
    options.AddPolicy("CanTaskDocumentCreate", policy => policy.RequireClaim("permission", Permissions.TaskDocumentCreate));
    options.AddPolicy("CanTaskDocumentDelete", policy => policy.RequireClaim("permission", Permissions.TaskDocumentDelete));

    // Task Status Permissions (NEW)
    options.AddPolicy("CanTaskStatusReadAll", policy => policy.RequireClaim("permission", Permissions.TaskStatusReadAll));
    options.AddPolicy("CanTaskStatusCreate", policy => policy.RequireClaim("permission", Permissions.TaskStatusCreate));
    options.AddPolicy("CanTaskStatusUpdate", policy => policy.RequireClaim("permission", Permissions.TaskStatusUpdate));
    options.AddPolicy("CanTaskStatusDelete", policy => policy.RequireClaim("permission", Permissions.TaskStatusDelete));

    // --- TIMELOG PERMISSIONS ---
    options.AddPolicy("CanTimelogRead", policy => policy.RequireClaim("permission", Permissions.TimelogRead));
    options.AddPolicy("CanTimelogCreate", policy => policy.RequireClaim("permission", Permissions.TimelogCreate));
    options.AddPolicy("CanTimelogUpdate", policy => policy.RequireClaim("permission", Permissions.TimelogUpdate));
    options.AddPolicy("CanTimelogDelete", policy => policy.RequireClaim("permission", Permissions.TimelogDelete));
    options.AddPolicy("CanTimelogReadAll", policy => policy.RequireClaim("permission", Permissions.TimelogReadAll));

    // --- PROJECT PERMISSIONS ---
    options.AddPolicy("CanProjectRead", policy => policy.RequireClaim("permission", Permissions.ProjectRead));
    options.AddPolicy("CanProjectReadAll", policy => policy.RequireClaim("permission", Permissions.ProjectReadAll));
    options.AddPolicy("CanProjectCreate", policy => policy.RequireClaim("permission", Permissions.ProjectCreate));
    options.AddPolicy("CanProjectUpdate", policy => policy.RequireClaim("permission", Permissions.ProjectUpdate));
    options.AddPolicy("CanProjectDelete", policy => policy.RequireClaim("permission", Permissions.ProjectDelete));

    // Project Member Permissions
    options.AddPolicy("CanProjectMemberRead", policy => policy.RequireClaim("permission", Permissions.ProjectMemberRead));
    options.AddPolicy("CanProjectMemberCreate", policy => policy.RequireClaim("permission", Permissions.ProjectMemberCreate));
    options.AddPolicy("CanProjectMemberDelete", policy => policy.RequireClaim("permission", Permissions.ProjectMemberDelete));

    // Project Document Permissions
    options.AddPolicy("CanProjectDocumentCreate", policy => policy.RequireClaim("permission", Permissions.ProjectDocumentCreate));
    options.AddPolicy("CanProjectDocumentDelete", policy => policy.RequireClaim("permission", Permissions.ProjectDocumentDelete));

    // --- COMPANY PERMISSIONS (NEW) ---
    options.AddPolicy("CanCompanyRead", policy => policy.RequireClaim("permission", Permissions.CompanyRead));
    options.AddPolicy("CanCompanyReadAll", policy => policy.RequireClaim("permission", Permissions.CompanyReadAll));
    options.AddPolicy("CanCompanyCreate", policy => policy.RequireClaim("permission", Permissions.CompanyCreate));
    options.AddPolicy("CanCompanyUpdate", policy => policy.RequireClaim("permission", Permissions.CompanyUpdate));
    options.AddPolicy("CanCompanyDelete", policy => policy.RequireClaim("permission", Permissions.CompanyDelete));

    // --- COMPANY ROLE PERMISSIONS (NEW) ---
    options.AddPolicy("CanCompanyRoleRead", policy => policy.RequireClaim("permission", Permissions.CompanyRoleRead));
    options.AddPolicy("CanCompanyRoleReadAll", policy => policy.RequireClaim("permission", Permissions.CompanyRoleReadAll));
    options.AddPolicy("CanCompanyRoleCreate", policy => policy.RequireClaim("permission", Permissions.CompanyRoleCreate));
    options.AddPolicy("CanCompanyRoleUpdate", policy => policy.RequireClaim("permission", Permissions.CompanyRoleUpdate));
    options.AddPolicy("CanCompanyRoleDelete", policy => policy.RequireClaim("permission", Permissions.CompanyRoleDelete));

    // --- REPORT PERMISSIONS (NEW) ---
    options.AddPolicy("CanReportRead", policy => policy.RequireClaim("permission", Permissions.ReportRead));
    options.AddPolicy("CanReportReadAll", policy => policy.RequireClaim("permission", Permissions.ReportReadAll));
    options.AddPolicy("CanReportCreate", policy => policy.RequireClaim("permission", Permissions.ReportCreate));
    options.AddPolicy("CanReportUpdate", policy => policy.RequireClaim("permission", Permissions.ReportUpdate));
    options.AddPolicy("CanReportDelete", policy => policy.RequireClaim("permission", Permissions.ReportDelete));

    // --- EMPLOYEE PERMISSIONS (NEW) ---
    options.AddPolicy("CanEmployeeRead", policy => policy.RequireClaim("permission", Permissions.EmployeeRead));
    options.AddPolicy("CanEmployeeReadAll", policy => policy.RequireClaim("permission", Permissions.EmployeeReadAll));
    options.AddPolicy("CanEmployeeCreate", policy => policy.RequireClaim("permission", Permissions.EmployeeCreate));
    options.AddPolicy("CanEmployeeUpdate", policy => policy.RequireClaim("permission", Permissions.EmployeeUpdate));
    options.AddPolicy("CanEmployeeDelete", policy => policy.RequireClaim("permission", Permissions.EmployeeDelete));
    // Password Management Permissions
    options.AddPolicy("CanEmployeeChangePassword",
        policy => policy.RequireClaim("permission", Permissions.EmployeeChangePassword));
    options.AddPolicy("CanCompanyAdminChangePassword",
        policy => policy.RequireClaim("permission", Permissions.CompanyAdminChangePassword));

    // --- DEPARTMENT PERMISSIONS (NEW) ---
    options.AddPolicy("CanDepartmentRead", policy => policy.RequireClaim("permission", Permissions.DepartmentRead));
    options.AddPolicy("CanDepartmentReadAll", policy => policy.RequireClaim("permission", Permissions.DepartmentReadAll));
    options.AddPolicy("CanDepartmentCreate", policy => policy.RequireClaim("permission", Permissions.DepartmentCreate));
    options.AddPolicy("CanDepartmentUpdate", policy => policy.RequireClaim("permission", Permissions.DepartmentUpdate));
    options.AddPolicy("CanDepartmentDelete", policy => policy.RequireClaim("permission", Permissions.DepartmentDelete));

    // --- GROUPED POLICIES (Optional for convenience) ---
    options.AddPolicy("CanManageTasks", policy =>
        policy.RequireAssertion(context => new[]
        {
            Permissions.TaskReadAll,
            Permissions.TaskRead,
            Permissions.TaskCreate,
            Permissions.TaskUpdate,
            Permissions.TaskDelete
        }.All(permission => context.User.HasClaim("permission", permission))));

    options.AddPolicy("CanManageTaskLists", policy =>
        policy.RequireAssertion(context => new[]
        {
            Permissions.TaskListRead,
            Permissions.TaskListCreate,
            Permissions.TaskListUpdate,
            Permissions.TaskListDelete
        }.All(permission => context.User.HasClaim("permission", permission))));

    options.AddPolicy("CanManageProjects", policy =>
        policy.RequireAssertion(context => new[]
        {
            Permissions.ProjectRead,
            Permissions.ProjectCreate,
            Permissions.ProjectUpdate,
            Permissions.ProjectDelete
        }.All(permission => context.User.HasClaim("permission", permission))));

    options.AddPolicy("CanManageCompanies", policy =>
        policy.RequireAssertion(context => new[]
        {
            Permissions.CompanyRead,
            Permissions.CompanyCreate,
            Permissions.CompanyUpdate,
            Permissions.CompanyDelete
        }.All(permission => context.User.HasClaim("permission", permission))));

    options.AddPolicy("CanManageEmployees", policy =>
        policy.RequireAssertion(context => new[]
        {
            Permissions.EmployeeRead,
            Permissions.EmployeeCreate,
            Permissions.EmployeeUpdate,
            Permissions.EmployeeDelete
        }.All(permission => context.User.HasClaim("permission", permission))));

    options.AddPolicy("CanManageDepartments", policy =>
        policy.RequireAssertion(context => new[]
        {
            Permissions.DepartmentRead,
            Permissions.DepartmentCreate,
            Permissions.DepartmentUpdate,
            Permissions.DepartmentDelete
        }.All(permission => context.User.HasClaim("permission", permission))));
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

// using (var scope = app.Services.CreateScope())
// {
//     var dbContext = scope.ServiceProvider.GetRequiredService<BizfreeApp.Data.ApplicationDbContext>();
//     dbContext.Database.Migrate();
// }

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Uploads")),
    RequestPath = "/Uploads"
});

app.UseRouting();

// **IMPORTANT: Add Authentication middleware before Authorization**
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Explicitly configure Kestrel to listen on port 80 if running in Docker
//var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
//app.Urls.Add($"http://*:{port}");

app.Run();
