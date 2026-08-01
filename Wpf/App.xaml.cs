using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.Services;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Services;
using Wpf.ViewModels;
using Wpf.ViewModels.Dashboard;
using Wpf.ViewModels.Login;
using Wpf.ViewModels.Products;
using Wpf.ViewModels.Sales;
using Wpf.ViewModels.Statistics;
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

            ApplyRussianCulture();

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
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<SessionService>();
            services.AddSingleton<ThemeService>();

            // Views
            services.AddTransient<LoginView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<ProductsView>();
            
            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            // Страницы с разделами — синглтоны: раздел выбирает навигация,
            // и состояние не теряется при переходах
            services.AddSingleton<ProductsPageViewModel>();
            services.AddSingleton<ProductCatalogViewModel>();
            services.AddSingleton<ReceivingViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddSingleton<ViewModels.Receipts.ReceiptsListViewModel>();
            services.AddSingleton<StatisticsPageViewModel>();
            services.AddSingleton<StockStatisticsViewModel>();
            services.AddSingleton<DebtsViewModel>();
            services.AddSingleton<PurchaseLogViewModel>();

            // Касса живёт всё время работы приложения: открытые чеки
            // не должны теряться при переходе на другую страницу
            services.AddSingleton<SalesViewModel>();

            _services = services.BuildServiceProvider();
            Services = _services;

            // Тема ставится до первого окна, иначе оно мигнёт светлым
            Services.GetRequiredService<ThemeService>().Apply();

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

        /// <summary>
        /// Интерфейс русский: без этого даты в DatePicker показывались как 8/1/2026,
        /// а суммы — по инвариантной культуре.
        /// </summary>
        private static void ApplyRussianCulture()
        {
            var culture = new CultureInfo("ru-RU");

            CultureInfo.DefaultThreadCurrentCulture   = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            Thread.CurrentThread.CurrentCulture   = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        }
    }
}
