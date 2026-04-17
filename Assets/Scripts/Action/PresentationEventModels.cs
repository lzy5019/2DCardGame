using System;

// 该结构只描述客户端应该播放什么演出，不参与真实对局结算。
public enum PresentationType
{
    LegacyAction,
    RemoveCards,
    GainCards,
    TransformCards,
    MoveCards,
    TextOnly
}

public enum PresentationStyle
{
    Default,
    FireDissolve,
    Arcane,
    Shadow
}

public struct PresentationEvent
{
    public int actorPlayerIndex;
    public PresentationType presentationType;
    public PresentationStyle presentationStyle;
    public int legacyActionTypeValue;
    // 这些数组分别表示演出的来源卡、变化前卡牌、变化后卡牌。
    public string[] sourceCardIds;
    public string[] beforeCardIds;
    public string[] afterCardIds;
    public string message;

    public PresentationEvent(
        int actorPlayerIndex,
        PresentationType presentationType,
        PresentationStyle presentationStyle,
        int legacyActionTypeValue,
        string[] sourceCardIds,
        string[] beforeCardIds,
        string[] afterCardIds,
        string message)
    {
        this.actorPlayerIndex = actorPlayerIndex;
        this.presentationType = presentationType;
        this.presentationStyle = presentationStyle;
        this.legacyActionTypeValue = legacyActionTypeValue;
        this.sourceCardIds = NormalizeCardIds(sourceCardIds);
        this.beforeCardIds = NormalizeCardIds(beforeCardIds);
        this.afterCardIds = NormalizeCardIds(afterCardIds);
        this.message = message ?? string.Empty;
    }

    public static PresentationEvent CreateLegacyAction(
        int actorPlayerIndex,
        string sourceCardId,
        PublicActionType actionType,
        string message = "")
    {
        return new PresentationEvent(
            actorPlayerIndex,
            PresentationType.LegacyAction,
            PresentationStyle.Default,
            (int)actionType,
            WrapCard(sourceCardId),
            Array.Empty<string>(),
            Array.Empty<string>(),
            message
        );
    }

    public static PresentationEvent CreateRemoveCards(
        int actorPlayerIndex,
        PresentationStyle presentationStyle,
        string[] sourceCardIds,
        string[] beforeCardIds,
        string message = "")
    {
        return new PresentationEvent(
            actorPlayerIndex,
            PresentationType.RemoveCards,
            presentationStyle,
            -1,
            sourceCardIds,
            beforeCardIds,
            Array.Empty<string>(),
            message
        );
    }

    public static PresentationEvent CreateGainCards(
        int actorPlayerIndex,
        PresentationStyle presentationStyle,
        string[] sourceCardIds,
        string[] afterCardIds,
        string message = "")
    {
        return new PresentationEvent(
            actorPlayerIndex,
            PresentationType.GainCards,
            presentationStyle,
            -1,
            sourceCardIds,
            Array.Empty<string>(),
            afterCardIds,
            message
        );
    }

    public static PresentationEvent CreateTransformCards(
        int actorPlayerIndex,
        PresentationStyle presentationStyle,
        string[] sourceCardIds,
        string[] beforeCardIds,
        string[] afterCardIds,
        string message = "")
    {
        return new PresentationEvent(
            actorPlayerIndex,
            PresentationType.TransformCards,
            presentationStyle,
            -1,
            sourceCardIds,
            beforeCardIds,
            afterCardIds,
            message
        );
    }

    public static PresentationEvent CreateMoveCards(
        int actorPlayerIndex,
        PresentationStyle presentationStyle,
        string[] sourceCardIds,
        string[] beforeCardIds,
        string[] afterCardIds,
        string message = "")
    {
        return new PresentationEvent(
            actorPlayerIndex,
            PresentationType.MoveCards,
            presentationStyle,
            -1,
            sourceCardIds,
            beforeCardIds,
            afterCardIds,
            message
        );
    }

    public static PresentationEvent CreateTextOnly(
        int actorPlayerIndex,
        string message)
    {
        return new PresentationEvent(
            actorPlayerIndex,
            PresentationType.TextOnly,
            PresentationStyle.Default,
            -1,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            message
        );
    }

    private static string[] NormalizeCardIds(string[] cardIds)
    {
        return cardIds ?? Array.Empty<string>();
    }

    private static string[] WrapCard(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return Array.Empty<string>();

        return new[] { cardId };
    }
}
