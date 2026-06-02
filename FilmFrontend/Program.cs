var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000";
app.MapGet("/api-base-url", () => Results.Ok(new { url = apiBaseUrl }));

var injectScript = $"<script>window.__API_BASE_URL__=\"{apiBaseUrl.Replace("\"", "\\\"")}\";</script></head>";

app.Use(async (context, next) =>
{
    var originalBody = context.Response.Body;
    try
    {
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await next();

        if (context.Response.ContentType != null &&
            context.Response.ContentType.Contains("text/html") &&
            context.Response.StatusCode == 200)
        {
            memStream.Position = 0;
            var html = await new StreamReader(memStream).ReadToEndAsync();
            html = html.Replace("</head>", injectScript);
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            context.Response.Body = originalBody;
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes);
        }
        else
        {
            memStream.Position = 0;
            await memStream.CopyToAsync(originalBody);
        }
    }
    finally
    {
        context.Response.Body = originalBody;
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "FilmFrontend" }));

app.Run();
