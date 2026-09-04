using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record TeamRosterMember(
    int TeamPlayerId,
    int TeamId,
    Player Player,
    int? JerseyNumber
);
