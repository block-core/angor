using Microsoft.JSInterop;
using System.Net.Http;
using System.Net.Http.Json;

namespace Angor.Client.Services
{
    public interface IIndexerService
    {
        Task<List<ProjectIndexerData>> GetProjectsAsync();
        Task AddProjectAsync(ProjectIndexerData project);
        Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId);
        Task AddInvestmentAsync(ProjectInvestment project);
    }
    public class ProjectIndexerData
    {
        public string FounderKey { get; set; }
        public string ProjectIdentifier { get; set; }
        public string TrxHex { get; set; }
    }

    public class ProjectInvestment
    {
        public string ProjectIdentifier { get; set; }
        public string TrxId { get; set; }
        public string TrxHex { get; set; }
    }

    public class IndexerService : IIndexerService
    {

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "/api/TestIndexer"; // "https://your-base-url/api/test";

        public IndexerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<ProjectIndexerData>> GetProjectsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectIndexerData>>();
        }

        public async Task AddProjectAsync(ProjectIndexerData project)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}", project);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/investment/{projectId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>();
        }

        public async Task AddInvestmentAsync(ProjectInvestment project)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/investment", project);
            response.EnsureSuccessStatusCode();
        }
    }
}
