using System.Text;

namespace Infrastructure.Updates
{
    /// <summary>
    /// Скрипт, который подменяет файлы, пока приложение закрыто.
    ///
    /// Отдельный процесс нужен не для красоты: Windows держит запущенный exe
    /// и его библиотеки открытыми, перезаписать их из самого приложения нельзя.
    /// </summary>
    public static class UpdateScript
    {
        /// <summary>Файлы, которые обновление не имеет права трогать.</summary>
        public static readonly IReadOnlyList<string> Protected =
        [
            "app.db",        // вся база: товары, продажи, клиенты, долги
            "app.db-wal",
            "app.db-shm",
            "appsettings.json", // настройки почты, включая пароль приложения
            "lang.txt",
        ];

        public static string LaunchTarget(string installDirectory)
            => Path.Combine(installDirectory, "Wpf.exe");

        public static string Write(string workDirectory)
        {
            var path = Path.Combine(workDirectory, "apply-update.ps1");

            File.WriteAllText(path, Build(), new UTF8Encoding(true));

            return path;
        }

        private static string Build()
        {
            var keep = string.Join(",", Protected.Select(name => "'" + name + "'"));

            return $$"""
                param(
                    [Parameter(Mandatory)] [int]    $ProcessId,
                    [Parameter(Mandatory)] [string] $Staging,
                    [Parameter(Mandatory)] [string] $Target,
                    [Parameter(Mandatory)] [string] $Backup,
                    [Parameter(Mandatory)] [string] $Launch
                )

                $ErrorActionPreference = 'Stop'

                $log = Join-Path (Split-Path $Backup) 'update.log'

                function Note($text) {
                    "$([DateTime]::Now.ToString('HH:mm:ss')) $text" | Out-File -FilePath $log -Append -Encoding utf8
                }

                # Файлы пользователя: обновление их не трогает
                $keep = @({{keep}})

                Note "Ждём выхода процесса $ProcessId"

                $deadline = (Get-Date).AddSeconds(60)

                while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
                    Start-Sleep -Milliseconds 300
                }

                if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
                    Note 'Приложение не закрылось за минуту — обновление отменено'
                    exit 1
                }

                # Файлы могут ещё секунду оставаться занятыми после выхода
                Start-Sleep -Milliseconds 700

                $copied = New-Object System.Collections.ArrayList

                try {
                    New-Item -ItemType Directory -Force -Path $Backup | Out-Null

                    Get-ChildItem -Path $Staging -Recurse -File | ForEach-Object {
                        $relative = $_.FullName.Substring($Staging.Length).TrimStart('\')
                        $destination = Join-Path $Target $relative

                        if ($keep -contains $_.Name) {
                            # Своё ставим только если у клиента такого файла ещё нет:
                            # иначе затрём базу или пароль в настройках
                            if (-not (Test-Path $destination)) {
                                New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
                                Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
                                Note "Добавлен $relative"
                            } else {
                                Note "Сохранён как есть: $relative"
                            }
                            return
                        }

                        if (Test-Path $destination) {
                            $saved = Join-Path $Backup $relative
                            New-Item -ItemType Directory -Force -Path (Split-Path $saved) | Out-Null
                            Copy-Item -LiteralPath $destination -Destination $saved -Force
                        }

                        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
                        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force

                        [void]$copied.Add($relative)
                    }

                    Note "Обновлено файлов: $($copied.Count)"
                }
                catch {
                    Note "Сбой: $_"
                    Note 'Откатываем на прежнюю версию'

                    # Возвращаем только то, что успели заменить: лишнего не трогаем
                    foreach ($relative in $copied) {
                        $saved = Join-Path $Backup $relative

                        if (Test-Path $saved) {
                            Copy-Item -LiteralPath $saved -Destination (Join-Path $Target $relative) -Force
                        }
                    }

                    Note 'Откат завершён, запускаем прежнюю версию'

                    Start-Process -FilePath $Launch
                    exit 1
                }

                Note 'Запускаем обновлённое приложение'

                Start-Process -FilePath $Launch
                """;
        }
    }
}
