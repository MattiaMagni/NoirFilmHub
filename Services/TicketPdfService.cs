using System.Globalization;
using System.Runtime.InteropServices;
using FilmAPI.Model;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace FilmAPI.Services;

public class TicketPdfService
{
    public byte[] GenerateOrderPdf(Prenotazione booking, string validateBaseUrl)
    {
        if (booking.Proiezione?.Film is null || booking.Proiezione?.Cinema is null)
        {
            throw new InvalidOperationException("Prenotazione incompleta: manca contesto show/film/cinema");
        }

        var seats = SplitSeats(booking.PostiSelezionati);
        if (seats.Count == 0)
        {
            seats.Add("N/A");
        }

        var barcodeBytes = BuildPngBarcode(booking.CodiceAcquisto, BarcodeFormat.CODE_128, 720, 180, 4);
        var qrUrl = $"{validateBaseUrl.TrimEnd('/')}/tickets/validate/{Uri.EscapeDataString(booking.CodiceAcquisto)}";
        var qrBytes = BuildPngBarcode(qrUrl, BarcodeFormat.QR_CODE, 320, 320, 1);
        var eventDate = booking.Proiezione.Data.Date.Add(booking.Proiezione.Ora.TimeOfDay);
        var salaNome = booking.Proiezione.Sala?.Nome ?? $"SALA {booking.Proiezione.SalaId}";
        var prezzoPerBiglietto = seats.Count > 0 ? Math.Round(booking.TotalePrezzo / seats.Count, 2) : booking.TotalePrezzo;

        return Document.Create(doc =>
        {
            foreach (var seat in seats)
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("NOIR FILM HUB - BIGLIETTO ELETTRONICO").SemiBold().FontSize(16);
                        col.Item().LineHorizontal(1);

                        col.Item().Text($"Titolo film: {booking.Proiezione.Film.Titolo}");
                        col.Item().Text($"Data e ora show: {eventDate.ToString("dd/MM/yyyy - HH:mm", CultureInfo.InvariantCulture)}");
                        col.Item().Text($"Sala: {salaNome} | Posto: {seat}");
                        col.Item().Text("Tipo evento: CINEMA");
                        col.Item().Text("Organizzatore: Noir Film Hub");
                        col.Item().Text($"Nome locale: {booking.Proiezione.Cinema.Nome}");
                        col.Item().Text($"Codice locale: {booking.Proiezione.Cinema.CodiceLocale}");
                        col.Item().Text($"Indirizzo locale: {booking.Proiezione.Cinema.Indirizzo}");
                        col.Item().Text("Descrizione biglietto: Biglietto Intero");
                        col.Item().Text($"Breakdown prezzo: PrezzoBase={booking.Proiezione.PrezzoBase:0.00} EUR | SupplementoSala=0.00 EUR | PrezzoTotale={prezzoPerBiglietto:0.00} EUR");
                        col.Item().Text($"Codice acquisto: {booking.CodiceAcquisto}").SemiBold();

                        col.Item().PaddingTop(4).Image(barcodeBytes);
                        col.Item().AlignCenter().Width(180).Image(qrBytes);
                        col.Item().AlignCenter().Text(qrUrl).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
            }
        }).GeneratePdf();
    }

    private static List<string> SplitSeats(string seats)
    {
        return (seats ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static byte[] BuildPngBarcode(string content, BarcodeFormat format, int width, int height, int margin)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = margin,
                PureBarcode = false
            }
        };

        var pixelData = writer.Write(content);
        var imageInfo = new SKImageInfo(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(imageInfo);
        Marshal.Copy(pixelData.Pixels, 0, bitmap.GetPixels(), pixelData.Pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
