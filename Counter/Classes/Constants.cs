using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Classes {
	public static class Constants {
		// Certificado e-CNPJ - WEBVOTO TECNOLOGIA EM ELEICOES LTDA:40732403000140
		public const string VoteSigningCertificateThumbprint = "5D91B749CE82743806E2ED1326A2D966EFB11BE5";

		public static class VoteTypes {
			public const string Null = "Null";
			public const string Blank = "Blank";
			public const string Party = "Party";
		}
	}
}
