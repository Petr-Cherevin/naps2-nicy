using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Widgets;

public class ScrollZoomImageViewer
{
    private readonly Scrollable _scrollable = new() { Border = BorderType.None };
    private readonly Drawable _imageView = new() { BackgroundColor = Colors.White };
    private PointF _scrollableLocation;
    private Size _renderSize;
    private float _renderFactor;
    private PointF? _mousePos;
    private PointF? _initialMousePos;
    private Point? _initialScrollPos;
    private bool _mouseIsDown;
    // Selection rectangles are in image pixel coordinates so they're independent of zoom and scroll position
    private readonly List<RectangleF> _selectionRects = [];
    private bool _rectSelectionEnabled;
    private PointF? _rectOrigin;
    private RectangleF? _activeRect;

    public ScrollZoomImageViewer()
    {
        _imageView.MouseDown += OnImageMouseDown;
        _imageView.MouseMove += OnImageMouseMove;
        _imageView.MouseUp += OnImageMouseUp;
        _scrollable.Content = _imageView;
        _scrollable.BackgroundColor = Colors.White;
        _imageView.Paint += ImagePaint;
        _scrollable.Shown += OnShown;
        _scrollable.MouseEnter += OnMouseEnter;
        _scrollable.MouseLeave += OnMouseLeave;
        _scrollable.MouseMove += OnMouseMove;
        _scrollable.MouseDown += OnMouseDown;
        _scrollable.MouseUp += OnMouseUp;
        EtoPlatform.Current.AttachMouseWheelEvent(_scrollable, OnMouseWheel);
        _scrollable.Cursor = Cursors.Pointer;
    }

    // For some reason Gtk can't render a few pixels width around the edge of the image, so we need to correct for that.
    private int BorderOffset { get; } = EtoPlatform.Current.IsGtk ? 6 : 0;

    public Bitmap? Image { get; set; }

    public ColorScheme? ColorScheme { get; set; }

    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>
    /// Gets or sets whether dragging the mouse over the image draws a selection rectangle (instead of panning).
    /// </summary>
    public bool RectSelectionEnabled
    {
        get => _rectSelectionEnabled;
        set
        {
            _rectSelectionEnabled = value;
            _rectOrigin = null;
            _activeRect = null;
            _mouseIsDown = false;
            _imageView.Cursor = value ? Cursors.Crosshair : _scrollable.Cursor;
            _imageView.Invalidate();
        }
    }

    /// <summary>
    /// Gets the selection rectangles that have been drawn, in image pixel coordinates. These are rendered over the
    /// image as solid rectangles.
    /// </summary>
    public IReadOnlyList<RectangleF> SelectionRects => _selectionRects;

    public Color SelectionRectColor { get; set; } = Colors.Black;

    public event EventHandler? SelectionRectsChanged;

    public void RemoveLastSelectionRect()
    {
        if (_selectionRects.Count == 0) return;
        _selectionRects.RemoveAt(_selectionRects.Count - 1);
        OnSelectionRectsChanged();
    }

    public void ClearSelectionRects()
    {
        _rectOrigin = null;
        _activeRect = null;
        if (_selectionRects.Count == 0) return;
        _selectionRects.Clear();
        OnSelectionRectsChanged();
    }

    private void OnSelectionRectsChanged()
    {
        _imageView.Invalidate();
        SelectionRectsChanged?.Invoke(this, EventArgs.Empty);
    }

    private Size RenderSize
    {
        get => _renderSize;
        set
        {
            _renderSize = value;
            _imageView.Size = Size.Round(value) + new Size(2, 2);
            _imageView.Invalidate();
        }
    }

    private int AvailableWidth => _scrollable.Width - 2 - BorderOffset;
    private int AvailableHeight => _scrollable.Height - 2 - BorderOffset;

    private bool IsWidthBound =>
        Image!.Width / (float) Image.Height > AvailableWidth / (float) AvailableHeight;

    private bool FitsInBounds => RenderSize.Width <= AvailableWidth && RenderSize.Height <= AvailableHeight;

    private float XOffset => Math.Max((_imageView.Width - RenderSize.Width) / 2, 0);
    private float YOffset => Math.Max((_imageView.Height - RenderSize.Height) / 2, 0);

    private void OnShown(object? sender, EventArgs e)
    {
        _scrollableLocation = _scrollable.Location;
    }

    private void OnMouseEnter(object? sender, MouseEventArgs e)
    {
        // TODO: Mouse enter/leave events aren't firing on WinForms, why?
        _mousePos = e.Location;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        _mouseIsDown = false;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (_rectSelectionEnabled)
        {
            // Dragging draws a rectangle rather than panning
            return;
        }
        _mouseIsDown = true;
        _initialMousePos = e.Location;
        _initialScrollPos = _scrollable.ScrollPosition;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_mouseIsDown && _initialMousePos.HasValue && _initialScrollPos.HasValue)
        {
            _scrollable.ScrollPosition = _initialScrollPos.Value + Point.Round(_initialMousePos.Value - e.Location);
        }
        _mousePos = e.Location;
    }

    private void OnMouseLeave(object? sender, MouseEventArgs e)
    {
        _mousePos = null;
    }

    // Converts a point relative to the image view control to image pixel coordinates (clamped to the image bounds)
    private PointF ToImagePoint(PointF viewPoint)
    {
        var x = (viewPoint.X - XOffset) * Image!.Width / RenderSize.Width;
        var y = (viewPoint.Y - YOffset) * Image.Height / RenderSize.Height;
        return new PointF(Math.Max(0, Math.Min(x, Image.Width)), Math.Max(0, Math.Min(y, Image.Height)));
    }

    private static RectangleF RectFromPoints(PointF a, PointF b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private bool CanSelectRect => _rectSelectionEnabled && Image != null && RenderSize.Width > 0 && RenderSize.Height > 0;

    private void OnImageMouseDown(object? sender, MouseEventArgs e)
    {
        if (!CanSelectRect || e.Buttons != MouseButtons.Primary) return;
        _rectOrigin = ToImagePoint(e.Location);
        _activeRect = null;
    }

    private void OnImageMouseMove(object? sender, MouseEventArgs e)
    {
        if (!CanSelectRect || _rectOrigin is not { } origin) return;
        _activeRect = RectFromPoints(origin, ToImagePoint(e.Location));
        _imageView.Invalidate();
    }

    private void OnImageMouseUp(object? sender, MouseEventArgs e)
    {
        if (!CanSelectRect || _rectOrigin is not { } origin) return;
        var rect = RectFromPoints(origin, ToImagePoint(e.Location));
        _rectOrigin = null;
        _activeRect = null;
        // Ignore accidental clicks that don't cover at least a pixel
        if (rect.Width >= 1 && rect.Height >= 1)
        {
            _selectionRects.Add(rect);
            OnSelectionRectsChanged();
        }
        else
        {
            _imageView.Invalidate();
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (e.Modifiers == Keys.Control)
        {
            ChangeZoom(e.Delta.Height, true);
            e.Handled = true;
        }
    }

    private void ImagePaint(object? sender, PaintEventArgs e)
    {
        var bgColor = ColorScheme?.BackgroundColor ?? Colors.White;
        e.Graphics.FillRectangle(bgColor, 0, 0, _imageView.Width, _imageView.Height);
        if (Image != null)
        {
            e.Graphics.DrawRectangle(
                ColorScheme?.BorderColor ?? Colors.Black,
                XOffset - 1, YOffset - 1, RenderSize.Width + 1, RenderSize.Height + 1);
            e.Graphics.DrawImage(Image, XOffset, YOffset, RenderSize.Width, RenderSize.Height);
            foreach (var rect in _selectionRects)
            {
                FillSelectionRect(e.Graphics, rect);
            }
            if (_activeRect is { } activeRect)
            {
                FillSelectionRect(e.Graphics, activeRect);
            }
        }
    }

    private void FillSelectionRect(Graphics graphics, RectangleF rect)
    {
        float xScale = RenderSize.Width / (float) Image!.Width;
        float yScale = RenderSize.Height / (float) Image.Height;
        graphics.FillRectangle(SelectionRectColor,
            XOffset + rect.X * xScale, YOffset + rect.Y * yScale, rect.Width * xScale, rect.Height * yScale);
    }

    public float ZoomFactor => _renderFactor;

    // GDI doesn't like when the render size (4 bytes per pixel) exceeds int.MaxValue
    public float MaxZoom => (float) Math.Sqrt(int.MaxValue * 0.99 / (Image!.Width * Image.Height * 4));

    public void ChangeZoom(float step, bool anchorToMouse = false)
    {
        SetZoom(_renderFactor * (float) Math.Pow(1.2, step), anchorToMouse);
    }

    public void SetZoom(float value, bool anchorToMouse = false)
    {
        _renderFactor = value.Clamp(0.01f, MaxZoom);
        _scrollable.SuspendLayout();
        var anchor = GetMouseAnchor(anchorToMouse);
        RenderSize =
            Size.Round(new SizeF(Image!.Width * _renderFactor, Image.Height * _renderFactor));
        SetMouseAnchor(anchor);
        _scrollable.ResumeLayout();
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_renderFactor));
    }

    public void ZoomToActual()
    {
        RenderSize = new Size(Image!.Width, Image.Height);
        _renderFactor = 1f;
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_renderFactor));
    }

    public void ZoomToContainer()
    {
        if (!_scrollable.Loaded || Image == null || _scrollable.Width <= 0 || _scrollable.Height <= 0)
        {
            return;
        }
        if (IsWidthBound)
        {
            RenderSize = new Size(AvailableWidth,
                (int) Math.Round(Image!.Height * AvailableWidth / (float) Image.Width));
            _renderFactor = AvailableWidth / (float) Image.Width;
        }
        else
        {
            RenderSize =
                new Size((int) Math.Round(Image!.Width * AvailableHeight / (float) Image.Height),
                    AvailableHeight);
            _renderFactor = AvailableHeight / (float) Image.Height;
        }
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_renderFactor));
    }

    // When we zoom in/out (e.g. with Ctrl+mousewheel), we want the point in the image underneath the mouse cursor to
    // stay stationary relative to the mouse (as much as permitted by the available scroll region). If the mouse is not
    // overtop the image, the middle of the image (as currently visible) should stay stationary.
    //
    // This function calculates the image anchor as a fraction (i.e. a point in the range [<0,0>, <1,1>]). It also
    // returns the mouse position relative to the top left of the Scrollable (or the middle of the scrollable if the
    // mouse is not overtop the image).
    private (PointF imageAnchor, PointF mouseRelativePos) GetMouseAnchor(bool anchorToMouse)
    {
        var anchorMiddle = new PointF(0.5f, 0.5f);
        var scrollableMiddle = new PointF(
            _scrollableLocation.X + _scrollable.Width / 2f,
            _scrollableLocation.Y + _scrollable.Height / 2f);
        if (!anchorToMouse ||
            _mousePos is not { } mousePos ||
            mousePos.X < _scrollableLocation.X ||
            mousePos.Y < _scrollableLocation.Y ||
            mousePos.X > _scrollableLocation.X + _scrollable.Width ||
            mousePos.Y > _scrollableLocation.Y + _scrollable.Height)
        {
            // Mouse is outside the scrollable
            return (anchorMiddle, scrollableMiddle);
        }
        var mouseRelativePos = mousePos - _scrollableLocation - new Point(1, 1);
        var x = (mouseRelativePos.X + _scrollable.ScrollPosition.X - XOffset) / RenderSize.Width;
        var y = (mouseRelativePos.Y + _scrollable.ScrollPosition.Y - YOffset) / RenderSize.Height;
        if (x < 0 || y < 0 || x > 1 || y > 1)
        {
            // Mouse is inside the scrollable but outside the area covered by the image
            return (anchorMiddle, scrollableMiddle);
        }
        return (new PointF(x, y), mouseRelativePos);
    }

    // This function inverts the calculation done in GetMouseAnchor to get the correct scroll position to move the point
    // in the image that was underneath the mouse back there (after the image size has been changed).
    private void SetMouseAnchor((PointF imageAnchor, PointF mouseRelativePos) anchor)
    {
        if (FitsInBounds)
        {
            return;
        }
        // TODO: This is off a bit for the "middle" anchor, probably because the scrollbars themselves appear
        var xScroll = anchor.imageAnchor.X * RenderSize.Width + XOffset - anchor.mouseRelativePos.X;
        var yScroll = anchor.imageAnchor.Y * RenderSize.Height + YOffset - anchor.mouseRelativePos.Y;
        _scrollable.ScrollPosition = Point.Round(new PointF(xScroll, yScroll));
    }

    public static implicit operator LayoutElement(ScrollZoomImageViewer control)
    {
        return control._scrollable;
    }
}