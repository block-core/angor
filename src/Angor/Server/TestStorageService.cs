#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Angor.Shared.Models;

namespace Angor.Server
{
    public class SerializeData
    {
        [Key]
        public string Key { get; set; }

        public string Data { get; set; }
    }
    
    public class ProjectKeys
    {
        [Key]
        public string Key { get; set; }

        public string nostrPrivateKey { get; set; }
        
        public string founderSigningPrivateKey { get; set; }
    }
    
    public class ProjectIndexerData
    {
        public string FounderKey { get; set; }
        [Key]
        public string ProjectIdentifier { get; set; }
        public string TrxHex { get; set; }

    }

    public class ProjectInvestment
    {
        public string ProjectIdentifier { get; set; }
        [Key]
        public string TrxId { get; set; }
        public string TrxHex { get; set; }
    }

    public class TestStorageService 
    {
        private readonly string dbPath;
        private readonly string dbConnection;

        //private SwapContext swapContext;

        public TestStorageService(IOptions<DataConfigOptions> options)
        {
            dbPath = Path.Combine(options.Value.DirectoryPath, $"testdata.db");

            using var context = new ProjectContext(dbPath);
            context.Database.EnsureCreated();
        }

        public async Task<IEnumerable<ProjectInfo>> Get()
        {
            await using var context = new ProjectContext(dbPath);

            var projects = context.Projects;

            List<ProjectInfo> lst = new();

            foreach (var data in projects)
            {
                if (data.Key.StartsWith("key:"))
                    continue;

                var ret = System.Text.Json.JsonSerializer.Deserialize<ProjectInfo>(data.Data);
                if (ret != null) lst.Add(ret);
            }

            return lst;

        }

        public async Task Add(ProjectInfo project)
        {
            await using var context = new ProjectContext(dbPath);

            context.Projects.Add(new SerializeData { Key = project.ProjectIdentifier, Data = System.Text.Json.JsonSerializer.Serialize(project) });

            await context.SaveChangesAsync();
        }

        public async Task AddKey(string projectid, string founderKey)
        {
            await using var context = new ProjectContext(dbPath);

            context.Projects.Add(new SerializeData { Key = "key:" + projectid, Data =  founderKey });

            await context.SaveChangesAsync();
        }
        
        public async Task AddKey(string projectid, SignData signData)
        {
            await using var context = new ProjectContext(dbPath);

            context.ProjectKeys.Add(new ProjectKeys { Key = projectid, founderSigningPrivateKey =  signData.FounderRecoveryPrivateKey, nostrPrivateKey = signData.NostrPrivateKey });

            await context.SaveChangesAsync();
        }

        public async Task<ProjectKeys> GetKeys(string projectid)
        {
            await using var context = new ProjectContext(dbPath);

            return context.ProjectKeys.First(_ => _.Key == projectid);
        }

        public async Task<IEnumerable<ProjectKeys>> GetAllKeys()
        {
            await using var context = new ProjectContext(dbPath);

            return context.ProjectKeys.ToList();
        }

        public async Task<string> GetKey(string projectid)
        {
            await using var context = new ProjectContext(dbPath);

            var projects = context.Projects;

            var key = string.Empty;

            foreach (var data in projects)
            {
                if (data.Key.StartsWith("key:"))
                {
                    if (data.Key.Contains(projectid))
                        key = data.Data;

                }
            }

            return key;
        }
    }

    public class ProjectContextIndexer : DbContext
    {
        public DbSet<ProjectIndexerData> Projects { get; set; }

        public DbSet<ProjectInvestment> Investments { get; set; }

        public string DbPath { get; }

        public ProjectContextIndexer(string path)
        {
            DbPath = path;
        }

        // The following configures EF to create a Sqlite database file in the
        // special "local" folder for your platform.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }

    public class TestStorageServiceIndexer
    {
        private readonly string dbPath;
        private readonly string dbConnection;


        public TestStorageServiceIndexer(IOptions<DataConfigOptions> options)
        {
            dbPath = Path.Combine(options.Value.DirectoryPath, $"testdataindexer.db");

            using var context = new ProjectContextIndexer(dbPath);
            context.Database.EnsureCreated();
        }

        public async Task<IEnumerable<ProjectIndexerData>> Get()
        {
            await using var context = new ProjectContextIndexer(dbPath);

            var projects = context.Projects;

            return projects.ToList();
        }

        public async Task Add(ProjectIndexerData project)
        {
            await using var context = new ProjectContextIndexer(dbPath);

            context.Projects.Add(project);
            await context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ProjectInvestment>> GetInv()
        {
            await using var context = new ProjectContextIndexer(dbPath);

            var investments = context.Investments;

            return investments.ToList();
        }

        public async Task Add(ProjectInvestment project)
        {
            await using var context = new ProjectContextIndexer(dbPath);

            context.Investments.Add(project);
            await context.SaveChangesAsync();
        }
    }

}