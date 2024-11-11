using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Counter {
	
	public static class Util {

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

		public static CsvConfiguration CsvConfiguration => new CsvConfiguration(Thread.CurrentThread.CurrentCulture) {
			Delimiter = ";",
			HasHeaderRecord = true,
			IgnoreBlankLines = true,
			TrimOptions = TrimOptions.Trim,
		};
	}
}
