using ITPSystem.Data;
using ITPSystem.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("MySqlConn"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MySqlConn"))
    )
);

builder.Services.AddSession();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient<OllamaStudentAssistantService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapRazorPages();

app.Run();

