using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace PewsClient
{
    class MainWindowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public MainWindowVM()
        {
            MmiLocationsView = CollectionViewSource.GetDefaultView(MmiLocations);
            MmiLocationsView.SortDescriptions.Add(new SortDescription("Mmi", ListSortDirection.Descending));

#if DEBUG
            MmiLocations.Add(new StationInfoView
            {
                Mmi = 6,
                MmiBackBrush = Brushes.Red,
                MmiBrush = Brushes.White,
                Name = "ABCD",
                Location = "울릉도",
            });
            MmiLocations.Add(new StationInfoView
            {
                Mmi = 5,
                MmiBackBrush = Brushes.Orange,
                MmiBrush = Brushes.Black,
                Name = "EFGH",
                Location = "독도(한국)",
            });
            MmiLocationsView.Refresh();
#endif
        }

        public ObservableCollection<StationInfoView> MmiLocations { get; private set; } = new ObservableCollection<StationInfoView>();
        public ICollectionView MmiLocationsView { get; private set; }
    }
}
