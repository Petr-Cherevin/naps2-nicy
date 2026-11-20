using NAPS2.Images.Bitwise;
using ZXing;
using ZXing.Common;

namespace NAPS2.Scan;

/// <summary>
/// A wrapper around the ZXing library that detects patch-t and other barcodes.
/// http://www.alliancegroup.co.uk/patch-codes.htm
/// </summary>
internal static class BarcodeDetector
{
    private static readonly BarcodeFormat PATCH_T_FORMAT = BarcodeFormat.CODE_39;

    public static Barcode Detect(IMemoryImage image, BarcodeDetectionOptions options)
    {
        // TODO: Probably shouldn't have DetectBarcodes be in the options class? The call shouldn't happen at all.
        if (!options.DetectBarcodes)
        {
            return Barcode.NoDetection;
        }

        var zxingOptions = options.ZXingOptions ?? new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = options.PatchTOnly ? [PATCH_T_FORMAT] : null
        };
        var reader = new BarcodeReader<IMemoryImage>(x => new MemoryImageLuminanceSource(x))
        {
             Options = zxingOptions
        };
        var result = reader.Decode(image);
        return new Barcode(true, result != null, result?.Text);
    }
    
    internal static IEnumerable<string> ReadNamingQrCodes(IMemoryImage image)
    {
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] }
        };
        var source = new MemoryImageLuminanceSource(image);
        var results = reader.DecodeMultiple(source);
        if (results is { Length: > 0 })
            return results.Select(r => r.Text).Distinct().ToArray();

        // A small printed code near the lower-right corner may need a binary,
        // enlarged image. Keep the rendered page and its PDF content untouched.
        int left = image.Width * 2 / 3, top = image.Height * 3 / 4;
        int width = image.Width - left, height = image.Height - top;
        var pixels = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = source.Matrix[(top + y) * image.Width + left + x] > 180 ? (byte)255 : (byte)0;
                int i = y * 4 * width + x * 2;
                pixels[i] = pixels[i + 1] = pixels[i + 2 * width] = pixels[i + 2 * width + 1] = value;
            }
        }
        var enlarged = new RGBLuminanceSource(pixels, width * 2, height * 2, RGBLuminanceSource.BitmapFormat.Gray8);
        return reader.DecodeMultiple(enlarged)?.Select(r => r.Text).Distinct().ToArray() ?? [];
    }

    private class MemoryImageLuminanceSource : LuminanceSource
    {
        public MemoryImageLuminanceSource(IMemoryImage image)
            : base(image.Width, image.Height)
        {
            var dstPixelInfo = new PixelInfo(image.Width, image.Height, SubPixelType.Gray);
            var matrix = new byte[dstPixelInfo.Length];
            new CopyBitwiseImageOp().Perform(image, matrix, dstPixelInfo);
            Matrix = matrix;
        }

        public override byte[] getRow(int y, byte[]? row)
        {
            row ??= new byte[Width];
            Array.Copy(Matrix, y * Width, row, 0, Width);
            return row;
        }

        public override byte[] Matrix { get; }
    }
}