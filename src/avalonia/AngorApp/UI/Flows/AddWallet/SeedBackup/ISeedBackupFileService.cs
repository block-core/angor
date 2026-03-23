namespace AngorApp.UI.Flows.AddWallet.SeedBackup;

public interface ISeedBackupFileService
{
    Task Save(string seedwords);
}
