using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HeyeTodo.Client.ViewModels;

namespace HeyeTodo.Client.Views;

public partial class GanttChartView : UserControl
{
    private Point _dragStartPoint;
    private GanttTaskBarViewModel? _dragBar;
    private GanttDragMode _dragMode;

    public GanttChartView()
    {
        InitializeComponent();
    }

    private void OnGanttBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: GanttTaskBarViewModel bar } control)
        {
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _dragBar = bar;
        _dragMode = ResolveDragMode(e.GetPosition(control).X, control.Bounds.Width);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnGanttBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragBar is null)
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        var dayDelta = (int)Math.Round((currentPoint.X - _dragStartPoint.X) / ResolveDragDayWidth());

        if (DataContext is GanttChartViewModel viewModel)
        {
            if (dayDelta == 0)
            {
                viewModel.OpenTaskDetails(_dragBar.Task);
            }
            else
            {
                viewModel.ApplyDrag(_dragBar, dayDelta, _dragMode);
            }
        }

        e.Pointer.Capture(null);
        _dragBar = null;
        e.Handled = true;
    }

    private double ResolveDragDayWidth()
    {
        return DataContext is GanttChartViewModel viewModel ? viewModel.DragDayWidth : 112;
    }

    private static GanttDragMode ResolveDragMode(double localX, double width)
    {
        const double resizeEdgeWidth = 12;
        if (localX <= resizeEdgeWidth)
        {
            return GanttDragMode.ResizeStart;
        }

        return localX >= width - resizeEdgeWidth ? GanttDragMode.ResizeEnd : GanttDragMode.Move;
    }
}
