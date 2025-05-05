namespace Angor.UI.Model.Implementation.Wallet.Password;

public interface IIcon
{
    string Key { get; }
}

public class IconDesign : IIcon
{
    public string Key { get; set; }
}

public class Icon : IIcon
{
    public string Key { get; }

    public Icon(string key)
    {
        Key = key;
    }
}