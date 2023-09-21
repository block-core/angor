using System.Net.Http.Json;
using Angor.Shared.Models;

namespace Angor.Client.Services
{
    public interface IRelayService
    {
        Task<List<ProjectInfo>> GetProjectsAsync();
        Task AddProjectAsync(ProjectInfo project);
        Task<ProjectInfo?> GetProjectAsync(string projectId);
    }

    public class RelayService : IRelayService
    {

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "/api/Test"; // "https://your-base-url/api/test";

        public RelayService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<ProjectInfo>> GetProjectsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectInfo>>();
        }

        public async Task AddProjectAsync(ProjectInfo project)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}", project);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ProjectInfo?> GetProjectAsync(string projectId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/project/{projectId}");

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ProjectInfo?>()
                : null;
        }

    }

}
