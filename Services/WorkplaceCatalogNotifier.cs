namespace FanShop.Services;

public interface IWorkplaceCatalogObserver
{
    void RefreshWorkplaceCatalog();
}

public static class WorkplaceCatalogNotifier
{
    private static readonly List<WeakReference<IWorkplaceCatalogObserver>> Observers = [];

    public static void Register(IWorkplaceCatalogObserver observer)
    {
        Cleanup();
        if (Observers.Any(reference =>
                reference.TryGetTarget(out var target) &&
                ReferenceEquals(target, observer)))
            return;

        Observers.Add(new WeakReference<IWorkplaceCatalogObserver>(observer));
    }

    public static void NotifyChanged()
    {
        foreach (var reference in Observers.ToList())
        {
            if (reference.TryGetTarget(out var observer))
                observer.RefreshWorkplaceCatalog();
        }

        Cleanup();
    }

    private static void Cleanup() =>
        Observers.RemoveAll(reference => !reference.TryGetTarget(out _));
}
