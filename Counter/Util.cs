using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter {
	
	public static class Util {

		// Reads a line from the console without echoing it, showing an asterisk per character: the password may be typed in
		// front of an audience or a recording. Redirected input (e.g. piped) is read as a plain line.
		public static string ReadPassword() {
			if (Console.IsInputRedirected) {
				return Console.ReadLine();
			}
			var password = new StringBuilder();
			while (true) {
				var key = Console.ReadKey(intercept: true);
				if (key.Key == ConsoleKey.Enter) {
					Console.WriteLine();
					return password.ToString();
				}
				if (key.Key == ConsoleKey.Backspace) {
					if (password.Length > 0) {
						password.Length--;
						Console.Write("\b \b");
					}
				} else if (!char.IsControl(key.KeyChar)) {
					password.Append(key.KeyChar);
					Console.Write('*');
				}
			}
		}

		public static byte[] DecodeHex(string s) {
			if (s == null) {
				throw new ArgumentNullException(nameof(s));
			}
			if (s.Length % 2 != 0) {
				throw new Exception("Invalid hex string: bad length");
			}
			var result = new byte[s.Length / 2];
			for (var i = 0; i < result.Length; i++) {
				result[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
			}
			return result;
		}

		public static byte[] DecodePem(byte[] pem) => DecodePem(Encoding.ASCII.GetString(pem));

		public static byte[] DecodePem(string pem) {
			var lines = pem.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var base64 = string.Join("", lines.Where(l => !l.StartsWith("---")));
			return Convert.FromBase64String(base64);
		}
	}
}
