using DinkToPdf;
using DinkToPdf.Contracts;
using FluentValidation;
using HESCO;
using HESCO.DAL;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDataProtection()
    .SetApplicationName("Hesco") // Unique name per app
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\DataProtectionKeys\Hesco")); // Unique folder

// Register services
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
builder.Services.AddTransient<ViewRenderService>();
builder.Services.AddTransient<PdfService>();
builder.Services.AddTransient<EmailService>();
builder.Services.AddTransient<MenuService>();
builder.Services.AddHttpClient<ApiService>();
builder.Services.AddTransient<SimsManagementDAL>();
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});
builder.Services.AddHttpContextAccessor();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(96); // Set session timeout
    options.Cookie.HttpOnly = true; // Make the session cookie HTTP only
    options.Cookie.IsEssential = true; // Ensure cookie is essential for session management
});

// Add authentication services
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/Account/LoginUser";
//        options.LogoutPath = "/Account/Logout";
//        options.ExpireTimeSpan = TimeSpan.FromHours(96);
//        options.SlidingExpiration = true;
//    });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Hesco.Auth"; // Unique name per app
        options.LoginPath = "/Account/LoginUser";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(48);
        options.SlidingExpiration = true;
    });



// Add services to the container
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session management
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=LoginUser}/{id?}");

app.Run();


