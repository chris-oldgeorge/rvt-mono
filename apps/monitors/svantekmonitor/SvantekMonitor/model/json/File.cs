using System.Text.Json;

namespace Svantek.Model.Http
{
    // Summary: JSON DTOs for Svantek project file metadata and sound recording trigger parsing.
    // Major updates:
    // - 2026-06-18: Added safe defaults for deserialization-backed DTOs to reduce nullability warnings.
    public class ProjectFilesResponse
    {
        public string status { get; set; } = string.Empty;
        public List<ProjectFile> files { get; set; } = [];
        public int files_size { get; set; }
    }

    public class ProjectFile : List<JsonElement>
    {
        public string filename => this[0].GetString()!;
        public int measurementPointId => this[1].GetInt32();
        public string dayCode => this[2].GetString()!;
        public int fileSize => this[3].GetInt32();
        public string stationType => this[4].GetString()!;
        public string stationSerial => this[5].GetString()!;
        public string modificationDate => this[6].GetString()!;
        public int status => this[7].GetInt32();
        public int index => this[8].GetInt32();
    }
}
