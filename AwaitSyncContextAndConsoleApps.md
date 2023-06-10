# Await, SynchronizationContext, and Console Apps

Stephen Toub - MSFT

January 20th, 2012

When I discuss the new async language features of C# and Visual Basic, one of the attributes I ascribe to the await keyword is that it “tries to bring you back to where you were.” For example, if you use await on the UI thread of your WPF application, the code that comes after the await completes should run back on that same UI thread.

There are several mechanisms that are used by the async/await infrastructure under the covers to make this marshaling work: SynchronizationContext and TaskScheduler. While the transformation is much more complicated than what I’m about to show, logically you can think of the following code:

```
await FooAsync();
RestOfMethod();
as being similar in nature to this:
var t = FooAsync();
var currentContext = SynchronizationContext.Current;
t.ContinueWith(delegate
{
    if (currentContext == null)
        RestOfMethod();
    else
        currentContext.Post(delegate { RestOfMethod(); }, null);
}, TaskScheduler.Current);
```

In other words, before the async method yields to asynchronously wait for the Task ‘t’, we capture the current SynchronizationContext. When the Task being awaited completes, a continuation will run the remainder of the asynchronous method. If the captured SynchronizationContext was null, then RestOfMethod() will be executed in the original TaskScheduler (which is often TaskScheduler.Default, meaning the ThreadPool). If, however, the captured context wasn’t null, then the execution of RestOfMethod() will be posted to the captured context to run there.

Both SynchronizationContext and TaskScheduler are abstractions that represent a “scheduler”, something that you give some work to, and it determines when and where to run that work. There are many different forms of schedulers. For example, the ThreadPool is a scheduler: you call ThreadPool.QueueUserWorkItem to supply a delegate to run, that delegate gets queued, and one of the ThreadPool’s threads eventually picks up and runs that delegate. Your user interface also has a scheduler: the message pump. A dedicated thread sits in a loop, monitoring a queue of messages and processing each; that loop typically processes messages like mouse events or keyboard events or paint events, but in many frameworks you can also explicitly hand it work to do, e.g. the Control.BeginInvoke method in Windows Forms, or the Dispatcher.BeginInvoke method in WPF.

SynchronizationContext, then, is just an abstract class that can be used to represent such a scheduler. The base class exposes several virtual methods, but we’ll focus on just one: Post. Post accepts a delegate, and the implementation of Post gets to decide when and where to run that delegate. The default implementation of SynchronizationContext.Post just turns around and passes it off to the ThreadPool via QueueUserWorkItem. But frameworks can derive their own context from SynchronizationContext and override the Post method to be more appropriate to the scheduler being represented. In the case of Windows Forms, for example, the WindowsFormsSynchronizationContext implements Post to pass the delegate off to Control.BeginInvoke. For DispatcherSynchronizationContext in WPF, it calls to Dispatcher.BeginInvoke. And so on.

That’s how await “brings you back to where you were.” It asks for the SynchronizationContext that’s representing the current environment, and then when the await completes, the continuation is posted back to that context. It’s up to the implementation of the captured context to run the delegate in the right place, e.g. in the case of a UI app, that means running the delegate on the UI thread. This explanation also helps to highlight what happens if the environment didn’t set a SynchronizationContext onto the current thread (and if there’s not special TaskScheduler, as there isn’t in this case). If the context comes back as null, then the continuation could run “anywhere”. I put anywhere in quotes because obviously the continuation can’t run “anywhere,” but logically you can think of it like that… it’ll either end up running on the same thread that completed the awaited task, or it’ll end up running in the ThreadPool.

All of the UI application types you can create in Visual Studio will end up having a special SynchronizationContext published on the UI thread. Windows Forms, Windows Presentation Foundation, Metro style apps… they all have one. But there’s one common kind of application that doesn’t have a SynchronizationContext: console apps. When your console application’s Main method is invoked, SynchronizationContext.Current will return null. That means that if you invoke an asynchronous method in your console app, unless you do something special, your asynchronous methods will not have thread affinity: the continuations within those asynchronous methods could end up running “anywhere.”

As an example, consider this application:

```
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static void Main()
    {
        DemoAsync().Wait();
    }

    static async Task DemoAsync()
    {
        var d = new Dictionary<int, int>();
        for (int i = 0; i < 10000; i++)
        {
            int id = Thread.CurrentThread.ManagedThreadId;
            int count;
            d[id] = d.TryGetValue(id, out count) ? count+1 : 1;

            await Task.Yield();
        }

        foreach (var pair in d) Console.WriteLine(pair);
    }
}
```

Here I’ve created a dictionary that maps thread IDs to the number of times we encountered that particular thread. For thousands of iterations, I get the current thread’s ID and increment the appropriate element of my histogram, then yield. The act of yielding will use a continuation to run the remainder of the method. Here’s some representative output I see from executing this app:

```
[1, 1]
[3, 2687]
[4, 2399]
[5, 2397]
[6, 2516]
Press any key to continue . . .
```

We can see here that the execution of this code used 5 threads over the course of its run. Interestingly, one of the threads only had one hit. Can you guess which thread that was? It’s the thread running the Main method of the console app. When we call DemoAsync, it runs synchronously until the first await the yields, so the first time we check the ManagedThreadId for the current thread, we’re still on the thread that invoked DemoAsync. Once we hit the await, the method returns back to Main(), which then blocks waiting on the returned Task to complete. The continuations used by the remainder of the async method’s execution would have been posted to SynchronizationContext.Current, except that it a console app, it’s null (unless you explicitly override that with SynchronizationContext.SetSynchronizationContext). So the continuations just get scheduled to run on the ThreadPool. That’s where the rest of those threads are coming from… they’re all ThreadPool threads.

Is it a problem then that using async like this in a console app might end up running continuations on ThreadPool threads? I can’t answer that, because the answer is entirely up to what kind of semantics you need in your application. For many applications, this will be perfectly reasonable behavior. Other applications, however, may require thread affinity, such that all of the continuations run on the same thread. For example, if you invoked multiple async methods concurrently, you might want all the continuations they use to be serialized, and an easy way to guarantee that is to ensure that only one thread is used for executing all of the continuations. If your application does demand such behavior, are you out of luck? Thankfully, the answer is ‘no’. You can add such behavior yourself.

If you’ve made it this far in reading, hopefully the components of a solution here have started to become obvious. You effectively need a message pump, a scheduler, something that runs on the Main thread of your app processing a queue of work. And you need a SynchronizationContext (or a TaskScheduler if you prefer) that feeds the await continuations into that queue. With that framework in place, let’s build a solution.

First, we need our SynchronizationContext. As described in the previous paragraph, we’ll need a queue to store the work to be done. The work provided to the Post method comes in the form of two objects: a SendOrPostCallback delegate, and an object state that is meant to be passed into that delegate when it’s invoked. As such, we’ll have our queue store a KeyValuePair<TKey,TValue> of these two objects. What kind of queue data structure should we use? We need something ideally suited to handle producer/consumer scenarios, as our asynchronous method will be “producing” these pairs of work, and our pumping loop will need to be “consuming” them from the queue and executing them. .NET 4 saw the introduction of the perfect type for the job: BlockingCollection<T>. BlockingCollection<T> is a data structure that encapsulates not only a queue, but also all of the synchronization necessary to coordinate between a producer adding elements to that queue and a consumer removing them, including blocking the consumer attempting a removal while the queue is empty.

With that, the pieces fall into place: a BlockingCollection<KeyValuePair<SendOrPostCallback,object>> instance; a Post method that adds to the queue; another method that sits in a consuming loop, removing each work item and processing it; and finally another method that lets the queue know that no more work will arrive, allowing the consuming loop to exit once the queue is empty.

```
private sealed class SingleThreadSynchronizationContext :  
    SynchronizationContext
{
    private readonly
     BlockingCollection<KeyValuePair<SendOrPostCallback,object>>
      m_queue =
       new BlockingCollection<KeyValuePair<SendOrPostCallback,object>>();

    public override void Post(SendOrPostCallback d, object state)
    {
        m_queue.Add(
            new KeyValuePair<SendOrPostCallback,object>(d, state));
    }

    public void RunOnCurrentThread()
    {
        KeyValuePair<SendOrPostCallback, object> workItem;
        while(m_queue.TryTake(out workItem, Timeout.Infinite))
            workItem.Key(workItem.Value);
    }

    public void Complete() { m_queue.CompleteAdding(); }
}
```

Believe it or not, we’re already half done with our solution. We need to instantiate one of these contexts and set it as current onto the current thread, so that when we then invoke the asynchronous method, that method’s awaits will see this context as Current. We need to alert the context to when there won’t be any more work arriving, which we can do by using a continuation to call Complete on our context when the Task returned from the async method is compelted. We need to run the processing loop via the context’s RunOnCurrentThread method. And we need to propagate any exceptions that may have occurred during the async method’s processing. All in all, it’s just a few lines:

```
public static void Run(Func<Task> func)
{
    var prevCtx = SynchronizationContext.Current;
    try
    {
        var syncCtx = new SingleThreadSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(syncCtx);

        var t = func();
        t.ContinueWith(
            delegate { syncCtx.Complete(); }, TaskScheduler.Default);

        syncCtx.RunOnCurrentThread();

        t.GetAwaiter().GetResult();
    }

    finally { SynchronizationContext.SetSynchronizationContext(prevCtx); }
}
```

That’s it. With our solution now available, I can change the Main method of my demo console app from:

```
static void Main()
{
    DemoAsync().Wait();
}
```

to instead use our new AsyncPump.Run method:

```
static void Main()
{
    AsyncPump.Run(async delegate
    {
        await DemoAsync();
    });
}
```

When I then run my app again, this time I get the following output:

```
[1, 10000]
Press any key to continue . . .
```

As you can see, all of the continuations have run on just one thread, the main thread of my console app.

The AsyncPump sample class described in this post is available as an attachment to this post.


## Footnote
I am not the author of this information that credit belongs to Stephen Toub. However, its state on its [official page](https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/) is not good. The code examples do not render correctly and the AsyncPump.cs mentioned is a dead link. This information is far too useful to be lost if that blog gets cleaned up and due to its relevance to the code in this repo it seemed valuable to reproduce it here.