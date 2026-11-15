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
    private PageText? _text;

    public PageTextView()
    {
        BackgroundColor = Colors.White;
        _layoutView.Paint += LayoutViewPaint;
        _layoutView.SizeChanged += (_, _) => _layoutView.Invalidate();
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
        foreach (var word in text.Words)
        {
            float width = word.Width * scale;
            float height = word.Height * scale;
            // Font sizes are in points (1/72 inch) and the word height is in pixels (nominally 1/96 inch)
            float fontSize = height * 72 / 96;
            if (fontSize < 1 || width <= 0)
            {
                continue;
            }
            var font = GetFont(fontSize);
            var measured = graphics.MeasureString(font, word.Text);
            if (measured.Width > width && measured.Width > 0)
            {
                // Shrink the text so it doesn't overlap the next word
                fontSize *= width / measured.Width;
                if (fontSize < 1)
                {
                    continue;
                }
                font = GetFont(fontSize);
            }
            graphics.DrawText(font, Colors.Black, xOffset + word.X * scale, yOffset + word.Y * scale, word.Text);
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
