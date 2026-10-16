using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using System;
using System.Globalization;
using System.Linq;

namespace sma.plan
{
    // Tools for Claude (MCP). Each [Function] with an [McpToolTrigger] is one
    // tool: Claude reads its name and description to decide when to use it.
    internal class McpTools
    {

        private readonly ICustomerRepo _customerRepo;
        private readonly ITeamMemberRepo _teamMemberRepo;
        private readonly IProjectService _projectService;
        private readonly ISecurityService _securityService;

        public McpTools(
            ICustomerRepo customerRepo,
            ITeamMemberRepo teamMemberRepo,
            IProjectService projectService,
            ISecurityService securityService)
        {
            _customerRepo = customerRepo;
            _teamMemberRepo = teamMemberRepo;
            _projectService = projectService;
            _securityService = securityService;
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpHello))]
        public string McpHello(
            [McpToolTrigger("hello", "Says hello. Use it to check the SMA timesheets connector works.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "Who to greet.", isRequired: true)]
                string name)
        {
            return $"Hello {name}, this is the SMA timesheets server.";
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpFindClient))]
        public object McpFindClient(
            [McpToolTrigger("find_client", "Finds clients whose name contains the text. Use it to get a client's id before creating a project. If several match, ask the user which one.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "Part of the client's name, e.g. 'VLW'.", isRequired: true)]
                string name)
            
        {
            return _customerRepo.GetAll()
				    .Where(c => (c.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
				    .Select(c => new { c.Id, c.Name })
				    .ToList();
        }
        [Allow(Role.Anyone)]
        [Function(nameof(McpFindTeamMember))]
        public object McpFindTeamMember(
            [McpToolTrigger("find_team_member", "Finds team members whose name contains the text. Use it to get a person's id, e.g. for a project manager. If several match, ask the user which one.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "Part of the person's name, e.g. 'Andriy'.", isRequired: true)]
                string name)
        {
            return _teamMemberRepo.GetAll()
                .Where(m => (m.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(m => new { m.Id, m.Name })
                .ToList();
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpCreateClient))]
        public string McpCreateClient(
            [McpToolTrigger("create_client", "Creates a new client. Admins only. Use it only when find_client finds no match, and confirm the name with the user first, so the same client isn't added twice.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "The client's full name, e.g. 'VLW Engineering Ltd.'.", isRequired: true)]
                string name,
            [McpToolProperty("code", "A short reference used on invoices, e.g. 'VLW'.")]
                string code,
            [McpToolProperty("currency", "3-letter currency code. CAD when not said.")]
                string currency)
        {
            // Only admins create clients, as on the website. (MCP calls skip
            // SecurityMiddleware, so the tool checks for itself.)
            if (!_securityService.IsCurrentUserAdmin())
            {
                return "Only admins can create clients.";
            }

            // Claude might miss that the client exists: an exact name match
            // returns the existing one instead of a duplicate.
            Customer existing = _customerRepo.GetAll().FirstOrDefault(c =>
                string.Equals(c.Name?.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return $"A client named '{existing.Name}' already exists (id {existing.Id}). Use that one.";
            }

            // Checked and saved by the same rules as the website.
            Customer customer = new Customer { Name = name, Code = code, Currency = currency };
            string invalid = Customer.Validate(customer);
            if (invalid != null)
            {
                return invalid;
            }

            Customer created = _customerRepo.Create(customer);
            return $"Created client '{created.Name}' (id {created.Id}).";
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpCreateProject))]
        public string McpCreateProject(
    [McpToolTrigger("create_project", "Creates a project. Admins only. First use find_client for the client's id and find_team_member for the project manager's id. If the client doesn't exist yet, offer to create it with create_client. Ask the user for anything missing, and confirm the details with them before calling this.")]
                ToolInvocationContext context,
    [McpToolProperty("name", "The project's name.", isRequired: true)]
                string name,
    [McpToolProperty("clientId", "The client's id, from find_client.", isRequired: true)]
                string clientId,
    [McpToolProperty("projectManagerId", "The project manager's id, from find_team_member.")]
                string projectManagerId,
    [McpToolProperty("projectCode", "The SMA project code, e.g. 24-017.")]
                string projectCode,
    [McpToolProperty("projectType", "How the client is charged: Hourly, LumpSum or NonBillable. Hourly when not said.")]
                string projectType,
    [McpToolProperty("startDate", "The start date, written yyyy-MM-dd.")]
                string startDate,
    [McpToolProperty("endDate", "The end date, written yyyy-MM-dd.")]
                string endDate,
    [McpToolProperty("division", "The division the project belongs to.")]
                string division,
    [McpToolProperty("poNumber", "The client's purchase order number.")]
                string poNumber)
        {
            // 1. Only admins create projects, as on the website. (MCP calls
            // skip SecurityMiddleware, so the tool checks for itself.)
            if (!_securityService.IsCurrentUserAdmin())
            {
                return "Only admins can create projects.";
            }

            // 2. The ids must be real: a made-up one would break the save.
            if (string.IsNullOrWhiteSpace(name))
            {
                return "The project needs a name.";
            }
            if (!_customerRepo.GetAll().Any(c => c.Id == clientId))
            {
                return "There's no client with that id. Use find_client to look it up.";
            }
            if (!string.IsNullOrWhiteSpace(projectManagerId) &&
                !_teamMemberRepo.GetAll().Any(m => m.Id == projectManagerId))
            {
                return "There's no team member with that id. Use find_team_member to look it up.";
            }

            // 3. Dates arrive as text.
            if (!TryReadDate(startDate, out DateOnly? start) || !TryReadDate(endDate, out DateOnly? end))
            {
                return "Dates must be written yyyy-MM-dd, e.g. 2026-11-01.";
            }

            // 4. Checked and saved by the same rules as the website.
            Project project = new Project
            {
                Name = name,
                CustomerId = clientId,
                ProjectManagerId = string.IsNullOrWhiteSpace(projectManagerId) ? null : projectManagerId,
                FundingCode = projectCode,
                ProjectType = projectType,
                StartDate = start,
                EndDate = end,
                Division = division,
                PoNumber = poNumber,
            };

            string invalid = _projectService.Validate(project);
            if (invalid != null)
            {
                return invalid;
            }

            Project created = _projectService.CreateWithManager(project);
            return $"Created project '{created.Name}' (id {created.Id}).";
        }

        // A date written yyyy-MM-dd, or nothing. False when it's something else.
        private static bool TryReadDate(string text, out DateOnly? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
            {
                date = parsed;
                return true;
            }
            // The Functions binding turns "2026-11-01" into a date and passes it on as "11/01/2026 00:00:00 -06:00".
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset converted))
            {
                date = DateOnly.FromDateTime(converted.DateTime);
                return true;
            }
            return false;
        }



    }
}
