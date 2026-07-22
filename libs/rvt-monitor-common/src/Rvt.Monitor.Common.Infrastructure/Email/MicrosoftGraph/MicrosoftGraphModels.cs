using System.Text.Json.Serialization;

namespace Rvt.Monitor.Common.Infrastructure.Email.MicrosoftGraph;

internal sealed record GraphSendMailRequest(
    [property: JsonPropertyName("message")] GraphMessage Message,
    [property: JsonPropertyName("saveToSentItems")] bool SaveToSentItems);

internal sealed record GraphMessage(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body")] GraphItemBody Body,
    [property: JsonPropertyName("toRecipients")] IReadOnlyList<GraphRecipient> ToRecipients,
    [property: JsonPropertyName("attachments")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<GraphFileAttachment>? Attachments);

internal sealed record GraphItemBody(
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("content")] string Content);

internal sealed record GraphRecipient(
    [property: JsonPropertyName("emailAddress")] GraphEmailAddress EmailAddress);

internal sealed record GraphEmailAddress(
    [property: JsonPropertyName("address")] string Address);

internal sealed record GraphFileAttachment(
    [property: JsonPropertyName("@odata.type")] string ODataType,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("contentBytes")] string ContentBytes);

internal sealed record GraphDraftResponse(
    [property: JsonPropertyName("id")] string? Id);

internal sealed record GraphUploadSessionRequest(
    [property: JsonPropertyName("attachmentItem")] GraphAttachmentItem AttachmentItem);

internal sealed record GraphAttachmentItem(
    [property: JsonPropertyName("attachmentType")] string AttachmentType,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("contentType")] string ContentType);

internal sealed record GraphUploadSessionResponse(
    [property: JsonPropertyName("uploadUrl")] string? UploadUrl);
