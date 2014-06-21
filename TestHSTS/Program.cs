using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NConsoler;

namespace TestHSTS {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("TestHSTS version {0:4}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Copyright (c) Michal A. Valasek - Altairis, 2014 | https://github.com/ridercz/TestHSTS");
            Console.WriteLine("Licensed under terms of MIT license.");
            Console.WriteLine();
            Consolery.Run();
        }

        [Action("Test single URL")]
        public static void Test(
            [Required(Description = "HTTPS URL to test")] string address,
            [Optional(5000, "t", Description = "Request timeout in ms")] int timeout,
            [Optional(false, "g", "Use GET method for tests instead of POST")] bool useGetMethod) {

            if (address == null) throw new ArgumentNullException("address");
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Value cannot be empty or whitespace only string.", "address");
            if (timeout < 0) throw new ArgumentOutOfRangeException("timeout");

            var testUri = ParseAsSecureUrl(address);

            Console.Write("Testing {0}...", testUri);
            string message;
            var result = TestSingleUrl(testUri, timeout, useGetMethod, out message);
            if (result) {
                Console.WriteLine("OK");
                Console.WriteLine("STS header: {0}", message);
            }
            else if (string.IsNullOrEmpty(message)) {
                Console.WriteLine("No HSTS");
            }
            else {
                Console.WriteLine("Error");
                Console.WriteLine("Error message: {0}", message);
            }
        }

        [Action("Test multiple URLs from file")]
        public static void Batch(
            [Required(Description = "Text file containing HTTPS URL to test")] string fileName,
            [Optional(5000, "t", Description = "Request timeout in ms")] int timeout,
            [Optional(false, "g", "Use GET method for tests instead of POST")] bool useGetMethod,
            [Optional(false, "f", "Display full URLs instead of host names only")] bool useFullUrls) {

            if (fileName == null) throw new ArgumentNullException("fileName");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", "fileName");
            if (timeout < 0) throw new ArgumentOutOfRangeException("timeout");

            // Try to read the file
            Console.Write("Reading URLs from file...");
            string[] lines;
            try {
                lines = System.IO.File.ReadAllLines(fileName);
            }
            catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                return;
            }
            Console.WriteLine("OK");

            // Get valid URLs
            Console.Write("Parsing URLs...");
            var validUrls = lines.Select(x => ParseAsSecureUrl(x)).Where(x => x != null).ToArray();
            if (validUrls.Length == 0) {
                Console.WriteLine("Failed!");
                Console.WriteLine("No valid URLs found in file.");
                return;
            }
            Console.WriteLine("OK, {0} URLs found.", validUrls.Length);

            // Test all the URLs
            foreach (var testUri in validUrls) {
                if (useFullUrls) {
                    Console.Write(testUri);
                }
                else {
                    Console.Write(testUri.Host);
                }

                string message;
                var result = TestSingleUrl(testUri, timeout, useGetMethod, out message);
                if (result) {
                    Console.WriteLine("\tyes\t{0}", message);
                }
                else if (string.IsNullOrEmpty(message)) {
                    Console.WriteLine("\tno");
                }
                else {
                    Console.WriteLine("\terror\t{0}", message);
                }
            }

        }

        /// <summary>
        /// Parses any string as HTTPS URL.
        /// </summary>
        /// <param name="s">The string to be parsed.</param>
        /// <returns>Returns Uri with HTTPS scheme or <c>null</c>, if input string cannot be converted to one.</returns>
        private static Uri ParseAsSecureUrl(string s) {
            try {
                var builder = new UriBuilder(s);
                if (builder.Scheme != Uri.UriSchemeHttps) {
                    builder.Scheme = Uri.UriSchemeHttps;
                    builder.Port = 443;
                }
                return builder.Uri;
            }
            catch (Exception) {
                return null;
            }
        }

        /// <summary>
        /// Tests the single URL for HSTS support.
        /// </summary>
        /// <param name="url">The URL to be tested.</param>
        /// <param name="timeout">The request timeout in miliseconds.</param>
        /// <param name="message">Value of <c>Strict-Transport-Security</c> header or error description.</param>
        /// <returns>Returns <c>true</c> if <c>Strict-Transport-Security</c> header is present, <c>false</c> otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">url</exception>
        /// <exception cref="System.ArgumentException">Only HTTPS scheme is supported.;url</exception>
        private static bool TestSingleUrl(Uri url, int timeout, bool useGetMethod, out string message) {
            // Validate arguments
            if (url == null) throw new ArgumentNullException("url");
            if (!url.Scheme.Equals(Uri.UriSchemeHttps)) throw new ArgumentException("Only HTTPS scheme is supported.", "url");

            // Prepare HTTP request
            var rq = HttpWebRequest.Create(url) as HttpWebRequest;
            rq.UserAgent = "Altairis TestHSTS - https://github.com/ridercz/TestHSTS/";
            rq.Timeout = timeout;
            rq.Method = useGetMethod ? "GET" : "HEAD";

            // Accept all certificates (including expired, untrusted...)
            rq.ServerCertificateValidationCallback = (x1, x2, x3, x4) => true;

            // Get response
            HttpWebResponse rp;
            try {
                rp = rq.GetResponse() as HttpWebResponse;
            }
            catch (WebException wex) {
                rp = wex.Response as HttpWebResponse;
                if (rp == null) {
                    message = wex.Message;
                    return false;
                }
            }

            // Get Strict-Transport-Security header
            message = rp.Headers["Strict-Transport-Security"];
            return !string.IsNullOrWhiteSpace(message);
        }

    }
}
