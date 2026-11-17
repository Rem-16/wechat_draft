namespace WechatDraft.Core;

// A title is scoped visible evidence, not a permanent contact identity.
public sealed class OcrPrefillGuard
{
    public OcrPrefillGuard(string title) => Title = ValidateTitle(title);
    private OcrPrefillGuard() { }

    public static OcrPrefillGuard WithoutTitleCheck() => new();

    public string? Title { get; }
    private string _reason = "";
    private int _writeIssued;
    public string Reason => Volatile.Read(ref _reason);
    public bool WriteIssued => Volatile.Read(ref _writeIssued) != 0;
    public bool Valid => Reason.Length == 0;

    public bool CheckTitle(string current) => Valid && (current.Trim() == Title || Reject("OCR 聊天标题已变化，未继续预填。"));
    public bool Reject(string reason) { Interlocked.CompareExchange(ref _reason, reason, ""); return false; }
    public bool TryIssueWrite()
    {
        return Valid && Interlocked.CompareExchange(ref _writeIssued, 1, 0) == 0 && Valid;
    }
    public static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80 || title.Any(char.IsControl))
            throw new InvalidDataException("本地 OCR 未能读取有效聊天标题。");
        var value = TrimNavigationAffordance(title.Trim());
        if (value.Length == 0) throw new InvalidDataException("本地 OCR 未能读取有效聊天标题。");
        var group = DraftRules.LooksLikeGroupTitle(value);
        DebugSession.Current?.Event("local-title-group-check", new { title = value, group, rule = "paired-member-count-with-ocr-spaces" });
        if (group) throw new GroupChatDetectedException();
        return value;
    }

    private const string ArrowAffordances = "‹〈＜«";

    // A detached single-chat window draws a back arrow inside the title area, and OCR reads it as a
    // leading angle glyph (observed "〈 妈"). Hyphenation is not this guard's job, so only the glyph
    // is dropped; a plain "<" is kept unless it stands alone, since a saved remark may start with it.
    private static string TrimNavigationAffordance(string value)
    {
        while (value.Length > 1)
        {
            var rest = value[1..];
            if (!ArrowAffordances.Contains(value[0]) && !(value[0] == '<' && char.IsWhiteSpace(rest[0]))) break;
            value = rest.TrimStart();
        }
        return value;
    }
}
