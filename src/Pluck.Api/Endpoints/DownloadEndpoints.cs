using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;

namespace Pluck.Api.Endpoints;

/// <summary>
/// Endpoints for download-related actions
/// </summary>
public static class DownloadEndpoints
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Maps all download-related endpoints
        /// </summary>
        public void MapDownloadEndpoints()
        {
            app.MapDownloadFile();
        }

        /// <summary>
        /// Streams the uploaded file to the client
        /// </summary>
        private void MapDownloadFile()
        {
            app.MapMethods("/f/{token}", ["GET", "POST"],
                    async Task<Results<NotFound<ErrorResponseDto>, UnauthorizedHttpResult, ContentHttpResult,
                        FileStreamHttpResult>> (
                        string token,
                        FileRepository fileRepository, IOptions<PluckApiOptions> apiOptions, HttpContext context,
                        [FromHeader(Name = "X-PLUCK-PASSWORD")]
                        string? headerPassword = null) =>
                    {
                        var file = await fileRepository.GetFileByToken(token);
                        if (file is null)
                        {
                            return TypedResults.NotFound(new ErrorResponseDto("File not found"));
                        }

                        var config = apiOptions.Value;
                        if (!file.IsDownloadable(config.UploadDirectory))
                        {
                            return TypedResults.NotFound(new ErrorResponseDto("File not found"));
                        }

                        // Extract password from the form data if present
                        string? formPassword = null;
                        if (context.Request.HasFormContentType &&
                            context.Request.Form.TryGetValue("pwd", out var pwdValues))
                        {
                            formPassword = pwdValues.ToString();
                        }

                        var providedPassword = headerPassword ?? formPassword;
                        bool isBrowserRequest = context.Request.Headers.Accept.ToString().Contains("text/html");

                        // Password check happens before decrementing downloads
                        if (file.IsPasswordProtected)
                        {
                            if (providedPassword is null)
                            {
                                if (isBrowserRequest)
                                {
                                    return TypedResults.Content(
                                        WebApplication.RenderPasswordHtml(file.OriginalFileName, token, isError: false),
                                        "text/html");
                                }

                                context.Response.Headers.Append("X-PLUCK-PASSWORD-REQUIRED", "true");
                                return TypedResults.Unauthorized();
                            }

                            if (!PasswordHasher.Verify(providedPassword, file.PasswordHash!))
                            {
                                if (isBrowserRequest)
                                {
                                    return TypedResults.Content(
                                        WebApplication.RenderPasswordHtml(file.OriginalFileName, token, isError: true),
                                        "text/html");
                                }

                                return TypedResults.Unauthorized();
                            }
                        }

                        await fileRepository.DecrementDownloadsLeft(file);
                        if (file.IsDirectory)
                        {
                            context.Response.Headers.Append("X-PLUCK-IS-DIRECTORY", "true");
                        }

                        // Stream the file from the disk
                        var filePath = Path.Combine(config.UploadDirectory, file.DiskFileName);
                        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                        return TypedResults.File(fileStream, file.ContentType, file.OriginalFileName,
                            enableRangeProcessing: true);
                    })
                .WithApiVersionSet(Utilities.GetApiVersionSet(app))
                .MapToApiVersion(1, 0)
                .WithName("DownloadFile")
                .WithSummary("Streams the uploaded file to the client")
                .WithDescription(
                    """
                    Streams the uploaded file to the client.
                    It returns 404 if the file is not found or is not downloadable.
                    A file is not downloadable if it has expired or if the download limit has been reached.
                    It returns 401 if the file is password protected and the password is missing or incorrect.
                    """);
        }

        /// <summary>
        /// Renders a simple HTML page with a form for entering a password
        /// </summary>
        private static string RenderPasswordHtml(string fileName, string token, bool isError)
        {
            var escapedFileName = WebUtility.HtmlEncode(fileName);
            var escapedToken = WebUtility.HtmlEncode(token);

            var errorAlert = isError
                ? """<div class="error">Incorrect password. Try again.</div>"""
                : string.Empty;

            return $$"""
                     <!DOCTYPE html>
                     <html lang="en">
                     <head>
                         <meta charset="UTF-8">
                         <meta name="viewport" content="width=device-width, initial-scale=1.0">
                         <title>Protected File - Pluck</title>
                         <style>
                             * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
                             body {
                                 background-color: #0a0a0a;
                                 color: #ededed;
                                 display: flex;
                                 align-items: center;
                                 justify-content: center;
                                 min-height: 100vh;
                                 padding: 1.5rem;
                             }
                             .card {
                                 background: #121212;
                                 border: 1px solid #262626;
                                 border-radius: 12px;
                                 padding: 2.5rem 2rem;
                                 max-width: 380px;
                                 width: 100%;
                             }
                             .header {
                                 margin-bottom: 2rem;
                             }
                             .badge {
                                 display: inline-flex;
                                 align-items: center;
                                 gap: 0.375rem;
                                 font-size: 0.75rem;
                                 font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
                                 text-transform: uppercase;
                                 letter-spacing: 0.05em;
                                 color: #a3a3a3;
                                 background: #171717;
                                 border: 1px solid #262626;
                                 padding: 0.25rem 0.625rem;
                                 border-radius: 9999px;
                                 margin-bottom: 1.25rem;
                             }
                             h1 {
                                 font-size: 1.25rem;
                                 font-weight: 600;
                                 color: #ffffff;
                                 letter-spacing: -0.02em;
                                 margin-bottom: 0.375rem;
                             }
                             p.filename {
                                 font-size: 0.875rem;
                                 color: #737373;
                                 font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
                                 word-break: break-all;
                             }
                             .error {
                                 background: #171717;
                                 border: 1px solid #404040;
                                 color: #ffffff;
                                 padding: 0.75rem 1rem;
                                 border-radius: 6px;
                                 font-size: 0.8125rem;
                                 margin-bottom: 1.5rem;
                             }
                             .form-group {
                                 margin-bottom: 1.25rem;
                             }
                             label {
                                 display: block;
                                 font-size: 0.75rem;
                                 font-weight: 500;
                                 text-transform: uppercase;
                                 letter-spacing: 0.05em;
                                 color: #a3a3a3;
                                 margin-bottom: 0.5rem;
                             }
                             input[type="password"] {
                                 width: 100%;
                                 padding: 0.75rem 0.875rem;
                                 background: #0a0a0a;
                                 border: 1px solid #262626;
                                 border-radius: 6px;
                                 color: #ffffff;
                                 font-size: 0.9375rem;
                                 outline: none;
                                 transition: border-color 0.15s ease;
                             }
                             input[type="password"]:focus {
                                 border-color: #737373;
                             }
                             button {
                                 width: 100%;
                                 padding: 0.75rem;
                                 background: #ffffff;
                                 color: #000000;
                                 font-weight: 600;
                                 font-size: 0.875rem;
                                 border: none;
                                 border-radius: 6px;
                                 cursor: pointer;
                                 transition: background-color 0.15s ease, opacity 0.15s ease;
                             }
                             button:hover {
                                 background: #e5e5e5;
                             }
                             button:active {
                                 opacity: 0.9;
                             }
                             .footer {
                                 margin-top: 2rem;
                                 font-size: 0.75rem;
                                 color: #404040;
                                 text-align: center;
                                 font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
                             }
                         </style>
                     </head>
                     <body>
                         <div class="card">
                             <div class="header">
                                 <h1>Enter Password</h1>
                                 <p class="filename">{{escapedFileName}}</p>
                             </div>

                             {{errorAlert}}

                             <form method="POST" action="/f/{{escapedToken}}">
                                 <div class="form-group">
                                     <label for="pwd">Password</label>
                                     <input type="password" id="pwd" name="pwd" required autofocus placeholder="••••••••" />
                                 </div>
                                 <button type="submit">Download</button>
                             </form>
                         </div>
                     </body>
                     </html>
                     """;
        }
    }
}