namespace WinDrop.Cli;

// Every accepted transport stream gets its own async-flow identity. Consent,
// progress, completion and errors keep that identity even when transfers overlap.
public static class UiConnectionContext
{
    public static readonly AsyncLocal<string?> Current = new();
}
