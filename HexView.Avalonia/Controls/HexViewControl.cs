﻿using System;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using HexView.Avalonia.Model;

namespace HexView.Avalonia.Controls;

public class HexViewControl : Control, ILogicalScrollable
{
    public static readonly StyledProperty<int> ToBaseProperty = 
        AvaloniaProperty.Register<HexViewControl, int>(nameof(ToBase), defaultValue: 16);

    public static readonly StyledProperty<int> BytesWidthProperty = 
        AvaloniaProperty.Register<HexViewControl, int>(nameof(BytesWidth), defaultValue: 8);

    private volatile bool _updating;
    private Size _extent;
    private Size _viewport;
    private Vector _offset;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll;
    private EventHandler? _scrollInvalidated;
    private Typeface _typeface;
    private double _lineHeight;
    private FontFamily? _fontFamily;
    private double _fontSize;
    private IBrush? _foreground;
    private Size _scrollSize = new(1, 1);
    private Size _pageScrollSize = new(10, 10);

    public int ToBase
    {
        get => GetValue(ToBaseProperty);
        set => SetValue(ToBaseProperty, value);
    }

    public int BytesWidth
    {
        get => GetValue(BytesWidthProperty);
        set => SetValue(BytesWidthProperty, value);
    }

    public IHexFormatter? HexFormatter { get;  set; }

    public ILineReader? LineReader { get;  set; }

    Size IScrollable.Extent => _extent;

    Vector IScrollable.Offset
    {
        get => _offset;
        set
        {
            if (_updating)
            {
                return;
            }
            _updating = true;
            _offset = CoerceOffset(value);
            InvalidateScrollable();
            _updating = false;
        }
    }

    Size IScrollable.Viewport => _viewport;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set
        {
            _canHorizontallyScroll = value;
            InvalidateMeasure();
        }
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set
        {
            _canVerticallyScroll = value;
            InvalidateMeasure();
        }
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    Size ILogicalScrollable.ScrollSize => _scrollSize;

    Size ILogicalScrollable.PageScrollSize => _pageScrollSize;

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
    {
        return false;
    }

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from)
    {
        return null;
    }

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
    {
        _scrollInvalidated?.Invoke(this, e);
    }
   
    private Vector CoerceOffset(Vector value)
    {
        var scrollable = (ILogicalScrollable)this;
        var maxX = Math.Max(scrollable.Extent.Width - scrollable.Viewport.Width, 0);
        var maxY = Math.Max(scrollable.Extent.Height - scrollable.Viewport.Height, 0);
        return new Vector(Clamp(value.X, 0, maxX), Clamp(value.Y, 0, maxY));
        static double Clamp(double val, double min, double max) => val < min ? min : val > max ? max : val;
    }

    private FormattedText CreateFormattedText(string text)
    {
        return new FormattedText(text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface,
            _fontSize,
            _foreground);
    }

    protected override void OnLoaded(RoutedEventArgs routedEventArgs)
    {
        base.OnLoaded(routedEventArgs);

        Invalidate();
        InvalidateScrollable();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            InvalidateScrollable();
        }
        
        if (change.Property == TextElement.FontFamilyProperty
            || change.Property == TextElement.FontSizeProperty
            || change.Property == TextElement.ForegroundProperty)
        {
            Invalidate();
            InvalidateScrollable();
        }

        if (change.Property == ToBaseProperty)
        {
            InvalidateVisual();
        }

        if (change.Property == BytesWidthProperty)
        {
            InvalidateScrollable();
        }
    }

    private void Invalidate()
    {
        _fontFamily = TextElement.GetFontFamily(this);
        _fontSize = TextElement.GetFontSize(this);
        _foreground = TextElement.GetForeground(this);
        _typeface = new Typeface(_fontFamily);
        _lineHeight = CreateFormattedText("0").Height;
    }

    public void InvalidateScrollable()
    {
        if (this is not ILogicalScrollable scrollable)
        {
            return;
        }

        var lines = HexFormatter?.Lines ?? 0;
        var width = Bounds.Width;
        var height = Bounds.Height;

        _scrollSize = new Size(1, _lineHeight);
        _pageScrollSize = new Size(_viewport.Width, _viewport.Height);
        _extent = new Size(width, lines * _lineHeight);
        _viewport = new Size(width, height);

        scrollable.RaiseScrollInvalidated(EventArgs.Empty);
        
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (HexFormatter is null || LineReader is null)
        {
            context.DrawRectangle(Brushes.Transparent, null, Bounds);

            return;
        }
   
        var toBase = ToBase;
        var bytesWidth = BytesWidth;

        if (bytesWidth != HexFormatter.Width)
        {
            HexFormatter.Width = bytesWidth;
        }

        var startLine = (long)Math.Ceiling(_offset.Y / _lineHeight);
        var lines = _viewport.Height / _lineHeight;
        var endLine = (long)Math.Min(Math.Floor(startLine + lines), HexFormatter.Lines - 1);

        var sb = new StringBuilder();
        for (var i = startLine; i <= endLine; i++)
        {
            var bytes = LineReader.GetLine(i, HexFormatter.Width);
            HexFormatter.AddLine(bytes, i, sb, toBase);
            sb.AppendLine();
        }

        var text = sb.ToString();
        var ft = CreateFormattedText(text);
        var origin = new Point();

        // TODO: Format diff.
        // ft.SetForegroundBrush(Brushes.Red, 8, 2);
        // ft.SetForegroundBrush(Brushes.Red, 11, 2);
        
        context.DrawText(ft, origin);
    }
}
