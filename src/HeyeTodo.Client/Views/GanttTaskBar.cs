using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HeyeTodo.Client.ViewModels;

namespace HeyeTodo.Client.Views;

public sealed class GanttTaskBar : Border
{
    public static readonly StyledProperty<GanttTaskItemViewModel?> TaskProperty =
        AvaloniaProperty.Register<GanttTaskBar, GanttTaskItemViewModel?>(nameof(Task));

    public GanttTaskItemViewModel? Task
    {
        get => GetValue(TaskProperty);
        set => SetValue(TaskProperty, value);
    }

    private Point? _dragStart;
    private GanttDragMode _dragMode;

    public GanttTaskBar()
    {
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Task is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        _dragStart = e.GetPosition(this);
        _dragMode = point.X switch
        {
            <= 8 => GanttDragMode.ResizeStart,
            var x when x >= Bounds.Width - 8 => GanttDragMode.ResizeEnd,
            _ => GanttDragMode.Move,
        };
        Cursor = new Cursor(_dragMode == GanttDragMode.Move ? StandardCursorType.Hand : StandardCursorType.SizeWestEast);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is null)
        {
            var point = e.GetPosition(this);
            Cursor = point.X <= 8 || point.X >= Bounds.Width - 8
                ? new Cursor(StandardCursorType.SizeWestEast)
                : new Cursor(StandardCursorType.Hand);
        }
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Task is null || _dragStart is null || DataContext is not GanttTaskItemViewModel task)
        {
            ResetDrag(e.Pointer);
            return;
        }

        var viewModel = FindDataContext<GanttViewModel>();
        if (viewModel is null)
        {
            ResetDrag(e.Pointer);
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = current.X - _dragStart.Value.X;
        var startDelta = _dragMode is GanttDragMode.Move or GanttDragMode.ResizeStart ? deltaX : 0;
        var endDelta = _dragMode is GanttDragMode.Move or GanttDragMode.ResizeEnd ? deltaX : 0;
        ResetDrag(e.Pointer);
        await viewModel.RescheduleTaskAsync(task.Id, startDelta, endDelta);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragStart = null;
        _dragMode = GanttDragMode.None;
        Cursor = null;
    }

    private void ResetDrag(IPointer pointer)
    {
        _dragStart = null;
        _dragMode = GanttDragMode.None;
        Cursor = null;
        pointer.Capture(null);
    }

    private T? FindDataContext<T>() where T : class
    {
        StyledElement? current = this;
        while (current is not null)
        {
            if (current.DataContext is T value)
            {
                return value;
            }

            current = current.Parent as StyledElement;
        }

        return null;
    }

    private enum GanttDragMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd,
    }
}
