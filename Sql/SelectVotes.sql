select
	VoteSlots.PoolId,
	VoteSlots.Slot,
	SUBSTRING(VoteSlots.Value, 1, VoteSlots.ValueLength) as Value,
	SUBSTRING(VoteSlots.CmsSignature, 1, VoteSlots.CmsSignatureLength) as CmsSignature,
	VoteSlots.ServerSignature,
	ServerInstances.Id as ServerInstanceId,
	CAST(ServerInstances.PublicKey as varbinary(300)) as ServerPublicKey
from
	VoteSlots
	inner join VotePools on VoteSlots.PoolId = VotePools.Id
	inner join ServerInstances on VotePools.ServerInstanceId = ServerInstances.Id
where VoteSlots.HasValue = 1
