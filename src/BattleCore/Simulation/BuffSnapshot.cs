namespace BattleCore.Simulation;

public sealed record BuffSnapshot(
    long InstanceId,
    string BuffId,
    int Stacks,
    long ExpiryFrame);
