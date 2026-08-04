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
        private DebtReminderWorker? _reminderWorker;

        // 🔥 глобальный доступ к DI
        public static IServiceProvider Services { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ApplyCulture();

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
            services.AddScoped<ISupplierService, SupplierService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IActivityLogService, ActivityLogService>();
            services.AddScoped<IReceiptTemplateService, ReceiptTemplateService>();
            services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IDebtReminderService, DebtReminderService>();
            services.AddSingleton<IEmailSender, Infrastructure.Notifications.SmtpEmailSender>();
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
            services.AddSingleton<ViewModels.Receipts.ReceiptTemplateViewModel>();
            services.AddSingleton<ViewModels.Activity.ActivityLogViewModel>();
            services.AddSingleton<ViewModels.Notifications.NotificationsViewModel>();
            services.AddSingleton<ViewModels.Users.UsersViewModel>();
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

            // Напоминания о долгах гоняются в фоне, пока приложение открыто
            _reminderWorker = new DebtReminderWorker(Services);
            _reminderWorker.Start();

            // 🔥 открываем Login через DI: продажа привязывается к кассиру,
            // поэтому без входа работать нельзя
            var login = Services.GetRequiredService<LoginView>();
            login.Show();
        }

        /// <summary>
        /// Ставит сохранённый язык интерфейса. Без этого даты в DatePicker
        /// показывались как 8/1/2026, а суммы — по инвариантной культуре.
        /// Кыргызская локаль на некоторых системах отсутствует — тогда
        /// откатываемся на русскую, тексты всё равно берутся из ресурсов.
        /// </summary>
        private static void ApplyCulture()
        {
            Localization.Loc.Instance.Apply();

            var culture = Localization.Loc.Instance.Culture;

            Thread.CurrentThread.CurrentCulture   = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        }
    }
}
