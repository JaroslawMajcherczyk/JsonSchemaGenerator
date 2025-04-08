using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JsonSchemaGenerator.ViewModels;

namespace JsonSchemaGenerator.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel(this);
            //asd
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}