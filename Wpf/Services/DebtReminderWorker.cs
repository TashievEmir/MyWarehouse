using System.Windows.Threading;
using Application.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Wpf.Services;

/// <summary>
/// Гоняет рассылку напоминаний, пока приложение открыто.
///
/// Это десктоп, а не сервер: при выключенном компьютере письма не уходят.
/// Пропущенный за день слот досылается при первом запуске в тот же день,
/// но если приложение весь день не включали — напоминания за этот день теряются.
/// </summary>
public class DebtReminderWorker
{
    /// <summary>Раз в пять минут — слоты заданы с точностью до минут, чаще незачем.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly DispatcherTimer _timer;

    private bool _running;

    public DebtReminderWorker(IServiceProvider services)
    {
        _services = services;

        _timer = new DispatcherTimer { Interval = Interval };
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public void Start()
    {
        _timer.Start();

        // Первый проход сразу после запуска: досылаем пропущенное за сегодня
        _ = TickAsync();
    }

    public void Stop() => _timer.Stop();

    private async Task TickAsync()
    {
        // Проход может не уложиться в интервал — второй запускать не нужно
        if (_running)
            return;

        _running = true;

        try
        {
            // Свой scope: у фонового прохода должен быть собственный DbContext
            using var scope = _services.CreateScope();

            var reminders = scope.ServiceProvider.GetRequiredService<IDebtReminderService>();

            await reminders.RunAsync(CancellationToken.None);
        }
        catch
        {
            // Напоминания — не критичный путь: молчим, чтобы не мешать работе кассы.
            // Неудачные отправки видны в журнале рассылки на странице настроек.
        }
        finally
        {
            _running = false;
        }
    }
}
