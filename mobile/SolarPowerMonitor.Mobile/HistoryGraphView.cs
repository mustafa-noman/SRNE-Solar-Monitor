using System.Collections.Specialized;
using Microsoft.Maui.Graphics;

namespace SolarPowerMonitor.Mobile;

public sealed class HistoryGraphView : GraphicsView
{
    public HistoryGraphView()
    {
        Drawable = new HistoryDrawable();
        DashboardViewModel.Current.History.CollectionChanged += OnHistoryChanged;
        Unloaded += (_, _) => DashboardViewModel.Current.History.CollectionChanged -= OnHistoryChanged;
    }

    private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e) => Invalidate();

    private sealed class HistoryDrawable : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeColor = Color.FromArgb("#203B58");
            canvas.StrokeSize = 1;
            for (var i = 1; i <= 3; i++)
            {
                var y = dirtyRect.Top + dirtyRect.Height * i / 4;
                canvas.DrawLine(dirtyRect.Left, y, dirtyRect.Right, y);
            }

            var points = DashboardViewModel.Current.History.Take(20).Reverse().ToArray();
            if (points.Length < 2) return;

            var min = points.Min(point => point.SolarWatts);
            var max = points.Max(point => point.SolarWatts);
            var range = Math.Max(1, max - min);
            var path = new PathF();
            for (var i = 0; i < points.Length; i++)
            {
                var x = dirtyRect.Left + dirtyRect.Width * i / (points.Length - 1);
                var y = dirtyRect.Bottom - 8 - (points[i].SolarWatts - min) * (dirtyRect.Height - 16) / range;
                if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }

            canvas.StrokeColor = Color.FromArgb("#FFAF3F");
            canvas.StrokeSize = 4;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
        }
    }
}
