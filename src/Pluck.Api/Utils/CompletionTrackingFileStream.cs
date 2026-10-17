namespace Pluck.Api.Utils;

/// <summary>
/// Custom filestream for running a callback function when the stream gets to the end of the file
/// </summary>
public class CompletionTrackingFileStream(string path, FileMode mode, FileAccess access, Func<Task> onEndOfFile)
    : FileStream(path, mode, access)
{
    private bool _callbackFired;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await base.ReadAsync(buffer, cancellationToken);

        if (!_callbackFired && Position >= Length)
        {
            _callbackFired = true;
            await onEndOfFile();
        }

        return bytesRead;
    }
}