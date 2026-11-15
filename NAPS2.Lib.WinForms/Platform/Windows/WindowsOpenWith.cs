using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

namespace NAPS2.Platform.Windows;

public class WindowsOpenWith : IOpenWith
{
    private static readonly Guid BHID_DataObject = new("b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4");

    private readonly ImageContext _imageContext;

    public WindowsOpenWith(ImageContext imageContext)
    {
        _imageContext = imageContext;
    }

    public IEnumerable<OpenWithEntry> GetEntries(string fileExt)
    {
        foreach (var handler in EnumerateHandlers(fileExt))
        {
            handler.GetUIName(out var uiName);
            handler.GetName(out var internalName);
            handler.GetIconLocation(out var iconPath, out var iconIndex);
            yield return new OpenWithEntry(internalName, uiName, iconPath, iconIndex);
        }
    }

    public void OpenWith(string entryId, IEnumerable<string> filePaths)
    {
        // An explicit executable is independent of Windows' file associations. This also works for
        // the portable NAPS2 package and for editors that aren't listed as recommended JPG handlers.
        if (entryId.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
        {
            string executable = ResolveExecutablePath(entryId.Substring(4));
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("Selected image editor was not found", executable);
            }
            foreach (var path in filePaths)
            {
                var startInfo = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(executable)!
                };
                startInfo.ArgumentList.Add(path);
                Process.Start(startInfo);
            }
            return;
        }

        var pidls = new List<IntPtr>();
        foreach (var path in filePaths)
        {
            int hr = Win32.SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
            if (hr != 0) throw new Win32Exception(hr);
            pidls.Add(pidl);
        }
        try
        {
            int hr = Win32.SHCreateShellItemArrayFromIDLists(pidls.Count, pidls.ToArray(), out var shellItemArray);
            if (hr != 0) throw new Win32Exception(hr);

            foreach (var handler in EnumerateHandlers(Path.GetExtension(filePaths.First())))
            {
                handler.GetName(out var internalName);
                if (internalName == entryId)
                {
                    var dao = shellItemArray.BindToHandler(null, BHID_DataObject, typeof(IDataObject).GUID);
                    handler.Invoke(dao);
                    return;
                }
            }
            throw new InvalidOperationException($"The chosen image editor is no longer registered: {entryId}");
        }
        finally
        {
            foreach (var pidl in pidls)
            {
                Win32.ILFree(pidl);
            }
        }
    }

    public IMemoryImage? LoadIcon(OpenWithEntry entry)
    {
        try
        {
            if (entry.Id.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsIcons.ExtractIcon(_imageContext, ResolveExecutablePath(entry.Id.Substring(4)), 0);
            }
            if (entry.IconPath.StartsWith("@"))
            {
                if (GetUnplatedIconPath(entry.IconPath) is { } unplatedPath)
                {
                    return _imageContext.Load(unplatedPath);
                }
                StringBuilder outBuff = new StringBuilder(1024);
                int hr = Win32.SHLoadIndirectString(entry.IconPath, outBuff, outBuff.Capacity, IntPtr.Zero);
                if (hr != 0) throw new Win32Exception(hr);
                return _imageContext.Load(outBuff.ToString());
            }
            return WindowsIcons.ExtractIcon(_imageContext, entry.IconPath, entry.IconIndex);
        }
        catch (Exception)
        {
            Log.Debug($"Couldn't load OpenWith icon for {entry.Name}");
            return null;
        }
    }

    private static string ResolveExecutablePath(string path) =>
        Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path, Paths.Executable);

    private static string? GetUnplatedIconPath(string indirectIconPath)
    {
        // Store app icons (e.g. "@{Microsoft.Paint_..._x64__8wekyb3d8bbwe?ms-resource://Microsoft.Paint/Files/Assets/
        // PaintAppList.png}") resolve to a tile with lots of padding, which looks tiny at small sizes. So prefer the
        // unpadded variant from the package if there is one.
        var match = Regex.Match(indirectIconPath, @"^@\{([^?]+)\?ms-resource://[^/]+/Files/(.+)\.png\}$");
        if (!match.Success)
        {
            return null;
        }
        var packagePath = new StringBuilder(1024);
        int length = packagePath.Capacity;
        if (Win32.GetPackagePathByFullName(match.Groups[1].Value, ref length, packagePath) != 0)
        {
            return null;
        }
        var unplatedPath = Path.Combine(packagePath.ToString(),
            $"{match.Groups[2].Value}.targetsize-48_altform-unplated.png");
        return File.Exists(unplatedPath) ? unplatedPath : null;
    }

    private IEnumerable<Win32.IAssocHandler> EnumerateHandlers(string fileExt)
    {
        int hr = Win32.SHAssocEnumHandlers(fileExt, Win32.ASSOC_FILTER.ASSOC_FILTER_RECOMMENDED,
            out Win32.IEnumAssocHandlers eah);
        if (hr != 0) throw new Win32Exception(hr);
        while (eah.Next(1, out var handler, out var fetched) == 0 && fetched > 0)
        {
            yield return handler;
        }
    }
}