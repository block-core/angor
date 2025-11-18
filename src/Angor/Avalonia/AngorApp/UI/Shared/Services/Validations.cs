using System.Net.Http;
using System.Threading.Tasks;

namespace AngorApp.UI.Shared.Services;

public class Validations : IValidations
{
    private readonly Nip05Validator nip05Validator;
    private readonly LightningAddressValidator lightningAddressValidator;
    private readonly IImageValidationService imageValidationService;

    public Validations(IHttpClientFactory httpClientFactory, IImageValidationService imageValidationService)
    {
        nip05Validator = new Nip05Validator(httpClientFactory);
        lightningAddressValidator = new LightningAddressValidator(httpClientFactory);
        this.imageValidationService = imageValidationService;
    }
    
    public Task<Result> CheckNip05Username(string username, string nostrPubKey)
    {
        return nip05Validator.CheckNip05Username(username, nostrPubKey);
    }

    public Task<Result> CheckLightningAddress(string lightningAddress)
    {
        return lightningAddressValidator.CheckLightningAddress(lightningAddress);
    }

    public Task<Result<bool>> IsImage(string url)
    {
        return imageValidationService.IsImage(url);
    }
}