select
	Subscriptions.Id as 'SubscriptionId',
	Subscriptions.Name as 'SubscriptionName',
	Elections.Id as 'ElectionId',
	Offices.Name as 'ElectionName',
	Parties.Id as 'PartyId',
	Parties.Name as 'PartyName',
	Parties.Number as 'PartyNumber',
	Parties.Enabled as 'Enabled'
from
	Parties
	inner join Elections on Parties.ElectionId = Elections.Id
	inner join Offices on Elections.OfficeId = Offices.Id
	inner join Subscriptions on Elections.SubscriptionId = Subscriptions.Id
where
	DateDeletedUtc is null
order by
	Subscriptions.Name,
	Offices.[Order],
	Parties.[Order]
