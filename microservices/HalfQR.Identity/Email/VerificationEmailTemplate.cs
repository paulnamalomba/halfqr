using System.Net;

namespace HalfQR.Identity.Email;

internal static class VerificationEmailTemplate
{
    public static EmailMessage Build(string to, string link, int validMinutes)
    {
        var encodedLink = WebUtility.HtmlEncode(link);

        var html = $"""
            <!doctype html>
            <html lang="en">
              <body style="margin:0;padding:32px 16px;background:#eef3fb;font-family:Plus Jakarta Sans,Segoe UI,Helvetica,Arial,sans-serif;color:#142743;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:16px;border:1px solid rgba(20,39,67,0.1);">
                  <tr>
                    <td style="padding:28px 32px;background:#10243c;border-radius:16px 16px 0 0;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:-0.02em;">HalfQR</td>
                  </tr>
                  <tr>
                    <td style="padding:32px;">
                      <h1 style="margin:0 0 12px;font-size:22px;letter-spacing:-0.02em;">Confirm your email to sign in</h1>
                      <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#62718b;">Use the button below to verify this address and open your HalfQR dashboard. The link works once and expires in {validMinutes} minutes.</p>
                      <a href="{encodedLink}" style="display:inline-block;padding:12px 22px;border-radius:999px;background:#1f61c0;color:#ffffff;font-weight:600;text-decoration:none;">Verify email and sign in</a>
                      <p style="margin:24px 0 0;font-size:13px;line-height:1.6;color:#62718b;">If you did not request this, you can ignore this email. No account changes were made.</p>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;

        var text = $"""
            Confirm your email to sign in to HalfQR.

            Open this link to verify your address. It works once and expires in {validMinutes} minutes:
            {link}

            If you did not request this, you can ignore this email.
            """;

        return new EmailMessage(to, "Verify your email for HalfQR", html, text);
    }
}
