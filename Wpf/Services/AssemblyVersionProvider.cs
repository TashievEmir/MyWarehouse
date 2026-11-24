using System.IO;
using System.Reflection;
using Application.Contracts.Interfaces;

namespace Wpf.Services;

/// <summary>
/// Версия берётся из самой сборки — её задаёт &lt;Version&gt; в Wpf.csproj.
/// Так номер нельзя забыть поднять отдельно от выпуска: он уже в exe.
/// </summary>
public class AssemblyVersionProvider : IAppVersionProvider
{
    public string CurrentVersion { get; }

    public string InstallDirectory { get; }

    public AssemblyVersionProvider()
    {
        var assembly = Assembly.GetEntryAssembly();

        // InformationalVersion хранит «1.0.0», тогда как AssemblyVersion
        // всегда дополнен до четырёх чисел: «1.0.0.0»
        var informational = assembly
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        CurrentVersion = Clean(informational)
                         ?? assembly?.GetName().Version?.ToString()
                         ?? "0.0.0";

        InstallDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Сборка из git дописывает к версии хеш через «+» — для сравнения он лишний.
    /// </summary>
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var plus = value.IndexOf('+');

        return plus > 0 ? value[..plus] : value;
    }
}
