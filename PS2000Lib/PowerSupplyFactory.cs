namespace PS2000Lib;

public static class PowerSupplyFactory
{
    public static IPowerSupply Create(bool dummy = false)
    {
        return dummy ? new DummyPowerSupply() : new PS2000();
    }

    // essaie la vraie alim, et bascule sur le dummy si aucune ne répond (pour travailler hors du labo)
    public static IPowerSupply ConnectOrDummy()
    {
        IPowerSupply powerSupply = new PS2000();
        try
        {
            powerSupply.Connect();
        }
        catch (IOException)
        {
            powerSupply = new DummyPowerSupply();
            powerSupply.Connect();
        }
        return powerSupply;
    }
}
