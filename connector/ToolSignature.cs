#region Metadata
/*
 * Tool Name     : AJ Tools Connector (signature check)
 * File Name     : ToolSignature.cs
 * Purpose       : Verifies that a downloaded tool was really published by Ajmal and has not been
 *                 altered since. This is the single gate that makes it safe to run code fetched
 *                 from a website on someone else's computer.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.Security.Cryptography
 *
 * Input         : The exact payload bytes as published, plus the signature that came with them.
 * Output        : True only if the signature is Ajmal's and the bytes are untouched.
 *
 * Notes         :
 * - WHY THIS EXISTS. The connector runs C# that arrives over the internet. Without this check, anyone
 *   who compromised the website - or sat between it and a colleague - could run whatever they liked on
 *   every machine that installed the connector. With it, an attacker who fully owns the website still
 *   cannot make the connector run anything, because they do not have Ajmal's private key.
 * - Only the PUBLIC half of the key is here. It is meant to be readable by anyone; publishing it
 *   changes nothing. The private half lives at %APPDATA%\AJTools\signing\ajtools-signing-key.json on
 *   Ajmal's machine, is never shipped, and must never be committed or shared - it IS the authority to
 *   publish tools.
 * - Key stored as raw modulus/exponent rather than XML on purpose: RSA.FromXmlString is a .NET
 *   Framework-era API whose availability across .NET Core/5+ has been inconsistent, while building
 *   RSAParameters by hand behaves identically on every target this project builds for (net472 through
 *   net10.0-windows).
 * - RSA-2048 with SHA-256, PKCS#1 v1.5. VerifyData with these overloads exists from .NET Framework 4.6
 *   onward, so one code path covers Revit 2020-2027.
 * - Verification failure is never explained in detail to the caller. "Refused" is the whole answer -
 *   telling an attacker which part of their forgery failed only helps them.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Security.Cryptography;

namespace AJToolsConnector
{
    internal static class ToolSignature
    {
        // Ajmal's PUBLIC signing key. Safe to publish. Generated 2026-08-12.
        // If this is ever rotated, every already-published tool must be re-signed with the new key.
        private const string PublicModulusBase64 =
            "yF5tEtT/37en6PfdV6L/j1/k3dMj9/5cFV9x7Fb9ojcatXjMwY657nXrgdXCsewR4fzoCRMHL+fJa0ONxJHqnufHapsc3oW+" +
            "bWRqgcDZfgi2x5bWhQwqt9TIAWlHYMvZ1ZyRbxvO8eAXcw5f2AXRIoLKVhPPmsiUEkK+8Gsn/v3VfI5nMDMfgNY38Sfqyz4O" +
            "R0favasfih49kORHsgdhAl5Bhp1plgzJV7a6OqeAL6D9FSGstcyOw+xI8U6TO69zREHbs7d5i+DDTYQe42EghoRrWdE4bSWm" +
            "OtOhD9h/bOnT/cqt6cp310/eWMM5ArMxG9zXe2gmzj/110bvLfvh8Q==";

        private const string PublicExponentBase64 = "AQAB";

        /// <summary>
        /// True only if <paramref name="signatureBase64"/> is Ajmal's signature over exactly these
        /// bytes. Any failure - malformed input, wrong key, altered payload - returns false.
        /// </summary>
        public static bool Verify(byte[] payload, string signatureBase64)
        {
            if (payload == null || payload.Length == 0) return false;
            if (string.IsNullOrWhiteSpace(signatureBase64)) return false;

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(signatureBase64);
            }
            catch
            {
                return false;
            }

            try
            {
                var parameters = new RSAParameters
                {
                    Modulus = Convert.FromBase64String(PublicModulusBase64),
                    Exponent = Convert.FromBase64String(PublicExponentBase64)
                };

                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(parameters);
                    return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch
            {
                // A malformed signature makes the platform throw rather than return false. Treat every
                // such case as a plain refusal - there is no failure mode here that should let code run.
                return false;
            }
        }
    }
}
