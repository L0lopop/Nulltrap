namespace Nulltrap.Platform.Abstractions;

public interface IDeferredRemover
{
    bool RemoveAfterExit(string path);
}
