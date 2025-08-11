using Avalonia;
using Avalonia.Controls.Primitives;

namespace AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsGeneration;

public partial class SeedWordsList : TemplatedControl
{
    public static readonly StyledProperty<SeedWords> SeedWordsProperty = AvaloniaProperty.Register<SeedWordsList, SeedWords>(
        nameof(SeedWords));

    public SeedWords SeedWords
    {
        get => GetValue(SeedWordsProperty);
        set => SetValue(SeedWordsProperty, value);
    }
}