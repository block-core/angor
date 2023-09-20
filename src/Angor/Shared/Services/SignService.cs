﻿using System.Net.Http.Json;
using Angor.Shared.Models;

namespace Angor.Client.Services
{
    public interface ISignService
    {
        Task AddSignKeyAsync(ProjectInfo project, string founderRecoveryPrivateKey);
        Task<SignatureInfo> GetInvestmentSigsAsync(SignRecoveryRequest signRecoveryRequest);
    }

    public class SignService : ISignService
    {

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "/api/TestSign"; // "https://your-base-url/api/test";

        public SignService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task AddSignKeyAsync(ProjectInfo project, string founderRecoveryPrivateKey)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}", new SignData { ProjectIdentifier = project.ProjectIdentifier, FounderRecoveryPrivateKey = founderRecoveryPrivateKey });
            response.EnsureSuccessStatusCode();
        }

        public async Task<SignatureInfo> GetInvestmentSigsAsync(SignRecoveryRequest signRecoveryRequest)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/sign", signRecoveryRequest);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SignatureInfo>();
        }
    }
}
