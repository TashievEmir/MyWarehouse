using System.IO;
using System.Windows;
using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.Services;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Services;
using Wpf.ViewModels;
using Wpf.ViewModels.Login;
using Wpf.ViewModels.Products;
using Wpf.ViewModels.Sales;
using Wpf.Views.Login;
using Wpf.Views.Products;

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
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<SessionService>();

            // Views
            services.AddTransient<LoginView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<ProductsView>();
            
            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<ProductsViewModel>();

            // Касса живёт всё время работы приложения: открытые чеки
            // не должны теряться при переходе на другую страницу
            services.AddSingleton<SalesViewModel>();

            _services = services.BuildServiceProvider();
            Services = _services;

            // 🔥 migrate + seed
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDataContext>();

            await db.MigrateAsync(CancellationToken.None);
            await DatabaseSeeder.SeedAsync(db, CancellationToken.None);

            // 🔥 открываем Login через DI: продажа привязывается к кассиру,
            // поэтому без входа работать нельзя
            var login = Services.GetRequiredService<LoginView>();
            login.Show();
        }
    }
}
