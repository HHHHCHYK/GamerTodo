namespace HeyeTodo.Client.ViewModels;

public sealed class GanttDayColumnViewModel
{
    public GanttDayColumnViewModel(string dayName, string dateText, bool isToday, double left)
    {
        DayName = dayName;
        DateText = dateText;
        IsToday = isToday;
        Left = left;
    }

    public string DayName { get; }

    public string DateText { get; }

    public bool IsToday { get; }

    public double Left { get; }
}
