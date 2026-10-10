using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Api.Utils;
using Pluck.Shared.Dtos;
using Pluck.Api.Services.Analytics;
using Pluck.Shared.Models.Events;

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
                        IAnalyticsQueue analyticsQueue,
                        [FromHeader(Name = "X-Pluck-AuthKey")] string? headerAuthKey = null) =>
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

                        var providedAuthKey = headerAuthKey ?? formPassword;
                        var isBrowserRequest = context.Request.Headers.Accept.ToString().Contains("text/html");

                        context.Response.Headers.Append("X-Pluck-Salt", file.Salt);
                        context.Response.Headers.Append("X-Pluck-IV", file.IV);

                        if (providedAuthKey is null)
                        {
                            if (isBrowserRequest)
                            {
                                return TypedResults.Content(
                                    RenderPasswordHtml(file.OriginalFileName, token),
                                    "text/html");
                            }

                            context.Response.Headers.Append("X-PLUCK-AUTH-REQUIRED", "true");
                            return TypedResults.Unauthorized();
                        }

                        if (!PasswordHasher.Verify(providedAuthKey, file.AuthHash))
                        {
                            if (isBrowserRequest)
                            {
                                return TypedResults.Content(
                                    RenderPasswordHtml(file.OriginalFileName, token),
                                    "text/html");
                            }

                            return TypedResults.Unauthorized();
                        }

                        await fileRepository.DecrementDownloadsLeft(file);
                        if (file.IsDirectory)
                        {
                            context.Response.Headers.Append("X-PLUCK-IS-DIRECTORY", "true");
                        }

                        // Dispatch analytics event
                        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
                        var userAgent = context.Request.Headers.UserAgent.ToString();

                        var clientType = ClientType.Unknown;
                        if (userAgent.Contains("PluckCli", StringComparison.OrdinalIgnoreCase))
                            clientType = ClientType.PluckCli;
                        else if (userAgent.Contains("curl", StringComparison.OrdinalIgnoreCase) ||
                                 userAgent.Contains("Wget", StringComparison.OrdinalIgnoreCase))
                            clientType = ClientType.Script;
                        else if (userAgent.Contains("Mozilla", StringComparison.OrdinalIgnoreCase))
                            clientType = ClientType.Browser;

                        await analyticsQueue.QueueAsync(new DownloadAnalyticsMessage(file.Id, ipAddress, userAgent,
                            clientType));

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
        /// Renders a beautiful Zero-Knowledge SPA for entering a password and decrypting locally
        /// </summary>
        private static string RenderPasswordHtml(string fileName, string token)
        {
            var escapedFileName = WebUtility.HtmlEncode(fileName);
            var escapedToken = WebUtility.HtmlEncode(token);

            return $$"""
                     <!DOCTYPE html>
                     <html lang="en">
                     <head>
                         <meta charset="UTF-8">
                         <meta name="viewport" content="width=device-width, initial-scale=1.0">
                         <title>Protected File - Pluck</title>
                         <style>
                             :root {
                                 --bg: #09090b;
                                 --card-bg: rgba(24, 24, 27, 0.7);
                                 --border: rgba(255, 255, 255, 0.1);
                                 --text: #fafafa;
                                 --text-muted: #a1a1aa;
                                 --accent: #3b82f6;
                                 --accent-hover: #2563eb;
                                 --error: #ef4444;
                             }
                             * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
                             body {
                                 background-color: var(--bg);
                                 color: var(--text);
                                 display: flex;
                                 align-items: center;
                                 justify-content: center;
                                 min-height: 100vh;
                                 padding: 1.5rem;
                                 background-image: radial-gradient(circle at 50% -20%, rgba(59, 130, 246, 0.2) 0%, transparent 50%);
                             }
                             .card {
                                 background: var(--card-bg);
                                 backdrop-filter: blur(16px);
                                 -webkit-backdrop-filter: blur(16px);
                                 border: 1px solid var(--border);
                                 border-radius: 16px;
                                 padding: 2.5rem 2rem;
                                 max-width: 400px;
                                 width: 100%;
                                 box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
                             }
                             .header { text-align: center; margin-bottom: 2rem; }
                             h1 { font-size: 1.5rem; font-weight: 600; margin-bottom: 0.5rem; letter-spacing: -0.025em; }
                             p.filename { font-size: 0.875rem; color: var(--text-muted); word-break: break-all; }
                             .error {
                                 background: rgba(239, 68, 68, 0.1);
                                 border: 1px solid rgba(239, 68, 68, 0.2);
                                 color: var(--error);
                                 padding: 0.75rem 1rem;
                                 border-radius: 8px;
                                 font-size: 0.875rem;
                                 margin-bottom: 1.5rem;
                                 display: none;
                             }
                             .form-group { margin-bottom: 1.5rem; display: none; }
                             label { display: block; font-size: 0.75rem; font-weight: 500; text-transform: uppercase; letter-spacing: 0.05em; color: var(--text-muted); margin-bottom: 0.5rem; }
                             input[type="password"] {
                                 width: 100%;
                                 padding: 0.875rem 1rem;
                                 background: rgba(0, 0, 0, 0.3);
                                 border: 1px solid var(--border);
                                 border-radius: 8px;
                                 color: var(--text);
                                 font-size: 1rem;
                                 outline: none;
                                 transition: border-color 0.2s ease, box-shadow 0.2s ease;
                             }
                             input[type="password"]:focus {
                                 border-color: var(--accent);
                                 box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
                             }
                             button {
                                 width: 100%;
                                 padding: 0.875rem;
                                 background: var(--accent);
                                 color: white;
                                 font-weight: 600;
                                 font-size: 1rem;
                                 border: none;
                                 border-radius: 8px;
                                 cursor: pointer;
                                 transition: all 0.2s ease;
                                 box-shadow: 0 4px 6px -1px rgba(59, 130, 246, 0.3);
                             }
                             button:hover { background: var(--accent-hover); transform: translateY(-1px); }
                             button:active { transform: translateY(0); }
                             .decrypting-state { display: none; text-align: center; }
                             .spinner {
                                 width: 40px;
                                 height: 40px;
                                 border: 3px solid rgba(255, 255, 255, 0.1);
                                 border-top-color: var(--accent);
                                 border-radius: 50%;
                                 animation: spin 1s linear infinite;
                                 margin: 0 auto 1.5rem auto;
                             }
                             @keyframes spin { to { transform: rotate(360deg); } }
                             .status-text { font-size: 0.875rem; color: var(--text-muted); }
                             .lock-icon { width: 48px; height: 48px; margin: 0 auto 1rem auto; opacity: 0.8; color: var(--accent); }
                         </style>
                     </head>
                     <body>
                         <div class="card">
                             <div class="header">
                                 <svg class="lock-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                     <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"></path>
                                 </svg>
                                 <h1>Zero-Knowledge Download</h1>
                                 <p class="filename">{{escapedFileName}}</p>
                             </div>

                             <div id="error-alert" class="error"></div>

                             <form id="decrypt-form">
                                 <div class="form-group" id="form-group">
                                     <label for="pwd">Decryption Password</label>
                                     <input type="password" id="pwd" required autofocus placeholder="••••••••" />
                                 </div>
                                 
                                 <div id="decrypting-state" class="decrypting-state">
                                     <div class="spinner"></div>
                                     <div id="status-text" class="status-text">Initializing...</div>
                                 </div>

                                 <button type="submit" id="submit-btn">Decrypt & Download</button>
                             </form>
                         </div>

                         <script>
                             function base64ToBytes(base64) {
                                 const binString = atob(base64);
                                 const bytes = new Uint8Array(binString.length);
                                 for (let i = 0; i < binString.length; i++) {
                                     bytes[i] = binString.charCodeAt(i);
                                 }
                                 return bytes;
                             }

                             function bytesToBase64(bytes) {
                                 let binString = "";
                                 for (let i = 0; i < bytes.byteLength; i++) {
                                     binString += String.fromCharCode(bytes[i]);
                                 }
                                 return btoa(binString);
                             }

                             async function deriveKeys(password, saltBytes) {
                                 const encoder = new TextEncoder();
                                 const keyMaterial = await window.crypto.subtle.importKey("raw", encoder.encode(password), { name: "PBKDF2" }, false, ["deriveBits"]);
                                 const bits = await window.crypto.subtle.deriveBits(
                                     { name: "PBKDF2", salt: saltBytes, iterations: 600000, hash: "SHA-256" },
                                     keyMaterial, 512
                                 );
                                 const bytes = new Uint8Array(bits);
                                 return { encryptionKey: bytes.slice(0, 32), authKey: bytes.slice(32, 64) };
                             }

                             async function decryptFile(token, fileName, password) {
                                 const errorAlert = document.getElementById("error-alert");
                                 const decryptingState = document.getElementById("decrypting-state");
                                 const formGroup = document.getElementById("form-group");
                                 const submitBtn = document.getElementById("submit-btn");
                                 
                                 errorAlert.style.display = 'none';
                                 if (formGroup) formGroup.style.display = 'none';
                                 submitBtn.style.display = 'none';
                                 decryptingState.style.display = 'block';

                                 try {
                                     document.getElementById("status-text").innerText = "Fetching cryptographic metadata...";
                                     const metaRes = await fetch(`/f/${token}`);
                                     if (!metaRes.headers.has("X-Pluck-Salt") || !metaRes.headers.has("X-Pluck-IV")) {
                                         throw new Error("Server did not return cryptographic metadata.");
                                     }
                                     
                                     const salt = base64ToBytes(metaRes.headers.get("X-Pluck-Salt"));
                                     const iv = base64ToBytes(metaRes.headers.get("X-Pluck-IV"));
                                     
                                     document.getElementById("status-text").innerText = "Deriving keys (this may take a moment)...";
                                     const { encryptionKey, authKey } = await deriveKeys(password, salt);
                                     
                                     document.getElementById("status-text").innerText = "Downloading encrypted file...";
                                     const fetchRes = await fetch(`/f/${token}`, {
                                         headers: { "X-Pluck-AuthKey": bytesToBase64(authKey) }
                                     });
                                     
                                     if (fetchRes.status === 401) throw new Error("Incorrect password.");
                                     if (!fetchRes.ok) throw new Error("Failed to download file.");
                                     
                                     const encryptedBuffer = await fetchRes.arrayBuffer();
                                     
                                     document.getElementById("status-text").innerText = "Decrypting file in memory...";
                                     const aesKey = await window.crypto.subtle.importKey("raw", encryptionKey, { name: "AES-CBC" }, false, ["decrypt"]);
                                     const decryptedBuffer = await window.crypto.subtle.decrypt({ name: "AES-CBC", iv: iv }, aesKey, encryptedBuffer);
                                     
                                     document.getElementById("status-text").innerText = "Success! Saving file...";
                                     
                                     const blob = new Blob([decryptedBuffer], { type: fetchRes.headers.get("Content-Type") || "application/octet-stream" });
                                     const url = URL.createObjectURL(blob);
                                     const a = document.createElement("a");
                                     a.href = url;
                                     a.download = fileName;
                                     document.body.appendChild(a);
                                     a.click();
                                     
                                     setTimeout(() => {
                                         URL.revokeObjectURL(url);
                                         document.body.removeChild(a);
                                         document.getElementById("status-text").innerText = "Download complete. You can close this window.";
                                     }, 1000);
                                     
                                 } catch (e) {
                                     errorAlert.innerText = e.message;
                                     errorAlert.style.display = 'block';
                                     if (formGroup) formGroup.style.display = 'block';
                                     submitBtn.style.display = 'block';
                                     decryptingState.style.display = 'none';
                                     window.location.hash = ''; // clear hash so they can try again
                                 }
                             }

                             document.addEventListener("DOMContentLoaded", () => {
                                 const hash = window.location.hash.substring(1);
                                 if (hash) {
                                     decryptFile('{{escapedToken}}', '{{escapedFileName}}', decodeURIComponent(hash));
                                 } else {
                                     document.getElementById("form-group").style.display = 'block';
                                 }
                                 
                                 document.getElementById("decrypt-form").addEventListener("submit", (e) => {
                                     e.preventDefault();
                                     const pwd = document.getElementById("pwd").value;
                                     decryptFile('{{escapedToken}}', '{{escapedFileName}}', pwd);
                                 });
                             });
                         </script>
                     </body>
                     </html>
                     """;
        }
    }
}