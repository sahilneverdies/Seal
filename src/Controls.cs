
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WPath = System.Windows.Shapes.Path;
using WF = System.Windows.Forms;
using SD = System.Drawing;

namespace Seal {

    sealed class Tracked : FrameworkElement {
        public string Text = "";
        public double Size = 10, Tracking = 1.4;
        public Brush Fill = Ui.Section;
        Typeface face;
        void Face() { if (face == null) face = new Typeface(Ui.MonoFont, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal); }
        FormattedText Glyph(char c) { return new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, Size, Fill, 1.0); }
        protected override Size MeasureOverride(Size a) {
            Face();
            double w = 0, h = 0;
            foreach (char c in Text) { var ft = Glyph(c); w += ft.WidthIncludingTrailingWhitespace + Tracking; h = Math.Max(h, ft.Height); }
            return new Size(w, h);
        }
        protected override void OnRender(DrawingContext dc) {
            Face();
            double x = 0;
            foreach (char c in Text) { var ft = Glyph(c); dc.DrawText(ft, new Point(x, 0)); x += ft.WidthIncludingTrailingWhitespace + Tracking; }
        }
    }


    sealed class StripPicker : FrameworkElement {
        public int Cells = 36;
        public bool Shade;
        public double Hue = 210;
        public Rgb Current;
        public bool HasCurrent;
        public event Action<Rgb> Picked;
        public StripPicker() { Cursor = Cursors.Cross; }
        protected override Size MeasureOverride(Size a) { return new Size(double.IsInfinity(a.Width) ? 200 : a.Width, 26); }
        public void Repaint() { InvalidateVisual(); }
        public Rgb ColorAt(int i) {
            if (!Shade) return Ui.Hsl(i * 360.0 / Cells, 0.85, 0.55);
            double t = Cells <= 1 ? 0 : i / (double)(Cells - 1);
            return Ui.Hsl(Hue, 0.28 + t * 0.6, 0.95 - t * 0.76);
        }
        int Nearest() {
            if (!HasCurrent) return -1;
            int best = -1;
            double bd = double.MaxValue;
            for (int i = 0; i < Cells; i++) {
                var c = ColorAt(i);
                double d = (c.R - Current.R) * (c.R - Current.R) + (c.G - Current.G) * (c.G - Current.G) + (c.B - Current.B) * (c.B - Current.B);
                if (d < bd) { bd = d; best = i; }
            }
            return bd <= 1200 ? best : -1;
        }
        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth / Cells, h = ActualHeight;
            for (int i = 0; i < Cells; i++) dc.DrawRectangle(Ui.Brush(ColorAt(i)), null, new Rect(i * w, 0, w + 0.7, h));
            int sel = Nearest();
            if (sel >= 0) dc.DrawRectangle(null, new Pen(Ui.Brush("#FCFAF9"), 2), new Rect(sel * w + 1, 1, Math.Max(1, w - 2), h - 2));
        }
        void Pick(Point p) {
            int i = Math.Max(0, Math.Min(Cells - 1, (int)(p.X / (ActualWidth / Cells))));
            var h = Picked;
            if (h != null) h(ColorAt(i));
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { CaptureMouse(); Pick(e.GetPosition(this)); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured) Pick(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (IsMouseCaptured) ReleaseMouseCapture(); }
    }

    
    sealed class ValueLink : Border {
        public string[] Items = new string[0];
        public event Action<int> Changed;
        int index;
        bool editable;
        readonly TextBlock text = new TextBlock { FontFamily = Ui.UiFont, FontSize = 12.5, Foreground = Ui.TextB, VerticalAlignment = VerticalAlignment.Center };

        public bool Editable {
            get { return editable; }
            set { editable = value; text.Foreground = value ? Ui.Brush(Ui.BalColor) : Ui.TextB; Cursor = value ? Cursors.Hand : Cursors.Arrow; if (!value) Background = Brushes.Transparent; }
        }
        readonly System.Windows.Controls.Primitives.Popup popup = new System.Windows.Controls.Primitives.Popup { StaysOpen = false, AllowsTransparency = true, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, VerticalOffset = 2 };
        public ValueLink() {

            Background = Brushes.Transparent; CornerRadius = new CornerRadius(4); Padding = new Thickness(4, 1, 4, 1); Margin = new Thickness(-4, 0, 0, 0);
            Cursor = Cursors.Arrow; VerticalAlignment = VerticalAlignment.Center; SnapsToDevicePixels = true; UseLayoutRounding = true; Child = text;
            popup.PlacementTarget = this;
            MouseEnter += delegate { if (editable) Background = Ui.Pill; };
            MouseLeave += delegate { if (!popup.IsOpen) Background = Brushes.Transparent; };
            popup.Closed += delegate { Background = IsMouseOver ? Ui.Pill : Brushes.Transparent; };
        }
        public int Index { get { return index; } set { index = Math.Max(0, Math.Min(Items.Length - 1, value)); text.Text = Items.Length > 0 ? Items[index] : ""; } }
        void Set(int i) { int was = index; Index = i; if (index != was) { var h = Changed; if (h != null) h(index); } }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            if (!editable) return;
            var list = new StackPanel();
            Border current = null;
            for (int i = 0; i < Items.Length; i++) {
                int idx = i;
                var item = new Border { Background = i == index ? Ui.Pill : Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 5, 24, 5), Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = Items[i], FontFamily = Ui.UiFont, FontSize = 12.5, Foreground = i == index ? Ui.TextHi : Ui.TextB } };
                item.MouseEnter += delegate { item.Background = Ui.Pill; };
                item.MouseLeave += delegate { item.Background = idx == index ? Ui.Pill : Brushes.Transparent; };
                item.MouseLeftButtonUp += delegate { popup.IsOpen = false; Set(idx); };
                list.Children.Add(item);
                if (i == index) current = item;
            }
            var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 200 };
            popup.Child = new Border { Background = Ui.Card, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Child = scroll,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.55 } };
            popup.IsOpen = true;
            if (current != null) current.BringIntoView();
        }
    }


    sealed class Seg : Border {
        public enum Kind { Page, Compact, Row }
        readonly Grid grid = new Grid();
        readonly Panel cells;
        readonly Border pill;
        readonly TranslateTransform pillT = new TranslateTransform();
        readonly List<TextBlock> labels = new List<TextBlock>();
        readonly List<Border> items = new List<Border>();
        readonly List<bool> enabled = new List<bool>();
        int sel = -1;
        public readonly object[] Tags;
        public event Action<int> Picked;
        public Seg(string[] names, string[] tips, object[] tags, Kind kind) {
            Tags = tags;
            double pad = kind == Kind.Page ? 4 : 3, radius = kind == Kind.Page ? 10 : kind == Kind.Compact ? 9 : 8, inner = kind == Kind.Row ? 6 : 7;
            double size = kind == Kind.Page ? 13.5 : 12, padY = kind == Kind.Page ? 9 : kind == Kind.Compact ? 7 : 6, padX = kind == Kind.Row ? 12 : 6;
            cells = kind == Kind.Row ? (Panel)new StackPanel { Orientation = Orientation.Horizontal } : new UniformGrid { Rows = 1 };
            Background = Ui.Sunken;
            CornerRadius = new CornerRadius(radius);
            Padding = new Thickness(pad);
            pill = new Border { CornerRadius = new CornerRadius(inner), Background = Ui.Accent, HorizontalAlignment = HorizontalAlignment.Left, Width = 40, Opacity = 0, RenderTransform = pillT };
            grid.Children.Add(pill);
            grid.Children.Add(cells);
            Child = grid;
            for (int i = 0; i < names.Length; i++) {
                int idx = i;
                var tb = new TextBlock { Text = names[i], FontFamily = Ui.UiFont, FontSize = size, Foreground = Ui.SegText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var cell = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand, Padding = new Thickness(padX, padY, padX, padY), CornerRadius = new CornerRadius(inner), Child = tb };
                if (tips != null && i < tips.Length && tips[i] != null) cell.ToolTip = tips[i];
                cell.MouseLeftButtonUp += delegate { if (idx == sel || !enabled[idx]) return; Select(idx, true); var h = Picked; if (h != null) h(idx); };
                cell.MouseEnter += delegate { if (idx != sel && enabled[idx]) tb.Foreground = Ui.TextB; };
                cell.MouseLeave += delegate { if (idx != sel && enabled[idx]) tb.Foreground = Ui.SegText; };
                labels.Add(tb);
                items.Add(cell);
                enabled.Add(true);
                cells.Children.Add(cell);
            }
            cells.SizeChanged += delegate { Place(false); };
        }
        public int Count { get { return items.Count; } }
        public void SetMono(double size) { foreach (var tb in labels) { tb.FontFamily = Ui.MonoFont; tb.FontSize = size; } }

        public void SetEnabled(int i, bool on, string why) {
            enabled[i] = on;
            items[i].Opacity = on ? 1 : 0.3;
            items[i].Cursor = on ? Cursors.Hand : Cursors.Arrow;
            if (!on) items[i].ToolTip = why;
        }
        public void Select(int i, bool animate) {
            sel = i;
            for (int j = 0; j < labels.Count; j++) { labels[j].Foreground = j == i ? Brushes.White : (items[j].IsMouseOver && enabled[j] ? Ui.TextB : Ui.SegText); labels[j].FontWeight = j == i ? FontWeights.Medium : FontWeights.Normal; }
            Place(animate);
        }
        void Place(bool animate) {
            if (sel < 0 || cells.ActualWidth <= 0) { pill.Opacity = 0; return; }
            double x = 0, w = 0;
            if (sel < items.Count && items[sel].ActualWidth > 0) { x = items[sel].TranslatePoint(new Point(0, 0), cells).X; w = items[sel].ActualWidth; }
            else { w = cells.ActualWidth / items.Count; x = w * sel; }
            x = Math.Round(x);
            w = Math.Round(w);
            if (pill.Opacity == 0 || !animate) { pill.BeginAnimation(WidthProperty, null); pillT.BeginAnimation(TranslateTransform.XProperty, null); pill.Width = w; pillT.X = x; pill.Opacity = 1; return; }
            Ui.Glide(pill, WidthProperty, w, 320, true);
            Ui.Glide(pillT, TranslateTransform.XProperty, x, 320, true);
        }
    }


    sealed class LinkSeg : Grid {
        readonly StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal }; readonly Border line; readonly TranslateTransform lineT = new TranslateTransform();
        readonly List<TextBlock> labels = new List<TextBlock>();
        readonly List<bool> off = new List<bool>();
        int sel = -1;
        public event Action<int> Picked;
        public LinkSeg(string[] names, double gap, double size, double under, string[] tips) {
            line = new Border { Height = 1, Background = Ui.Accent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Width = 20, Opacity = 0, RenderTransform = lineT };
            Children.Add(sp);
            Children.Add(line);
            for (int i = 0; i < names.Length; i++) {
                int idx = i;
                var tb = new TextBlock { Text = names[i], FontFamily = Ui.UiFont, FontSize = size, Foreground = Ui.Desc, Cursor = Cursors.Hand,
                    Padding = new Thickness(0, 0, 0, under), Margin = new Thickness(0, 0, i < names.Length - 1 ? gap : 0, 0), VerticalAlignment = VerticalAlignment.Center };
                if (tips != null && i < tips.Length && tips[i] != null) tb.ToolTip = tips[i];
                tb.MouseLeftButtonUp += delegate { if (off[idx]) return; Select(idx, true); var h = Picked; if (h != null) h(idx); };
                tb.MouseEnter += delegate { if (idx != sel && !off[idx]) tb.Foreground = Ui.TextB; };
                tb.MouseLeave += delegate { if (idx != sel) tb.Foreground = Ui.Desc; };
                tb.SizeChanged += delegate { Place(false); };
                labels.Add(tb);
                off.Add(false);
                sp.Children.Add(tb);
            }
            SizeChanged += delegate { Place(false); };
        }
        public void SetText(int i, string t) { if (labels[i].Text != t) labels[i].Text = t; }

        public void SetEnabled(int i, bool on, string why) {
            off[i] = !on;
            labels[i].Opacity = on ? 1 : 0.35;
            labels[i].Cursor = on ? Cursors.Hand : Cursors.Arrow;
            labels[i].ToolTip = on ? null : why;
        }
        public void Select(int i, bool animate) {
            sel = i;
            for (int j = 0; j < labels.Count; j++) labels[j].Foreground = j == i ? Ui.TextHi : (labels[j].IsMouseOver ? Ui.TextB : Ui.Desc);
            Place(animate);
        }
        void Place(bool animate) {
            if (sel < 0 || ActualWidth <= 0 || labels[sel].ActualWidth <= 0) { line.Opacity = 0; return; }
            var tb = labels[sel];
            Point p = tb.TranslatePoint(new Point(0, 0), this);
            double x = Math.Round(p.X), w = Math.Round(tb.ActualWidth);
            if (line.Opacity == 0 || !animate) { line.BeginAnimation(WidthProperty, null); lineT.BeginAnimation(TranslateTransform.XProperty, null); line.Width = w; lineT.X = x; line.Opacity = 1; return; }
            Ui.Glide(line, WidthProperty, w, 320, true);
            Ui.Glide(lineT, TranslateTransform.XProperty, x, 320, true);
        }
    }


    sealed class NavBtn : Border {
        public readonly int Index;
        readonly List<Shape> strokes = new List<Shape>(), fills = new List<Shape>();
        bool sel, accent;

        public bool Accent { set { accent = value; Tint(); } }
        public event Action<int> Clicked;
        public NavBtn(int index, string tip, string[] strokePaths, string[] fillPaths) {
            Index = index;
            Width = 42;
            Height = 42;
            CornerRadius = new CornerRadius(12);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;
            ToolTip = tip;
            Margin = new Thickness(0, 2, 0, 2);
            var g = new Grid { Width = 18, Height = 18 };
            foreach (string d in strokePaths) { var p = new WPath { Data = Scaled(d), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Stretch = Stretch.None, Width = 18, Height = 18 }; strokes.Add(p); g.Children.Add(p); }
            foreach (string d in fillPaths) { var p = new WPath { Data = Scaled(d), Stretch = Stretch.None, Width = 18, Height = 18 }; fills.Add(p); g.Children.Add(p); }
            Child = g;
            Tint();
            MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { e.Handled = true; var h = Clicked; if (h != null) h(Index); };
            MouseEnter += delegate { Tint(); }; MouseLeave += delegate { Tint(); };
        }

        static Geometry Scaled(string d) {
            var g = new GeometryGroup { FillRule = FillRule.EvenOdd, Transform = new ScaleTransform(0.75, 0.75) };
            g.Children.Add(Geometry.Parse(d));
            g.Freeze();
            return g;
        }
        public void SetSelected(bool on) { sel = on; Tint(); }
        void Tint() {
            var b = sel ? Ui.TextB : IsMouseOver ? Ui.Sub : accent ? (Brush)Ui.Accent : Ui.Axis;
            foreach (var p in strokes) p.Stroke = b;
            foreach (var p in fills) p.Fill = b;
        }
    }

    sealed class ChipSeg : Border {
        readonly WrapPanel wp = new WrapPanel { Orientation = Orientation.Horizontal };
        readonly List<Border> chips = new List<Border>();
        readonly List<TextBlock> labels = new List<TextBlock>();
        int sel = -1;
        public event Action<int> Picked;

        public ChipSeg(string[] names, string[] tips) {
            Child = wp;
            for (int i = 0; i < names.Length; i++) {
                int idx = i;
                var tb = new TextBlock {
                    Text = names[i],
                    FontFamily = Ui.UiFont,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = Ui.Desc,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var chip = new Border {
                    Background = Ui.Pill,
                    BorderBrush = Ui.Line,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 6),
                    Cursor = Cursors.Hand,
                    SnapsToDevicePixels = true,
                    Child = tb
                };
                chip.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) {
                    e.Handled = true;
                    Select(idx, true);
                    var h = Picked;
                    if (h != null) h(idx);
                };
                chip.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e) {
                    e.Handled = true;
                    Select(idx, true);
                    var h = Picked;
                    if (h != null) h(idx);
                };
                chip.MouseEnter += delegate {
                    if (idx != sel) {
                        chip.Background = Ui.Card;
                        tb.Foreground = Ui.TextHi;
                    }
                };
                chip.MouseLeave += delegate {
                    if (idx != sel) {
                        chip.Background = Ui.Pill;
                        tb.Foreground = Ui.Desc;
                    }
                };
                chips.Add(chip);
                labels.Add(tb);
                wp.Children.Add(chip);
            }
        }

        public void Select(int i, bool animate) {
            sel = i;
            for (int j = 0; j < chips.Count; j++) {
                bool isSel = (j == i);
                chips[j].Background = isSel ? Ui.Accent : Ui.Pill;
                chips[j].BorderBrush = isSel ? Ui.Accent : Ui.Line;
                labels[j].Foreground = isSel ? Brushes.White : Ui.Desc;
                labels[j].FontWeight = isSel ? FontWeights.SemiBold : FontWeights.Medium;
            }
        }
    }

    sealed class Osd : Window {
        readonly WPath icon;
        readonly TextBlock txt, sub;
        readonly DispatcherTimer hide;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int idx, int val);
        public Osd() {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Opacity = 0;
            IsHitTestVisible = false;
            var box = new Border { CornerRadius = new CornerRadius(14), Background = Ui.Brush("#F2161311"), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Padding = new Thickness(18, 12, 20, 12) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            icon = new WPath { StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 18, Height = 18, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt = new TextBlock { FontFamily = Ui.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Ui.TextB };
            sub = new TextBlock { FontFamily = Ui.UiFont, FontSize = 12.5, Foreground = Ui.Sub, Margin = new Thickness(0, 3, 0, 0) };
            col.Children.Add(txt);
            col.Children.Add(sub);
            sp.Children.Add(icon);
            sp.Children.Add(col);
            box.Child = sp;
            Content = box;
            hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
            hide.Tick += delegate { hide.Stop(); var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)); a.Completed += delegate { if (Opacity < 0.05) Hide(); }; BeginAnimation(OpacityProperty, a); };
            SourceInitialized += delegate {
                var h = new WindowInteropHelper(this).Handle;
                SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x08000000 | 0x00000080);
            };
        }
        public void Flash(string title, string detail, Color c, string pathData) {
            txt.Text = title;
            sub.Text = detail;
            sub.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
            icon.Data = Geometry.Parse(pathData);
            icon.Stroke = Ui.Brush(c);
            if (!IsVisible) { Opacity = 0; Show(); }
            UpdateLayout();
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2;
            Top = wa.Bottom - ActualHeight - 72;
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            hide.Stop();
            hide.Start();
        }
    }
}
