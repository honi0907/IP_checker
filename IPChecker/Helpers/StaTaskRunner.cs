namespace IPChecker.Helpers;

public static class StaTaskRunner
{
    public static Task<T> RunAsync<T>(Func<T> func)
    {
        return Task.Factory.StartNew(
            () =>
            {
                T? result = default;
                Exception? error = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        result = func();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                if (error is not null)
                {
                    throw error;
                }

                return result!;
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);
    }
}
