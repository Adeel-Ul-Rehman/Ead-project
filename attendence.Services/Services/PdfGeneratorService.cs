using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace attendence.Services.Services;

public class PdfGeneratorService
{
    public PdfGeneratorService()
    {
        // Set license for QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Generate a credential PDF for a single student
    /// </summary>
    public byte[] GenerateStudentCredentialPdf(string fullName, string email, string password, string badgeNumber, string sectionName, string loginUrl)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.PageColor(Colors.White);

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    // Header
                    column.Item().AlignCenter().Text("🎓").FontSize(40);
                    column.Item().AlignCenter().Text("University Attendance System")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    
                    column.Item().AlignCenter().Text("Student Login Credentials")
                        .FontSize(14).FontColor(Colors.Grey.Darken2);

                    column.Item().PaddingVertical(10).LineHorizontal(2).LineColor(Colors.Blue.Lighten2);

                    // Credentials Box
                    column.Item().Border(2).BorderColor(Colors.Blue.Lighten2).Background(Colors.Grey.Lighten4).Padding(15).Column(credColumn =>
                    {
                        credColumn.Spacing(8);

                        // Name
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("Name:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(fullName).FontSize(12);
                        });

                        // Badge Number
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("Roll No:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(badgeNumber).FontSize(12);
                        });

                        // Section
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("Section:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(sectionName).FontSize(12);
                        });

                        credColumn.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                        // Email
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("📧 Email:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(email).FontSize(11).FontFamily("Courier New");
                        });

                        // Password
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("🔒 Password:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(password).FontSize(13).Bold().FontFamily("Courier New");
                        });
                    });

                    // Login URL
                    column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Blue.Lighten4).Padding(10).Column(urlColumn =>
                    {
                        urlColumn.Item().Text("🌐 Login URL:").Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
                        urlColumn.Item().Text(loginUrl).FontSize(9).FontColor(Colors.Blue.Darken1);
                    });

                    // Warning
                    column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Orange.Medium).Background(Colors.Orange.Lighten4).Padding(10).Text("⚠️ Please change your password after first login for security.")
                        .FontSize(9).Italic().FontColor(Colors.Orange.Darken2);

                    // Footer
                    column.Item().PaddingTop(15).AlignCenter().Text($"Generated on: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Generate a credential PDF for a single teacher
    /// </summary>
    public byte[] GenerateTeacherCredentialPdf(string fullName, string email, string password, string badgeNumber, string loginUrl)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.PageColor(Colors.White);

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    // Header
                    column.Item().AlignCenter().Text("🎓").FontSize(40);
                    column.Item().AlignCenter().Text("University Attendance System")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    
                    column.Item().AlignCenter().Text("Teacher Login Credentials")
                        .FontSize(14).FontColor(Colors.Grey.Darken2);

                    column.Item().PaddingVertical(10).LineHorizontal(2).LineColor(Colors.Blue.Lighten2);

                    // Credentials Box
                    column.Item().Border(2).BorderColor(Colors.Blue.Lighten2).Background(Colors.Grey.Lighten4).Padding(15).Column(credColumn =>
                    {
                        credColumn.Spacing(8);

                        // Name
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("Name:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(fullName).FontSize(12);
                        });

                        // Badge Number
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("Badge No:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(badgeNumber).FontSize(12);
                        });

                        credColumn.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                        // Email
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("📧 Email:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(email).FontSize(11).FontFamily("Courier New");
                        });

                        // Password
                        credColumn.Item().Row(row =>
                        {
                            row.AutoItem().Width(100).Text("🔒 Password:").Bold().FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text(password).FontSize(13).Bold().FontFamily("Courier New");
                        });
                    });

                    // Login URL
                    column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Blue.Lighten4).Padding(10).Column(urlColumn =>
                    {
                        urlColumn.Item().Text("🌐 Login URL:").Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
                        urlColumn.Item().Text(loginUrl).FontSize(9).FontColor(Colors.Blue.Darken1);
                    });

                    // Warning
                    column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Orange.Medium).Background(Colors.Orange.Lighten4).Padding(10).Text("⚠️ Please change your password after first login for security.")
                        .FontSize(9).Italic().FontColor(Colors.Orange.Darken2);

                    // Footer
                    column.Item().PaddingTop(15).AlignCenter().Text($"Generated on: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }
}
