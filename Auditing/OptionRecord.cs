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

	#region V2

	// The number the candidate was given on joining the body holding the election, when the Option is the candidate (WVS-716)
	public string InscriptionNumber { get; set; }

	// A Parent option is never chosen itself: it opens a further Round of its child options, under its Round statement (WVS-669)
	public bool IsParent { get; set; }

	public Guid? ParentOptionId { get; set; }

	public string RoundStatement { get; set; }

	// The values a Question's Tie-break criteria compare the Option on (WVS-717). The dates are calendar dates, with no time or offset.
	public DateOnly? BirthDate { get; set; }

	public DateOnly? InscriptionDate { get; set; }

	public int? TieBreakPriority { get; set; }

	#endregion
}
