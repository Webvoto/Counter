using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Counter;

public static class Util {

	public static byte[] DecodeHex(string s) {
		ArgumentNullException.ThrowIfNull(s);
		if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
			s = s[2..];
		}
		return Convert.FromHexString(s);
	}

	public static byte[] DecodePem(byte[] pem) => DecodePem(Encoding.ASCII.GetString(pem));

	public static byte[] DecodePem(string pem) {
		var lines = pem.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
		var base64 = string.Join("", lines.Where(l => !l.StartsWith("---")));
		return Convert.FromBase64String(base64);
	}

	public static ECDsa GetPublicKey(byte[] encodedPublicKey) {
		var publicKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		publicKey.ImportSubjectPublicKeyInfo(encodedPublicKey, out _);
		return publicKey;
	}

	public static bool VerifyServerSignature(ECDsa serverPublicKey, byte[] data, byte[] signature)
		=> serverPublicKey.VerifyData(data, signature, HashAlgorithmName.SHA256);
}
