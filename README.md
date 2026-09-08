# Disk Space Analyzer

A comprehensive .NET 10 console application for analyzing and reporting disk space usage across one or multiple drives with detailed folder breakdowns.

## Features

✨ **Key Capabilities:**
- **Multi-Drive Analysis** - Scan one, multiple, or all available drives simultaneously
- **Parallel Processing** - Fast analysis using concurrent folder and file processing
- **Customizable Depth Control** - Set maximum scan depth (unlimited or limited to specific levels)
- **Minimum Size Filtering** - Filter folders by minimum size threshold in MB
- **Color-Coded Output** - Visual indicators for disk usage levels (green, yellow, red)
- **Top 100 Folders** - Identify the largest folders on each drive
- **Comprehensive Statistics** - Total files, folders, scan time, and usage percentages
- **Access Control Handling** - Graceful handling of restricted directories requiring admin access

## System Requirements

- **.NET 10** (or .NET 9+)
- **Windows OS** (for DriveInfo and folder analysis)
- **Administrator Access** (recommended for full drive scanning)

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run
```

Or run the compiled executable from the `bin/Debug/net10.0/` directory.

## How to Use

### Step 1: Review Available Drives
The application displays all available drives with:
- Total size (GB)
- Used space (GB)
- Free space (GB)
- Usage percentage

### Step 2: Select Drives
Enter the drives you want to scan:
- Individual drives: `C:, D:` (comma-separated)
- All drives: `all`

### Step 3: Configure Scan Parameters

**Maximum Depth:**
- `0` = Unlimited (scans all subfolders)
- `1-N` = Scans up to N levels deep (default: 5)

**Minimum Folder Size:**
- Enter size in MB (default: 100)
- Only folders meeting this threshold are reported

### Step 4: Review Results

The application displays:
- **Summary** - Total drives, scan time, folders analyzed
- **Per-Drive Analysis** - Total/used/free space with percentage
- **Top 100 Folders** - Largest folders ranked by size
- **Statistics** - File and folder counts, scan duration

## Project Structure

```
DiskMonitoring.csproj      # Project configuration
Program.cs                 # Main application logic
├── Models                 # FolderSizeInfo, DriveScanResult
├── Main Program           # User interaction and orchestration
├── Scanning Logic         # Drive and folder analysis
└── Display Methods        # Console output formatting
```

## Models

### FolderSizeInfo
Represents folder metadata:
- `Path` - Full folder path
- `SizeBytes` - Total size in bytes
- `SizeMB` / `SizeGB` - Formatted sizes
- `FileCount` - Number of files in folder
- `FolderCount` - Number of subfolders
- `SubFolders` - List of child folders
- `PercentageOfParent` - Relative size

### DriveScanResult
Contains drive scan results:
- `DriveName` - Drive letter (e.g., C:)
- `TotalSize` / `UsedSize` / `FreeSize` - Drive space info
- `UsedPercentage` - Usage percentage
- `TopFolders` - List of largest folders
- `TotalFolderScanned` / `TotalFilesScanned` - Counts
- `ScanDuration` - Time taken for scan
- `ExcludedPaths` - Paths that couldn't be scanned

## Excluded Folders

The application automatically excludes system-critical folders from scanning:
- `Windows`, `System32`, `SysWOW64`
- `Program Files`, `Program Files (x86)`
- `$Recycle.Bin`, `Boot`, `Recovery`

And common temporary/cache folders:
- `Cache`, `Temp`, `Temporary Internet Files`
- `Logs`, `Backup`, `Archive`

## Performance

- **Parallel File Processing** - Files within a folder are processed concurrently
- **Concurrent Drive Scanning** - Multiple drives scanned simultaneously
- **Typical Scan Time** - Varies by drive size and file count; displayed in results

## Troubleshooting

### "Access denied" Error
**Solution:** Run the application as Administrator
```bash
Run as administrator → dotnet run
```

### Incomplete Results
**Reason:** Some folders require elevated permissions
**Solution:** Execute with administrator privileges

### Very Slow Scan
**Tip:** Increase minimum size threshold to skip small folders
**Tip:** Reduce maximum depth to limit recursion

## Example Usage Session

```
🔎 Enter drives to scan (comma-separated, e.g., C:, D: or 'all' for all): C:
📏 Enter maximum depth to scan (0 = unlimited, default 5): 0
📁 Enter minimum folder size to report in MB (default 100): 500
```

## Technical Notes

- Uses `System.IO.DriveInfo` for drive information
- Implements `ConcurrentBag<T>` for thread-safe file aggregation
- Uses `Task.WhenAll()` for efficient recursive folder processing
- Console colors provide visual feedback on space utilization
- Handles `UnauthorizedAccessException` gracefully

## Future Enhancements

- Export results to JSON/CSV format
- Real-time progress indicators
- Filtering by file type
- Scheduled scanning
- Historical comparison

## License

This project is provided as-is for disk monitoring and analysis purposes.
