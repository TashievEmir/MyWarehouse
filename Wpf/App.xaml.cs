using System.IO;
using System.Windows;
using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.Services;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IServiceProvider _services;
        
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            var dbPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "app.db");
            services.AddDbContext<DataContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            services.AddScoped<IDataContext>(sp =>
                sp.GetRequiredService<DataContext>());

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ISalesService, SalesService>();
            services.AddScoped<IPurchaseService, PurchaseService>();

            _services = services.BuildServiceProvider();

            // 🔥 RUN SEEDER HERE
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDataContext>();

            await db.MigrateAsync(CancellationToken.None);
            
            await DatabaseSeeder.SeedAsync(db, CancellationToken.None);

            // start UI
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

}
