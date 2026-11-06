// Imperial Space Start
using System.IO;
using Robust.Server.Imperial.Serialization;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
// Imperial Space End
using Robust.Shared.Serialization;

namespace Robust.Server.Serialization;

// Imperial Space Start
// internal sealed partial class ServerRobustSerializer : RobustSerializer
internal sealed partial class ServerRobustSerializer : RobustSerializer, IRobustSerializer
// Imperial Space End
{
    // Who doesn't love a good empty server/client specific class that just exists so that everything can be sealed?

    // Imperial Space Start
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private long _maxDeserializeStackBytes;

    /// <summary>
    /// Re-implements <see cref="IRobustSerializer.Initialize"/> to also set up the deserialization guard,
    /// because its CVar is only registered by the time the server initializes the serializer.
    /// </summary>
    void IRobustSerializer.Initialize()
    {
        Initialize();

        _sawmill = _logManager.GetSawmill("szr.guard");
        _configurationManager.OnValueChanged(SerializationICCvars.NetMaxDeserializeStack, OnMaxDeserializeStackChanged, true);
    }

    private void OnMaxDeserializeStackChanged(int value)
    {
        _maxDeserializeStackBytes = value * 1024L;

        var message = value > 0 ? $"Deserialization guard is ON, limit {value} KB of stack." : "Deserialization guard budget is OFF, end-of-stack backstop only.";
        _sawmill.Info(message);
    }

    /// <summary>
    /// Everything the server deserializes comes from clients, so every read goes through a <see cref="RecursionGuardStream"/>.
    /// </summary>
    protected override Stream WrapDeserializeStream(Stream stream) => new RecursionGuardStream(stream, _maxDeserializeStackBytes, _sawmill);
    // Imperial Space End
}
