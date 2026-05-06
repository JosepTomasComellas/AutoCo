namespace AutoCo.Api.Services;

public sealed class LogLevelHolder
{
    private volatile int _level = (int)LogLevel.Warning;
    public LogLevel Level { get => (LogLevel)_level; set => _level = (int)value; }
}
