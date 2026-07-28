namespace Rvt.Storage;

public sealed class ObjectStorageClientFactory : IObjectStorageClientFactory
{
    private readonly IReadOnlyDictionary<string, IObjectStorageClient> clients;

    public ObjectStorageClientFactory(
        IEnumerable<ObjectStorageClientRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        Dictionary<string, IObjectStorageClient> registeredClients = new Dictionary<string, IObjectStorageClient>(StringComparer.Ordinal);
        foreach (ObjectStorageClientRegistration registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);
            if (string.IsNullOrWhiteSpace(registration.ResourceName))
            {
                throw new ArgumentException("Object storage resource name cannot be blank.", nameof(registrations));
            }

            if (!registeredClients.TryAdd(registration.ResourceName, registration.Client))
            {
                throw new ArgumentException(
                    $"Object storage resource '{registration.ResourceName}' is registered more than once.",
                    nameof(registrations));
            }
        }

        clients = registeredClients;
    }

    public IObjectStorageClient GetRequiredClient(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException("Object storage resource name cannot be blank.", nameof(resourceName));
        }

        return clients.TryGetValue(resourceName, out IObjectStorageClient? client)
            ? client
            : throw new InvalidOperationException(
                $"Object storage resource '{resourceName}' is not registered.");
    }
}
