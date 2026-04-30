using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HeyeTodo.Client.Persistence;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class TestPageViewModel : ViewModelBase
{
    private const string ModuleId = "test.persistence";

    private readonly IPersistenceStore _persistenceStore;
    private CancellationTokenSource? _saveCancellationTokenSource;
    private bool _isLoading;

    public TestPageViewModel(IPersistenceStore persistenceStore)
    {
        _persistenceStore = persistenceStore;
        _ = LoadAsync();
    }

    [ObservableProperty]
    private string _persistenceText = string.Empty;

    [ObservableProperty]
    private string _persistenceStatus = "正在读取测试持久化内容";

    partial void OnPersistenceTextChanged(string value)
    {
        if (_isLoading)
        {
            return;
        }

        QueueSave();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;

        try
        {
            var state = await _persistenceStore.LoadAsync<PersistenceTestState>(ModuleId);
            PersistenceText = state?.Text ?? string.Empty;
            PersistenceStatus = state is null ? "还没有测试持久化内容" : "已读取测试持久化内容";
        }
        catch (PersistenceException exception)
        {
            PersistenceStatus = exception.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void QueueSave()
    {
        _saveCancellationTokenSource?.Cancel();
        _saveCancellationTokenSource?.Dispose();
        _saveCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _saveCancellationTokenSource.Token;

        _ = SaveAfterDelayAsync(cancellationToken);
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            PersistenceStatus = "正在自动保存";
            await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
            await _persistenceStore.SaveAsync(ModuleId, new PersistenceTestState { Text = PersistenceText }, cancellationToken);
            PersistenceStatus = "已自动保存";
        }
        catch (OperationCanceledException)
        {
        }
        catch (PersistenceException exception)
        {
            PersistenceStatus = exception.Message;
        }
    }
}
