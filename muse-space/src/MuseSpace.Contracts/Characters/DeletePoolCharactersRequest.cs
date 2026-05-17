namespace MuseSpace.Contracts.Characters;

public sealed class DeletePoolCharactersRequest
{
    public List<Guid> CharacterIds { get; init; } = [];
}
