namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    public interface ICycleRuleProvider
    {
        /// <summary>
        /// Returns the rule for a given machine/scope. Implementations should be fast and
        /// return a cached snapshot.
        /// </summary>
        CycleRule? GetRule(string machineCode, string scopeKey);
    }

    public interface IScopeResolver
    {
        /// <summary>
        /// Derive a scope key (e.g., 'SP1', 'SP2', 'Line') from a tag sample.
        /// </summary>
        string ResolveScope(TagSample sample);
    }

    public interface IProductRunTracker
    {
        TrackResult Process(TagSample sample);
    }
}
