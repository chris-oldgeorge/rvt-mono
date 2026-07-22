namespace Svantek.Model.Http
{
    // Summary: JSON DTOs for Svantek project and station catalog responses.
    // Major updates:
    // - 2026-06-18: Added safe defaults for deserialization-backed DTOs to reduce nullability warnings.
    public class ProjectsResponse
    {
        public string status { get; set; } = string.Empty;
        public List<Project> projects { get; set; } = new();
    }

    public class Project
    {
        public string id { get; set; } = string.Empty;
        public string project_name { get; set; } = string.Empty;
        public string location { get; set; } = string.Empty;
        public string project_description { get; set; } = string.Empty;
        public string afd_download_en { get; set; } = string.Empty;
        public string afd_download_mask { get; set; } = string.Empty;
        public string afd_clear_mode { get; set; } = string.Empty;
        public string afd_clear_day { get; set; } = string.Empty;
        public string afd_clear_time { get; set; } = string.Empty;
        public string afd_clear_mask { get; set; } = string.Empty;
        public string afd_rtc_tolerance { get; set; } = string.Empty;
        public string afd_rtc_tz { get; set; } = string.Empty;
        public string afd_download_period { get; set; } = string.Empty;
        public string afd_cleanup_limit { get; set; } = string.Empty;
        public string afd_cleanup_days { get; set; } = string.Empty;
        public List<ProjectStation> stations { get; set; } = new();
    }

    public class ProjectStation
    {
        public string point_id { get; set; } = string.Empty;
        public string serial { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string short_name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string latitude { get; set; } = string.Empty;
        public string longitude { get; set; } = string.Empty;
        public string auto_geo_update { get; set; } = string.Empty;
    }
}
