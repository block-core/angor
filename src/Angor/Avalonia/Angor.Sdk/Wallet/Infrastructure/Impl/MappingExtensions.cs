using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Dto;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public static class MappingExtensions
{
    public static WalletDescriptorDto ToDto(this WalletDescriptor descriptor) =>
        new(
            descriptor.Network.ToString(), 
            descriptor.XPubs.Select(x => x.ToDto())
        );

    public static XPubDto ToDto(this XPub xpub) =>
        new XPubDto(
            xpub.Value,
            xpub.ScriptType,
            new DerivationPathDto(xpub.Path.Purpose, xpub.Path.CoinType, xpub.Path.Account)
        );

    public static Result<WalletDescriptor> ToDomain(this WalletDescriptorDto dto)
    {
        var xpubList = dto.XPubs.Select(x => x.ToDomain()).ToList();

        var segwitXpub = xpubList.FirstOrDefault(x => x.ScriptType == DomainScriptType.SegWit);
        var taprootXpub = xpubList.FirstOrDefault(x => x.ScriptType == DomainScriptType.Taproot);
        if (segwitXpub is null || taprootXpub is null)
            return Result.Failure<WalletDescriptor>("The required XPubs are missing to create a Wallet Descriptor");

        var xpubCollection = new XPubCollection([segwitXpub, taprootXpub]);

        if (!Enum.TryParse<BitcoinNetwork>(dto.Network, out var network))
        {
            return Result.Failure<WalletDescriptor>($"Invalid network found for Wallet Descriptor: {dto.Network}");
        }

        return new WalletDescriptor(network, xpubCollection);
    }

    public static XPub ToDomain(this XPubDto dto)
    {
        var path = DerivationPath.Create(dto.Path.Purpose, dto.Path.CoinType, dto.Path.Account);
        return XPub.Create(dto.Value, dto.ScriptType, path);
    }
}