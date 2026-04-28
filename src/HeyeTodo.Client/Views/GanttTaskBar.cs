using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HeyeTodo.Client.ViewModels;

namespace HeyeTodo.Client.Views;

/// <summary>
/// 甘特图中的单条任务条控件，支持拖拽移动任务位置和拖拽两端调整任务时间范围。
/// 按下时根据指针在条上的水平位置判定操作模式：左侧边缘为调整开始时间，右侧边缘为调整结束时间，中间为整体移动。
/// </summary>
public sealed class GanttTaskBar : Border
{
    /// <summary>
    /// 绑定到当前任务条的任务数据。
    /// </summary>
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

    /// <summary>
    /// 指针按下时记录拖拽起始位置，根据指针所在区域判定操作模式：
    /// 左侧 8px 以内为调整开始时间、右侧 8px 以内为调整结束时间、中间为整体移动。
    /// </summary>
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

    /// <summary>
    /// 指针在任务条上移动时更新光标样式：边缘区域显示水平调整箭头，中间显示抓手。
    /// 拖拽中时由 OnPointerMoved 不更新光标（由 OnPointerPressed 设定）。
    /// </summary>
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

    /// <summary>
    /// 指针释放时根据拖拽模式计算位移量，调用 ViewModel 的 RescheduleTaskAsync 执行任务重新排期。
    /// 整体移动模式将位移同时作用于开始和结束时间；调整模式仅作用于对应端。
    /// </summary>
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

    /// <summary>
    /// 指针捕获丢失时重置拖拽状态和光标。
    /// </summary>
    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragStart = null;
        _dragMode = GanttDragMode.None;
        Cursor = null;
    }

    /// <summary>
    /// 重置拖拽状态，释放捕获并还原光标。
    /// </summary>
    private void ResetDrag(IPointer pointer)
    {
        _dragStart = null;
        _dragMode = GanttDragMode.None;
        Cursor = null;
        pointer.Capture(null);
    }

    /// <summary>
    /// 沿可视化树向上查找指定类型的 DataContext，用于在嵌套控件中找到甘特图的 ViewModel。
    /// </summary>
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

    /// <summary>
    /// 甘特图拖拽操作模式。
    /// </summary>
    private enum GanttDragMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd,
    }
}
