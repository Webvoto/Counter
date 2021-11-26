using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Classes {
	public static class Util {

		public static string EncodePem(string header, byte[] content) {
			var pem = new StringBuilder();
			pem.AppendLine($"-----BEGIN {header}-----");
			var b64 = Convert.ToBase64String(content, Base64FormattingOptions.InsertLineBreaks);
			pem.Append(b64);
			if (!b64.EndsWith('\n')) {
				pem.AppendLine();
			}
			pem.AppendLine($"-----END {header}-----");
			return pem.ToString();
		}

		public static byte[] DecodePem(byte[] pem) => DecodePem(Encoding.ASCII.GetString(pem));

		public static byte[] DecodePem(string pem) {
			var lines = pem.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var base64 = string.Join("", lines.Where(l => !l.StartsWith("---")));
			return Convert.FromBase64String(base64);
		}
	}
}
