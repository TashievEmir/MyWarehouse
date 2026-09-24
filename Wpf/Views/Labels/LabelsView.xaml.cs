using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Labels;

namespace Wpf.Views.Labels
{
    public partial class LabelsView : UserControl
    {
        public LabelsView()
        {
            InitializeComponent();

            DataContext = App.Services.GetRequiredService<LabelsViewModel>();
        }
    }
}
