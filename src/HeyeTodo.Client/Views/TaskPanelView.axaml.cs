using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using HeyeTodo.Client.ViewModels;

namespace HeyeTodo.Client.Views;

public partial class TaskPanelView : UserControl
{
    private static readonly IBrush NormalCardBackground = Brush.Parse("#FFFFFB");
    private static readonly IBrush HoverCardBackground = Brush.Parse("#FFF7EF");
    private static readonly IBrush PressedCardBackground = Brush.Parse("#FFD1B3");
    private static readonly IBrush NormalCardBorder = Brush.Parse("#F0B392");
    private static readonly IBrush ActiveCardBorder = Brush.Parse("#FFB37A");

    public TaskPanelView()
    {
        InitializeComponent();
    }

    private void OnTaskCardPointerEntered(object? sender, PointerEventArgs e)
    {
        SetTaskCardVisual(sender, HoverCardBackground, ActiveCardBorder);
    }

    private void OnTaskCardPointerExited(object? sender, PointerEventArgs e)
    {
        SetTaskCardVisual(sender, NormalCardBackground, NormalCardBorder);
    }

    private void OnTaskCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFromTaskCheckButton(e.Source))
        {
            return;
        }

        SetTaskCardVisual(sender, PressedCardBackground, ActiveCardBorder);
    }

    private void OnTaskCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        SetTaskCardVisual(sender, HoverCardBackground, ActiveCardBorder);

        if (IsFromTaskCheckButton(e.Source))
        {
            return;
        }

        if (sender is not Control { DataContext: TaskItemViewModel task })
        {
            return;
        }

        if (DataContext is TaskPanelViewModel viewModel)
        {
            viewModel.SelectTask(task);
            e.Handled = true;
        }
    }

    private static bool IsFromTaskCheckButton(object? source)
    {
        if (source is not Control control)
        {
            return false;
        }

        return control.FindAncestorOfType<Button>(includeSelf: true)?.Classes.Contains("TaskCheck") == true;
    }

    private static void SetTaskCardVisual(object? sender, IBrush background, IBrush borderBrush)
    {
        if (sender is Border border)
        {
            border.Background = background;
            border.BorderBrush = borderBrush;
        }
    }
}
