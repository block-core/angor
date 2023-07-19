using Microsoft.AspNetCore.Mvc;

namespace Blockcore.AtomicSwaps.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private static List<ProjectInfo> projects = new();

        [HttpGet]
        public Task<List<ProjectInfo>> Get()
        {
            return Task.FromResult(projects);
        }

        [HttpPost]
        public Task Post([FromBody] ProjectInfo project)
        {
            projects.Add(project);

            return Task.CompletedTask;
        }

        [HttpGet]
        [Route("project/{projectId}")]
        public Task<ProjectInfo?> GetProject(string projectId)
        {
            return Task.FromResult(projects.FirstOrDefault(p => p.ProjectIdentifier == projectId));
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class TestIndexerController : ControllerBase
    {
        private static List<ProjectIndexerData> projects = new();

        [HttpGet]
        public Task<List<ProjectIndexerData>> Get()
        {
            return Task.FromResult(projects);
        }

        [HttpPost]
        public Task Post([FromBody] ProjectIndexerData project)
        {
            projects.Add(project);

            return Task.CompletedTask;
        }
    }

    public class ProjectIndexerData
    {
        public string FounderKey { get; set; }
        public string ProjectIdentifier { get; set; }
        public string TrxHex { get; set; }

    }
}