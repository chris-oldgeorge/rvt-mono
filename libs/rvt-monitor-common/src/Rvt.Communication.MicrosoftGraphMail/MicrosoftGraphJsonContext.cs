using System.Text.Json.Serialization;

namespace Rvt.Communication.MicrosoftGraphMail;

[JsonSerializable(typeof(GraphSendMailRequest))]
[JsonSerializable(typeof(GraphMessage))]
[JsonSerializable(typeof(GraphFileAttachment))]
[JsonSerializable(typeof(GraphDraftResponse))]
[JsonSerializable(typeof(GraphUploadSessionRequest))]
[JsonSerializable(typeof(GraphUploadSessionResponse))]
internal sealed partial class MicrosoftGraphJsonContext : JsonSerializerContext;
