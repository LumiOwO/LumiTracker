using LumiTracker.Config;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Security.Policy;
using System.Threading;

namespace LumiTracker.Services
{
    enum EPackageType
    {
        Patch,
        Assets,
        Python,

        NumPackages
    }

    class AttachMeta
    {
        // gitee & github
        public string name { get; set; } = ""; // [Notice] use .txt placeholder on gitee
        public string browser_download_url { get; set; } = "";
        // Not in the response body
        public bool need_update { get; set; } = false;
        public string package { get; set; } = "";
        public string md5 { get; set; } = "";
        public string zip_path { get; set; } = "";
    }

    class ReleaseMeta
    {
        public string id { get; set; } = "";
        public string tag_name { get; set; } = "";
        public string body { get; set; } = "";
        public AttachMeta[] assets { get; set; } = [];
    }
    
    public class UpdateService
    {
        public async Task TryUpdateAsync()
        {
            try
            {
                await MainTask();
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[Update] An error occurred while updating.\n{ex.ToString()}");
            }
        }

        private readonly string metaUrl = $"https://gitee.com/api/v5/repos/LumiOwO/LumiTracker/releases/latest";

        private readonly string packagesUrl = $"https://github.com/LumiOwO/LumiTracker/releases/download/Packages";

        private readonly HttpClient mainClient;
        private readonly HttpClient downloadTestClient;

        public UpdateService()
        {
            mainClient = new HttpClient();
            mainClient.DefaultRequestHeaders.Add("User-Agent", "CSharpApp");
            mainClient.Timeout = TimeSpan.FromSeconds(300);

            downloadTestClient = new HttpClient();
            downloadTestClient.DefaultRequestHeaders.Add("User-Agent", "CSharpApp");
            downloadTestClient.Timeout = TimeSpan.FromSeconds(5);
        }

        private async Task MainTask()
        {
            // Step 1: Get the latest release info from gitee
            Configuration.Logger.LogDebug("============= [Update] Step 1 =============");

            var response = await mainClient.GetStringAsync(metaUrl);
            var releaseMeta = JsonConvert.DeserializeObject<ReleaseMeta>(response);
            if (releaseMeta == null)
            {
                Configuration.Logger.LogWarning("[Update] Invalid release meta from http response.");
                return;
            }
            string latestVersion = releaseMeta.tag_name.TrimStart('v');
            if (VersionCompare(Configuration.Ini["Version"], latestVersion) >= 0)
            {
                Configuration.Logger.LogInformation($"[Update] Version {latestVersion} is already latest.");
                return;
            }

            // Step 2: Process meta data of packages that need to download
            Configuration.Logger.LogDebug("============= [Update] Step 2 =============");

            if (!Directory.Exists(Configuration.CacheDir))
            {
                Directory.CreateDirectory(Configuration.CacheDir);
            }
            string bestUrl = await GetBestDownloadUrl();

            List<AttachMeta> downloadMetas = [];
            foreach (var attachMeta in releaseMeta.assets)
            {
                string[] fields = attachMeta.name.Split('-');
                if (fields.Length != 3 && fields[0] != "Package")
                    continue;

                string package = fields[1];
                string md5 = fields[2].Substring(0, fields[2].Length - 4); // remove postfix (.zip or .txt)
                if (package == "Patch" || md5 != Configuration.Ini[package])
                {
                    attachMeta.name = $"Package-{package}-{md5}.zip";
                    attachMeta.browser_download_url = $"{bestUrl}/{attachMeta.name}";
                    attachMeta.need_update = true;
                    attachMeta.package = package;
                    attachMeta.md5 = md5;
                    attachMeta.zip_path = Path.Combine(Configuration.CacheDir, attachMeta.name);

                    downloadMetas.Add(attachMeta);
                }
            }

            // Step 3: Download packages
            Configuration.Logger.LogDebug("============= [Update] Step 3 =============");

            string[] md5Hashs = new string[(int)EPackageType.NumPackages];
            for (EPackageType type = 0; type < EPackageType.NumPackages; type++)
            {
                md5Hashs[(int)type] = Configuration.Ini[type.ToString()];
            }


            foreach (var meta in downloadMetas)
            {
                string url = meta.browser_download_url;

                using (var downloadResponse = await mainClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    downloadResponse.EnsureSuccessStatusCode();

                    using (var contentStream = await downloadResponse.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(meta.zip_path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                    {
                        var buffer = new byte[8192];
                        var totalBytesRead = 0L;
                        var totalBytes = downloadResponse.Content.Headers.ContentLength ?? -1;
                        Configuration.Logger.LogDebug($"[Update] Downloading {meta.package}: {FormatBytes(totalBytes)}, {url}");

                        var stopwatch = Stopwatch.StartNew();
                        int bytesRead;
                        int logBytes = 0;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            logBytes += bytesRead;
                            if (stopwatch.Elapsed.TotalSeconds >= 1)
                            {
                                // Calculate progress
                                var progress = (double)totalBytesRead / totalBytes * 100;

                                // Calculate download speed
                                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                var downloadSpeed = logBytes / (elapsedSeconds + 1e-5);

                                // Print progress and speed
                                Configuration.Logger.LogDebug($"[Update] Downloaded {totalBytesRead} of {totalBytes} bytes ({progress:0.00}%) - Speed: {FormatBytes(downloadSpeed)}/s");

                                logBytes = 0;
                                stopwatch.Restart();
                            }
                        }
                    }
                }

                // Calculate the MD5 hash of the downloaded file asynchronously
                string md5Hash = await FileMD5Sum(meta.zip_path);

                // Check if the computed hash matches the expected hash
                if (!md5Hash.Equals(meta.md5, StringComparison.OrdinalIgnoreCase))
                {
                    Configuration.Logger.LogError($"[Update] Package {meta.package} downloaded failed. md5 hash {md5Hash} does not match the expected value {meta.md5}.");
                    return;
                }

                Configuration.Logger.LogDebug($"[Update] Package {meta.package} downloaded, md5: {md5Hash}");
                EPackageType type = Enum.Parse<EPackageType>(meta.package);
                md5Hashs[(int)type] = md5Hash;
            }

            // Step 4: Unzip packages
            Configuration.Logger.LogDebug("============= [Update] Step 4 =============");

            string dstDir = Path.Combine(Configuration.RootDir, "LumiTrackerApp-" + latestVersion);
            foreach (var meta in downloadMetas)
            {
                string upzip_path = meta.package switch
                {
                    "Patch"  => dstDir,
                    "Assets" => Path.Combine(dstDir, "assets"),
                    "Python" => Path.Combine(dstDir, "python"),
                    _ => "__INVALID__",
                };
                if (!Directory.Exists(upzip_path))
                {
                    Directory.CreateDirectory(upzip_path);
                }

                ZipFile.ExtractToDirectory(meta.zip_path, upzip_path);
                Configuration.Logger.LogDebug($"[Update] Extracted package {meta.package} to: {upzip_path}");
            }

            // Step 5: Update ini file
            Configuration.Logger.LogDebug("============= [Update] Step 5 =============");

            using (StreamWriter writer = new StreamWriter(Configuration.IniFilePath))
            {
                writer.WriteLine("[Application]");
                writer.WriteLine($"Version = {latestVersion}");
                for (EPackageType type = 0; type < EPackageType.NumPackages; type++)
                {
                    writer.WriteLine($"{type.ToString()} = {md5Hashs[(int)type]}");
                }
            }

            // Step 6: Restart
            Configuration.Logger.LogDebug("============= [Update] Step 6 =============");

            string launcherPath = Path.Combine(Configuration.RootDir, "LumiTracker.exe");
            Configuration.Logger.LogDebug(launcherPath);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName        = launcherPath,
                Arguments       = Configuration.RootDir + "\\",
                UseShellExecute = false,  // Required to set CreateNoWindow to true
                CreateNoWindow  = true,   // Hides the console window
            };

            var process = new Process();
            process.StartInfo = startInfo;
            if (!process.Start())
            {
                Configuration.Logger.LogError("[Update] Failed to Restart app.");
                return;
            }
            Application.Current.Shutdown();
        }


        private readonly string[] ghPrefixs = [
            "https://mirror.ghproxy.com/",
            "https://github.abskoop.workers.dev/",
            "https://gh-proxy.com/",
            "https://github.moeyy.xyz/",
        ];

        private async Task<string> GetBestDownloadUrl()
        {
            double max_speed = 0.0;
            string bestPrefix = "";
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
                    max_speed  = speeds[i];
                    bestPrefix = ghPrefixs[i];
                }
            }
            Configuration.Logger.LogInformation($"[Update] Best download speed {FormatBytes(max_speed)}/s with url prefix: {bestPrefix}");
            return bestPrefix + packagesUrl;
        }

        private async Task<double> DownloadSpeedTest(string url)
        {
            Configuration.Logger.LogDebug($"[Update] Download test: {url}");

            long bytesRead = 0;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Send a GET request
                HttpResponseMessage response = await downloadTestClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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
                        }
                    }
                }
                else
                {
                    Configuration.Logger.LogDebug($"[Update] Failed to download from {url}. Status code: {response.StatusCode}");
                    bytesRead = 0;
                }
            }
            catch (TaskCanceledException)
            {
                Configuration.Logger.LogDebug($"[Update] Download from {url} timed out.");
            }
            stopwatch.Stop();

            double speed = bytesRead / stopwatch.Elapsed.TotalSeconds;
            Configuration.Logger.LogDebug($"[Update] Download speed = {FormatBytes(speed)}/s from {url}");
            return speed;
        }

        private string FormatBytes(double bytes)
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

        private int VersionCompare(string v1, string v2)
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

        private async Task<string> FileMD5Sum(string path)
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
    }
}
