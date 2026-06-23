using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTrackReporterOS.Models;

namespace TimeTrackReporterOS
{
    public static class BuildFileHelper
    {
        public static void BuildCsv(List<TimeEntryModel> entries)
        {
            Console.WriteLine("Building CSV...");

            entries = entries
                .OrderBy(x => x.Project)
                .ThenBy(x => x.Issue)
                .ToList();

            var users = entries
                .Select(x => x.User)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            string fileName = "Timesheet_ByUser.csv";

            using (var writer = new StreamWriter(fileName, false, Encoding.UTF8))
            {
                writer.Write("Task");

                foreach (var user in users)
                    writer.Write($";{Escape(user)}");

                writer.WriteLine(";TOTAL");

                var projects = entries
                    .GroupBy(x => x.Project)
                    .OrderBy(x => x.Key);

                foreach (var project in projects)
                {
                    writer.WriteLine(Escape(project.Key));

                    var issues = project
                        .GroupBy(x => x.Issue)
                        .OrderBy(x => x.Key);

                    foreach (var issue in issues)
                    {
                        writer.Write(Escape(issue.Key));

                        double issueTotal = 0;

                        for (int i = 0; i < users.Count; i++)
                        {
                            var hours = issue
                                .Where(x => x.User == users[i])
                                .Sum(x => x.Hours);

                            writer.Write(";");

                            if (hours > 0)
                                writer.Write(hours.ToString("0.##", CultureInfo.InvariantCulture));

                            issueTotal += hours;
                        }

                        writer.WriteLine($";{issueTotal.ToString("0.##", CultureInfo.InvariantCulture)}");
                    }

                    writer.Write("TOTAL BY PROJECT");

                    double projectTotal = 0;

                    for (int i = 0; i < users.Count; i++)
                    {
                        var sum = project
                            .Where(x => x.User == users[i])
                            .Sum(x => x.Hours);

                        writer.Write($";{Math.Round(sum, 2).ToString("0.##", CultureInfo.InvariantCulture)}");

                        projectTotal += sum;
                    }

                    writer.WriteLine($";{Math.Round(projectTotal, 2).ToString("0.##", CultureInfo.InvariantCulture)}");

                    writer.WriteLine();
                }

                writer.Write("TOTAL BY EMPLOYEE");

                double grandTotal = 0;

                for (int i = 0; i < users.Count; i++)
                {
                    var userTotal = entries
                        .Where(x => x.User == users[i])
                        .Sum(x => x.Hours);

                    writer.Write($";{Math.Round(userTotal, 2).ToString("0.##", CultureInfo.InvariantCulture)}");

                    grandTotal += userTotal;
                }

                writer.WriteLine($";{Math.Round(grandTotal, 2).ToString("0.##", CultureInfo.InvariantCulture)}");
            }

            Console.WriteLine($"Report saved: {Path.GetFullPath(fileName)}");
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(";") || value.Contains("\"") || value.Contains("\n"))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }

        public static void BuildExcel(List<TimeEntryModel> entries)
        {
            Console.WriteLine("Building Excel...");
            entries = entries.OrderBy(x => x.Project).ThenBy(x => x.Issue).ToList();

            var users = entries
                .Select(x => x.User)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Report");

                ws.Cell(1, 1).Value = "Task";

                for (int i = 0; i < users.Count; i++)
                {
                    ws.Cell(1, i + 2).Value = users[i];
                    ws.Cell(1, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                ws.Cell(1, users.Count + 2).Value = "TOTAL";

                ws.Row(1).Style.Font.Bold = true;
                ws.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 2;

                var projects = entries
                    .GroupBy(x => x.Project)
                    .OrderBy(x => x.Key);

                foreach (var project in projects)
                {
                    ws.Cell(row, 1).Value = project.Key;

                    ws.Row(row).Style.Font.Bold = true;
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2D7C9");

                    row++;

                    var issues = project
                        .GroupBy(x => x.Issue)
                        .OrderBy(x => x.Key);

                    foreach (var issue in issues)
                    {
                        ws.Cell(row, 1).Value = issue.Key;

                        double issueTotal = 0;

                        for (int i = 0; i < users.Count; i++)
                        {
                            var hours = issue
                                .Where(x => x.User == users[i])
                                .Sum(x => x.Hours);

                            if (hours > 0)
                                ws.Cell(row, i + 2).Value = hours;

                            issueTotal += hours;
                        }

                        ws.Cell(row, users.Count + 2).Value = issueTotal;

                        row++;
                    }

                    ws.Cell(row, 1).Value = "TOTAL BY PROJECT";

                    ws.Row(row).Style.Font.Bold = true;
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#D5E8D4");

                    double projectTotal = 0;

                    for (int i = 0; i < users.Count; i++)
                    {
                        var sum = project
                            .Where(x => x.User == users[i])
                            .Sum(x => x.Hours);

                        ws.Cell(row, i + 2).Value = Math.Round(sum, 2);

                        projectTotal += sum;
                    }

                    ws.Cell(row, users.Count + 2).Value = projectTotal;

                    row += 2;
                }

                ws.Cell(row, 1).Value = "TOTAL BY EMPLOYEE";

                ws.Row(row).Style.Font.Bold = true;
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");

                double grandTotal = 0;

                for (int i = 0; i < users.Count; i++)
                {
                    var userTotal = entries
                        .Where(x => x.User == users[i])
                        .Sum(x => x.Hours);

                    ws.Cell(row, i + 2).Value = Math.Round(userTotal, 2);

                    grandTotal += userTotal;
                }

                ws.Cell(row, users.Count + 2).Value = Math.Round(grandTotal, 2);

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);

                string fileName = "Timesheet_ByUser.xlsx";
                workbook.SaveAs(fileName);

                Console.WriteLine($"Report saved: {Path.GetFullPath(fileName)}");
            }
        }
    }
}
