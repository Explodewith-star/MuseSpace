namespace MuseSpace.Application.Abstractions.Story;

public interface IStoryContextBuilder
{
    Task<StoryContext> BuildAsync(
        StoryContextRequest request,
        CancellationToken cancellationToken = default);
}
