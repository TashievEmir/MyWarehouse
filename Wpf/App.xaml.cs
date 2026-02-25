using System.IO;
using System.Windows;
using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.Services;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Services;
using Wpf.ViewModels.Login;
using Wpf.Views.Login;

namespace Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IServiceProvider _services;

        // 🔥 глобальный доступ к DI
        public static IServiceProvider Services { get; private set; } = null!;

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

            // Views
            services.AddTransient<LoginView>();
            services.AddTransient<MainWindow>();
            
            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddSingleton<NavigationService>();

            _services = services.BuildServiceProvider();
            Services = _services;

            // 🔥 migrate + seed
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDataContext>();

            await db.MigrateAsync(CancellationToken.None);
            await DatabaseSeeder.SeedAsync(db, CancellationToken.None);

            // 🔥 открываем Login через DI
            var login = Services.GetRequiredService<LoginView>();
            login.Show();
        }
    }
}
