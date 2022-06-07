using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Mentalist.BusinessCache;

internal static class Executor
{
    public static async Task Execute(Func<Task> action, ILogger logger, CancellationToken token, string errorMessage, params object[] errorMessageArgs)
    {
        var timer = Stopwatch.StartNew();
        var finished = false;
        while (!finished)
        {
            try
            {
                await action();
                finished = true;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (timer.Elapsed > TimeSpan.FromMinutes(1))
                {
                    logger.LogError(e, errorMessage, errorMessageArgs);
                    timer.Restart();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
    }
}