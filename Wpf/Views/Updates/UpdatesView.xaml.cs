using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Updates;

namespace Wpf.Views.Updates
{
    public partial class UpdatesView : UserControl
    {
        public UpdatesView()
        {
            InitializeComponent();

            DataContext = App.Services.GetRequiredService<UpdatesViewModel>();
        }
    }
}
