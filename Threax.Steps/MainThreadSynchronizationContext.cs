using System.Collections.Concurrent;

namespace Threax.Steps;

//Modified from
//https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/

/// <summary>
/// This class will allow async await to work from the main thread and keep the tasks on that thread.
/// Tasks can get queued up and will be pumped on demand.
/// </summary>
public class MainThreadSynchronizationContext : SynchronizationContext
{
    private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> m_queue =
       new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();

    private Exception completeException;

    public override void Post(SendOrPostCallback d, object state)
    {
        m_queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
    }

    public override void Send(SendOrPostCallback d, object state)
    {
        //This might be better pooled. I dunno if send is even ever used.
        using (var ev = new ManualResetEventSlim(false))
        {
            m_queue.Add(new KeyValuePair<SendOrPostCallback, object>(s => { d(s); ev.Set(); }, state));
            ev.Wait();
        }
    }

    public void RunOnCurrentThread()
    {
        KeyValuePair<SendOrPostCallback, object> workItem;
        while (m_queue.TryTake(out workItem, Timeout.Infinite))
        {
            workItem.Key(workItem.Value);
        }

        if (completeException != null)
        {
            throw completeException;
        }
    }

    public void Complete()
    {
        m_queue.CompleteAdding();
    }

    public void CompleteWithException(Exception ex)
    {
        completeException = ex;
        Complete();
    }
}
