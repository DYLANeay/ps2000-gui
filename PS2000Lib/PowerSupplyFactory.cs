namespace PS2000Lib;

public static class PowerSupplyFactory
{
    public static IPowerSupply Create()
    {
        return new PS2000();
    }
}
