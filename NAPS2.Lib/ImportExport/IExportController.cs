using NAPS2.EtoForms.Notifications;

namespace NAPS2.ImportExport;

public interface IExportController
{
    Task<bool> SavePdf(ICollection<UiImage> uiImages, ISaveNotify notify);
    Task<bool> SaveSeparatePdfs(ICollection<UiImage> uiImages, ISaveNotify notify, bool extract);
    Task<bool> ExtractPdf(ICollection<UiImage> uiImages, ISaveNotify notify);
    Task<bool> SaveImages(ICollection<UiImage> uiImages, ISaveNotify notify);
    Task<bool> SavePdfOrImages(ICollection<UiImage> uiImages, ISaveNotify notify);
    Task<bool> EmailPdf(ICollection<UiImage> uiImages);
}