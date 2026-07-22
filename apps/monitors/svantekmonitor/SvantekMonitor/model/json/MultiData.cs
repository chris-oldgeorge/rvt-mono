namespace Svantek.Model.Http
{
    // Summary: JSON DTOs for Svantek multi-point measurement responses.
    // Major updates:
    // - 2026-06-18: Added safe defaults for deserialization-backed DTOs to reduce nullability warnings.
    public class MultiDataResponse
    {
        public string status { get; set; } = string.Empty;
        public List<MultiData> data { get; set; } = new();
    }

    public class MultiData
    {
        public int point { get; set; }
        public DataResponse data { get; set; } = new();
    }

    public class DataResponse
    {
        public string status { get; set; } = string.Empty;
        public List<DataPoints> results { get; set; } = new();
    }

    public class DataPoints
    {
        public List<Key> keys { get; set; } = new();
        public List<DataPoint> data { get; set; } = new();
    }

    public class Key
    {
        public string code { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
    }
    public class DataPoint
    {
        public string timestamp { get; set; } = string.Empty;
        public List<string> values { get; set; } = new();
    }

    public class MultiDataArgument
    {
        public int point { get; set; }
        public string time_from { get; set; } = string.Empty;
        public string time_to { get; set; } = string.Empty;
    }
}
