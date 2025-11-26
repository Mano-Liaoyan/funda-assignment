using MakelaarLeaderboard.Data;
using MakelaarLeaderboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace MakelaarLeaderboard
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddDbContext<MakelaarLeaderboardContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("MakelaarLeaderboardContext") ?? throw new InvalidOperationException("Connection string 'MakelaarLeaderboardContext' not found.")));

            builder.Services.AddHttpClient();
            builder.Services.AddHostedService<DataSyncService>();

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Leaderboard}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
