using System;
using System.Threading;
using System.Threading.Tasks;
using Condor.Infrastructure.Retry;

namespace Condor.Infrastructure.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task Execute_ExitosoAlPrimerIntento_DevuelveTrue()
    {
        var calls = 0;

        var ok = await RetryPolicy.ExecuteAsync(
            _ => { calls++; return Task.FromResult(true); },
            3,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Execute_ExitosoAlSegundoIntento_Reintenta()
    {
        var calls = 0;

        var ok = await RetryPolicy.ExecuteAsync(
            _ => { calls++; return Task.FromResult(calls >= 2); },
            3,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Execute_SiempreFalla_AgotaReintentosYDevuelveFalse()
    {
        var calls = 0;

        var ok = await RetryPolicy.ExecuteAsync(
            _ => { calls++; return Task.FromResult(false); },
            3,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.False(ok);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Execute_Cancelacion_DevuelveFalse()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ok = await RetryPolicy.ExecuteAsync(
            _ => Task.FromResult(false),
            3,
            TimeSpan.Zero,
            cts.Token);

        Assert.False(ok);
    }
}
