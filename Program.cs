using Microsoft.Extensions.DependencyInjection;
using Polly;
using RetryPattern.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register HttpClient with Polly retry policy for RetryServices
builder.Services.AddHttpClient<RetryServices>();
//builder.Services.AddHttpClient<RetryServices>(client =>
//{
//    // You can configure HttpClient here if needed (e.g., base address, headers, etc.)
//    client.BaseAddress = new Uri("https://example.com");
//})
//.AddPolicyHandler(Policy
//    .Handle<HttpRequestException>()
//    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
