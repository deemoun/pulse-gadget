namespace PulseGadget.Core.Abstractions;

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Success(string message);
    void Debug(string message);
}
