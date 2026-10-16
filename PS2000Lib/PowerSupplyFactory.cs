namespace PS2000Lib;

public static class PowerSupplyFactory
{
    public static IPowerSupply Create(bool dummy = false)
    {
        return dummy ? new DummyPowerSupply() : new PS2000();
    }
}
