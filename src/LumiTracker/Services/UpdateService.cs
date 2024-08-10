using LumiTracker.Config;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;
using Wpf.Ui.Controls;
using System.Windows.Data;
using System.Windows.Media;

#pragma warning disable CS8618

namespace LumiTracker.Services
{
    public enum EPackageType
    {
        Patch,
        Assets,
        Python,

        NumPackages
    }

    public class AttachMeta
    {
        // gitee & github
        public string name { get; set; } = ""; // [Notice] use .txt placeholder on gitee
        // not valid from gitee
        public long size { get; set; } = 0;
        // Not in the response body
        public bool need_update { get; set; } = false;
        public string package { get; set; } = "";
        public string md5 { get; set; } = "";
        public string zip_path { get; set; } = "";
    }

    public class ReleaseMeta
    {
        public string id { get; set; } = "";
        public string tag_name { get; set; } = "";
        public string body { get; set; } = "";
        public AttachMeta[] assets { get; set; } = [];
    }

    public enum EUpdateState
    {
        None,
        GetReleaseMeta,
        AlreadyLatest,
        ClearOldFiles,
        ProcessMetadata,
        Download,
        UnzipAndCopy,
        UpdateIniFile,
        ReadyToRestart,

        NumStages
    }

    public partial class UpdateContext : ObservableObject
    {
        // Settings UI
        [ObservableProperty]
        private LocalizationTextItem _promptText;

        [ObservableProperty]
        private Brush _promptColor;

        [ObservableProperty]
        private SymbolRegular _promptIcon;

        [ObservableProperty]
        private Visibility _promptShowLoading;

        [ObservableProperty]
        private Visibility _promptShowIcon;

        // Controls
        public EUpdateState State;

        public Task<ContentDialogResult>? ProgressDialogTask;

        public ReleaseMeta? ReleaseMeta;

        public Stopwatch? globalStopwatch;

        public long downloadedBytes;

        public long totalBytes;

        // Progress dialog UI
        [ObservableProperty]
        private string _progressText;

        [ObservableProperty]
        private bool _indeterminate;

        [ObservableProperty]
        private string _remainTime;

        [ObservableProperty]
        private string _downloadSpeed;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _downloadedSize;

        [ObservableProperty]
        private string _totalSize;

        [ObservableProperty]
        bool _readyToRestart;

        public UpdateContext()
        {
            // Settings UI
            PromptText         = new ();
            PromptColor        = Brushes.White;
            PromptIcon         = SymbolRegular.Question24;
            PromptShowLoading  = Visibility.Collapsed;
            PromptShowIcon     = Visibility.Collapsed;

            Reset();
            State = EUpdateState.None;
        }

        public void Reset()
        {
            // Controls
            State              = EUpdateState.GetReleaseMeta;
            ProgressDialogTask = null;
            ReleaseMeta        = null;
            globalStopwatch    = null;
            downloadedBytes    = 0;
            totalBytes         = 0;

            // Progress dialog UI
            ProgressText       = "";
            Indeterminate      = true;
            RemainTime         = "";
            DownloadSpeed      = "";
            Progress           = 0.0;
            DownloadedSize     = "0.00 MB";
            TotalSize          = "0.00 MB";
            ReadyToRestart     = false;
        }
    }

    public class UpdateService
    {
        public async Task TryUpdateAsync(UpdateContext ctx)
        {
            if (ctx.State != EUpdateState.None)
                return;

            await MainTask(ctx);
            ctx.State = EUpdateState.None;
        }

        private async Task MainTask(UpdateContext ctx)
        { 
            var binding = LocalizationExtension.Create("UpdatePrompt_Fetching");
            BindingOperations.SetBinding(ctx.PromptText, LocalizationTextItem.TextProperty, binding);
            ctx.PromptShowLoading = Visibility.Visible;
            ctx.PromptShowIcon    = Visibility.Collapsed;
            ctx.PromptColor = new SolidColorBrush(Colors.Gray);

            // Get latest version meta
            int update_retries = Configuration.Get<int>("update_retries");
            bool success = false;
            for (int i = 0; i < update_retries; i++)
            {
                Configuration.Logger.LogDebug($"[Update] Trying to update, retry count = {i}");
                try
                {
                    ctx.Reset();
                    success = await GetLatestVersionMeta(ctx);
                }
                catch (Exception ex)
                {
                    Configuration.Logger.LogError($"[Update] An error occurred while updating.\n{ex.ToString()}");
                }
                if (success) break;
            }
            if (!success)
            {
                goto failed;
            }
            else if (ctx.State == EUpdateState.AlreadyLatest)
            {
                binding = LocalizationExtension.Create("UpdatePrompt_AlreadyLatest");
                BindingOperations.SetBinding(ctx.PromptText, LocalizationTextItem.TextProperty, binding);
                ctx.PromptShowLoading = Visibility.Collapsed;
                ctx.PromptShowIcon = Visibility.Visible;
                ctx.PromptIcon     = SymbolRegular.Checkmark24;
                ctx.PromptColor    = new SolidColorBrush(Colors.Green);
                return;
            }
            else if (ctx.ProgressDialogTask == null)
            {
                BindingOperations.ClearBinding(ctx.PromptText, LocalizationTextItem.TextProperty);
                ctx.PromptText.Text   = "";
                ctx.PromptShowLoading = Visibility.Collapsed;
                ctx.PromptShowIcon    = Visibility.Collapsed;
                return;
            }

            // Download latest version
            success = false;
            for (int i = 0; i < update_retries; i++)
            {
                Configuration.Logger.LogDebug($"[Update] Trying to update, retry count = {i}");
                try
                {
                    success = await Update(ctx, retry: (i != 0));
                }
                catch (Exception ex)
                {
                    Configuration.Logger.LogError($"[Update] An error occurred while updating.\n{ex.ToString()}");
                }
                if (success) break;
            }

            if (!success)
            {
                goto failed;
            }
            else if (ctx.State == EUpdateState.ReadyToRestart)
            {
                // Restart
                Configuration.SetTemporal("restart", true);
                Application.Current.Shutdown();
                return;
            }

            // Should not reach here
            Configuration.Logger.LogError($"[Update] Unknown error. Should not reach here.");

        failed:
            binding = LocalizationExtension.Create("UpdatePrompt_Failed");
            BindingOperations.SetBinding(ctx.PromptText, LocalizationTextItem.TextProperty, binding);
            ctx.PromptShowLoading = Visibility.Collapsed;
            ctx.PromptShowIcon = Visibility.Visible;
            ctx.PromptIcon     = SymbolRegular.Dismiss24;
            ctx.PromptColor    = new SolidColorBrush(Colors.Red);

            ContentDialogService.ClearUpdateDialog();
        }

        private readonly string metaUrl = $"https://gitee.com/api/v5/repos/LumiOwO/LumiTracker/releases/latest";

        private readonly string packagesUrl = $"https://github.com/LumiOwO/LumiTracker/releases/download/Packages";

        private readonly HttpClient mainClient;

        private readonly HttpClient subClient;

        private readonly StyledContentDialogService ContentDialogService;

        private Task? progressUpdater = null;

        public UpdateService(StyledContentDialogService contentDialogService)
        {
            ContentDialogService = contentDialogService;

            mainClient = new HttpClient();
            mainClient.DefaultRequestHeaders.Add("User-Agent", "CSharpApp");
            mainClient.Timeout = TimeSpan.FromSeconds(300);

            subClient = new HttpClient();
            subClient.DefaultRequestHeaders.Add("User-Agent", "CSharpApp");
            subClient.Timeout = TimeSpan.FromSeconds(5);
        }

        private async Task<bool> GetLatestVersionMeta(UpdateContext ctx)
        {
            // Get the latest release info from gitee
            Configuration.Logger.LogDebug($"============= [Update] Update stage : {ctx.State.ToString()} =============");
            ctx.State = EUpdateState.GetReleaseMeta;

            var releaseMeta = Configuration.Get<ReleaseMeta>("releaseMeta");
            if (releaseMeta == null)
            {
                var response = await subClient.GetStringAsync(metaUrl);
                releaseMeta = JsonConvert.DeserializeObject<ReleaseMeta>(response);
                if (releaseMeta == null)
                {
                    Configuration.Logger.LogWarning("[Update] Invalid release meta from http response.");
                    return false;
                }
                Configuration.SetTemporal("releaseMeta", releaseMeta);
            }
            ctx.ReleaseMeta = releaseMeta;

            string latestVersion = releaseMeta.tag_name.TrimStart('v');
            if (UpdateUtils.VersionCompare(Configuration.Ini["Version"], latestVersion) >= 0)
            {
                Configuration.Logger.LogInformation($"[Update] Version {latestVersion} is already latest.");
                ctx.State = EUpdateState.AlreadyLatest;
                return true;
            }
            // Show update available dialog
            var task = await ContentDialogService.ShowUpdateDialogAsync(ctx);
            ctx.ProgressDialogTask = task;
            return true;
        }

        private async Task<bool> Update(UpdateContext ctx, bool retry) 
        {
            // Clear old files
            Configuration.Logger.LogDebug($"============= [Update] Update stage : {ctx.State.ToString()} =============");
            ctx.State = EUpdateState.ClearOldFiles;
            if (progressUpdater != null)
            {
                await progressUpdater;
            }
            ctx.ProgressText   = retry ? LocalizationSource.Instance["UpdatePrompt_Retrying"] : LocalizationSource.Instance["UpdatePrompt_Connecting"];
            ctx.Indeterminate  = true;
            ctx.RemainTime     = "";
            ctx.DownloadSpeed  = "";
            ctx.Progress       = 0.0;
            ctx.DownloadedSize = "0.00 MB";
            ctx.TotalSize      = "0.00 MB";
            UpdateUtils.CleanCacheAndOldFiles();

            // Process meta data of packages that need to download
            Configuration.Logger.LogDebug($"============= [Update] Update stage : {ctx.State.ToString()} =============");
            ctx.State = EUpdateState.ProcessMetadata;

            if (!Directory.Exists(Configuration.CacheDir))
            {
                Directory.CreateDirectory(Configuration.CacheDir);
            }
            string bestUrl = await GetBestDownloadUrl();

            long totalBytes = 0;
            List<AttachMeta> downloadMetas = [];
            foreach (var attachMeta in ctx.ReleaseMeta!.assets)
            {
                string[] fields = attachMeta.name.Split('-');
                if (fields[0] != "Package")
                {
                    // not a package
                    continue;
                }
                if (attachMeta.need_update)
                {
                    // already processed
                    downloadMetas.Add(attachMeta);
                    totalBytes += attachMeta.size;
                    continue;
                }

                int lastIdx = fields.Length - 1;
                fields[lastIdx] = fields[lastIdx].Substring(0, fields[lastIdx].Length - 4); // remove postfix (.txt)

                string package = fields[1];
                string md5     = fields[2];
                long   size    = long.Parse(fields[3]); 
                if (package == "Patch" || md5 != Configuration.Ini[package])
                {
                    attachMeta.name = $"Package-{package}-{md5}.zip";
                    attachMeta.size = size;
                    attachMeta.need_update = true;
                    attachMeta.package = package;
                    attachMeta.md5 = md5;
                    attachMeta.zip_path = Path.Combine(Configuration.CacheDir, attachMeta.name);

                    totalBytes += size;
                    downloadMetas.Add(attachMeta);
                }
            }

            // Download packages
            Configuration.Logger.LogDebug($"============= [Update] Update stage : {ctx.State.ToString()} =============");
            ctx.State = EUpdateState.Download;
            ctx.TotalSize = UpdateUtils.FormatBytes(totalBytes);

            string[] md5Hashs = new string[(int)EPackageType.NumPackages];
            for (EPackageType type = 0; type < EPackageType.NumPackages; type++)
            {
                md5Hashs[(int)type] = Configuration.Ini[type.ToString()];
            }

            ctx.Indeterminate   = false;
            ctx.ProgressText    = LocalizationSource.Instance["UpdatePrompt_Downloading"];
            ctx.globalStopwatch = new Stopwatch();
            ctx.downloadedBytes = 0;
            ctx.totalBytes      = totalBytes;
            progressUpdater = ProgressUpdater(ctx);

            ctx.globalStopwatch.Start();
            foreach (var meta in downloadMetas)
            {
                string url = $"{bestUrl}/{meta.name}";

                using (var downloadResponse = await mainClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    downloadResponse.EnsureSuccessStatusCode();

                    using (var contentStream = await downloadResponse.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(meta.zip_path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                    {
                        var buffer = new byte[8192];
                        Configuration.Logger.LogDebug($"[Update] Downloading {meta.package}: {UpdateUtils.FormatBytes(meta.size)}, {url}");

                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            ctx.downloadedBytes += bytesRead;
                        }
                    }
                }

                // Calculate the MD5 hash of the downloaded file asynchronously
                string md5Hash = await UpdateUtils.FileMD5Sum(meta.zip_path);

                // Check if the computed hash matches the expected hash
                if (!md5Hash.Equals(meta.md5, StringComparison.OrdinalIgnoreCase))
                {
                    Configuration.Logger.LogError($"[Update] Package {meta.package} downloaded failed. md5 hash {md5Hash} does not match the expected value {meta.md5}.");
                    return false;
                }

                Configuration.Logger.LogDebug($"[Update] Package {meta.package} downloaded, md5: {md5Hash}");
                EPackageType type = Enum.Parse<EPackageType>(meta.package);
                md5Hashs[(int)type] = md5Hash;
            }
            ctx.globalStopwatch.Stop();


            // Unzip packages & copy unchanged files
            Configuration.Logger.LogDebug($"============= [Update] Update stage : {ctx.State.ToString()} =============");
            ctx.State = EUpdateState.UnzipAndCopy;
            await progressUpdater;

            ctx.RemainTime     = "";
            ctx.DownloadSpeed  = "";
            ctx.Progress       = 1.0;
            ctx.DownloadedSize = ctx.TotalSize;
            ctx.Indeterminate  = true;
            ctx.ProgressText   = LocalizationSource.Instance["UpdatePrompt_Unpacking"];

            string latestVersion = ctx.ReleaseMeta.tag_name.TrimStart('v');
            string dstDir = Path.Combine(Configuration.RootDir, "LumiTrackerApp-" + latestVersion);
            foreach (var meta in downloadMetas)
            {
                string upzip_path = meta.package switch
                {
                    "Patch" => dstDir,
                    "Assets" => Path.Combine(dstDir, "assets"),
                    "Python" => Path.Combine(dstDir, "python"),
                    _ => "__INVALID__",
                };
                if (!Directory.Exists(upzip_path))
                {
                    Directory.CreateDirectory(upzip_path);
                }

                ZipFile.ExtractToDirectory(meta.zip_path, upzip_path);
                Configuration.Logger.LogInformation($"[Update] Extracted package {meta.package} to: {upzip_path}");
            }
            for (EPackageType type = 0; type < EPackageType.NumPackages; type++)
            {
                if (md5Hashs[(int)type] != Configuration.Ini[type.ToString()])
                {
                    // Already unzipped
                    continue;
                }
                if (type == EPackageType.Patch)
                {
                    // Impossible
                    Configuration.Logger.LogError("[Update] Unknown error: Package Patch should always be updated!");
                    continue;
                }

                string copySrc = Configuration.AppDir;
                string copyDst = dstDir;
                if (type == EPackageType.Assets)
                {
                    copySrc = Path.Combine(copySrc, "assets", "images");
                    copyDst = Path.Combine(copyDst, "assets", "images");
                }
                else if (type == EPackageType.Python)
                {
                    copySrc = Path.Combine(copySrc, "python");
                    copyDst = Path.Combine(copyDst, "python");
                }
                Configuration.Logger.LogInformation($"[Update] Copy from {copySrc} to {copyDst}");
                await UpdateUtils.CopyDirectoryAsync(copySrc, copyDst);
            }

            // Update ini file
            Configuration.Logger.LogDebug($"============= [Update] Update stage : {ctx.State.ToString()} =============");
            ctx.State = EUpdateState.UpdateIniFile;

            using (StreamWriter writer = new StreamWriter(Configuration.IniFilePath))
            {
                writer.WriteLine("[Application]");
                writer.WriteLine($"Version = {latestVersion}");
                writer.WriteLine($"Console = 0");
                for (EPackageType type = 0; type < EPackageType.NumPackages; type++)
                {
                    writer.WriteLine($"{type.ToString()} = {md5Hashs[(int)type]}");
                }
            }
            Configuration.Logger.LogDebug("[Update] .ini file updated");

            // Wait for user confirm, then restart
            Configuration.Logger.LogDebug("[Update] Ready to restart, waiting for confirm...");
            ctx.State = EUpdateState.ReadyToRestart;
            ctx.Indeterminate  = false;
            ctx.ProgressText   = LocalizationSource.Instance["UpdatePrompt_Complete"];
            ctx.ReadyToRestart = true;
            await ctx.ProgressDialogTask!;
            return true;
        }

        private async Task ProgressUpdater(UpdateContext ctx)
        {
            long prevDownloadedBytes = ctx.downloadedBytes;
            int  deltaTimeMS = 200; // ms
            while (ctx.State == EUpdateState.Download) 
            {
                await Task.Delay(deltaTimeMS);
                long downloadedBytes = ctx.downloadedBytes;
                long totalBytes = ctx.totalBytes;
                long deltaBytes = downloadedBytes - prevDownloadedBytes;

                double downloadSpeed = 1000.0 / deltaTimeMS * deltaBytes;
                var averageSpeed = downloadedBytes / (ctx.globalStopwatch!.Elapsed.TotalSeconds + 1e-5);
                var remainTime = (totalBytes - downloadedBytes) / averageSpeed;


                ctx.RemainTime = UpdateUtils.FormatRemainTime(remainTime);
                ctx.DownloadSpeed = UpdateUtils.FormatBytes(downloadSpeed) + "/s";
                ctx.Progress = (double)downloadedBytes / totalBytes;
                ctx.DownloadedSize = UpdateUtils.FormatBytes(downloadedBytes);

                //Configuration.Logger.LogDebug(
                //    $"[Update] {ctx.DownloadedSize} / {ctx.TotalSize} - Speed: {ctx.DownloadSpeed}");

                prevDownloadedBytes += deltaBytes;
            }
        }


        private readonly string[] ghPrefixs = [
            "https://github.moeyy.xyz/",
            "https://github.abskoop.workers.dev/",
            "https://gh-proxy.com/",
            "https://mirror.ghproxy.com/",
        ];

        private async Task<string> GetBestDownloadUrl()
        {
            double max_speed = 0.0;
            string bestPrefix = ghPrefixs[0];
            string url = $"{packagesUrl}/DownloadTest";

            var tasks = new Task<double>[ghPrefixs.Length];
            for (int i = 0; i < ghPrefixs.Length; i++)
            {
                tasks[i] = DownloadSpeedTest(ghPrefixs[i] + url);
            }
            var speeds = await Task.WhenAll(tasks);

            for (int i = 0; i < ghPrefixs.Length; i++)
            {
                if (speeds[i] > max_speed)
                {
                    max_speed = speeds[i];
                    bestPrefix = ghPrefixs[i];
                }
            }
            Configuration.Logger.LogInformation($"[Update] Best download speed {UpdateUtils.FormatBytes(max_speed)}/s with url prefix: {bestPrefix}");
            return bestPrefix + packagesUrl;
        }

        private async Task<double> DownloadSpeedTest(string url)
        {
            Configuration.Logger.LogDebug($"[Update] Download test: {url}");

            long bytesRead = 0;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource(subClient.Timeout);
                CancellationToken token = cts.Token;

                // Send a GET request
                HttpResponseMessage response = await subClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.IsSuccessStatusCode)
                {
                    // Read the response content as a stream
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        // Get the length of the content
                        long contentLength = response.Content.Headers.ContentLength ?? -1;

                        // Read the content in chunks
                        byte[] buffer = new byte[8192];
                        int bytes;
                        while ((bytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            bytesRead += bytes;
                            if (stopwatch.Elapsed.TotalSeconds > 2)
                            {
                                cts.Cancel();
                            }
                        }
                    }
                }
                else
                {
                    Configuration.Logger.LogDebug($"[Update] Failed to download from {url}. Status code: {response.StatusCode}");
                    bytesRead = 0;
                }
            }
            catch (OperationCanceledException)
            {
                Configuration.Logger.LogDebug($"[Update] Download from {url} timed out.");
            }
            catch (Exception e)
            {
                Configuration.Logger.LogWarning($"[Update] Download failed from {url}: {e.Message}");
            }
            stopwatch.Stop();

            double speed = bytesRead / stopwatch.Elapsed.TotalSeconds;
            Configuration.Logger.LogDebug($"[Update] Download speed = {UpdateUtils.FormatBytes(speed)}/s from {url}");
            return speed;
        }
    }

    public class UpdateUtils
    {
        public static string FormatBytes(double bytes)
        {
            const double KB = 1024;
            const double MB = KB * 1024;
            const double GB = MB * 1024;

            if (bytes >= GB)
            {
                return $"{bytes / GB:0.00} GB";
            }
            else if (bytes >= MB)
            {
                return $"{bytes / MB:0.00} MB";
            }
            else
            {
                return $"{bytes / KB:0.00} KB";
            }
        }

        public static string FormatRemainTime(double seconds)
        {
            // Define the maximum allowable seconds (99 minutes and 59 seconds)
            const double maxSeconds = 99 * 60 + 59;

            // Clamp the seconds to the maximum allowable value
            seconds = Math.Min(seconds, maxSeconds);

            // Calculate minutes and seconds
            int minutes = (int)seconds / 60;
            int secondsPart = (int)seconds % 60;

            // Format as "MM:SS" with leading zeros
            return $"{minutes:D2}:{secondsPart:D2}";
        }

        public static int VersionCompare(string v1, string v2)
        {
            // Remove the 'v' prefix and split the version numbers
            string[] parts1 = v1.Split('.');
            string[] parts2 = v2.Split('.');

            // Ensure both arrays have the same length
            int maxLength = Math.Max(parts1.Length, parts2.Length);

            // Compare each part of the version number
            for (int i = 0; i < maxLength; i++)
            {
                // Get the current part or 0 if the part is missing
                int num1 = i < parts1.Length ? int.Parse(parts1[i]) : 0;
                int num2 = i < parts2.Length ? int.Parse(parts2[i]) : 0;

                // Compare the current part
                int comparison = num1.CompareTo(num2);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            // Versions are equal if all parts are equal
            return 0;
        }

        public static async Task<string> FileMD5Sum(string path)
        {
            using (MD5 md5 = MD5.Create())
            using (FileStream fileStream = File.OpenRead(path))
            {
                byte[] hashBytes = await md5.ComputeHashAsync(fileStream);

                // Convert byte array to hex string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static async Task CopyDirectoryAsync(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }
            // Create the destination directory if it doesn't exist
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                await CopyFileAsync(file, destFile);
            }

            // Copy subdirectories
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destDir, subDirName);
                await CopyDirectoryAsync(subDir, destSubDir);
            }
        }

        public static async Task CopyFileAsync(string sourceFile, string destFile)
        {
            using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
            using (FileStream destinationStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }
        }

        public static void CleanCacheAndOldFiles()
        {
            if (Directory.Exists(Configuration.CacheDir))
            {
                Configuration.Logger.LogInformation($"Removing cache dir...");
                Directory.Delete(Configuration.CacheDir, recursive: true);
            }

            Configuration.Logger.LogInformation($"Removing old version files...");
            foreach (var directory in Directory.GetDirectories(Configuration.RootDir))
            {
                if (Path.GetFullPath(directory) == Path.GetFullPath(Configuration.AppDir))
                    continue;
                string dirName = Path.GetFileName(directory);
                if (!dirName.StartsWith("LumiTrackerApp-"))
                    continue;

                Configuration.Logger.LogInformation($"Removing {dirName}...");
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
