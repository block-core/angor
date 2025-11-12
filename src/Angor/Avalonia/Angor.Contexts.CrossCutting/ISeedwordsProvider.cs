﻿using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public interface ISeedwordsProvider
{
    Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(string walletId);
}