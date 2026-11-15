using System.Collections.Immutable;
using NAPS2.Config.Model;
using NAPS2.Escl;
using NAPS2.ImportExport.Email;
using NAPS2.ImportExport.Images;
using NAPS2.Pdf;
using NAPS2.Ocr;
using NAPS2.Scan;
using NAPS2.Scan.Batch;

namespace NAPS2.Config;

[Config]
public class CommonConfig
{
    public const int CURRENT_VERSION = 3;

    [Common]
    public int? Version { get; set; } = CURRENT_VERSION;

    [Common]
    public string? Culture { get; set; }

    [User]
    public ImmutableList<FormState> FormStates { get; set; } = ImmutableList<FormState>.Empty;

    [User]
    public ImmutableHashSet<string> BackgroundOperations { get; set; } = ImmutableHashSet<string>.Empty;

    [Common]
    public ImmutableList<NamedPageSize> CustomPageSizePresets { get; set; } = ImmutableList<NamedPageSize>.Empty;

    [User]
    public int LastCustomResolution { get; set; }

    [App]
    public string? StartupMessageTitle { get; set; }

    [App]
    public string? StartupMessageText { get; set; }

    [App]
    public MessageBoxIcon StartupMessageIcon { get; set; }

    [Common]
    public Theme Theme { get; set; }

    [Common]
    public bool ScanChangesDefaultProfile { get; set; }

    [Common]
    public bool ShowProfilesToolbar { get; set; }

    [Common]
    public ScanButtonDefaultAction ScanButtonDefaultAction { get; set; }

    [Common]
    public SaveButtonDefaultAction SaveButtonDefaultAction { get; set; }

    [Common]
    public ToolbarButtons HiddenButtons { get; set; }

    [Common]
    public ToolbarButtons ExtraButtons { get; set; }

    [App]
    public bool DisableAutoSave { get; set; }

    [App]
    public bool LockSystemProfiles { get; set; }

    [App]
    public bool LockUnspecifiedDevices { get; set; }

    [App]
    public bool NoUserProfiles { get; set; }

    [Common]
    public bool AlwaysRememberDevice { get; set; }

    [Common]
    public bool DeviceListAsTextOnly { get; set; }

    [Common]
    public bool NoUpdatePrompt { get; set; }

    [Common]
    public bool CheckForUpdates { get; set; }

    [User]
    public bool HasCheckedForUpdates { get; set; }

    [User]
    public DateTime? LastUpdateCheckDate { get; set; }

    [User]
    public bool HasBeenRun { get; set; }

    /// <summary>
    /// The order and visibility of the preview window toolbar buttons. See PreviewToolbarLayout.
    /// </summary>
    [User]
    public string? PreviewToolbarLayout { get; set; }

    /// <summary>
    /// Whether redaction in the preview window fills with black instead of white.
    /// </summary>
    [User]
    public bool RedactWithBlack { get; set; }

    /// <summary>
    /// Controls whether an outline is shown around redaction selections while dragging or before applying.
    /// The default is a temporary outline that disappears on mouse release.
    /// </summary>
    [User]
    public RedactionOutlineMode RedactionOutlineMode { get; set; } = RedactionOutlineMode.DuringDrag;

    [User]
    public DateTime? FirstRunDate { get; set; }

    [User]
    public bool HasBeenPromptedForDonation { get; set; }

    [User]
    public DateTime? LastDonatePromptDate { get; set; }

    [User]
    public bool HasBeenPromptedForReview { get; set; }

    [User]
    public DateTime? LastReviewPromptDate { get; set; }

    [Common]
    public bool DeleteAfterSaving { get; set; }

    [Common]
    public bool KeepSession { get; set; }

    [Common]
    public bool DisableSaveNotifications { get; set; }

    [Common]
    public bool DisableExitConfirmation { get; set; }

    [Common]
    public bool SingleInstance { get; set; }

    [App]
    public string? ComponentsPath { get; set; }

    [Common]
    public double OcrTimeoutInSeconds { get; set; }

    [Common]
    public bool EnableOcr { get; set; }

    [Common]
    public string? OcrLanguageCode { get; set; }

    [Common]
    public string? LastOcrMultiLangCode { get; set; }

    // Keep language choices independent across the three OCR settings tabs.
    [User]
    public string? OcrSingleLanguageCode { get; set; }

    [User]
    public string? OcrFavoriteLanguageCode { get; set; }

    [User]
    public string? OcrCustomLanguageCode { get; set; }

    [User]
    public int OcrLanguageTab { get; set; }

    [Common]
    public LocalizedOcrMode OcrMode { get; set; }

    [Common]
    public bool OcrPreProcessing { get; set; }

    [Common]
    public bool OcrAfterScanning { get; set; }

    /// <summary>
    /// If EnableOcr is on, whether OCR is also re-run for scanned pages that already have hidden text from a previous
    /// OCR (e.g. from an imported PDF), rather than only for pages without text.
    /// </summary>
    [Common]
    public bool OcrAllScans { get; set; }

    [User]
    public string? LastImageExt { get; set; }

    [User]
    public string? LastPdfOrImageExt { get; set; }

    [Common]
    public int ThumbnailSize { get; set; }

    [Common]
    public DockStyle DesktopToolStripDock { get; set; }

    [Common]
    public DockStyle ProfilesToolStripDock { get; set; }

    [Common]
    public EsclSecurityPolicy EsclSecurityPolicy { get; set; }

    [Common]
    public string? EsclServerCertificatePath { get; set; }

    [App]
    public EventType EventLogging { get; set; }

    [Config]
    [Common]
    public ManualDuplexSettings ManualDuplexSettings { get; set; } = new();

    [Config]
    [Common]
    public PdfSettings PdfSettings { get; set; } = new();

    [User]
    public bool RememberPdfSettings { get; set; }

    [Config]
    [Common]
    public ImageSettings ImageSettings { get; set; } = new();

    [User]
    public bool RememberImageSettings { get; set; }

    [Config]
    [Common]
    public EmailSettings EmailSettings { get; set; } = new();

    [User]
    public bool RememberEmailSettings { get; set; }

    [Config]
    [Common]
    public EmailSetup EmailSetup { get; set; } = new();

    [Config]
    [Common]
    public BatchSettings BatchSettings { get; set; } = new();

    [Config]
    [Common]
    public KeyboardShortcuts KeyboardShortcuts { get; set; } = new();

    [Common]
    public ScanProfile? DefaultProfileSettings { get; set; }

    [Common]
    public bool EnableDebugLogging { get; set; }
    
    [User]
    public bool ShowPageNumbers { get; set; }

    [Common]
    public bool DisableScannerSharing { get; set; }

    [User]
    public bool SidebarVisible { get; set; }

    [User]
    public int SidebarWidth { get; set; }

    [User]
    public string? EditWithAppPath { get; set; }

    [User]
    public string? EditWithAppName { get; set; }

    [User]
    public bool ApplyToAllSelected { get; set; }
}