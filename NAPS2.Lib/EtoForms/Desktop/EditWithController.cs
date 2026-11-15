using Eto.Drawing;
using NAPS2.EtoForms.Ui;
using NAPS2.Scan;

namespace NAPS2.EtoForms.Desktop;

public class EditWithController(
    ScanningContext scanningContext,
    UiImageList imageList,
    Naps2Config config,
    IOpenWith openWith,
    ErrorOutput errorOutput,
    IFormFactory formFactory)
{
    public event EventHandler? AppChanged;

    public bool HasApp => !string.IsNullOrEmpty(config.Get(c => c.EditWithAppPath));

    public string? AppName => config.Get(c => c.EditWithAppName);

    public string? AppId => config.Get(c => c.EditWithAppPath);

    public Image? AppIcon { get; private set; }

    public IReadOnlyList<OpenWithEntry> GetAvailableEditors()
    {
        try
        {
            return openWith.GetEntries(".jpg")
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) &&
                                !entry.Name.StartsWith("NAPS2", StringComparison.OrdinalIgnoreCase))
                .GroupBy(entry => entry.Id)
                .Select(group => group.First())
                .ToList();
        }
        catch (Exception ex)
        {
            // Missing or damaged file associations must not prevent choosing an editor by .exe.
            Log.ErrorException("Error enumerating image editors", ex);
            return [];
        }
    }

    public async Task LoadAppIcon()
    {
        string? appId = AppId;
        if (string.IsNullOrEmpty(appId))
        {
            return;
        }
        using var icon = await Task.Run(() =>
        {
            try
            {
                var entry = GetAvailableEditors().FirstOrDefault(entry => entry.Id == appId);
                if (entry == null && appId.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
                {
                    entry = new OpenWithEntry(appId, AppName ?? "Editor", appId.Substring(4), 0);
                }
                return entry != null ? openWith.LoadIcon(entry) : null;
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error loading icon for {appId}", ex);
                return null;
            }
        });
        SetAppIcon(icon);
    }

    private void SetAppIcon(IMemoryImage? icon)
    {
        AppIcon = icon?.ToEtoImage();
        AppChanged?.Invoke(this, EventArgs.Empty);
    }

    public OpenWithEntry? PickApp()
    {
        var pickForm = formFactory.Create<EditWithForm>();
        pickForm.ShowModal();
        return pickForm.Result;
    }

    public void SelectApp(OpenWithEntry entry)
    {
        var transact = config.User.BeginTransaction();
        transact.Set(c => c.EditWithAppPath, entry.Id);
        transact.Set(c => c.EditWithAppName, entry.Name);
        transact.Commit();
        using var icon = openWith.LoadIcon(entry);
        SetAppIcon(icon);
    }

    public void EditWithApp(IEnumerable<UiImage> selection)
    {
        string? appId = AppId;
        if (string.IsNullOrEmpty(appId))
        {
            EditWithPick(selection);
            return;
        }
        EditWithPath(appId, selection);
    }

    public void EditWithPick(IEnumerable<UiImage> selection)
    {
        var entry = PickApp();
        if (entry != null)
        {
            EditWithEntry(entry, selection);
        }
    }

    public void EditWithEntry(OpenWithEntry entry, IEnumerable<UiImage> selection)
    {
        SelectApp(entry);
        EditWithPath(entry.Id, selection);
    }

    private void EditWithPath(string appPath, IEnumerable<UiImage> selection)
    {
        var tempFilePaths = new List<string>();
        foreach (var uiImage in selection)
        {
            if (!uiImage.IsDisposed)
            {
                var editorSession = new ExternalEditorSession(scanningContext, imageList, uiImage);
                uiImage.EditorSessions.Add(editorSession);
                tempFilePaths.Add(editorSession.TempFilePath);
            }
        }
        if (tempFilePaths.Count == 0)
        {
            return;
        }
        try
        {
            openWith.OpenWith(appPath, tempFilePaths);
        }
        catch (Exception ex)
        {
            errorOutput.DisplayError(string.Format(UiStrings.ErrorStartingApplication, appPath), ex);
        }
    }
}
