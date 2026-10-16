namespace PS2000Lib;

internal class DummyPowerSupply : IPowerSupply
{
    bool _remote, _output;
    double _setpoint;

    public float NominalVoltage { get; private set; }
    public string DeviceType { get; private set; } = "";
    public string SerialNumber { get; private set; } = "";
    public string ArticleNumber { get; private set; } = "";
    public string PortName { get; private set; } = "";

    public void Connect()
    {
        NominalVoltage = 84f;
        DeviceType = "PS 2084-03 B (dummy)";
        SerialNumber = "DUMMY001";
        ArticleNumber = "00000000";
        PortName = "dummy";
    }

    public PsuStatus ReadStatus()
    {
        return new PsuStatus(_remote, _output, _output ? _setpoint : 0);
    }

    public double GetVoltageSetpoint()
    {
        return _setpoint;
    }

    public void SetRemote(bool on)
    {
        _remote = on;
    }

    public void SetOutput(bool on)
    {
        if (!_remote)
            throw new IOException("Error 0x09 writing output: remote not enabled");
        _output = on;
    }

    public void SetVoltage(double voltage)
    {
        if (!_remote)
            throw new IOException("Error 0x09 writing voltage: remote not enabled");
        _setpoint = Math.Clamp(voltage, 0, NominalVoltage);
    }
}
