using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Classes {
	public static class Constants {
		// Certificado e-CNPJ - WEBVOTO TECNOLOGIA EM ELEICOES LTDA:40732403000140
		public const string VoteSigningCertificateThumbprint = "89B0A5AA116938FCFA03844D6CBF3F7BA268120E";

		public static class VoteTypes {
			public const string Null = "Null";
			public const string Blank = "Blank";
			public const string Party = "Party";
		}
	}
}
