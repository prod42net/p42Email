using System;

namespace p42Email.Interfaces;

public interface IEmailEvents
{
    event Action<int>? NewMailDetected;
    void RaiseNewMailDetected(int newUnseenCount);
}

public sealed class EmailEvents : IEmailEvents
{
    public event Action<int>? NewMailDetected;
    public void RaiseNewMailDetected(int newUnseenCount) => NewMailDetected?.Invoke(newUnseenCount);
}
