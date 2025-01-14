public interface IFounderKeyService
{
    Task<bool> IsFounderKeyInUseAsync(string founderKey);
    
    Task<FounderKeyCheckResult> CheckFounderKeyAsync(string founderKey);

}