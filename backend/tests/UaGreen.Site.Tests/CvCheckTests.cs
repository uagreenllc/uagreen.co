using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Tests;

/// The CV check at the form must agree with Gospodar's, or an application it accepts is
/// refused by Gospodar on every retry and never reaches HR.
public class CvCheckTests
{
    private const string Pdf = "application/pdf", Doc = "application/msword",
        Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public void Real_documents_pass()
    {
        Assert.True(FileStorage.ContentMatches(Pdf, "%PDF-1.7\n"u8));
        Assert.True(FileStorage.ContentMatches(Pdf, "\n\n%PDF-1.4"u8)); // print-to-PDF exports lead with a blank line
        Assert.True(FileStorage.ContentMatches(Doc, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00]));
        Assert.True(FileStorage.ContentMatches(Docx, "PK\u0003\u0004word/"u8));
    }

    [Fact]
    public void Renamed_or_exported_files_fail()
    {
        Assert.False(FileStorage.ContentMatches(Pdf, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])); // PNG named .pdf
        Assert.False(FileStorage.ContentMatches(Doc, "{\\rtf1\\ansi"u8)); // «Word» export that is really RTF
        Assert.False(FileStorage.ContentMatches(Doc, "<html><body>"u8));
        Assert.False(FileStorage.ContentMatches(Docx, "%PDF-1.7"u8));
        Assert.False(FileStorage.ContentMatches("text/plain", "hello"u8));
    }
}
