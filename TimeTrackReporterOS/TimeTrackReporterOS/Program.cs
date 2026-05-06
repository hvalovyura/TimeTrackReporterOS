using TimeTrackReporterOS.ApiClients;

string ServiceApiUrl = "YOUR URL HERE";
string ServiceUsername = "YOUR USERNAME HERE"; //only for jira
string ServiceApiToken = "YOUR API TOKEN HERE";

DateTime dateFrom = DateTime.Now.AddDays(-30);
DateTime dateTo = DateTime.Today.AddDays(1);

var client = new GitlabGQLClient(ServiceApiUrl, ServiceApiToken);
await client.Run(dateFrom, dateTo);