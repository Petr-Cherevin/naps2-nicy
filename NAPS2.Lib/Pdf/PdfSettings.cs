using NAPS2.Config.Model;

namespace NAPS2.Pdf;

public record PdfSettings
{
    private PdfMetadata _metadata;
    private PdfEncryption _encryption;
    private PdfCompression _compression;

    public PdfSettings()
    {
        _metadata = new PdfMetadata();
        _encryption = new PdfEncryption();
        _compression = new PdfCompression();
    }

    public string? DefaultFileName { get; set; }

    public bool ExternalNameEnabled { get; set; }
    public string? ExternalProgram { get; set; }
    public string? ExternalArguments { get; set; }
    public string? ExternalInput { get; set; }

    public bool SkipSavePrompt { get; set; }

    public bool SinglePagePdfs { get; set; }

    [Config]
    public PdfMetadata Metadata
    {
        get => _metadata;
        set => _metadata = value ?? throw new ArgumentNullException(nameof(value));
    }

    [Config]
    public PdfEncryption Encryption
    {
        get => _encryption;
        set => _encryption = value ?? throw new ArgumentNullException(nameof(value));
    }

    [Config]
    public PdfCompression Compression
    {
        get => _compression;
        set => _compression = value ?? throw new ArgumentNullException(nameof(value));
    }

    public PdfCompat Compat { get; set; }
}