using Microsoft.EntityFrameworkCore;
using MyIRC.Application.Interfaces.Repositories;
using MyIRC.Application.Interfaces.Security;
using MyIRC.Application.Interfaces.Stores;
using MyIRC.Infrastructure.Data;
using MyIRC.Infrastructure.Repositories;
using MyIRC.Infrastructure.Security;
using MyIRC.Infrastructure.Stores;
using MyIRC.Web.Hubs;

namespace MyIRC.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Railway için
            var port = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrWhiteSpace(port))
            {
                builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
            }

            builder.Services.AddControllersWithViews();

            builder.Services.AddSignalR();

            var connectionString =
    Environment.GetEnvironmentVariable("MYSQLCONNSTR_DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString)
                ));

            builder.Services.AddScoped<IUserAccountRepository, UserAccountRepository>();
            builder.Services.AddScoped<IChannelRegistrationRepository, ChannelRegistrationRepository>();

            builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

            builder.Services.AddSingleton<IOnlineUserStore, OnlineUserStore>();

            builder.Services.AddScoped<MyIRC.Application.Services.NickServ.NickServService>();
            builder.Services.AddScoped<MyIRC.Application.Services.ChanServ.ChanServService>();
            builder.Services.AddScoped<MyIRC.Application.Services.ChannelModes.ChannelModeService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "sohbet",
                pattern: "sohbet",
                defaults: new { controller = "Home", action = "Sohbet" });

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapHub<ChatHub>("/chatHub");

            app.Run();
        }
    }
}