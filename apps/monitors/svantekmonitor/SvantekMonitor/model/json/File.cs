using System.Globalization;
using System.Text.Json;

namespace Svantek.Model.Http
{
    // Summary: JSON DTOs for Svantek project file metadata and sound recording trigger parsing.
    // Major updates:
    // - 2026-06-18: Added safe defaults for deserialization-backed DTOs to reduce nullability warnings.
    public class ProjectFilesResponse
    {
        public string status { get; set; } = string.Empty;
        public List<ProjectFile> files { get; set; } = new();
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

        public DateTime triggerDate
        {
            get
            {
                DateTime dt;
                string filenametime = filename.Substring(0, 4) + "-" + filename.Substring(4, 2) + "-" + filename.Substring(6, 2) + " " + filename.Substring(9, 2) + ":" + filename.Substring(12, 2) + ":" + filename.Substring(15, 2);
                return DateTime.TryParse(filenametime, CultureInfo.InvariantCulture, out dt) ? dt : DateTime.Now.AddDays(1);
            }
        }
    }
}
