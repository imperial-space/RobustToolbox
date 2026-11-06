using Robust.Shared.Configuration;

namespace Robust.Server.Imperial.Serialization;

/// <summary>
/// CVars of the server-side deserialization guard, see <see cref="RecursionGuardStream"/>.
/// </summary>
[CVarDefs]
internal static class SerializationICCvars
{
    /// <summary>
    /// Stack budget in kilobytes for deserializing one piece of client data. Legitimate data needs a few kilobytes,
    /// so anything over the budget is treated as a crash attempt and the sender is disconnected.
    /// 0 disables the budget and leaves only the end-of-stack backstop.
    /// </summary>
    public static readonly CVarDef<int> NetMaxDeserializeStack = CVarDef.Create("net.max_deserialize_stack_kb", 256, CVar.SERVERONLY);
}
