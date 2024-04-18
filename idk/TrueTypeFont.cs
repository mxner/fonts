using idk.IO;
using idk.Tables;
using idk.Tables.Cmap;

namespace idk;

public class TrueTypeFont
{
    private readonly OffsetTable _offsetTable;
    private readonly IReadOnlyDictionary<string, TableRecordEntry> _entries;
    private readonly CmapTable _cmapTable;
    
    public TrueTypeFont(string fontPath)
    {
        using var fStream = File.Open(fontPath, FileMode.Open);
        using var reader = new FontReader(fStream);

        _offsetTable = OffsetTable.FromReader(reader);
        _entries = ReadTableRecords(reader, _offsetTable);
        _cmapTable = ReadCmapTable(fontPath, reader, _entries);
        
        reader.Seek(_entries["glyf"].Offset);
        
    }
    
    /// <summary>
    /// Returns the glyph index for the given character, or 0 if the character is not supported by this font
    /// </summary>        
    public static UInt32 GetGlyphIndex(char c, TrueTypeFont font)
    {
        uint glyphIndex = 0;

        // Prefer Windows platform UCS2 glyphs as they are the recommended default on the Windows platform
        var preferred = font._cmapTable.EncodingRecords.FirstOrDefault(
            e => e.PlatformId == Platform.Windows
                 && e.WindowsEncodingId == WindowsEncoding.UnicodeUCS2);
        
        if (preferred != null)
        {
            glyphIndex = preferred.Subtable.GetGlyphIndex(c);
        }

        if (glyphIndex != 0)
        {
            return glyphIndex;
        }

        // Fall back to using any table to find the match
        foreach (var record in font._cmapTable.EncodingRecords)
        {
            glyphIndex = record.Subtable.GetGlyphIndex(c);
            if (glyphIndex != 0)
            {
                return glyphIndex;
            }
        }

        return 0;
    }
    
    private static IReadOnlyDictionary<string, TableRecordEntry> ReadTableRecords(FontReader reader, OffsetTable offsetTable)
    {
        var entries = new Dictionary<string, TableRecordEntry>(offsetTable.Tables);
        for (var i = 0; i < offsetTable.Tables; i++)
        {
            var entry = TableRecordEntry.FromReader(reader);
            entries.Add(entry.Tag, entry);
        }

        return entries;
    }
    
    private static CmapTable ReadCmapTable(string path, FontReader reader, IReadOnlyDictionary<string, TableRecordEntry> entries)
    {
        if (entries.TryGetValue("cmap", out var cmapEntry))
        {
            reader.Seek(cmapEntry.Offset);
            return CmapTable.FromReader(reader);
        }

        throw new Exception(
            $"Font {path} does not contain a Character To Glyph Index Mapping Table (cmap)");
    }
}