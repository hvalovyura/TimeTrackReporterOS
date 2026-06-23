using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using TimeTrackReporterOS.Models;

namespace TimeTrackReporterOS.ApiClients
{
    public class GitlabGQLClient
    {
        public string GitLabUrl { get; set; }
        public string ApiToken { get; set; }

        public GitlabGQLClient(string gitlabUrl, string gitlabToken)
        {
            GitLabUrl = gitlabUrl;
            ApiToken = gitlabToken;
        }

        public async Task<IEnumerable<string>> FetchGitlabProjects()
        {
            Console.WriteLine("Connecting to GitLab...");

            var client = new GraphQLHttpClient(GitLabUrl, new NewtonsoftJsonSerializer());
            client.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiToken}");

            var projects = await GetAllProjects(client);
            var projectsFullPath = projects.Select(p => p.FullPath).OrderBy(p => p);
            return projectsFullPath;
        }

        public async Task Run(DateTime dateFrom, DateTime dateTo, IEnumerable<string>? projectsFilter = null)
        {
            GitLabUrl = GitLabUrl + "/api/graphql";
            Console.WriteLine("Connecting to GitLab...");

            var client = new GraphQLHttpClient(GitLabUrl, new NewtonsoftJsonSerializer());
            client.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiToken}");

            var entries = new List<TimeEntryModel>();

            var projects = await GetAllProjects(client);

            if (projectsFilter != null && projectsFilter.Count() > 0) projects = projects.Where(p => projectsFilter.Any(pf => pf.Equals(p.FullPath))).ToList();

            foreach (var project in projects)
            {
                Console.WriteLine($"Project: {project.Name}");

                var issues = await GetAllIssues(client, project.FullPath);

                var semaphore = new SemaphoreSlim(10);

                var tasks = issues.Select(async issue =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        var timelogs = await GetAllTimelogs(client, project.FullPath, issue.Iid);

                        var filteredLogs = timelogs
                            .Where(log => log?.SpentAt >= dateFrom && log?.SpentAt <= dateTo)
                            .ToList();

                        if (!filteredLogs.Any())
                            return null;

                        return new
                        {
                            Issue = issue,
                            Logs = filteredLogs
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var results = await Task.WhenAll(tasks);

                foreach (var result in results.Where(r => r != null))
                {
                    foreach (var log in result.Logs)
                    {
                        entries.Add(new TimeEntryModel
                        {
                            Project = project.Name,
                            Issue = $"#{result.Issue.Iid} {result.Issue.Title}",
                            User = log?.User?.Name ?? "Unknown",
                            Hours = Math.Round((log?.TimeSpent ?? 0) / 3600.0, 2)
                        });
                    }
                }
            }

            BuildFileHelper.BuildCsv(entries);
            BuildFileHelper.BuildExcel(entries);
        }

        static async Task<List<Project>> GetAllProjects(GraphQLHttpClient client)
        {
            var result = new List<Project>();
            string cursor = null;

            do
            {
                var request = new GraphQLRequest
                {
                    Query = @"
                    query($cursor:String){
                      projects(membership:true, first:100, after:$cursor){
                        pageInfo{
                          hasNextPage
                          endCursor
                        }
                        nodes{
                          name
                          fullPath
                        }
                      }
                    }",
                    Variables = new { cursor }
                };

                var response = await client.SendQueryAsync<ProjectsResponse>(request);

                result.AddRange(response.Data.Projects.Nodes);

                cursor = response.Data.Projects.PageInfo.HasNextPage
                    ? response.Data.Projects.PageInfo.EndCursor
                    : null;

            } while (cursor != null);

            return result;
        }

        static async Task<List<Issue>> GetAllIssues(GraphQLHttpClient client, string projectPath)
        {
            var result = new List<Issue>();
            string cursor = null;

            do
            {
                var request = new GraphQLRequest
                {
                    Query = @"
                    query($path:ID!,$cursor:String){
                      project(fullPath:$path){
                        issues(first:100, after:$cursor){
                          pageInfo{
                            hasNextPage
                            endCursor
                          }
                          nodes{
                            iid
                            title
                          }
                        }
                      }
                    }",
                    Variables = new { path = projectPath, cursor }
                };

                var response = await client.SendQueryAsync<IssuesResponse>(request);

                var issues = response.Data.Project.Issues;

                result.AddRange(issues.Nodes);

                cursor = issues.PageInfo.HasNextPage
                    ? issues.PageInfo.EndCursor
                    : null;

            } while (cursor != null);

            return result;
        }

        static async Task<List<Timelog>> GetAllTimelogs(GraphQLHttpClient client, string projectPath, int iid)
        {
            var result = new List<Timelog>();
            string cursor = null;

            do
            {
                var request = new GraphQLRequest
                {
                    Query = @"
                    query($path:ID!,$iid:String!,$cursor:String){
                      project(fullPath:$path){
                        issue(iid:$iid){
                          timelogs(first:100, after:$cursor){
                            pageInfo{
                              hasNextPage
                              endCursor
                            }
                            nodes{
                              timeSpent
                              spentAt
                              user{
                                name
                              }
                            }
                          }
                        }
                      }
                    }",
                    Variables = new
                    {
                        path = projectPath,
                        iid = iid.ToString(),
                        cursor
                    }
                };

                var response = await client.SendQueryAsync<TimelogResponse>(request);

                var logs = response.Data?.Project?.Issue?.Timelogs;

                if (logs?.Nodes != null)
                    result.AddRange(logs.Nodes);

                cursor = logs?.PageInfo?.HasNextPage ?? false
                    ? logs.PageInfo.EndCursor
                    : null;

            } while (cursor != null);

            return result;
        }
    }

    internal class ProjectsResponse
    {
        public ProjectsContainer Projects { get; set; }
    }

    internal class IssuesResponse
    {
        public ProjectIssues Project { get; set; }
    }

    internal class TimelogResponse
    {
        public ProjectIssue Project { get; set; }
    }

    internal class ProjectsContainer
    {
        public PageInfo PageInfo { get; set; }
        public List<Project> Nodes { get; set; }
    }

    internal class Project
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }

    internal class ProjectIssues
    {
        public IssuesContainer Issues { get; set; }
    }

    internal class ProjectIssue
    {
        public IssueTimelogs Issue { get; set; }
    }

    internal class IssuesContainer
    {
        public PageInfo PageInfo { get; set; }
        public List<Issue> Nodes { get; set; }
    }

    internal class Issue
    {
        public int Iid { get; set; }
        public string Title { get; set; }
    }

    internal class IssueTimelogs
    {
        public TimelogsContainer Timelogs { get; set; }
    }

    internal class TimelogsContainer
    {
        public PageInfo PageInfo { get; set; }
        public List<Timelog> Nodes { get; set; }
    }

    internal class Timelog
    {
        public int TimeSpent { get; set; }
        public DateTime SpentAt { get; set; }
        public User User { get; set; }
    }

    internal class User
    {
        public string Name { get; set; }
    }

    internal class PageInfo
    {
        public bool HasNextPage { get; set; }
        public string EndCursor { get; set; }
    }
}
