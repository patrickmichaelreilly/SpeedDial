using SpeedDial.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Windows Service
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "SpeedDial";
});

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Register our services
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddScoped<TechnitiumDnsService>();
builder.Services.AddScoped<NginxProxyManagerService>();
builder.Services.AddScoped<ServiceOrchestrator>();

// Configure Kestrel to listen on port 5555
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5555);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();