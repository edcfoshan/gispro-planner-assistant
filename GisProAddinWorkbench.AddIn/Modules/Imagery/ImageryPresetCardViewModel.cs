using System.Windows.Media;
using ArcGIS.Desktop.Framework.Contracts;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryPresetCardViewModel : PropertyChangedBase
{
    private bool _isAdded;
    private bool _hasCredential;
    private bool _isSelected;
    private ImageSource? _thumbnail;

    internal ImageryPresetCardViewModel(ImageryServicePreset preset)
    {
        Preset = preset;
    }

    public ImageryServicePreset Preset { get; }

    public string Id => Preset.Id;

    public string TitleLine => Preset.DisplayName;

    public string ProviderLabel => ImageryServiceCatalog.GetProviderLabel(Preset);

    public string ContentTypeLabel => ImageryServiceCatalog.GetContentTypeLabel(Preset);

    public string ProviderSummary => $"{ProviderLabel} \u00b7 {ContentTypeLabel}";

    public string ClassificationSummary => ProviderSummary;

    public int SortOrder => Preset.SortOrder;

    public string SortOrderText => Preset.SortOrder.ToString();

    public string Summary => Preset.Summary;

    public string Tag => Preset.Tag;

    public string Symbol => Preset.Symbol;

    public string ServiceKindText => ImageryServiceCatalog.GetServiceKindLabel(Preset.ServiceKind);

    public bool ShowAddedState => Preset.ServiceKind != ImageryServiceKind.Wfs;

    public Brush AccentBrush => Preset.AccentBrush;

    public Brush AccentSoftBrush => Preset.AccentSoftBrush;

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value, nameof(Thumbnail));
    }

    public bool IsAdded
    {
        get => _isAdded;
        set
        {
            if (!SetProperty(ref _isAdded, value, nameof(IsAdded)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(StatusBadgeText));
            NotifyPropertyChanged(nameof(DisplayBadgeText));
            NotifyPropertyChanged(nameof(ActionHintText));
        }
    }

    public bool HasCredential
    {
        get => _hasCredential;
        set
        {
            if (!SetProperty(ref _hasCredential, value, nameof(HasCredential)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(AccessBadgeText));
            NotifyPropertyChanged(nameof(DisplayBadgeText));
            NotifyPropertyChanged(nameof(ActionHintText));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value, nameof(IsSelected));
    }

    public string StatusBadgeText => IsAdded && ShowAddedState ? "\u5df2\u6dfb\u52a0" : "\u53ef\u6dfb\u52a0";

    public string AccessBadgeText => Preset.CredentialKind == ImageryCredentialKind.None
        ? "\u76f4\u8fde"
        : (HasCredential ? "\u5bc6\u94a5\u5c31\u7eea" : "\u5f85\u914d\u7f6e");

    public string DisplayBadgeText => $"{ServiceKindText} \u00b7 {AccessBadgeText}";

    public string ActionHintText => IsAdded && ShowAddedState
        ? "\u5df2\u52a0\u5165\u5185\u5bb9\u5217\u8868"
        : Preset.ServiceKind == ImageryServiceKind.Wfs
            ? "\u5148\u6253\u5f00 WFS \u53c2\u6570\u7a97\u53e3\uff0c\u786e\u8ba4\u540e\u518d\u6dfb\u52a0\u5230\u5185\u5bb9\u5217\u8868"
            : (Preset.CredentialKind == ImageryCredentialKind.None || HasCredential
                ? "\u70b9\u51fb\u5361\u7247\u5373\u53ef\u6dfb\u52a0\u5230\u5f53\u524d\u5730\u56fe"
                : "\u8bf7\u5148\u5230\u8bbe\u7f6e\u91cc\u8865\u5145\u5bf9\u5e94\u5bc6\u94a5");
}
