// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiskMonitoring
{
    #region Models

    public class FolderSizeInfo
    {
        public string Path { get; set; } = "";
        public long SizeBytes { get; set; }
        public double SizeMB => Math.Round(SizeBytes / 1048576.0, 2);
        public double SizeGB => Math.Round(SizeBytes / 1073741824.0, 2);
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public DateTime ScanTime { get; set; }
        public List<FolderSizeInfo> SubFolders { get; set; } = new List<FolderSizeInfo>();
        public string SizeDisplay => $"{SizeGB} GB {SizeMB} MB";

        // For percentage of parent
        public double PercentageOfParent { get; set; }
    }

    public class DriveScanResult
    {
        public string DriveName { get; set; } = "";
        public long TotalSize { get; set; }
        public long UsedSize { get; set; }
        public long FreeSize { get; set; }
        public double UsedPercentage { get; set; }
        public List<FolderSizeInfo> TopFolders { get; set; } = new List<FolderSizeInfo>();
        public int TotalFolderScanned { get; set; }
        public int TotalFilesScanned { get; set; }
        public TimeSpan ScanDuration { get; set; }
        public DateTime ScanTime { get; set; }
        public List<string> ExcludedPaths { get; set; } = new List<string>();
    }

    #endregion

    #region Main Program

    class Program
    {
        private static readonly string[] ExcludedSystemFolders = new[]
        {
            "$Recycle.Bin",
            "System Volume Information",
            "Windows",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "Boot",
            "Documents and Settings",
            "PerfLogs",
            "Recovery",
            "System32",
            "SysWOW64",
        };

        private static readonly string[] ExcludedFolders = new[]
        {
            "Cache",
            "Temp",
            "Temporary Internet Files",
            "Logs",
            "Backup",
            "Archive",
        };

        static async Task Main(string[] args)
        {
            Console.Title = "Disk Space Analyzer - .NET 9";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔==============================================================╗");
            Console.WriteLine("║            DISK SPACE ANALYZER - DETAILED REPORT             ║");
            Console.WriteLine("║                   .Net 10 - Enterprise Edition               ║ ");
            Console.WriteLine("╚==============================================================╝");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                // Get All available drives
                var drives = DriveInfo
                    .GetDrives()
                    .Where(d => d.IsReady && d.TotalSize > 0)
                    .Select(d => new
                    {
                        Drive = d,
                        Name = d.Name.TrimEnd('\\'),
                        TotalGB = Math.Round(d.TotalSize / 1073741824.0, 2),
                        FreeGB = Math.Round(d.AvailableFreeSpace / 1073741824.0, 2),
                        UsedGB = Math.Round((d.TotalSize - d.AvailableFreeSpace) / 1073741824.0, 2),
                    })
                    .OrderByDescending(d => d.UsedGB)
                    .ToList();

                //Display drives
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" AVAILABLE DRIVES:");
                Console.ResetColor();
                Console.WriteLine(
                    "┌─────────┬─────────────┬─────────────┬─────────────┬───────────┐"
                );
                Console.WriteLine(
                    "│ Drive   │ Total (GB)  │ Used (GB)   │ Free (GB)   │ Used %    │"
                );
                Console.WriteLine(
                    "├─────────┼─────────────┼─────────────┼─────────────┼───────────┤"
                );

                foreach (var drive in drives)
                {
                    var percentUsed = drive.UsedGB / drive.TotalGB * 100;
                    ConsoleColor color =
                        percentUsed > 85 ? ConsoleColor.Red
                        : percentUsed > 70 ? ConsoleColor.Yellow
                        : ConsoleColor.Green;
                    Console.ForegroundColor = color;
                    Console.WriteLine(
                        $"│ {drive.Name, -7} │ {drive.TotalGB, 11:F2} │ {drive.UsedGB, 11:F2} │ {drive.FreeGB, 11:F2} │ {percentUsed, 8:F1}% │"
                    );
                    Console.ResetColor();
                }

                Console.WriteLine(
                    "└─────────┴─────────────┴─────────────┴─────────────┴───────────┘"
                );
                Console.WriteLine();

                // Ask which drives to scan
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(
                    "🔎 Enter drives to scan (comma-separeted, e.g., C:, D: or 'all' for all: "
                );
                Console.ResetColor();

                string input = Console.ReadLine()?.Trim() ?? "all";
                List<string> selectedDrives;

                if (input.ToLower() == "all")
                {
                    selectedDrives = drives.Select(d => d.Name).ToList();
                }
                else
                {
                    selectedDrives = input
                        .Split(',')
                        .Select(s => s.Trim().TrimEnd('\\').ToUpper())
                        .Where(s => drives.Any(d => d.Name == s))
                        .ToList();
                }

                if (!selectedDrives.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ No valid drives selected. Exiting...");
                    Console.ResetColor();
                    return;
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("📏 Enter maximun depth to scan (0 = unlimited, default 5): ");
                Console.ResetColor();
                int maxDepth = int.TryParse(Console.ReadLine(), out int depth) ? depth : 5;
                maxDepth = maxDepth < 0 ? 0 : maxDepth;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("📁 Enter minimum folder size to report in MB (default 100): ");
                Console.ResetColor();
                int minSizeMB = int.TryParse(Console.ReadLine(), out int size) ? size : 100;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Starting scan for drives: {string.Join(", ", selectedDrives)}");
                Console.WriteLine(
                    $"Max depth: {(maxDepth == 0 ? "Unlimited" : maxDepth.ToString())}"
                );
                Console.WriteLine($"Minimum size to report: {minSizeMB} MB");
                Console.ResetColor();
                Console.WriteLine();

                var stopwatch = Stopwatch.StartNew();
                var allResults = new List<DriveScanResult>();

                // Scan each drive in parallel
                await Parallel.ForEachAsync(
                    selectedDrives,
                    async (driveName, token) =>
                    {
                        var result = await ScanDriveAsync(driveName, maxDepth, minSizeMB);
                        lock (allResults)
                        {
                            allResults.Add(result);
                        }
                    }
                );

                stopwatch.Stop();

                // Display results
                DisplayResults(allResults, stopwatch.Elapsed);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n Scan completed succesfully");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nPress any key to exit...");
                Console.ResetColor();
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.ResetColor();
                Console.ReadKey();
            }
        }

        #region Scanning Logic

        private static async Task<DriveScanResult> ScanDriveAsync(
            string drivePath,
            int maxDepth,
            int minSizeMB
        )
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n📂 Scanning drive {drivePath}...");
            Console.ResetColor();

            var result = new DriveScanResult
            {
                DriveName = drivePath,
                ScanTime = DateTime.Now,
                ExcludedPaths = [],
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate drive exists and is ready
                var driveInfo = new DriveInfo(drivePath);

                if (!driveInfo.IsReady)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"⚠️ Drive {drivePath} is not ready");
                    Console.ResetColor();
                    result.ExcludedPaths.Add("Drive not ready");
                    return result;
                }

                // Check if we can access the drive
                try
                {
                    var testDir = Directory.GetDirectories(drivePath).FirstOrDefault();
                }
                catch (UnauthorizedAccessException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"⚠️ Access denied to drive {drivePath}. Run as Administrator."
                    );
                    Console.ResetColor();
                    result.ExcludedPaths.Add("Access denied - Run as Administrator");
                    return result;
                }
                result.TotalSize = driveInfo.TotalSize;
                result.FreeSize = driveInfo.AvailableFreeSpace;
                result.UsedSize = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                result.UsedPercentage = Math.Round(
                    (result.UsedSize / (double)result.TotalSize) * 100,
                    2
                );

                // Scan folders - pass the normalized path
                string normalizedPath = drivePath.EndsWith("\\") ? drivePath : drivePath + "\\";
                var rootInfo = await GetFolderSizeAsync(normalizedPath, maxDepth, 0, minSizeMB);

                if (rootInfo != null)
                {
                    // Get folders and sort
                    var allFolders = GetAllFolders(rootInfo);

                    result.TopFolders = allFolders
                        .OrderByDescending(f => f.SizeBytes)
                        .Take(100)
                        .ToList();

                    result.TotalFolderScanned = allFolders.Count;
                    result.TotalFilesScanned = rootInfo.FileCount;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"⚠️ No folders found on drive {drivePath}");
                    Console.ResetColor();
                }

                stopwatch.Stop();
                result.ScanDuration = stopwatch.Elapsed;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    $"✅ Drive {drivePath} scanned: {result.TotalFolderScanned} folders, {result.TotalFilesScanned} files in {result.ScanDuration.TotalSeconds:F1} seconds"
                );
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"⚠  Error scanning drive {drivePath}: {ex.Message}");
                Console.ResetColor();
                result.ExcludedPaths.Add($"Error:; {ex.Message}");
            }

            return result;
        }

        private static async Task<FolderSizeInfo?> GetFolderSizeAsync(
            string path,
            int maxDepth,
            int currentDepth,
            int minSizeMB
        )
        {
            try
            {
                // Normalize the path to ensure it ends backslash for root drives
                string normalizedPath = path;
                if (!normalizedPath.EndsWith("\\"))
                {
                    normalizedPath += "\\";
                }

                // Check if directory exists
                if (!Directory.Exists(normalizedPath))
                {
                    return null;
                }

                // Get folder name - handle root drives specially
                var folderName = Path.GetFileName(normalizedPath.TrimEnd('\\'));

                // For root drives (C:\, D:\), Path.GetFileName returns empty string
                // So we use the drive name instead
                if (string.IsNullOrEmpty(folderName))
                {
                    // It's a root drive
                    var driveInfo = new DriveInfo(normalizedPath);
                    folderName = driveInfo.Name.TrimEnd('\\');
                }

                // Skip system folders (but allow root drives
                if (
                    !string.IsNullOrEmpty(folderName)
                    && currentDepth > 0
                    && // Only skip at subfolder level, not root
                    folderName.Contains(@"C:\\Users")
                    && (
                        ExcludedSystemFolders.Contains(folderName)
                        || ExcludedFolders.Contains(folderName)
                    )
                )
                {
                    return null;
                }

                var folderInfo = new FolderSizeInfo
                {
                    Path = normalizedPath,
                    ScanTime = DateTime.Now,
                };

                long totalSize = 0;
                int fileCount = 0;
                int folderCount = 0;

                // Process files
                try
                {
                    var files = Directory.GetFiles(normalizedPath);
                    fileCount = files.Length;

                    // Use Parallel.ForEach for faster file processing
                    var fileSizes = new ConcurrentBag<long>();

                    Parallel.ForEach(
                        files,
                        file =>
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                fileSizes.Add(fileInfo.Length);
                            }
                            catch
                            {
                                // Skip files can't access
                            }
                        }
                    );

                    totalSize = fileSizes.Sum();
                }
                catch (UnauthorizedAccessException)
                {
                    // If we can't access root, return null
                    if (currentDepth > 0)
                    {
                        return null;
                    }
                    return null;
                }
                catch
                {
                    // Skip directories we can't access
                    return null;
                }

                folderInfo.SizeBytes = totalSize;
                folderInfo.FileCount = fileCount;
                folderInfo.FolderCount = 0;

                // Process subfolders if within depth limit
                if (maxDepth == 0 || currentDepth < maxDepth)
                {
                    try
                    {
                        var subFolders = Directory.GetDirectories(normalizedPath);
                        folderCount = subFolders.Length;
                        var tasks = subFolders.Select(subFolder =>
                            GetFolderSizeAsync(subFolder, maxDepth, currentDepth + 1, minSizeMB)
                        );

                        var results = await Task.WhenAll(tasks);
                        var validResults = results.Where(r => r != null).ToList();

                        // Add subfolder sizes
                        foreach (var subFolder in validResults)
                        {
                            totalSize += subFolder?.SizeBytes ?? 0;
                            if (subFolder is not null)
                            {
                                folderInfo.SubFolders.Add(subFolder);
                            }
                        }

                        // Update counts
                        folderInfo.FileCount += validResults.Sum(f => f?.FileCount ?? 0);
                        folderInfo.FolderCount +=
                            validResults.Count + validResults.Sum(f => f?.FolderCount ?? 0);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip subfolders we can't access
                    }
                }

                folderInfo.SizeBytes = totalSize;

                // For root drives, always include them even if below minimun size
                if (currentDepth == 0)
                {
                    return folderInfo;
                }

                // Filter by minimun size for subfolders
                if (totalSize < (long)minSizeMB * 1048576)
                {
                    // Only return if it has Large subfolders
                    if (!folderInfo.SubFolders.Any(f => f.SizeBytes >= (long)minSizeMB * 1048576))
                    {
                        return null;
                    }
                }

                return folderInfo;
            }
            catch // (Exception ex)
            {
                // Log error for debugging if needed
                // Console.WriteLine($"Error scanning {path}: {ex.Message}");
                return null;
            }
        }

        private static List<FolderSizeInfo> GetAllFolders(FolderSizeInfo root)
        {
            var result = new List<FolderSizeInfo>();
            var queue = new Queue<FolderSizeInfo>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);

                foreach (var sub in current.SubFolders)
                {
                    queue.Enqueue(sub);
                }
            }
            return result;
        }

        #endregion

        #region Display Methods

        private static void DisplayResults(List<DriveScanResult> results, TimeSpan totalTime)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔==============================================================╗");
            Console.WriteLine("║                    SCAN RESULTS SUMMARY                      ║");
            Console.WriteLine("╚==============================================================╝");
            Console.ResetColor();
            Console.WriteLine();

            // Summary
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("📊 SUMMARY");
            Console.ResetColor();
            Console.WriteLine($" • Total drives scanned: {results.Count}");
            Console.WriteLine($" • Total scan time: {totalTime.TotalSeconds:F1} seconds");
            Console.WriteLine(
                $" • Total folders analyzed: {results.Sum(r => r.TotalFolderScanned):N0}"
            );
            Console.WriteLine();

            foreach (var result in results.OrderByDescending(r => r.UsedPercentage))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n📁 DRIVE: {result.DriveName}");
                Console.ResetColor();
                Console.WriteLine($" • Total Space: {FormatSize(result.TotalSize)}");
                Console.WriteLine(
                    $" • Used Space: {FormatSize(result.UsedSize)} ({result.UsedPercentage:F1}%)"
                );
                Console.WriteLine($" • Free Space: {FormatSize(result.FreeSize)}");
                Console.WriteLine(
                    $" • Scan Duration: {result.ScanDuration.TotalSeconds:F1} seconds"
                );
                Console.WriteLine($" • Folders Found: {result.TotalFolderScanned:N0}");
                Console.WriteLine($" • Files Found: {result.TotalFilesScanned:N0}");
                Console.WriteLine();

                if (result.TopFolders.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("📁 TOP 100 LARGEST FOLDERS:");
                    Console.ResetColor();
                    Console.WriteLine(
                        "  ┌───────────────────────────────────────────────────────────────┬──────────────┬──────────────┐"
                    );
                    Console.WriteLine(
                        "  │ Folder Path                                                   │ Size         │ % of Drive   │"
                    );
                    Console.WriteLine(
                        "  ├───────────────────────────────────────────────────────────────┼──────────────┼──────────────┤"
                    );

                    int rank = 1;
                    foreach (var folder in result.TopFolders.Take(20))
                    {
                        string path =
                            folder.Path.Length > 60
                                ? "..." + folder.Path.Substring(folder.Path.Length - 57)
                                : folder.Path;
                        double percentOfDrive = (folder.SizeBytes / (double)result.UsedSize) * 100;

                        ConsoleColor color =
                            percentOfDrive > 20 ? ConsoleColor.Red
                            : percentOfDrive > 10 ? ConsoleColor.Yellow
                            : ConsoleColor.Green;

                        Console.ForegroundColor = color;
                        Console.WriteLine(
                            $"  | {rank, 2}. {path, -60} | {FormatSize(folder.SizeBytes), 12} | {percentOfDrive, 8:F1}%    |"
                        );
                        Console.ResetColor();
                        rank++;
                    }
                    Console.WriteLine(
                        "  └───────────────────────────────────────────────────────────────┴──────────────┴──────────────┘"
                    );
                    Console.WriteLine();
                }
            }

            // Recomendations
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n💡 RECOMMENDATIONS: ");
            Console.ResetColor();

            foreach (var result in results.OrderByDescending(r => r.UsedPercentage))
            {
                if (result.UsedPercentage > 90)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"  ⚠️  CRITICAL: Drive {result.DriveName} is {result.UsedPercentage:F1}% full!"
                    );
                    Console.WriteLine($"    • Free space: {FormatSize(result.FreeSize)}");

                    // Find Largest folder to suggest cleanup
                    var largest = result.TopFolders.FirstOrDefault();
                    if (largest != null)
                    {
                        Console.WriteLine(
                            $"    • Largest folder: {largest.Path} ({FormatSize(largest.SizeBytes)})"
                        );
                        Console.WriteLine(
                            $"    • Consider cleaning up this folder or moving daa to another drive."
                        );
                    }
                    Console.ResetColor();
                }
                else if (result.UsedPercentage > 80)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"  ⚠️  Warning: Drive {result.DriveName} is {result.UsedPercentage:F1}% full!"
                    );
                    Console.WriteLine($"    • Free space: {FormatSize(result.FreeSize)}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        $" All good!: Drive {result.DriveName} is {result.UsedPercentage:F1}%"
                    );
                    Console.WriteLine($"    • Free space: {FormatSize(result.FreeSize)}");
                }
            }

            Console.WriteLine();
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:F2} {sizes[order]}";

        #endregion
        }
    #endregion
    }
}
