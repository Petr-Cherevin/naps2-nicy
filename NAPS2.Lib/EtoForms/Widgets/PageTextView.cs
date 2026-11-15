using Eto.Drawing;
using Eto.Forms;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Widgets;

/// <summary>
/// Displays the text of a page. If the positions of the words are known, the words are drawn in the same places as
/// on the page (as if the scan was removed and only its text layer was left). Otherwise the plain text is shown.
/// </summary>
public class PageTextView : Panel
{
    private readonly Drawable _layoutView = new();
    private readonly TextArea _plainView = new() { ReadOnly = true, Wrap = true };
    private readonly Dictionary<int, Font> _fonts = new();

    // Font size (in points) relative to the height of a line of text (in pixels). A line's bounds go from the top of
    // its tallest letters to the bottom of its descenders, which is roughly the font's size in pixels, and there are
    // 72 points to 96 pixels.
    private const float FontSizePerLineHeight = 0.8f * 72 / 96;
    // How much taller than a typical line a line has to be to get its own (larger) font size
    private const float HeadingHeightRatio = 1.35f;
    // How much wider than the original line the drawn text can be before it's shrunk to fit
    private const float MaxLineWidthRatio = 1.15f;
    private PageText? _text;

    public PageTextView()
    {
        BackgroundColor = Colors.White;
        _layoutView.Paint += LayoutViewPaint;
        _layoutView.SizeChanged += (_, _) => _layoutView.Invalidate();
    }

    /// <summary>
    /// Shows a message in place of any text, e.g. while the text is being generated.
    /// </summary>
    public string Message
    {
        set
        {
            _text = null;
            Content = new Label
            {
                Text = value,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = Colors.Gray
            };
        }
    }

    public PageText? Text
    {
        get => _text;
        set
        {
            _text = value;
            if (value == null)
            {
                Content = null;
            }
            else if (value.HasLayout)
            {
                Content = _layoutView;
                _layoutView.Invalidate();
            }
            else
            {
                _plainView.Text = value.PlainText;
                Content = _plainView;
            }
        }
    }

    private void LayoutViewPaint(object? sender, PaintEventArgs e)
    {
        var text = _text;
        var graphics = e.Graphics;
        graphics.FillRectangle(Colors.White, 0, 0, _layoutView.Width, _layoutView.Height);
        if (text is not { HasLayout: true } || _layoutView.Width <= 0 || _layoutView.Height <= 0)
        {
            return;
        }
        // Fit the whole page in the view
        float scale = Math.Min(_layoutView.Width / text.PageWidth, _layoutView.Height / text.PageHeight);
        float xOffset = (_layoutView.Width - text.PageWidth * scale) / 2;
        float yOffset = (_layoutView.Height - text.PageHeight * scale) / 2;
        var lines = text.Lines;
        if (lines.Count == 0)
        {
            return;
        }
        // We don't try to reproduce the page exactly, just to give the text a similar layout. Each line is drawn as
        // normal text at the position of the original line. Body text all uses the same font size (based on the
        // typical line height) so it looks even; only lines that are clearly bigger (e.g. headings) get their own.
        var heights = lines.Select(x => x.Height).OrderBy(x => x).ToList();
        float typicalHeight = heights[heights.Count / 2];
        foreach (var line in lines)
        {
            float lineHeight = line.Height > typicalHeight * HeadingHeightRatio ? line.Height : typicalHeight;
            float fontSize = lineHeight * scale * FontSizePerLineHeight;
            if (fontSize < 1)
            {
                continue;
            }
            float x = xOffset + line.X * scale;
            float y = yOffset + line.Y * scale;
            var font = GetFont(fontSize);
            // Keep the line from running past where the original ended (with some slack) or off the page
            float maxWidth = Math.Min(line.Width * scale * MaxLineWidthRatio, xOffset + text.PageWidth * scale - x);
            var measured = graphics.MeasureString(font, line.Text);
            if (measured.Width > maxWidth && measured.Width > 0 && maxWidth > 0)
            {
                fontSize *= maxWidth / measured.Width;
                if (fontSize < 1)
                {
                    continue;
                }
                font = GetFont(fontSize);
            }
            graphics.DrawText(font, Colors.Black, x, y, line.Text);
        }
    }

    private Font GetFont(float size)
    {
        // Cache fonts in half-point steps so we don't create one for every word
        int key = Math.Max((int) Math.Round(size * 2), 2);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = Fonts.Sans(key / 2f);
            _fonts[key] = font;
        }
        return font;
    }
}
