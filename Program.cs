using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RYT.Data;
using RYT.Models.Entities;
using RYT.Services.CloudinaryService;
using RYT.Services.Emailing;
using RYT.Services.NotificationSaga;
using RYT.Services.Payment;
using RYT.Services.PaymentGateway;
using RYT.Services.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddSession();
System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "wwwroot/firebase/firebase.json");
builder.Services.AddDbContext<RYTDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("default")));
//builder.Services.AddDbContext<RYTDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<RYTDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IRepository, Repository>();
builder.Services.AddScoped<IEmailService, Emailing>();

builder.Services.AddScoped<IPaymentService, PayStackService>();
builder.Services.AddScoped<IPayments, Payments>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddTransient<IFirebaseService, FirebaseService>();
builder.Services.AddSingleton(provider =>
{
    var firebaseApp = FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.GetApplicationDefault(),
    });

    var projectId = "ryt-decagon-14ec0";

    // Register FirestoreDb
    var firestoreDb = FirestoreDb.Create(projectId);
    return firestoreDb;
});
builder.Services.AddSignalR();

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
app.MapHub<NotificationHub>("Notification-Hub");
app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

Seeder.SeedeMe(app);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
