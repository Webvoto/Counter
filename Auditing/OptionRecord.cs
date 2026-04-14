using System;

namespace Webvoto.VotingSystem.Auditing;

public class OptionRecord {

	public Guid Id { get; set; }

	public Guid QuestionId { get; set; }

	public string Name { get; set; }

	public string Identifier { get; set; }

	public string TypeCode { get; set; }

	public int Order { get; set; }

	public bool IsEnabled { get; set; }

	public string ImageContentType { get; set; }

	public string ImageThumbprint { get; set; }

	public string Description { get; set; }

	public DateTime? DateDeletedUtc { get; set; }
}
