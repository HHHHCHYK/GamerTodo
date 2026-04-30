using System.Threading;
using System.Threading.Tasks;

namespace HeyeTodo.Client.Persistence;

public interface IPersistenceStore
{
    Task<T?> LoadAsync<T>(string moduleId, CancellationToken cancellationToken = default);

    Task SaveAsync<T>(string moduleId, T data, CancellationToken cancellationToken = default);
}
