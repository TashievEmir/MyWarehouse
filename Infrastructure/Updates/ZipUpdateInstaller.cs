using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using Application.Contracts.Interfaces;
using Application.DTOs.Updates;

namespace Infrastructure.Updates
{
    /// <summary>
    /// Ставит обновление из zip-архива.
    ///
    /// Работающий exe не может переписать сам себя, поэтому подменой файлов
    /// занимается отдельный скрипт: приложение готовит всё, запускает его
    /// и закрывается, а скрипт дожидается выхода, копирует файлы и стартует
    /// приложение заново.
    /// </summary>
    public class ZipUpdateInstaller : IUpdateInstaller
    {
        private readonly IHttpClientFactory _factory;
        private readonly IAppVersionProvider _app;

        public ZipUpdateInstaller(IHttpClientFactory factory, IAppVersionProvider app)
        {
            _factory = factory;
            _app = app;
        }

        public async Task InstallAsync(
            UpdateManifest manifest,
            IProgress<UpdateProgress>? progress,
            CancellationToken ct)
        {
            if (!Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Ссылка на архив обновления должна быть https");
            }

            var work = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(),
                "MyWarehouse.update",
                manifest.Version + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));

            var archive = Path.Combine(work.FullName, "package.zip");
            var staging = Path.Combine(work.FullName, "files");

            await DownloadAsync(uri, archive, manifest.SizeBytes, progress, ct);

            progress?.Report(new UpdateProgress { Stage = UpdateStage.Verifying });

            VerifyHash(archive, manifest.Sha256);

            progress?.Report(new UpdateProgress { Stage = UpdateStage.Extracting });

            Extract(archive, staging);

            progress?.Report(new UpdateProgress { Stage = UpdateStage.Preparing });

            var target = _app.InstallDirectory;

            var source = FindPackageRoot(staging, target);

            var script = UpdateScript.Write(work.FullName);

            progress?.Report(new UpdateProgress { Stage = UpdateStage.Restarting });

            Launch(script, source, target, Path.Combine(work.FullName, "backup"), work.FullName);
        }

        // ===================== Скачивание =====================

        private async Task DownloadAsync(
            Uri uri,
            string path,
            long expectedSize,
            IProgress<UpdateProgress>? progress,
            CancellationToken ct)
        {
            var client = _factory.CreateClient(UpdateHttp.ClientName);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? expectedSize;

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var destination = File.Create(path);

            var buffer = new byte[81920];
            long done = 0;
            var lastPercent = -1;

            while (true)
            {
                var read = await source.ReadAsync(buffer, ct);

                if (read == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, read), ct);

                done += read;

                if (total <= 0 || progress is null)
                    continue;

                var percent = (int)(done * 100 / total);

                // Не дёргаем интерфейс на каждом блоке — только когда цифра сменилась
                if (percent == lastPercent)
                    continue;

                lastPercent = percent;

                progress.Report(new UpdateProgress { Stage = UpdateStage.Downloading, Percent = percent });
            }
        }

        // ===================== Проверка =====================

        /// <summary>
        /// Без сверки контрольной суммы любой сбой закачки или подмена архива
        /// по пути превратились бы в неработающее приложение у клиента.
        /// </summary>
        private static void VerifyHash(string path, string expected)
        {
            using var stream = File.OpenRead(path);

            var actual = Convert.ToHexString(SHA256.HashData(stream));

            if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Контрольная сумма архива не совпала. Ожидали {expected.Trim()}, получили {actual}");
            }
        }

        /// <summary>
        /// Распаковка с проверкой путей: запись в архиве может притвориться
        /// именем с переходом вверх по дереву и вылезти за пределы папки.
        /// </summary>
        private static void Extract(string archivePath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            var root = Path.GetFullPath(targetDir + Path.DirectorySeparatorChar);

            using var zip = ZipFile.OpenRead(archivePath);

            foreach (var entry in zip.Entries)
            {
                // Папки приходят записями с пустым именем
                if (entry.Name.Length == 0)
                    continue;

                var full = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Архив пытается записать файл за своими пределами: {entry.FullName}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(full)!);

                entry.ExtractToFile(full, overwrite: true);
            }
        }

        /// <summary>
        /// Папка внутри архива, где лежит приложение. Заодно проверяем, что это
        /// вообще наша сборка: иначе скрипт разложит рядом с базой случайные
        /// файлы, а запускать будет нечего.
        /// </summary>
        private static string FindPackageRoot(string staging, string target)
        {
            var exe = Path.GetFileName(UpdateScript.LaunchTarget(target));

            var found = Directory
                .EnumerateFiles(staging, exe, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (found is null)
                throw new InvalidOperationException($"В архиве нет {exe} — это не сборка приложения");

            return Path.GetDirectoryName(found)!;
        }

        // ===================== Запуск подменщика =====================

        private static void Launch(string script, string source, string target, string backup, string work)
        {
            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = work,
            };

            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-ExecutionPolicy");
            info.ArgumentList.Add("Bypass");
            info.ArgumentList.Add("-File");
            info.ArgumentList.Add(script);
            info.ArgumentList.Add("-ProcessId");
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            info.ArgumentList.Add("-Staging");
            info.ArgumentList.Add(source);
            info.ArgumentList.Add("-Target");
            info.ArgumentList.Add(target);
            info.ArgumentList.Add("-Backup");
            info.ArgumentList.Add(backup);
            info.ArgumentList.Add("-Launch");
            info.ArgumentList.Add(UpdateScript.LaunchTarget(target));

            Process.Start(info);
        }
    }
}
