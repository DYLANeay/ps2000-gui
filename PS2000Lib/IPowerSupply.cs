namespace PS2000Lib;

public interface IPowerSupply
{
    float NominalVoltage { get; }
    string DeviceType { get; }
    string SerialNumber { get; }
    string ArticleNumber { get; }

    void Connect();
    PsuStatus ReadStatus();
    double GetVoltageSetpoint();
    void SetOutput(bool on);
    void SetVoltage(double voltage);
    void SetRemote(bool on);
}
