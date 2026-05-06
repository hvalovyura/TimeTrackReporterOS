using Newtonsoft.Json;
using System.Net.Http.Headers;
using TimeTrackReporterOS.Models;

namespace TimeTrackReporterOS.ApiClients
{
    public class JiraClient
    {
        public string JiraUrl { get; set; }
        public string Email { get; set; }
        public string ApiToken { get; set; }
        private readonly HttpClient _client;
        public JiraClient(string jiraUrl, string email, string apiToken)
        {
            JiraUrl = jiraUrl.TrimEnd('/');
            Email = email;
            ApiToken = apiToken;

            _client = new HttpClient();
            var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        public async Task Run(DateTime dateFrom, DateTime dateTo)
        {
            Console.WriteLine("Connecting to Jira...");

            var entries = new List<TimeEntryModel>();

            var issues = await GetAllIssues(dateFrom, dateTo);

            var semaphore = new SemaphoreSlim(10);

            var tasks = issues.Select(async issue =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var logs = await GetWorklogs(issue.Key);

                    var filtered = logs
                        .Where(x => x.Started >= dateFrom && x.Started <= dateTo)
                        .ToList();

                    return new
                    {
                        Issue = issue,
                        Logs = filtered
                    };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var result in results.Where(x => x != null))
            {
                foreach (var log in result.Logs)
                {
                    entries.Add(new TimeEntryModel
                    {
                        Project = result.Issue.Fields.Project.Name,
                        Issue = $"{result.Issue.Key} {result.Issue.Fields.Summary}",
                        User = log.Author.DisplayName,
                        Hours = Math.Round(log.TimeSpentSeconds / 3600.0, 2)
                    });
                }
            }

            BuildFileHelper.BuildExcel(entries);
        }

        private async Task<List<JiraIssue>> GetAllIssues(DateTime from, DateTime to)
        {
            var result = new List<JiraIssue>();
            int startAt = 0;
            int maxResults = 100;

            string jql =
                $"worklogDate >= '{from:yyyy-MM-dd}' AND worklogDate <= '{to:yyyy-MM-dd}'";

            do
            {
                string response;
                try
                {
                    var url =
                        $"{JiraUrl}/rest/api/3/search?jql={Uri.EscapeDataString(jql)}" +
                        $"&startAt={startAt}&maxResults={maxResults}&fields=summary,project";
                    response = await _client.GetStringAsync(url);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    Console.WriteLine(ex.Message);
                    var url =
                        $"{JiraUrl}/rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}" +
                        $"&startAt={startAt}&maxResults={maxResults}&fields=summary,project";
                    response = await _client.GetStringAsync(url);
                }

                var data = JsonConvert.DeserializeObject<JiraSearchResponse>(response);

                result.AddRange(data.Issues);

                startAt += maxResults;

                if (startAt >= data.Total)
                    break;

            } while (true);

            return result;
        }

        private async Task<List<JiraWorklog>> GetWorklogs(string issueKey)
        {
            var url = $"{JiraUrl}/rest/api/3/issue/{issueKey}/worklog";

            var response = await _client.GetStringAsync(url);
            var data = JsonConvert.DeserializeObject<JiraWorklogResponse>(response);

            return data.Worklogs ?? new List<JiraWorklog>();
        }
    }

    internal class JiraSearchResponse
    {
        public List<JiraIssue> Issues { get; set; }
        public int Total { get; set; }
    }

    internal class JiraIssue
    {
        public string Key { get; set; }
        public JiraFields Fields { get; set; }
    }

    internal class JiraFields
    {
        public string Summary { get; set; }
        public JiraProject Project { get; set; }
    }

    internal class JiraProject
    {
        public string Name { get; set; }
    }

    internal class JiraWorklogResponse
    {
        [JsonProperty("worklogs")]
        public List<JiraWorklog> Worklogs { get; set; }
    }

    internal class JiraWorklog
    {
        public int TimeSpentSeconds { get; set; }
        public DateTime Started { get; set; }
        public JiraUser Author { get; set; }
    }

    internal class JiraUser
    {
        public string DisplayName { get; set; }
    }
}
