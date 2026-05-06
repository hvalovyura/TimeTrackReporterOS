namespace TimeTrackReporterOS.Models
{
    public class TimeEntryModel
    {
        public string Project { get; set; }
        public string Issue { get; set; }
        public string User { get; set; }
        public double Hours { get; set; }
    }
}
