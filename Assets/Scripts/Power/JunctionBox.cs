namespace Voidborne.Power
{
    /// <summary>
    /// V8.3 — Junction Box. Pure pass-through power node: not a generator,
    /// not a consumer, not storage. Exists so multiple cables can fan out
    /// from a single physical location. The graph treats it as any other
    /// node; <see cref="PowerNetwork"/> uses it during BFS to bridge
    /// connected components without changing their generation / demand
    /// totals.
    /// </summary>
    /// <remarks>
    /// Trivial M2 implementation; M7 polish can add a "max-throughput"
    /// stat if junction boxes themselves become a balance point.
    /// </remarks>
    public class JunctionBox : PowerNode
    {
        protected override void InitializeRole()
        {
            // Junction is neutral -- all three role flags stay false.
            isGenerator = false;
            isConsumer = false;
            isStorage = false;
            base.InitializeRole();
        }
    }
}
