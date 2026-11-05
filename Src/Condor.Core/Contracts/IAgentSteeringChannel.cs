using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Condor.Core.Contracts;

public interface IAgentSteeringChannel
{
    bool TryDequeue(out string? instruction);
    ValueTask<string> ReadNextAsync(CancellationToken cancellationToken = default);
}

/// <summary>Thread-safe channel for user requirements submitted while the agent is running.</summary>
public sealed class AgentSteeringChannel : IAgentSteeringChannel
{
    private readonly Channel<string> _instructions = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public void Submit(string instruction)
    {
        if (!string.IsNullOrWhiteSpace(instruction))
            _instructions.Writer.TryWrite(instruction.Trim());
    }

    public bool TryDequeue(out string? instruction)
    {
        var found = _instructions.Reader.TryRead(out var value);
        instruction = value;
        return found;
    }

    public ValueTask<string> ReadNextAsync(CancellationToken cancellationToken = default)
        => _instructions.Reader.ReadAsync(cancellationToken);
}
