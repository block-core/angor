using AngorApp.Model;
using AngorApp.Sections.Wallet.Create.Step_2;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace AngorApp.Sections.Wallet.Create;

public partial class SeedWordsList : TemplatedControl
{
    public static readonly StyledProperty<WordList> SeedWordsProperty = AvaloniaProperty.Register<SeedWordsList, WordList>(
        nameof(SeedWords));

    public WordList SeedWords
    {
        get => GetValue(SeedWordsProperty);
        set => SetValue(SeedWordsProperty, value);
    }
}