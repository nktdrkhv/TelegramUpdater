namespace TelegramUpdater.FilterAttributes.Attributes;

/// <summary>
/// Filter attibute for <see cref="FilterCutify.Caption"/>
/// </summary>
public sealed class CaptionAttribute : FilterAttributeBuilder
{
    /// <summary>
    /// Initialize a new instance of <see cref="CaptionAttribute"/>.
    /// </summary>
    public CaptionAttribute()
        : base(x => x.AddFilterForUpdate(FilterCutify.Caption()))
    {
    }
}
