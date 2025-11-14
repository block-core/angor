﻿﻿using CSharpFunctionalExtensions;

namespace Angor.Contexts.CrossCutting;

public interface ISeedwordsProvider
{
    Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(string walletId);
}