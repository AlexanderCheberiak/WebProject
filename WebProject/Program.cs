using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using WebProject.Data;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Identity;
using Google.Cloud.Storage.V1;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using WebProject.Models;
using Azure.Search.Documents.Indexes.Models;

var builder = WebApplication.CreateBuilder(args);
FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.GetApplicationDefault(),
    ProjectId = "restaurant-cloud-476111" ,
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts =>
{
    opts.Level = CompressionLevel.Fastest; 
});
builder.Services.Configure<GzipCompressionProviderOptions>(opts =>
{
    opts.Level = CompressionLevel.Fastest;
});

builder.Services.AddControllersWithViews();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });



builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 30))
    ));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication();

builder.Services.AddHttpClient();
builder.Services.AddSingleton(provider => StorageClient.Create());
builder.Services.AddSingleton(provider => {
    string endpoint = builder.Configuration["Search:Endpoint"];
    string apiKey = builder.Configuration["Search:ApiKey"];

    if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("Azure Search Endpoint або ApiKey не налаштовано.");
    }
    
    var credential = new AzureKeyCredential(apiKey);
    var endpointUri = new Uri(endpoint);

    return new SearchIndexClient(endpointUri, credential);
});

builder.Services.AddSingleton(provider => {
    string endpoint = builder.Configuration["Search:Endpoint"];
    string apiKey = builder.Configuration["Search:ApiKey"];
    var credential = new AzureKeyCredential(apiKey);
    var endpointUri = new Uri(endpoint);
    
    return new SearchClient(endpointUri, "menu-items", credential);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorPages();
app.MapDefaultControllerRoute();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
try
{
    var indexClient = app.Services.GetRequiredService<SearchIndexClient>();
    
    var fieldBuilder = new FieldBuilder();
    var searchFields = fieldBuilder.Build(typeof(MenuItemSearchDocument));
    var index = new SearchIndex("menu-items", searchFields);

    await indexClient.CreateIndexAsync(index);
    Console.WriteLine("[Search] Індекс 'menu-items' успішно створено або вже існує.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Search] ПОМИЛКА: Не вдалося створити індекс: {ex.Message}");
}

async Task SeedRolesAndAdmins(WebApplication webApp)
{
    using (var scope = webApp.Services.CreateScope())
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Console.WriteLine($"[Roles] Роль '{role}' успішно створена.");
            }
        }

        var allUsers = userManager.Users.ToList();
        foreach (var user in allUsers)
        {
            if (!await userManager.IsInRoleAsync(user, "Admin"))
            {
                await userManager.AddToRoleAsync(user, "Admin");
                Console.WriteLine($"[Roles] Користувач '{user.UserName}' додано до ролі 'Admin'.");
            }
        }

        Console.WriteLine("[Roles] Ініціалізація ролей завершена.");
    }
}

await SeedRolesAndAdmins(app);

app.Run();
