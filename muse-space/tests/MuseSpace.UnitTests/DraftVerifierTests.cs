using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Jobs.Internal;

namespace MuseSpace.UnitTests;

public sealed class DraftVerifierTests
{
    [Fact]
    public void Verify_BlocksFutureBeatLeak()
    {
        var scope = NewScope(
            currentPlanText: "标题：最后一节课\n概要：宿舍打闹，广播杂音。",
            reservedFutureBeats: ["第 2 章《门后》；概要：角色进入地下室，看到门后世界。"],
            allowedRevealLevel: ChapterRevealLevel.ForeshadowOnly);

        var result = DraftVerifier.Verify(scope, "杨间离开宿舍后进入地下室，看到门后世界。");

        Assert.False(result.IsPassed);
        Assert.Contains(result.Violations, v => v.Type == DraftViolationType.FutureBeatLeak);
    }

    [Fact]
    public void Verify_BlocksConcreteFutureSignalEvenWhenDraftWordingChanges()
    {
        var scope = NewScope(
            currentPlanText: "概要：广播杂音，手机信号中断。",
            reservedFutureBeats: ["第 2 章《门后》；概要：角色进入地下室，看到门后世界。"],
            allowedRevealLevel: ChapterRevealLevel.DirectAnomaly);

        var result = DraftVerifier.Verify(scope, "他绕到教学楼后面，推门走进地下室，空气冷得不正常。");

        Assert.False(result.IsPassed);
        Assert.Contains(result.Violations, v =>
            v.Type == DraftViolationType.FutureBeatLeak
            && v.Evidence.Contains("地下室", StringComparison.Ordinal));
    }

    [Fact]
    public void Verify_DoesNotTreatGenericGenreTermsAsFutureBeatLeak()
    {
        var scope = NewScope(
            currentPlanText: "概要：广播杂音，手机信号中断。",
            reservedFutureBeats: ["第 2 章《灵异规律》；概要：角色开始研究灵异规律，怀疑鬼的真实来源。"],
            allowedRevealLevel: ChapterRevealLevel.DirectAnomaly);

        var result = DraftVerifier.Verify(scope, "那段广播像灵异传闻里的鬼故事，节奏似乎有某种规律。");

        Assert.True(result.IsPassed);
        Assert.DoesNotContain(result.Violations, v => v.Type == DraftViolationType.FutureBeatLeak);
    }

    [Fact]
    public void Verify_BlocksRevealLevelExceeded()
    {
        var scope = NewScope(
            currentPlanText: "概要：广播杂音，手机信号中断。",
            allowedRevealLevel: ChapterRevealLevel.ForeshadowOnly);

        var result = DraftVerifier.Verify(scope, "地下室里出现尸体，真正的鬼开始追杀学生。");

        Assert.False(result.IsPassed);
        Assert.Contains(result.Violations, v => v.Type == DraftViolationType.RevealLevelExceeded);
    }

    [Fact]
    public void Verify_BlocksConfirmedEntityInForeshadowOnly()
    {
        var scope = NewScope(
            currentPlanText: "概要：广播杂音，手机信号中断。",
            allowedRevealLevel: ChapterRevealLevel.ForeshadowOnly);

        var result = DraftVerifier.Verify(scope, "广播停下后，教室门口的鬼出现了，所有人都看见鬼站在那里。");

        Assert.False(result.IsPassed);
        Assert.Contains(result.Violations, v =>
            v.Type == DraftViolationType.RevealLevelExceeded
            && v.Evidence.Contains("鬼出现", StringComparison.Ordinal));
    }

    [Fact]
    public void Verify_MissingRequiredBeatIsWarningOnly()
    {
        var scope = NewScope(
            currentPlanText: "概要：宿舍日常。",
            requiredBeats: ["手机信号完全中断"],
            allowedRevealLevel: ChapterRevealLevel.ForeshadowOnly);

        var result = DraftVerifier.Verify(scope, "宿舍里几个人还在争论晚饭吃什么。");

        Assert.True(result.IsPassed);
        Assert.Contains(result.Violations, v =>
            v.Type == DraftViolationType.MissingRequiredBeat
            && v.Severity == DraftViolationSeverity.Warning);
    }

    private static ChapterDraftScope NewScope(
        string currentPlanText = "",
        List<string>? requiredBeats = null,
        List<string>? reservedFutureBeats = null,
        ChapterRevealLevel allowedRevealLevel = ChapterRevealLevel.ForeshadowOnly)
        => new()
        {
            ProjectId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid(),
            OutlineId = Guid.NewGuid(),
            ChapterNumber = 1,
            CurrentPlanText = currentPlanText,
            RequiredBeats = requiredBeats ?? [],
            ReservedFutureBeats = reservedFutureBeats ?? [],
            AllowedRevealLevel = allowedRevealLevel,
            GenerationMode = GenerationMode.Original,
            DivergencePolicy = DivergencePolicy.SoftCanon,
        };
}
