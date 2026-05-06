using TimeTrackReporterOS.ApiClients;

string ServiceApiUrl = "YOUR SERVICE URL HERE"; //service url, https://example.gitlab.com or https://gitlab.com or https://example.atlassian.net
string ServiceUsername = "USERNAME HERE"; //only for jira, example@google.com
string ServiceApiToken = "API TOKEN HERE";

DateTime dateFrom = DateTime.Now.AddDays(-30);
DateTime dateTo = DateTime.Today.AddDays(1);

var client = new GitlabGQLClient(ServiceApiUrl, ServiceApiToken);
await client.Run(dateFrom, dateTo);

//var client = new JiraClient(ServiceApiUrl, ServiceUsername, ServiceApiToken);
//await client.Run(dateFrom, dateTo);