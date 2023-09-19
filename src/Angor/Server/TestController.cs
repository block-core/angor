using Angor.Server;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Microsoft.AspNetCore.Mvc;

namespace Blockcore.AtomicSwaps.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly TestStorageService _storage;

        public TestController(TestStorageService storage)
        {
            _storage = storage;
        }

        [HttpGet]
        public async Task<List<ProjectInfo>> Get()
        {
            return (await _storage.Get()).ToList();
        }

        [HttpPost]
        public async Task Post([FromBody] ProjectInfo project)
        {
            await _storage.Add(project);
        }

        [HttpGet]
        [Route("project/{projectId}")]
        public async Task<ProjectInfo?> GetProject(string projectId)
        {
            var projects = (await _storage.Get()).ToList();

            return projects.FirstOrDefault(p => p.ProjectIdentifier == projectId);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class TestIndexerController : ControllerBase
    {
        private readonly TestStorageServiceIndexer _storage;

        public TestIndexerController(TestStorageServiceIndexer storage)
        {
            _storage = storage;
        }

        [HttpGet]
        public async Task<List<ProjectIndexerData>> Get()
        {
            return (await _storage.Get()).ToList();
        }

        [HttpPost]
        public async Task Post([FromBody] ProjectIndexerData project)
        {
            await _storage.Add(project);
        }

        [HttpGet]
        [Route("investment/{projectId}")]
        public async Task<List<ProjectInvestment>> Get(string projectId)
        {
            return (await _storage.GetInv()).ToList().Where(item => item.ProjectIdentifier == projectId).ToList();
        }

        [HttpPost]
        [Route("investment")]
        public async Task PostInv([FromBody] ProjectInvestment project)
        {
            await _storage.Add(project);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class TestSignController : ControllerBase
    {
        private readonly TestStorageService _storage;
        private readonly IFounderTransactionActions _founderTransactionActions;
        private readonly IInvestorTransactionActions _investorTransactionActions;

        public TestSignController(TestStorageService storage, IFounderTransactionActions founderTransactionActions, IInvestorTransactionActions investorTransactionActions)
        {
            _storage = storage;
            _founderTransactionActions = founderTransactionActions;
            _investorTransactionActions = investorTransactionActions;
        }
        
        [HttpPost]
        public async Task Post([FromBody] SignData project)
        {
            await _storage.AddKey(project.ProjectIdentifier, project.FounderRecoveryPrivateKey);
        }

        [HttpGet]
        [Route("{projectId}/{investorKey}/{totalInvestmentAmount}")]
        public async Task<SignatureInfo> Get(string projectId, string investorKey, long totalInvestmentAmount)
        {
            var key = await _storage.GetKey(projectId);

            var project = (await _storage.Get()).First(f => f.ProjectIdentifier == projectId);

            // build sigs
            var investmenttrx = _investorTransactionActions.CreateInvestmentTransaction(project, investorKey, totalInvestmentAmount);
            var recoverytrx = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(project, investmenttrx);
            var sigs = _founderTransactionActions.SignInvestorRecoveryTransactions(project, investmenttrx.ToHex(), recoverytrx, key);

            return sigs;

        }

       
    }
}