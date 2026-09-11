using Dip.Domain.Entities;

namespace Dip.Application.Documents;

// How many digits the sequence field carries, per document type. Built once per
// import or per request from DocumentTypeSerials and handed to the parser and the
// editor, so the width is a piece of editable data rather than a constant.
//
// The default is three — the picklist calls the unlisted types "documents", and a
// document's serial is three digits. It only ever affects a number this system
// composes; a number read from a sheet is taken as written (DocumentNumbering.
// NormalizeNumber), so a wrong width can never rewrite an imported number.
public sealed class SerialWidths
{
    public const int DefaultWidth = 3;

    private readonly IReadOnlyDictionary<string, int> _byDocType;

    private SerialWidths(IReadOnlyDictionary<string, int> byDocType) => _byDocType = byDocType;

    public static SerialWidths Empty { get; } =
        new(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    public static SerialWidths Create(IEnumerable<DocumentTypeSerial> serials)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var serial in serials)
        {
            if (serial.IsDeleted || string.IsNullOrWhiteSpace(serial.DocType)) continue;
            if (serial.SequenceWidth <= 0) continue;
            map[serial.DocType.Trim()] = serial.SequenceWidth;
        }
        return new SerialWidths(map);
    }

    public static SerialWidths Create(IEnumerable<(string DocType, int Width)> widths)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (docType, width) in widths)
        {
            if (string.IsNullOrWhiteSpace(docType) || width <= 0) continue;
            map[docType.Trim()] = width;
        }
        return new SerialWidths(map);
    }

    public int For(string? docType) =>
        !string.IsNullOrWhiteSpace(docType) && _byDocType.TryGetValue(docType.Trim(), out var width)
            ? width
            : DefaultWidth;
}
